using UnityEngine;

namespace GravityBreak
{
    public enum InteractKind { Power, Breach, Exit }

    /// <summary>A quest console: power switch, hull-breach panel or the airlock exit.</summary>
    public class Interactable : MonoBehaviour
    {
        public InteractKind kind;
        public bool Done { get; private set; }
        PaletteTint tint;

        public string Prompt
        {
            get
            {
                switch (kind)
                {
                    case InteractKind.Power: return "Restore emergency power";
                    case InteractKind.Breach: return "Seal the hull breach";
                    default: return "Open the airlock";
                }
            }
        }

        void Awake() { tint = GetComponent<PaletteTint>(); }

        public void TryActivate()
        {
            if (Done || GameManager.Instance.Runner == null) return;
            if (GameManager.Instance.Runner.TryComplete(kind)) MarkDone();
        }

        public void MarkDone()
        {
            Done = true;
            if (tint != null) { tint.role = Role.Safe; tint.alpha = 0.7f; }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController p;
            if (!Done && other.TryGetComponent(out p)) p.Nearby = this;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            PlayerController p;
            if (other.TryGetComponent(out p) && p.Nearby == this) p.Nearby = null;
        }
    }

    /// <summary>Repair kit: restores Structural Integrity.</summary>
    public class RepairPickup : MonoBehaviour
    {
        public float amount = 25f;

        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController p;
            if (!other.TryGetComponent(out p)) return;
            p.Integrity.Heal(amount);
            AudioCues.Play(Cue.Pickup, transform.position);
            Destroy(gameObject);
        }
    }
}
