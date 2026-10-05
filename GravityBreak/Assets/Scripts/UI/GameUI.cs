using UnityEngine;

namespace GravityBreak
{
    /// <summary>
    /// All screens, drawn with IMGUI so the prototype needs no UI assets: title / save slots, station map,
    /// loadout, HUD, pause (map / settings / control remapping) and results. Mouse driven.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        const float VH = 720f;

        GUIStyle title, header, body, small, button, boxStyle, center;
        Texture2D white;
        float scale;
        float vw;
        string rebindMsg = "";
        Vector2 controlsScroll;

        static readonly string[] MedalNames = { "No medal", "Bronze", "Silver", "Gold" };

        void Setup()
        {
            if (title != null) return;
            white = Texture2D.whiteTexture;
            title = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            header = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            center = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            boxStyle = new GUIStyle(GUI.skin.box);
        }

        void OnGUI()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            Setup();
            scale = Screen.height / VH;
            vw = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Captions.Prune();

            switch (gm.State)
            {
                case GameState.Title: DrawTitle(gm); break;
                case GameState.Map: DrawMap(gm, true); break;
                case GameState.Briefing: DrawBriefing(gm); break;
                case GameState.Playing: DrawHud(gm); break;
                case GameState.Paused: DrawHud(gm); DrawPause(gm); break;
                case GameState.Result: DrawHud(gm); DrawResult(gm); break;
            }
        }

        // ---- helpers ----------------------------------------------------------------------------
        void Panel(Rect r)
        {
            Color c = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.92f);
            GUI.DrawTexture(r, white);
            GUI.color = c;
        }

        void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        static string Fmt(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return m + ":" + s.ToString("00.0");
        }

        bool Btn(string text, float w = 260f)
        {
            bool clicked = GUILayout.Button(text, button, GUILayout.Width(w), GUILayout.Height(40));
            if (clicked) AudioCues.Play(Cue.UI, null, false);
            return clicked;
        }

        // ---- title / save slots -----------------------------------------------------------------
        void DrawTitle(GameManager gm)
        {
            Panel(new Rect(0, 0, vw, VH));
            if (gm.Tab == PauseTab.Settings || gm.Tab == PauseTab.Controls) { DrawSettingsScreen(gm, true); return; }

            GUILayout.BeginArea(new Rect(vw / 2 - 330, 70, 660, 600));
            GUILayout.Label("GRAVITY BREAK", title);
            GUILayout.Label("Rotate gravity. Run for your life. Escape The Halcyon Reach.", center);
            GUILayout.Space(30);
            GUILayout.Label("Choose a save slot", header);
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                GUILayout.BeginHorizontal();
                string label = "Slot " + (i + 1) + " - ";
                if (SaveSystem.Exists(i))
                {
                    SaveData d = SaveSystem.Load(i);
                    label += d.records.Count + "/" + LevelLibrary.All.Length + " sections cleared";
                }
                else label += "New game";
                if (Btn(label, 440f)) gm.LoadSlot(i);
                if (SaveSystem.Exists(i) && Btn("Delete", 120f)) gm.DeleteSlot(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(20);
            if (Btn("Settings / Accessibility / Controls")) { gm.Tab = PauseTab.Settings; }
            GUILayout.EndArea();
        }

        // ---- station map ------------------------------------------------------------------------
        void DrawMap(GameManager gm, bool canLaunch)
        {
            if (canLaunch) Panel(new Rect(0, 0, vw, VH));
            Rect area = new Rect(vw / 2 - 360, canLaunch ? 40 : 110, 720, canLaunch ? 640 : 500);
            GUILayout.BeginArea(area);
            GUILayout.Label("THE HALCYON REACH - STATION MAP", header);
            GUILayout.Label("Escape section by section. Each section is lost as the collapse spreads inward toward the Command Deck.", small);
            GUILayout.Space(10);

            for (int i = 0; i < LevelLibrary.All.Length; i++)
            {
                SectionDefinition def = LevelLibrary.All[i];
                SectionRecord rec = gm.Save.Get(def.id);
                bool unlocked = gm.IsUnlocked(i);
                bool current = gm.Current == def && gm.State != GameState.Map;

                GUILayout.BeginHorizontal();
                string mark = rec != null ? "[X]" : unlocked ? "[>]" : "[ ]";
                GUILayout.Label(mark + " " + (i + 1) + ". " + def.displayName, body, GUILayout.Width(380));
                string info = rec != null ? "Best " + Fmt(rec.bestTime) + " - " + MedalNames[rec.medal] : unlocked ? "Open" : "Locked";
                GUILayout.Label(info, body, GUILayout.Width(190));
                if (canLaunch)
                {
                    GUI.enabled = unlocked;
                    if (GUILayout.Button(rec != null ? "Replay" : "Launch", button, GUILayout.Width(100), GUILayout.Height(32))) gm.Select(i);
                    GUI.enabled = true;
                }
                else if (current) GUILayout.Label("<- you are here", body);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(14);
            GUILayout.Label("Unlocked gravity abilities", header);
            if (Abilities.Unlocked.Count == 0) GUILayout.Label("None yet - clear the first section.", body);
            foreach (AbilityType a in System.Enum.GetValues(typeof(AbilityType)))
                if (Abilities.Unlocked.Contains(a)) GUILayout.Label("- " + Abilities.Title(a) + ": " + Abilities.Description(a), small);

            if (canLaunch)
            {
                GUILayout.Space(16);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Save slot " + (gm.Slot + 1) + " (autosaves)", body, GUILayout.Width(300));
                if (Btn("Change slot", 200f)) gm.ToTitle();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        // ---- loadout ----------------------------------------------------------------------------
        void DrawBriefing(GameManager gm)
        {
            Panel(new Rect(0, 0, vw, VH));
            GUILayout.BeginArea(new Rect(vw / 2 - 340, 80, 680, 560));
            GUILayout.Label(gm.Current.displayName, header);
            GUILayout.Label(gm.Current.intro, body);
            GUILayout.Space(14);
            GUILayout.Label("Choose up to " + Abilities.LoadoutSize + " abilities to bring", header);
            bool any = false;
            foreach (AbilityType a in System.Enum.GetValues(typeof(AbilityType)))
            {
                if (!Abilities.Unlocked.Contains(a)) continue;
                any = true;
                bool on = Abilities.Loadout.Contains(a);
                bool canAdd = on || Abilities.Loadout.Count < Abilities.LoadoutSize;
                GUI.enabled = canAdd;
                bool now = GUILayout.Toggle(on, "  " + Abilities.Title(a) + " - " + Abilities.Description(a), GUILayout.Height(30));
                GUI.enabled = true;
                if (now && !on) Abilities.Loadout.Add(a);
                else if (!now && on) Abilities.Loadout.Remove(a);
            }
            if (!any) GUILayout.Label("No abilities unlocked yet.", body);
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            if (Btn("Begin section", 240f)) gm.StartSection(gm.Current);
            if (Btn("Back to map", 240f)) gm.ToMap();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- HUD --------------------------------------------------------------------------------
        void DrawHud(GameManager gm)
        {
            SectionRunner run = gm.Runner;
            PlayerController p = gm.Player;
            if (run == null || p == null) return;
            GravityManager grav = GravityManager.Instance;
            GameInput input = GameInput.Instance;

            // Collapse warning flash (also readable via the timer / captions).
            if (run.Phase2 && gm.State == GameState.Playing)
            {
                float a = 0.10f + 0.07f * Mathf.Sin(Time.unscaledTime * 6f);
                Color hz = Palette.Get(Role.Hazard);
                Fill(new Rect(0, 0, vw, 14), new Color(hz.r, hz.g, hz.b, a * 3f));
                Fill(new Rect(0, VH - 14, vw, 14), new Color(hz.r, hz.g, hz.b, a * 3f));
            }

            // Structural integrity (bar + number).
            float frac = p.Integrity.Current / p.Integrity.max;
            Fill(new Rect(20, 20, 260, 26), new Color(0, 0, 0, 0.6f));
            Color barColor = frac > 0.35f ? Palette.Get(Role.Safe) : Palette.Get(Role.Hazard);
            Fill(new Rect(22, 22, 256 * frac, 22), barColor);
            GUI.Label(new Rect(22, 20, 260, 26), "  INTEGRITY " + Mathf.CeilToInt(p.Integrity.Current) + "%", small);

            // Timer.
            string timerText = run.Def.tutorial ? "TRAINING  " + Fmt(run.Elapsed) : Fmt(run.TimeLeft) + " until collapse";
            GUI.Label(new Rect(vw / 2 - 200, 14, 400, 30), timerText, center);
            GUI.Label(new Rect(vw / 2 - 200, 40, 400, 24), "Gold " + Fmt(run.Def.gold) + "  Silver " + Fmt(run.Def.silver) + "  Bronze " + Fmt(run.Def.bronze), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            // Quest tracker: active step highlighted, always visible.
            float qy = 20;
            GUI.Label(new Rect(vw - 330, qy, 320, 24), "RESCUE QUEST", small);
            for (int i = 0; i < 3; i++)
            {
                string mark = run.StepDone[i] ? "[X] " : (run.ActiveStep == i ? "[>] " : "[ ] ");
                GUI.Label(new Rect(vw - 330, qy + 22 + i * 22, 320, 24), mark + SectionRunner.StepNames[i], small);
            }

            // Gravity indicator: arrow shows which way the station's original "up" points.
            Rect arrow = new Rect(26, 62, 34, 34);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(grav.Roll, arrow.center);
            Color old = GUI.color;
            GUI.color = Palette.Get(Role.Info);
            GUI.DrawTexture(arrow, SpriteFactory.Get(Shape.Triangle).texture);
            GUI.color = old;
            GUI.matrix = saved;
            GUI.Label(new Rect(68, 62, 300, 34), "GRAVITY: " + grav.Dir.Label() + "   (triangle = station up)", small);
            Fill(new Rect(22, 100, 160, 8), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(22, 100, 160 * (1f - grav.Cooldown / Mathf.Max(0.01f, grav.CooldownDuration)), 8), Palette.Get(Role.Info));

            // Ability cooldowns.
            float ay = 118;
            if (Abilities.IsActive(AbilityType.GravityAnchor))
                GUI.Label(new Rect(22, ay, 330, 22), "Anchor [" + input.Label(Act.Anchor) + "] " + (p.Anchored ? "ACTIVE" : p.AnchorCooldownLeft > 0 ? p.AnchorCooldownLeft.ToString("0.0") + "s" : "ready"), small);
            if (Abilities.IsActive(AbilityType.GravityPulse))
                GUI.Label(new Rect(22, ay + 20, 330, 22), "Pulse [" + input.Label(Act.Pulse) + "] " + (p.PulseCooldownLeft > 0 ? p.PulseCooldownLeft.ToString("0.0") + "s" : "ready"), small);
            if (Abilities.IsActive(AbilityType.DiagonalShift))
                GUI.Label(new Rect(22, ay + 40, 330, 22), "Diagonal: hold [" + input.Label(Act.Diagonal) + "] + gravity key", small);

            // Interact prompt.
            if (p.Nearby != null && gm.State == GameState.Playing)
            {
                Rect pr = new Rect(vw / 2 - 220, VH - 150, 440, 34);
                Fill(pr, new Color(0, 0, 0, 0.7f));
                GUI.Label(pr, "[" + input.Label(Act.Interact) + "] " + p.Nearby.Prompt, center);
            }

            // Station AI line (doubles as caption for the AI).
            if (StationAI.Visible)
            {
                Rect ar = new Rect(vw / 2 - 360, VH - 105, 720, 62);
                Fill(ar, new Color(0, 0, 0, 0.75f));
                GUI.Label(new Rect(ar.x + 10, ar.y + 4, ar.width - 20, ar.height - 6), "STATION AI: " + StationAI.Current, body);
            }

            // Closed captions for sound cues.
            if (Settings.Captions)
            {
                float cy = VH - 230;
                foreach (Captions.Line l in Captions.Lines)
                {
                    GUI.Label(new Rect(vw - 330, cy, 320, 22), l.text, small);
                    cy -= 22;
                }
            }
            DrawPings();
        }

        // On-screen indicators for sound cues (flash at the source, or at the screen edge if off-screen).
        void DrawPings()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (Captions.Ping pg in Captions.Pings)
            {
                Vector3 sp = cam.WorldToScreenPoint(pg.pos);
                float x = Mathf.Clamp(sp.x / scale, 24f, vw - 24f);
                float y = Mathf.Clamp((Screen.height - sp.y) / scale, 24f, VH - 24f);
                Rect r = new Rect(x - 12, y - 12, 24, 24);
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((pg.expire - Time.unscaledTime) / 0.6f));
                GUI.DrawTexture(r, SpriteFactory.Get(Shape.Diamond).texture);
                GUI.color = old;
            }
        }

        // ---- pause ------------------------------------------------------------------------------
        void DrawPause(GameManager gm)
        {
            Panel(new Rect(vw / 2 - 380, 80, 760, 560));
            if (gm.Tab == PauseTab.Settings || gm.Tab == PauseTab.Controls) { DrawSettingsScreen(gm, false); return; }

            if (gm.Tab == PauseTab.Map)
            {
                DrawMap(gm, false);
                GUILayout.BeginArea(new Rect(vw / 2 - 360, 580, 720, 50));
                if (Btn("Back", 160f)) gm.Tab = PauseTab.Menu;
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(vw / 2 - 150, 140, 300, 440));
            GUILayout.Label("PAUSED", title);
            GUILayout.Space(20);
            if (Btn("Resume", 300f)) gm.Resume();
            GUILayout.Space(8);
            if (Btn("Station map", 300f)) gm.Tab = PauseTab.Map;
            GUILayout.Space(8);
            if (Btn("Settings / Controls", 300f)) gm.Tab = PauseTab.Settings;
            GUILayout.Space(8);
            if (Btn("Restart section", 300f)) gm.Retry();
            GUILayout.Space(8);
            if (Btn("Abandon to map", 300f)) gm.ToMap();
            GUILayout.EndArea();
        }

        void DrawSettingsScreen(GameManager gm, bool fromTitle)
        {
            Rect area = fromTitle ? new Rect(vw / 2 - 360, 40, 720, 640) : new Rect(vw / 2 - 360, 100, 720, 520);
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(gm.Tab == PauseTab.Settings, "Accessibility & Audio", button, GUILayout.Width(240), GUILayout.Height(34))) gm.Tab = PauseTab.Settings;
            if (GUILayout.Toggle(gm.Tab == PauseTab.Controls, "Controls", button, GUILayout.Width(240), GUILayout.Height(34))) gm.Tab = PauseTab.Controls;
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            if (gm.Tab == PauseTab.Settings)
            {
                bool cb = GUILayout.Toggle(Settings.Colorblind, "  Colour-blind palette (hazards also use distinct shapes)", GUILayout.Height(30));
                bool cap = GUILayout.Toggle(Settings.Captions, "  Closed captions + on-screen sound indicators", GUILayout.Height(30));
                GUILayout.Space(10);
                GUILayout.Label("Master volume " + Mathf.RoundToInt(Settings.Volume * 100) + "%", body);
                float vol = GUILayout.HorizontalSlider(Settings.Volume, 0f, 1f, GUILayout.Width(300));
                if (cb != Settings.Colorblind || cap != Settings.Captions || !Mathf.Approximately(vol, Settings.Volume))
                {
                    Settings.Colorblind = cb;
                    Settings.Captions = cap;
                    Settings.Volume = vol;
                    Settings.Save();
                }
                GUILayout.Space(14);
                GUILayout.Label("Every sound cue is stereo-positioned (left/right = where it is on screen, higher pitch = higher on screen) and has a caption + flash indicator.", small);
            }
            else
            {
                DrawControls();
            }

            GUILayout.FlexibleSpace();
            if (Btn("Back", 160f)) gm.Tab = fromTitle ? PauseTab.Menu : PauseTab.Menu;
            GUILayout.EndArea();
        }

        void DrawControls()
        {
            GameInput input = GameInput.Instance;
            GUILayout.Label("Click a binding, then press the new key / button (Esc cancels).", small);
            if (input.IsRebinding) GUILayout.Label(rebindMsg, header);
            controlsScroll = GUILayout.BeginScrollView(controlsScroll, GUILayout.Height(330));
            foreach (Act a in System.Enum.GetValues(typeof(Act)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(input.ActionName(a), body, GUILayout.Width(220));
                for (int i = 0; i < input.BindingCount(a); i++)
                {
                    GUI.enabled = !input.IsRebinding;
                    if (GUILayout.Button(input.BindingLabel(a, i), button, GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        rebindMsg = "Press a key for " + input.ActionName(a) + "...";
                        input.StartRebind(a, i, () => rebindMsg = "");
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (!input.IsRebinding && Btn("Reset to defaults", 220f)) input.ResetBindings();
        }

        // ---- results ----------------------------------------------------------------------------
        void DrawResult(GameManager gm)
        {
            SectionResult r = gm.LastResult;
            Panel(new Rect(vw / 2 - 360, 120, 720, 480));
            GUILayout.BeginArea(new Rect(vw / 2 - 320, 140, 640, 440));
            GUILayout.Label(r.success ? "SECTION CLEARED" : "SECTION LOST", title);
            GUILayout.Space(10);
            if (r.success)
            {
                GUILayout.Label("Time " + Fmt(r.time) + "   -   " + MedalNames[r.medal], center);
                if (r.newAbility.HasValue)
                    GUILayout.Label("NEW ABILITY: " + Abilities.Title(r.newAbility.Value) + " - " + Abilities.Description(r.newAbility.Value), center);
            }
            else GUILayout.Label(r.failReason, center);
            GUILayout.Space(14);
            GUILayout.Label("STATION AI: " + r.tip, body);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (Btn("Retry", 200f)) gm.Retry();
            if (Btn("Station map", 200f)) gm.ToMap();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
