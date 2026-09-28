using UnityEngine;
using UnityEngine.Rendering;

// ParallaxLayer goes on each background layer inside a biome background scene's Content object
// (e.g. Content/Sky, Content/FarMountains, Content/MidTrees). Each layer moves slower than the camera, so it looks far away.
//
// HOW IT'S MEASURED:
// The parallax is measured from the center of the room the player is currently in, not from the world origin.
// That keeps the offset small inside any room, so a layer only needs art big enough for one room, and every room shows
// the layer framed the same way. Place each layer in the biome scene as it should look when the camera is at the center
// of a room, i.e. arranged around the point (0, 0).
// When the room changes, the layer eases to the new room's framing over Room Change Glide seconds (match it to the
// RoomTransitionSmoother's Glide Time so it reads as part of the camera move). Teleports snap instantly.
//
// WHEN IT UPDATES:
// Right before the main camera renders, which is after Cinemachine has moved the camera for the frame, so layers never
// lag a frame behind the camera (no jitter). The camera lives in another scene, so it's found at runtime via Camera.main.
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("How much the layer follows the camera. 1 = stuck to the screen (sky), 0.8-0.95 = far away, 0.5 = middle distance, " +
             "0.2 = close, 0 = fixed in the world like the level. X and Y are separate so vertical movement can be subtler.")]
    [SerializeField] private Vector2 followCamera = new Vector2(0.8f, 0.9f);

    [Tooltip("Seconds for the layer to ease to the new room's framing when the room changes. " +
             "Match the RoomTransitionSmoother's Glide Time.")]
    [SerializeField] private float roomChangeGlide = 0.5f;

    [Tooltip("If the camera moves further than this in a single frame (a teleport), the layer snaps instead of easing.")]
    [SerializeField] private float teleportDistance = 10f;

    [Tooltip("For art that repeats sideways (e.g. a Sprite Renderer with Draw Mode set to Tiled): the width of one repeat " +
             "in world units. The layer shifts by whole repeats to stay under the camera, so it never runs out. 0 = off.")]
    [SerializeField] private float repeatWidth = 0f;

    // Where the layer was placed in the biome scene, i.e. where it sits when the camera is at the room's center
    private Vector3 _placedPosition;

    private Vector2 _anchor;
    private Vector2 _anchorVelocity;
    private bool _hasAnchor;
    private Vector2 _lastCameraPosition;

    private void Awake()
    {
        _placedPosition = transform.position;
    }

    // The layer only updates while its biome is shown. Coming back on (biome change) re-frames it instantly.
    private void OnEnable()
    {
        _hasAnchor = false;
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
        if (renderingCamera != mainCamera || mainCamera == null) return;

        Vector2 cameraPosition = mainCamera.transform.position;
        Vector2 targetAnchor = GetRoomCenter(cameraPosition);

        // Snap on the first frame (or right after the biome is shown) and after teleports, otherwise ease to the new room
        bool teleported = _hasAnchor && Vector2.Distance(cameraPosition, _lastCameraPosition) > teleportDistance;
        if (!_hasAnchor || teleported || roomChangeGlide <= 0f)
        {
            _anchor = targetAnchor;
            _anchorVelocity = Vector2.zero;
            _hasAnchor = true;
        }
        else
        {
            _anchor = Vector2.SmoothDamp(_anchor, targetAnchor, ref _anchorVelocity, roomChangeGlide);
        }
        _lastCameraPosition = cameraPosition;

        // followCamera = 1 keeps the layer at the same spot on screen, 0 keeps it at the same spot in the room
        Vector2 offsetFromAnchor = cameraPosition - _anchor;
        Vector3 position = new Vector3(
            _anchor.x + offsetFromAnchor.x * followCamera.x + _placedPosition.x,
            _anchor.y + offsetFromAnchor.y * followCamera.y + _placedPosition.y,
            _placedPosition.z);

        // Repeating art: move by whole repeats so the layer stays centered under the camera
        if (repeatWidth > 0f)
        {
            position.x += Mathf.Round((cameraPosition.x - position.x) / repeatWidth) * repeatWidth;
        }

        transform.position = position;
    }

    // The center of the room the player is in. Before any room is known, use the camera itself, which keeps the
    // layer framed as placed.
    private Vector2 GetRoomCenter(Vector2 fallback)
    {
        WorldStreamer streamer = WorldStreamer.Instance;
        if (streamer == null || streamer.CurrentRoom == null || streamer.CurrentRoom.Bounds == null)
        {
            return _hasAnchor ? _anchor : fallback;
        }
        return streamer.CurrentRoom.Bounds.bounds.center;
    }
}
