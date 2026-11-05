using System;
using System.Collections.Generic;
using UnityEngine;

// GameFlags keeps track of which GameFlags are on, and saves them through the SaveManager like any other ISaveable.
// It lives for the whole game (not in a scene), so flags carry over between scenes. It registers itself with the
// SaveManager the first time anything uses it, which is always after SaveBootstrap has loaded the save file.
//
// HOW TO USE:
//   GameFlags.IsSet(flag)  - true if the flag is on (e.g. skip a cutscene that has already played)
//   GameFlags.Set(flag)    - turns the flag on (e.g. when a cutscene finishes or a door opens)
//
// Flags are written to disk with everything else on the next SaveManager.Save() (a checkpoint), unless the flag has
// Save Immediately ticked. If the player quits before then, the flag is lost and the event happens again,
// the same as any other unsaved progress.
// Setting a flag also fires EventHandler.OnGameFlagSet, so objects can react straight away (see ActiveIfFlag).
public static class GameFlags
{
    private static FlagStore _store;

    // Created and registered with the SaveManager the first time it's used. Registering loads any saved flags.
    private static FlagStore Store
    {
        get
        {
            if (_store == null)
            {
                _store = new FlagStore();
                SaveManager.Register(_store);
            }
            return _store;
        }
    }

    // Clears the store when entering play mode with domain reload turned off (Enter Play Mode Options)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _store = null;
    }

    // True if the flag is on. A missing (empty) flag slot counts as off.
    public static bool IsSet(GameFlag flag)
    {
        return flag != null && Store.Flags.Contains(flag.Id);
    }

    // Turns the flag on. Does nothing if the slot is empty or the flag is already on.
    public static void Set(GameFlag flag)
    {
        if (flag == null || !Store.Flags.Add(flag.Id)) return;

        Debug.Log($"[GameFlags] Set {flag.name}");
        EventHandler.InvokeGameFlagSet(flag);

        if (flag.SaveImmediately)
        {
            SaveManager.Save();
        }
    }

    // Turns every flag off in memory. Used by Dev Mode's Delete Save, otherwise the flags still held in memory
    // would be written straight back on the next save.
    public static void ClearAll()
    {
        Store.Flags.Clear();
    }

    // How many flags are on (for the dev panel)
    public static int Count => Store.Flags.Count;

    // The ISaveable that actually holds the flags. A plain class rather than a MonoBehaviour,
    // so it isn't tied to a scene and never gets destroyed.
    private class FlagStore : ISaveable
    {
        public readonly HashSet<string> Flags = new();

        private GameFlagsSaveData _saveData = new();
        public SaveData saveData => _saveData;

        public SaveData Save()
        {
            _saveData = new GameFlagsSaveData { flags = new List<string>(Flags) };
            return _saveData;
        }

        public void Load(string data)
        {
            GameFlagsSaveData loadedData = JsonUtility.FromJson<GameFlagsSaveData>(data);

            Flags.Clear();
            if (loadedData != null && loadedData.flags != null)
            {
                Flags.UnionWith(loadedData.flags);
            }
        }
    }
}

// The save data for GameFlags: the ids of every flag that's on. Saved as one entry under the id "GameFlags".
[Serializable]
public class GameFlagsSaveData : SaveData
{
    public List<string> flags = new();
    public override string id => "GameFlags";
}
