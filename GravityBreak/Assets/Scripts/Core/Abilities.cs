using System.Collections.Generic;

namespace GravityBreak
{
    public enum AbilityType { FastRotation, DiagonalShift, GravityAnchor, GravityPulse }

    /// <summary>
    /// Unlocked abilities are never consumed (GDD: "like a spell list"). Before higher-difficulty sections
    /// the player picks a loadout of up to two; an ability only works if it is unlocked and, when the
    /// section requires a loadout, selected.
    /// </summary>
    public static class Abilities
    {
        public const int LoadoutSize = 2;

        public static readonly HashSet<AbilityType> Unlocked = new HashSet<AbilityType>();
        public static readonly List<AbilityType> Loadout = new List<AbilityType>();
        public static bool LoadoutRequired;

        public static bool IsActive(AbilityType a)
        {
            return Unlocked.Contains(a) && (!LoadoutRequired || Loadout.Contains(a));
        }

        public static string Title(AbilityType a)
        {
            switch (a)
            {
                case AbilityType.FastRotation: return "Fast Rotation";
                case AbilityType.DiagonalShift: return "Diagonal Shift";
                case AbilityType.GravityAnchor: return "Gravity Anchor";
                default: return "Gravity Pulse";
            }
        }

        public static string Description(AbilityType a)
        {
            switch (a)
            {
                case AbilityType.FastRotation: return "Gravity shift recharges more than twice as fast.";
                case AbilityType.DiagonalShift: return "Hold the diagonal modifier while shifting to aim gravity between two walls.";
                case AbilityType.GravityAnchor: return "Briefly pause a fall in mid-air.";
                default: return "Shove nearby debris away from you.";
            }
        }
    }
}
