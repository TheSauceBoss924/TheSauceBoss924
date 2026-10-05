using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityBreak
{
    public enum DamageSource { Debris, Hazard, Landing, Collapse }

    /// <summary>The Structural Integrity meter: the game's health resource.</summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        public float max = 100f;
        public float invulnerability = 0.5f;

        public float Current { get; private set; }
        public bool IsDepleted { get { return Current <= 0f; } }
        public readonly Dictionary<DamageSource, float> Taken = new Dictionary<DamageSource, float>();
        public event Action<float, DamageSource> Damaged;

        float lastHit = -99f;

        void Awake() { ResetFull(); }

        public void ResetFull()
        {
            Current = max;
            Taken.Clear();
            foreach (DamageSource s in Enum.GetValues(typeof(DamageSource))) Taken[s] = 0f;
            lastHit = -99f;
        }

        /// <param name="continuous">Small per-frame damage (gas, collapse) that skips the hit cooldown and the hit effects.</param>
        public bool Damage(float amount, DamageSource source, bool continuous = false)
        {
            if (IsDepleted || amount <= 0f) return false;
            if (!continuous && Time.time - lastHit < invulnerability) return false;

            Current = Mathf.Max(0f, Current - amount);
            Taken[source] += amount;
            if (!continuous)
            {
                lastHit = Time.time;
                AudioCues.Play(Cue.Hit, transform.position);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.25f);
            }
            if (Damaged != null) Damaged(amount, source);
            return true;
        }

        public void Heal(float amount) { Current = Mathf.Min(max, Current + amount); }
    }
}
