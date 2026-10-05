using UnityEngine;

namespace GravityBreak
{
    public class LevelInfo
    {
        public Transform root;
        public Vector2 start;
        public Rect bounds; // interior, world units
    }

    /// <summary>Builds a section from its ASCII map at runtime. Cell (x, row) sits at world (x, height-1-row).</summary>
    public static class LevelBuilder
    {
        public static LevelInfo Build(SectionDefinition def, Transform parent)
        {
            int h = def.rows.Length;
            int w = 0;
            for (int i = 0; i < h; i++) w = Mathf.Max(w, def.rows[i].Length);

            GameObject root = new GameObject("Level");
            root.transform.SetParent(parent, false);

            // Walls: one static body whose box colliders merge into a single composite, so the player slides
            // along flat surfaces without catching on tile seams (important when "floor" can be any wall).
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);
            Rigidbody2D wallBody = walls.AddComponent<Rigidbody2D>();
            wallBody.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = walls.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            composite.sharedMaterial = new PhysicsMaterial2D("gb_wall") { friction = 0f, bounciness = 0f };

            LevelInfo info = new LevelInfo { root = root.transform };
            info.bounds = new Rect(-0.5f, -0.5f, w, h);

            for (int r = -1; r <= h; r++)
            {
                for (int x = -1; x <= w; x++)
                {
                    char c = '.';
                    bool border = r < 0 || r >= h || x < 0 || x >= w;
                    if (!border && x < def.rows[r].Length) c = def.rows[r][x];
                    Vector2 pos = new Vector2(x, h - 1 - r);

                    if (border || c == '#') { MakeWall(walls.transform, pos, def.wallColor); continue; }
                    Spawn(c, root.transform, pos, info);
                }
            }
            return info;
        }

        static void MakeWall(Transform parent, Vector2 pos, Color color)
        {
            GameObject t = SpriteFactory.Make("Wall", parent, Shape.Tile, pos, Vector2.one, 0, color);
            BoxCollider2D col = t.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
#if UNITY_2022_2_OR_NEWER
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
            col.usedByComposite = true;
#endif
        }

        static void Spawn(char c, Transform root, Vector2 pos, LevelInfo info)
        {
            switch (c)
            {
                case 'P': info.start = pos; break;
                case 'S': MakeConsole(root, pos, InteractKind.Power); break;
                case 'B': MakeConsole(root, pos, InteractKind.Breach); break;
                case 'E': MakeConsole(root, pos, InteractKind.Exit); break;
                case 'a': MakeHazard(root, pos, HazardKind.Arc, Shape.Triangle, 1.5f, 1.5f, pos.x * 0.37f); break;
                case 'v': MakeHazard(root, pos, HazardKind.Vent, Shape.Cross, 2.5f, 2f, pos.y * 0.53f); break;
                case 'o': MakePickup(root, pos); break;
            }
        }

        static void MakeConsole(Transform root, Vector2 pos, InteractKind kind)
        {
            Vector2 size = kind == InteractKind.Exit ? new Vector2(0.9f, 0.9f) : new Vector2(0.7f, 0.7f);
            GameObject go = SpriteFactory.Make(kind.ToString(), root, Shape.Circle, pos, size, 2, Color.white);
            go.AddComponent<PaletteTint>().role = Role.Interact;
            BoxCollider2D trig = go.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(2f, 2f);
            Interactable it = go.AddComponent<Interactable>();
            it.kind = kind;
        }

        static void MakeHazard(Transform root, Vector2 pos, HazardKind kind, Shape shape, float on, float off, float phase)
        {
            GameObject go = SpriteFactory.Make(kind.ToString(), root, shape, pos, new Vector2(0.9f, 0.9f), 3, Color.white);
            go.AddComponent<PaletteTint>().role = Role.Hazard;
            BoxCollider2D trig = go.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(0.8f, 0.8f);
            Hazard h = go.AddComponent<Hazard>();
            h.kind = kind;
            h.damage = kind == HazardKind.Arc ? 15f : 10f; // vent: per second
            h.onTime = on;
            h.offTime = off;
            h.phase = phase;
        }

        static void MakePickup(Transform root, Vector2 pos)
        {
            GameObject go = SpriteFactory.Make("RepairKit", root, Shape.Square, pos, new Vector2(0.5f, 0.5f), 2, Color.white);
            go.AddComponent<PaletteTint>().role = Role.Safe;
            BoxCollider2D trig = go.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(0.8f, 0.8f);
            go.AddComponent<RepairPickup>();
        }
    }
}
