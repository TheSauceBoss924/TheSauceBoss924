using System;
using UnityEngine;

namespace GravityBreak
{
    public enum GameState { Title, Map, Briefing, Playing, Paused, Result }

    /// <summary>Top-level flow: title/save slots -> station map -> (loadout) -> section -> results.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public int Slot { get; private set; }
        public SaveData Save { get; private set; }
        public SectionDefinition Current { get; private set; }
        public SectionRunner Runner { get; private set; }
        public PlayerController Player { get; private set; }
        public SectionResult LastResult { get; private set; }
        public LevelInfo Level { get; private set; }

        GameObject sectionRoot;

        void Awake()
        {
            Instance = this;
            Settings.Load();
            gameObject.AddComponent<GameInput>();
            AudioCues.Init(gameObject);
            gameObject.AddComponent<GravityManager>();
            SetupCamera();
            gameObject.AddComponent<GameUI>();
            State = GameState.Title;
            Slot = -1;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;
            if (cam.GetComponent<CameraRig>() == null) cam.gameObject.AddComponent<CameraRig>();
        }

        void Update()
        {
            GameInput input = GameInput.Instance;
            if (input.IsRebinding) return;
            if (State == GameState.Playing)
            {
                if (input.Pressed(Act.Pause)) Pause(PauseTab.Menu);
                else if (input.Pressed(Act.Map)) Pause(PauseTab.Map);
            }
            else if (State == GameState.Paused && (input.Pressed(Act.Pause) || input.Pressed(Act.Map)))
            {
                Resume();
            }
        }

        // ---- saves ------------------------------------------------------------------------------
        public void LoadSlot(int slot)
        {
            Slot = slot;
            Save = SaveSystem.Load(slot);
            Abilities.Unlocked.Clear();
            Abilities.Loadout.Clear();
            foreach (string s in Save.unlocked)
            {
                AbilityType a;
                if (Enum.TryParse(s, out a)) Abilities.Unlocked.Add(a);
            }
            foreach (string s in Save.loadout)
            {
                AbilityType a;
                if (Enum.TryParse(s, out a) && Abilities.Unlocked.Contains(a) && Abilities.Loadout.Count < Abilities.LoadoutSize) Abilities.Loadout.Add(a);
            }
            State = GameState.Map;
        }

        public void SaveNow()
        {
            if (Slot < 0 || Save == null) return;
            Save.unlocked.Clear();
            foreach (AbilityType a in Abilities.Unlocked) Save.unlocked.Add(a.ToString());
            Save.loadout.Clear();
            foreach (AbilityType a in Abilities.Loadout) Save.loadout.Add(a.ToString());
            SaveSystem.Save(Slot, Save);
        }

        public void DeleteSlot(int slot) { SaveSystem.Delete(slot); }

        public void ToTitle()
        {
            ClearSection();
            Slot = -1;
            Save = null;
            State = GameState.Title;
        }

        // ---- map / sections ---------------------------------------------------------------------
        public bool IsUnlocked(int index)
        {
            return index == 0 || Save.Completed(LevelLibrary.All[index - 1].id);
        }

        public void ToMap()
        {
            ClearSection();
            SaveNow();
            State = GameState.Map;
        }

        public void Select(int index)
        {
            Current = LevelLibrary.All[index];
            if (Current.loadoutRequired) State = GameState.Briefing;
            else StartSection(Current);
        }

        public void StartSection(SectionDefinition def)
        {
            ClearSection();
            Current = def;
            Abilities.LoadoutRequired = def.loadoutRequired;
            SaveNow();

            sectionRoot = new GameObject("Section");
            Level = LevelBuilder.Build(def, sectionRoot.transform);

            GravityManager.Instance.ResetTo(GravityDir.Down);

            GameObject pgo = new GameObject("Player");
            pgo.transform.SetParent(sectionRoot.transform, false);
            pgo.transform.position = Level.start;
            Player = pgo.AddComponent<PlayerController>();
            Player.landingDamage = !def.tutorial;
            Player.SnapTo(Level.start);

            Runner = sectionRoot.AddComponent<SectionRunner>();
            Runner.Init(def, Player);
            Runner.Finished += OnSectionFinished;

            if (def.debrisInterval > 0f)
            {
                DebrisSpawner sp = sectionRoot.AddComponent<DebrisSpawner>();
                sp.bounds = Level.bounds;
                sp.interval = def.debrisInterval;
                sp.runner = Runner;
            }
            if (def.tutorial) sectionRoot.AddComponent<TutorialDirector>().Init(Player, Runner);

            Captions.Clear();
            StationAI.Clear();
            if (!def.tutorial) StationAI.Say(def.intro, 6f);
            CameraRig.Instance.Snap(Player.transform);

            Time.timeScale = 1f;
            State = GameState.Playing;
        }

        public void Retry() { if (Current != null) StartSection(Current); }

        void ClearSection()
        {
            Time.timeScale = 1f;
            if (sectionRoot != null) Destroy(sectionRoot);
            sectionRoot = null;
            Runner = null;
            Player = null;
        }

        void OnSectionFinished(SectionResult r)
        {
            LastResult = r;
            if (r.success)
            {
                SectionRecord rec = Save.Get(Current.id);
                if (rec == null)
                {
                    rec = new SectionRecord { id = Current.id, bestTime = r.time, medal = r.medal };
                    Save.records.Add(rec);
                    if (Current.unlocks.HasValue && Abilities.Unlocked.Add(Current.unlocks.Value))
                    {
                        r.newAbility = Current.unlocks;
                        if (Abilities.Loadout.Count < Abilities.LoadoutSize) Abilities.Loadout.Add(Current.unlocks.Value);
                    }
                }
                else
                {
                    rec.bestTime = Mathf.Min(rec.bestTime, r.time);
                    rec.medal = Mathf.Max(rec.medal, r.medal);
                }
                SaveNow();
            }
            Time.timeScale = 0f;
            State = GameState.Result;
        }

        // ---- pause ------------------------------------------------------------------------------
        public PauseTab Tab { get; set; }

        public void Pause(PauseTab tab)
        {
            Tab = tab;
            Time.timeScale = 0f;
            State = GameState.Paused;
        }

        public void Resume()
        {
            Time.timeScale = 1f;
            State = GameState.Playing;
        }
    }

    public enum PauseTab { Menu, Map, Settings, Controls }
}
