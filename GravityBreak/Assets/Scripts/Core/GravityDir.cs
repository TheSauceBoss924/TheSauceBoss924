using UnityEngine;

namespace GravityBreak
{
    /// <summary>Gravity directions in 45-degree steps. Even values are cardinal, odd are diagonal.</summary>
    public enum GravityDir { Down, DownRight, Right, UpRight, Up, UpLeft, Left, DownLeft }

    public static class GravityDirExt
    {
        public static float AngleDeg(this GravityDir d) { return -90f + 45f * (int)d; }

        public static Vector2 ToVector(this GravityDir d)
        {
            float a = d.AngleDeg() * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>Camera / player roll (degrees, counter-clockwise) that makes this gravity point screen-down.</summary>
        public static float RollDeg(this GravityDir d) { return 45f * (int)d; }

        public static bool IsCardinal(this GravityDir d) { return ((int)d & 1) == 0; }

        public static string Label(this GravityDir d)
        {
            switch (d)
            {
                case GravityDir.DownRight: return "DOWN-RIGHT";
                case GravityDir.UpRight: return "UP-RIGHT";
                case GravityDir.UpLeft: return "UP-LEFT";
                case GravityDir.DownLeft: return "DOWN-LEFT";
                default: return d.ToString().ToUpperInvariant();
            }
        }
    }

    public static class GravityDirs
    {
        public static GravityDir Nearest(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            int idx = Mathf.RoundToInt((angle + 90f) / 45f);
            idx = ((idx % 8) + 8) % 8;
            return (GravityDir)idx;
        }

        /// <summary>Diagonal halfway between two perpendicular cardinal directions.</summary>
        public static GravityDir Combine(GravityDir a, GravityDir b)
        {
            return Nearest(a.ToVector() + b.ToVector());
        }
    }

    /// <summary>Rigidbody2D velocity API changed names in Unity 6.</summary>
    public static class Rb2DCompat
    {
        public static Vector2 GetVel(this Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody2D rb, Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }
    }
}
