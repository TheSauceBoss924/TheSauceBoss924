using UnityEngine;

namespace GravityBreak
{
    /// <summary>Follows the player and rolls with gravity so "down" is always screen-down.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public Transform target;
        Vector3 basePos, vel;
        float shake;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        public void Snap(Transform t)
        {
            target = t;
            basePos = new Vector3(t.position.x, t.position.y, -10f);
            vel = Vector3.zero;
            transform.position = basePos;
            float roll = GravityManager.Instance != null ? GravityManager.Instance.Roll : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, roll);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = new Vector3(target.position.x, target.position.y, -10f);
            if (Time.deltaTime > 0f) basePos = Vector3.SmoothDamp(basePos, desired, ref vel, 0.12f);
            transform.rotation = Quaternion.Euler(0f, 0f, GravityManager.Instance.Roll);
            Vector3 offset = Vector3.zero;
            if (shake > 0f)
            {
                offset = (Vector3)(Random.insideUnitCircle * shake);
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.2f);
            }
            transform.position = basePos + offset;
        }
    }
}
