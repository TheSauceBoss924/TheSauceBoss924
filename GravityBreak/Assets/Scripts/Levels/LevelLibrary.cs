using UnityEngine;

namespace GravityBreak
{
    /// <summary>
    /// A short, self-contained level. Maps are ASCII grids (row 0 = top); the outer wall is added automatically.
    ///   #  solid wall      .  empty         P  player start
    ///   S  power switch    B  hull breach   E  airlock exit
    ///   a  electrical arc  v  gas vent      o  repair kit
    /// </summary>
    public class SectionDefinition
    {
        public string id, displayName, zone, intro;
        public bool tutorial;
        public bool loadoutRequired;
        public float timeLimit, gold, silver, bronze;
        public float debrisInterval;     // seconds between debris drops, 0 = none
        public float collapseDrain;      // fraction of max integrity the collapse drains over timeLimit
        public AbilityType? unlocks;     // ability granted on first clear
        public Color wallColor;
        public string[] rows;
    }

    public static class LevelLibrary
    {
        public static readonly SectionDefinition[] All =
        {
            new SectionDefinition
            {
                id = "sec1_tutorial", displayName = "Engineering: Orientation", zone = "Engineering",
                tutorial = true, loadoutRequired = false,
                timeLimit = 300f, gold = 60f, silver = 90f, bronze = 150f,
                debrisInterval = 0f, collapseDrain = 0.0f,
                unlocks = AbilityType.FastRotation,
                intro = "Welcome to The Halcyon Reach. Let's get you out of here.",
                wallColor = new Color(0.25f, 0.35f, 0.5f),
                rows = new[]
                {
                    "..................S.......",
                    "..........................",
                    ".........................B",
                    "..........................",
                    "E.........................",
                    "..........................",
                    "..........................",
                    "..........................",
                    "..........................",
                    "...........#..............",
                    ".P....o..................."
                }
            },
            new SectionDefinition
            {
                id = "sec2_engineering", displayName = "Engineering: Rotating Corridors", zone = "Engineering",
                tutorial = false, loadoutRequired = false,
                timeLimit = 120f, gold = 50f, silver = 70f, bronze = 95f,
                debrisInterval = 0f, collapseDrain = 0.35f,
                unlocks = AbilityType.DiagonalShift,
                intro = "Corridors ahead are sealed. Use gravity to move between decks.",
                wallColor = new Color(0.25f, 0.35f, 0.5f),
                rows = new[]
                {
                    "........a.....S.....o.........",
                    "..............................",
                    "..............................",
                    "..............................",
                    "##########################...#",
                    "..............................",
                    "..............................",
                    "B.............................",
                    "...............a..............",
                    "#...##########################",
                    "..............................",
                    ".............................E",
                    "..........o....v..........P..."
                }
            },
            new SectionDefinition
            {
                id = "sec3_cargo", displayName = "Cargo Bay 4", zone = "Cargo Bay",
                tutorial = false, loadoutRequired = true,
                timeLimit = 140f, gold = 60f, silver = 85f, bronze = 115f,
                debrisInterval = 3.5f, collapseDrain = 0.4f,
                unlocks = AbilityType.GravityAnchor,
                intro = "Cargo is breaking loose. Watch for debris overhead.",
                wallColor = new Color(0.4f, 0.32f, 0.25f),
                rows = new[]
                {
                    "........S.........................",
                    "..................................",
                    "..............##..................",
                    "..............##..........#####...",
                    "..................................",
                    "..................................",
                    ".....####........................B",
                    "..................................",
                    "o.................###.............",
                    "..................###.............",
                    "..................................",
                    "..................................",
                    "..........###.....................",
                    "..........###...........###.......",
                    "..........###...........###......E",
                    ".P........###.........o.###......."
                }
            },
            new SectionDefinition
            {
                id = "sec4_lab", displayName = "Research Lab C", zone = "Research Lab",
                tutorial = false, loadoutRequired = true,
                timeLimit = 150f, gold = 70f, silver = 95f, bronze = 125f,
                debrisInterval = 0f, collapseDrain = 0.4f,
                unlocks = AbilityType.GravityPulse,
                intro = "Arcs and venting gas ahead. Time your crossings.",
                wallColor = new Color(0.3f, 0.2f, 0.45f),
                rows = new[]
                {
                    "..........#.....B.v...v.........",
                    "..........#.....................",
                    "..........#.....................",
                    "..........#.....................",
                    "..........#.........#...........",
                    "..........#.........#...........",
                    "..........#....o....#.....o.....",
                    "..........#.........#...........",
                    "..........#.........#.....###...",
                    "..........#.........#...........",
                    "....................#...........",
                    "....................#...........",
                    "....................#...........",
                    "..P..a.a..a.a.S.....#...a......E"
                }
            },
            new SectionDefinition
            {
                id = "sec5_command", displayName = "Command Deck", zone = "Command Deck",
                tutorial = false, loadoutRequired = true,
                timeLimit = 160f, gold = 75f, silver = 105f, bronze = 140f,
                debrisInterval = 3.0f, collapseDrain = 0.5f,
                unlocks = null,
                intro = "This is it. Open the airlock and get off this station.",
                wallColor = new Color(0.45f, 0.2f, 0.25f),
                rows = new[]
                {
                    ".....S......#.....E....#............",
                    "............#..........#............",
                    "...o........#..........#............",
                    "............#..........#............",
                    "....####.....................####...",
                    "....................................",
                    "v...............####................",
                    "................####................",
                    "..................o................B",
                    "....................................",
                    "........###..............###........",
                    "........###..............###........",
                    "........###..............###........",
                    "........###..............###.......v",
                    "........###..............###........",
                    ".P......###...a......a...###........"
                }
            }
        };
    }
}
