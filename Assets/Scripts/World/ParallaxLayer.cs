using UnityEngine;
using UnityEngine.Rendering;

// The ParallaxLayer script makes an object scroll at a different speed than the level as the camera moves,
// so it looks further away (background) or closer (foreground). Put one on each layer or prop that should have depth.
//
// SCROLL SPEED:
// How fast the layer scrolls across the screen compared to the level, separately for horizontal and vertical.
//   0      = doesn't scroll at all, stays put on the screen (a sky or color wash)
//   0.1-0.3 = far away (distant walls, machinery)
//   0.4-0.7 = middle distance
//   1      = scrolls exactly with the level (no parallax)
//   above 1 = closer than the level, scrolls faster (foreground props in front of the player)
// Keep vertical lower than horizontal for subtle vertical movement, e.g. (0.2, 0.05).
//
// ANCHOR (what the parallax is measured from):
//   World      - measured from the world origin. For tileable layers that repeat (Repeat Width/Height), so the pattern
//                carries on seamlessly through every room and in every direction. Goes in a biome scene's Content.
//   RoomCenter - measured from the center of the room the player is in, easing to the new room when the room changes.
//                For one painted (non-repeating) background, so every room frames it the same way. Goes in a biome
//                scene's Content, arranged around the point (0, 0).
//   Self       - measured from where the object was placed. For props and set pieces placed in the level (midground and
//                foreground). The prop sits exactly where it was placed when the camera is centered on it.
//
// It updates right before the main camera renders, after Cinemachine has moved the camera, so layers never jitter.
// The camera is found at runtime (Camera.main) because biome layers live in a different scene than the camera.
public class ParallaxLayer : MonoBehaviour
{
    public enum Anchor
    {
        World,
        RoomCenter,
        Self
    }

    [Tooltip("World: tileable layers that repeat. RoomCenter: one painted background framed the same in every room. " +
             "Self: props and set pieces placed in the level.")]
    [SerializeField] private Anchor anchor = Anchor.World;

    [Tooltip("How fast the layer scrolls compared to the level. 0 = stays put on screen, 0.1-0.3 = far, 0.4-0.7 = middle, " +
             "1 = with the level, above 1 = foreground. Keep vertical lower than horizontal for subtle vertical movement.")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.2f, 0.05f);

    [Header("Repeating art (World anchor)")]
    [Tooltip("Width of one repeat of the art in world units, for a Sprite Renderer with Draw Mode set to Tiled. " +
             "The layer shifts by whole repeats to stay under the camera, so it never runs out. 0 = doesn't repeat sideways.")]
    [SerializeField] private float repeatWidth = 0f;

    [Tooltip("Height of one repeat of the art in world units. 0 = doesn't repeat vertically.")]
    [SerializeField] private float repeatHeight = 0f;

    [Header("Room changes (RoomCenter anchor)")]
    [Tooltip("Seconds to ease to the new room's framing when the room changes. Match the RoomTransitionSmoother's Glide Time.")]
    [SerializeField] private float roomChangeGlide = 0.5f;

    [Tooltip("If the camera moves further than this in one frame (a teleport), the layer snaps instead of easing.")]
    [SerializeField] private float teleportDistance = 10f;

    [Header("Pixel art")]
    [Tooltip("The art's Pixels Per Unit. Snaps the layer to whole pixels so pixel art doesn't shimmer while scrolling. 0 = off.")]
    [SerializeField] private float pixelsPerUnit = 0f;

    // Where the object was placed in its scene
    private Vector3 _placedPosition;

    // RoomCenter only: the eased room center the parallax is measured from
    private Vector2 _roomAnchor;
    private Vector2 _roomAnchorVelocity;
    private bool _hasRoomAnchor;
    private Vector2 _lastCameraPosition;

    private void Awake()
    {
        _placedPosition = transform.position;
    }

    // Biome layers only update while their biome is shown. Coming back on re-frames them instantly.
    private void OnEnable()
    {
        _hasRoomAnchor = false;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        // Only follow the game camera, not the Scene view or any other camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null || renderingCamera != mainCamera) return;

        Vector2 cameraPosition = mainCamera.transform.position;

        // The point the parallax is measured from, and where the layer sits relative to it
        Vector2 anchorPoint;
        Vector2 placedOffset;
        switch (anchor)
        {
            case Anchor.RoomCenter:
                anchorPoint = GetEasedRoomCenter(cameraPosition);
                placedOffset = _placedPosition;
                break;
            case Anchor.Self:
                anchorPoint = _placedPosition;
                placedOffset = Vector2.zero;
                break;
            default:
                anchorPoint = Vector2.zero;
                placedOffset = _placedPosition;
                break;
        }

        // Scroll speed 1 keeps the layer fixed in the world (scrolls with the level), 0 keeps it fixed on screen
        Vector2 follow = Vector2.one - scrollSpeed;
        Vector2 fromAnchor = cameraPosition - anchorPoint;
        Vector3 position = new Vector3(
            anchorPoint.x + fromAnchor.x * follow.x + placedOffset.x,
            anchorPoint.y + fromAnchor.y * follow.y + placedOffset.y,
            _placedPosition.z);

        // Repeating art: shift by whole repeats so the layer stays centered under the camera
        if (repeatWidth > 0f)
        {
            position.x += Mathf.Round((cameraPosition.x - position.x) / repeatWidth) * repeatWidth;
        }
        if (repeatHeight > 0f)
        {
            position.y += Mathf.Round((cameraPosition.y - position.y) / repeatHeight) * repeatHeight;
        }

        if (pixelsPerUnit > 0f)
        {
            position.x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
            position.y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position = position;
    }

    // The center of the room the player is in, eased when the room changes. Snaps on the first frame (or right after the
    // biome is shown) and after teleports.
    private Vector2 GetEasedRoomCenter(Vector2 cameraPosition)
    {
        WorldStreamer streamer = WorldStreamer.Instance;
        bool hasRoom = streamer != null && streamer.CurrentRoom != null && streamer.CurrentRoom.Bounds != null;

        // No room yet: stay where we are, or start at the camera, which keeps the layer framed as placed
        Vector2 target = hasRoom
            ? (Vector2)streamer.CurrentRoom.Bounds.bounds.center
            : (_hasRoomAnchor ? _roomAnchor : cameraPosition);

        bool teleported = _hasRoomAnchor && Vector2.Distance(cameraPosition, _lastCameraPosition) > teleportDistance;
        if (!_hasRoomAnchor || teleported || roomChangeGlide <= 0f)
        {
            _roomAnchor = target;
            _roomAnchorVelocity = Vector2.zero;
            _hasRoomAnchor = true;
        }
        else
        {
            _roomAnchor = Vector2.SmoothDamp(_roomAnchor, target, ref _roomAnchorVelocity, roomChangeGlide);
        }

        _lastCameraPosition = cameraPosition;
        return _roomAnchor;
    }
}
