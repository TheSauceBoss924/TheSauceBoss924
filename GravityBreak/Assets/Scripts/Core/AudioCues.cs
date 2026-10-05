using System.Collections.Generic;
using UnityEngine;

namespace GravityBreak
{
    public enum Cue
    {
        GravityShift, Denied, Jump, Land, HardLand, Hit, DebrisIncoming, ArcOn, Vent,
        Pickup, Interact, QuestStep, Collapse, SectionClear, Fail, AI, UI
    }

    /// <summary>
    /// Procedurally generated sound effects (no audio files needed). Every cue is positional in stereo
    /// (pan = where it is on screen, pitch nudged by height) so it works as a spatial audio cue for blind
    /// players, and every cue can post a caption + on-screen ping for deaf players.
    /// </summary>
    public static class AudioCues
    {
        struct Def
        {
            public float f0, f1, dur, vol;
            public int wave; // 0 sine, 1 square, 2 noise
            public string caption;
            public Def(float f0, float f1, float dur, float vol, int wave, string caption)
            { this.f0 = f0; this.f1 = f1; this.dur = dur; this.vol = vol; this.wave = wave; this.caption = caption; }
        }

        const int Rate = 22050;
        static readonly Dictionary<Cue, Def> defs = new Dictionary<Cue, Def>
        {
            { Cue.GravityShift, new Def(220, 660, 0.22f, 0.5f, 0, "[Gravity shifts]") },
            { Cue.Denied, new Def(160, 110, 0.18f, 0.4f, 1, "[Not ready]") },
            { Cue.Jump, new Def(300, 520, 0.10f, 0.3f, 0, null) },
            { Cue.Land, new Def(140, 90, 0.08f, 0.3f, 2, null) },
            { Cue.HardLand, new Def(120, 50, 0.30f, 0.7f, 2, "[Hard landing]") },
            { Cue.Hit, new Def(200, 60, 0.28f, 0.7f, 2, "[Impact]") },
            { Cue.DebrisIncoming, new Def(880, 880, 0.35f, 0.45f, 1, "[Debris incoming]") },
            { Cue.ArcOn, new Def(1400, 600, 0.25f, 0.4f, 2, "[Electrical arc]") },
            { Cue.Vent, new Def(500, 480, 0.40f, 0.35f, 2, "[Gas venting]") },
            { Cue.Pickup, new Def(600, 1200, 0.15f, 0.5f, 0, "[Repair kit collected]") },
            { Cue.Interact, new Def(400, 800, 0.20f, 0.5f, 1, "[Console activated]") },
            { Cue.QuestStep, new Def(523, 784, 0.35f, 0.5f, 0, "[Objective complete]") },
            { Cue.Collapse, new Def(60, 40, 0.80f, 0.6f, 2, "[Station groans]") },
            { Cue.SectionClear, new Def(523, 1046, 0.60f, 0.5f, 0, "[Section cleared]") },
            { Cue.Fail, new Def(300, 80, 0.70f, 0.6f, 1, "[Section lost]") },
            { Cue.AI, new Def(700, 700, 0.08f, 0.25f, 0, null) },
            { Cue.UI, new Def(500, 500, 0.05f, 0.25f, 0, null) },
        };

        static readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        static AudioSource[] pool;
        static int next;

        public static void Init(GameObject host)
        {
            clips.Clear();
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                AudioSource s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                pool[i] = s;
            }
        }

        static AudioClip Clip(Cue cue)
        {
            AudioClip c;
            if (clips.TryGetValue(cue, out c) && c != null) return c;
            Def d = defs[cue];
            int n = Mathf.Max(1, (int)(Rate * d.dur));
            float[] data = new float[n];
            float phase = 0f;
            System.Random rng = new System.Random((int)cue * 7919 + 13);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(d.f0, d.f1, t);
                phase += 2f * Mathf.PI * f / Rate;
                float s;
                if (d.wave == 0) s = Mathf.Sin(phase);
                else if (d.wave == 1) s = Mathf.Sin(phase) >= 0 ? 0.6f : -0.6f;
                else s = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.8f;
                float env = Mathf.Min(1f, i / (Rate * 0.005f)) * Mathf.Pow(1f - t, 1.6f);
                data[i] = s * env * d.vol;
            }
            c = AudioClip.Create("gb_" + cue, n, 1, Rate, false);
            c.SetData(data, 0);
            clips[cue] = c;
            return c;
        }

        public static void Play(Cue cue, Vector2? worldPos = null, bool caption = true)
        {
            if (pool == null) return;
            Def d = defs[cue];
            float pan = 0f, vol = 1f, pitch = 1f;
            Camera cam = Camera.main;
            if (worldPos.HasValue && cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(worldPos.Value);
                pan = Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f);
                pitch = 1f + Mathf.Clamp((vp.y - 0.5f) * 0.5f, -0.25f, 0.25f);
                PlayerController p = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (p != null)
                    vol = Mathf.Clamp01(1.25f - Vector2.Distance(worldPos.Value, p.transform.position) / 20f);
            }
            AudioSource src = pool[next];
            next = (next + 1) % pool.Length;
            src.panStereo = pan;
            src.pitch = pitch;
            src.volume = vol;
            src.PlayOneShot(Clip(cue));

            if (caption && d.caption != null) Captions.Add(d.caption, worldPos);
        }
    }

    /// <summary>Closed captions and on-screen pings (the visual twin of every audio cue).</summary>
    public static class Captions
    {
        public class Line { public string text; public float expire; }
        public class Ping { public Vector2 pos; public string label; public float expire; }

        public static readonly List<Line> Lines = new List<Line>();
        public static readonly List<Ping> Pings = new List<Ping>();

        public static void Add(string text, Vector2? worldPos)
        {
            float now = Time.unscaledTime;
            if (Lines.Count > 0 && Lines[Lines.Count - 1].text == text && Lines[Lines.Count - 1].expire - now > 1.5f) return;
            Lines.Add(new Line { text = text, expire = now + 3f });
            if (Lines.Count > 4) Lines.RemoveAt(0);
            if (worldPos.HasValue) Pings.Add(new Ping { pos = worldPos.Value, label = text, expire = now + 1.2f });
        }

        public static void Prune()
        {
            float now = Time.unscaledTime;
            Lines.RemoveAll(l => l.expire < now);
            Pings.RemoveAll(p => p.expire < now);
        }

        public static void Clear() { Lines.Clear(); Pings.Clear(); }
    }

    /// <summary>Station AI: tutorial guidance, tips and mission updates. Always shown as text.</summary>
    public static class StationAI
    {
        public static string Current = "";
        public static float Expire;
        public static bool Essential;

        public static void Say(string text, float seconds = 6f, bool essential = false)
        {
            Current = text;
            Essential = essential;
            Expire = Time.unscaledTime + seconds;
            AudioCues.Play(Cue.AI, null, false);
        }

        public static void Clear() { Current = ""; Expire = 0f; }

        public static bool Visible
        {
            get { return Current.Length > 0 && Time.unscaledTime < Expire && (Essential || Settings.Captions); }
        }
    }
}
