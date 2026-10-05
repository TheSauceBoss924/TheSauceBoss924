using System;
using UnityEngine;

namespace GravityBreak
{
    /// <summary>
    /// Owns the current gravity direction. Everything that falls (player, debris) asks this for the
    /// gravity vector instead of using Unity's built-in gravity, so "down" can be reassigned at runtime.
    /// </summary>
    public class GravityManager : MonoBehaviour
    {
        public static GravityManager Instance { get; private set; }

        public float strength = 32f;
        public float baseCooldown = 0.7f;
        [Tooltip("If false, gravity can only rotate to an adjacent (90 degree) orientation.")]
        public bool allowOppositeFlip = true;

        public GravityDir Dir { get; private set; }
        public Vector2 Vector { get { return Dir.ToVector(); } }
        /// <summary>Smoothed roll (degrees) applied to the camera and the player so rotation feels continuous.</summary>
        public float Roll { get; private set; }
        public float Cooldown { get; private set; }
        public float CooldownDuration
        {
            get { return baseCooldown * (Abilities.IsActive(AbilityType.FastRotation) ? 0.4f : 1f); }
        }

        public event Action<GravityDir, GravityDir> Changed;
        float rollVel;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void ResetTo(GravityDir d)
        {
            Dir = d;
            Roll = d.RollDeg();
            rollVel = 0f;
            Cooldown = 0f;
        }

        public bool TryShift(GravityDir target)
        {
            if (target == Dir) return false;
            if (!allowOppositeFlip && Vector2.Dot(target.ToVector(), Dir.ToVector()) < -0.9f) return false;
            if (Cooldown > 0f) return false;
            GravityDir prev = Dir;
            Dir = target;
            Cooldown = CooldownDuration;
            if (Changed != null) Changed(prev, Dir);
            return true;
        }

        void Update()
        {
            if (Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - Time.deltaTime);
            if (Time.deltaTime > 0f)
                Roll = Mathf.SmoothDampAngle(Roll, Dir.RollDeg(), ref rollVel, 0.14f);
        }
    }
}
