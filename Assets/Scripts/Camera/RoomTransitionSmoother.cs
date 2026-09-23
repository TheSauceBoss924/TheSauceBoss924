using UnityEngine;
using Unity.Cinemachine;

// The RoomTransitionSmoother is a Cinemachine extension that goes on the CinemachineCamera, next to the Confiner 2D.
// It softens the camera jump when the player walks into a new room.
//
// WHY IT'S NEEDED:
// While the player is in a room, the Confiner 2D holds the camera inside that room's bounds. When the room changes,
// the confiner switches to the new room's bounds and lets go of the camera all at once, so the camera jumps to where it
// should be in the new room in a single frame. The confiner's own Damping doesn't help here, because it only smooths
// changes it detects as going around a corner of the bounds, not a change of bounds.
//
// HOW IT WORKS:
// For a short window after the room changes, it watches the camera's position every frame. If the camera tries to move
// faster than it normally would (a jump), it keeps the camera where it was and remembers the difference as an offset.
// That offset then shrinks smoothly to zero, so the camera glides into the new room instead of snapping.
// It runs after everything else in the camera pipeline (the Finalize stage), so it smooths the confiner's result.
//
// Teleports (respawn, loading a save, dev mode room teleport) still cut instantly: CameraTarget.SnapToPlayer tells
// Cinemachine the previous frame isn't valid anymore, and this resets whenever that happens.
public class RoomTransitionSmoother : CinemachineExtension
{
    [Tooltip("How long the glide into a new room takes, in seconds. Higher = slower, softer.")]
    [SerializeField] private float glideTime = 0.5f;

    [Tooltip("How long after a room change to watch for the jump, in seconds.")]
    [SerializeField] private float watchWindow = 0.5f;

    [Tooltip("Camera movement faster than this (units per second) during the watch window counts as a jump and gets smoothed. " +
             "Keep it above the fastest the camera normally moves (e.g. during a dash) so normal movement isn't slowed down.")]
    [SerializeField] private float jumpSpeed = 40f;

    private Vector3 _offset;
    private Vector3 _offsetVelocity;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private RoomTrigger _lastRoom;
    private float _watchTimer;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        // Run last, after the confiner has already done its job
        if (stage != CinemachineCore.Stage.Finalize) return;

        Vector3 position = state.GetCorrectedPosition();

        // Start watching whenever the WorldStreamer reports a new room
        RoomTrigger room = WorldStreamer.Instance != null ? WorldStreamer.Instance.CurrentRoom : null;
        if (room != _lastRoom)
        {
            _lastRoom = room;
            _watchTimer = watchWindow;
        }

        // Teleports and the first frame cut straight to the new position, so reset and don't smooth anything
        if (!vcam.PreviousStateIsValid || deltaTime < 0f || !_hasLastPosition)
        {
            _offset = Vector3.zero;
            _offsetVelocity = Vector3.zero;
            _lastPosition = position;
            _hasLastPosition = true;
            return;
        }

        // During the watch window, a move faster than jumpSpeed is the confiner letting go.
        // Absorb it into the offset so the camera stays put this frame, and let the offset shrink away over glideTime
        if (_watchTimer > 0f)
        {
            _watchTimer -= deltaTime;

            Vector3 move = position - _lastPosition;
            if (deltaTime > 0f && move.magnitude / deltaTime > jumpSpeed)
            {
                _offset -= move;
            }
        }
        _lastPosition = position;

        if (_offset.sqrMagnitude > 0.00001f)
        {
            _offset = Vector3.SmoothDamp(_offset, Vector3.zero, ref _offsetVelocity, glideTime, Mathf.Infinity, deltaTime);
            state.PositionCorrection += _offset;
        }
        else
        {
            _offset = Vector3.zero;
            _offsetVelocity = Vector3.zero;
        }
    }
}
