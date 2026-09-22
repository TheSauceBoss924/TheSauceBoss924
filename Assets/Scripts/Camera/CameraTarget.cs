using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// This is the Camera Target script that is attached to the camera target object. It handles the camera's position and behavior based on the player's movement and state.
// Both the Target and the Camera are Prefabs so that you can place them wherever you want in whatever scene you want, and then assign all the Player's
// properties accordingly. The camera target is the object that the camera follows, and it is responsible for calculating the camera's position based on the player's movement and state.

public class CameraTarget : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private StateController stateController;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Horizontal Lead")]
    [SerializeField] private float horizontalLeadDistance = 3.5f;
    [SerializeField] private float horizontalLeadSmoothing = 4f;

    [Header("Vertical Offset")]
    [SerializeField] private float verticalFollowSmoothTime = 0.15f; // seconds to close the gap. lower = snappier, higher = smoother
    [SerializeField] private float verticalFollowMaxSpeed = 25f; // max speed the camera can move vertically.

    // The following variables are used to control the camera's behavior when the player is falling.
    // The fallPanDistance is the distance the camera will pan down when the player is falling,
    // and the fallPanSmoothing is how quickly the camera will pan down.
    // The fallPanTriggerDistance is the distance the player must fall before the camera starts to pan down.
    [Header("Vertical Pan (Falling)")]
    [SerializeField] private float fallPanDistance = 1.25f;
    [SerializeField] private float fallPanSmoothing = 4f;
    [SerializeField] private float fallPanTriggerDistance = 3f;

    // The following variables are used to control the camera's behavior when the player is falling or ascending.
    [Header("Fall Zoom")]
    [SerializeField] private float fallZoomAmount = 1f;
    [SerializeField] private float fallZoomSmoothing = 4f;
    [SerializeField] private float fallVelocityThreshold = 5f;
    [SerializeField] private float fallVelocityMax = 20f;

    [Header("Ascent Zoom")]
    [SerializeField] private float ascentZoomAmount = 1f;
    [SerializeField] private float ascentZoomSmoothing = 4f;
    [SerializeField] private float ascentVelocityThreshold = 8f;
    [SerializeField] private float ascentVelocityMax = 20f;

    // The following variables are used to control the camera's behavior when the player is holding down the "look down" (S) key.
    [Header("Look Down")]
    [SerializeField] private float lookDownDelay = 0.3f;
    [SerializeField] private float lookDownDistance = 3f;
    [SerializeField] private float lookDownSmoothing = 5f;

    private float _fallPanOffset;
    private float _fallStartY;
    private bool _wasFalling;

    private float _lookDownHoldTimer;
    private float _lookDownOffset;

    private float _followedPlayerY;
    private float _followedPlayerYVelocity;

    private Vector3 _baselineOffset;
    private Vector3 _currentOffset;
    private float _lastDirection = 1f;

    private float _baseOrthoSize;
    private float _fallZoomOffset;
    private float _ascentZoomOffset;

    private void Start()
    {
        if (player != null)
        {
            _baselineOffset = transform.position - player.transform.position;
            _currentOffset = _baselineOffset;
            _followedPlayerY = player.transform.position.y;
        }

        if (cinemachineCamera != null)
        {
            _baseOrthoSize = cinemachineCamera.Lens.OrthographicSize;
        }
    }

    private void LateUpdate()
    {
        if (playerRb == null || player == null) return;

        // Horizontal lead
        if (Mathf.Abs(playerRb.linearVelocity.x) > 0.1f)
        {
            _lastDirection = Mathf.Sign(playerRb.linearVelocity.x);
        }
        float targetX = _lastDirection * horizontalLeadDistance;
        _currentOffset.x = Mathf.Lerp(_currentOffset.x, targetX, Time.deltaTime * horizontalLeadSmoothing);

        // This block handles the vertical follow logic.
        // It checks if the player's vertical position has changed beyond a certain threshold,
        // and if so, it smoothly adjusts the camera's vertical position to follow the player.;
        _followedPlayerY = Mathf.SmoothDamp(
            _followedPlayerY,
            player.transform.position.y,
            ref _followedPlayerYVelocity,
            verticalFollowSmoothTime,
            verticalFollowMaxSpeed
        );
        // Fall pan
        if (stateController.IsFalling && !_wasFalling)
        {
            _fallStartY = player.transform.position.y;
        }
        _wasFalling = stateController.IsFalling;
        float fallDistance = _fallStartY - player.transform.position.y;
        bool shouldFallPan = stateController.IsFalling && fallDistance > fallPanTriggerDistance;
        float fallTarget = shouldFallPan ? fallPanDistance : 0f;
        _fallPanOffset = Mathf.Lerp(_fallPanOffset, fallTarget, Time.deltaTime * fallPanSmoothing);

        // This block handles the zoom logic based on the player's vertical velocity.
        // It will zoom out when the player is falling and ascending, based on the defined thresholds and maximums.
        if (cinemachineCamera != null)
        {
            float vSpeed = playerRb.linearVelocity.y;

            // Fall zoom, negate so downward speed reads as a positive magnitude
            float fallT = Mathf.InverseLerp(fallVelocityThreshold, fallVelocityMax, -vSpeed);
            float fallZoomTarget = fallT * fallZoomAmount;
            _fallZoomOffset = Mathf.Lerp(_fallZoomOffset, fallZoomTarget, Time.deltaTime * fallZoomSmoothing);

            // Ascent zoom
            float ascentT = Mathf.InverseLerp(ascentVelocityThreshold, ascentVelocityMax, vSpeed);
            float ascentZoomTarget = ascentT * ascentZoomAmount;
            _ascentZoomOffset = Mathf.Lerp(_ascentZoomOffset, ascentZoomTarget, Time.deltaTime * ascentZoomSmoothing);

            float totalZoom = Mathf.Max(_fallZoomOffset, _ascentZoomOffset);

            var lens = cinemachineCamera.Lens;
            lens.OrthographicSize = _baseOrthoSize + totalZoom;
            cinemachineCamera.Lens = lens;
        }

        // Look down, polled directly here since this is camera behavior,
        // not a gameplay action, so it doesn't go through the ability/input-binding pipeline
        bool isIdle = stateController.GetCurrentState() == stateController.idleState;
        bool holdingDown = Keyboard.current != null && Keyboard.current.sKey.isPressed;

        if (isIdle && holdingDown)
        {
            _lookDownHoldTimer += Time.deltaTime;
        }
        else
        {
            _lookDownHoldTimer = 0f;
        }

        bool shouldLookDown = _lookDownHoldTimer >= lookDownDelay;
        float lookDownTarget = shouldLookDown ? lookDownDistance : 0f;
        _lookDownOffset = Mathf.Lerp(_lookDownOffset, lookDownTarget, Time.deltaTime * lookDownSmoothing);

        // Combine, shared axis, use the larger rather than stacking them
        float totalDownPan = Mathf.Max(_fallPanOffset, _lookDownOffset);
        _currentOffset.y = _baselineOffset.y - totalDownPan;

        transform.position = new Vector3(
            player.transform.position.x + _currentOffset.x,
            _followedPlayerY + _currentOffset.y,
            transform.position.z
        );
    }

    public void SnapToPlayer(float facingDirection = 1f)
    {
        if (player == null) return;

        _followedPlayerY = player.transform.position.y;
        _lastDirection = facingDirection;
        _currentOffset.x = _lastDirection * horizontalLeadDistance;
        _currentOffset.y = _baselineOffset.y;

        transform.position = new Vector3(
            player.transform.position.x + _currentOffset.x,
            _followedPlayerY + _baselineOffset.y,
            transform.position.z
        );
    }
}
