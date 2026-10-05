using System.Collections.Generic;
using UnityEngine;

namespace GravityBreak
{
    public enum Shape { Square, Tile, Circle, Triangle, Diamond, Cross }

    /// <summary>Generates all placeholder art at runtime so the prototype needs no imported assets.</summary>
    public static class SpriteFactory
    {
        const int N = 32;
        static readonly Dictionary<Shape, Sprite> cache = new Dictionary<Shape, Sprite>();

        public static Sprite Get(Shape shape)
        {
            Sprite s;
            if (cache.TryGetValue(shape, out s) && s != null) return s;

            Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f;
                    float v = (y + 0.5f) / N * 2f - 1f;
                    byte shade = 255;
                    bool inside = Inside(shape, u, v, x, y, ref shade);
                    px[y * N + x] = inside ? new Color32(shade, shade, shade, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            s.name = "gb_" + shape;
            cache[shape] = s;
            return s;
        }

        static bool Inside(Shape shape, float u, float v, int x, int y, ref byte shade)
        {
            switch (shape)
            {
                case Shape.Square: return true;
                case Shape.Tile:
                    bool edge = x < 2 || y < 2 || x >= N - 2 || y >= N - 2;
                    shade = (byte)(edge ? 150 : 255);
                    return true;
                case Shape.Circle: return u * u + v * v <= 1f;
                case Shape.Triangle: return Mathf.Abs(u) <= (1f - v) * 0.5f; // apex up
                case Shape.Diamond: return Mathf.Abs(u) + Mathf.Abs(v) <= 1f;
                case Shape.Cross: return Mathf.Abs(u) < 0.3f || Mathf.Abs(v) < 0.3f;
            }
            return false;
        }

        public static GameObject Make(string name, Transform parent, Shape shape, Vector2 pos, Vector2 scale, int order, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Get(shape);
            sr.sortingOrder = order;
            sr.color = color;
            return go;
        }
    }
}
