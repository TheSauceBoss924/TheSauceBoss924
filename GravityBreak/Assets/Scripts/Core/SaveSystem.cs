using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GravityBreak
{
    [Serializable]
    public class SectionRecord
    {
        public string id;
        public float bestTime;
        public int medal; // 0 none, 1 bronze, 2 silver, 3 gold
    }

    [Serializable]
    public class SaveData
    {
        public List<string> unlocked = new List<string>();
        public List<string> loadout = new List<string>();
        public List<SectionRecord> records = new List<SectionRecord>();

        public SectionRecord Get(string id)
        {
            for (int i = 0; i < records.Count; i++) if (records[i].id == id) return records[i];
            return null;
        }

        public bool Completed(string id) { return Get(id) != null; }
    }

    /// <summary>Three JSON save slots in Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        static string PathFor(int slot) { return Path.Combine(Application.persistentDataPath, "gravitybreak_slot" + slot + ".json"); }

        public static bool Exists(int slot) { return File.Exists(PathFor(slot)); }

        public static SaveData Load(int slot)
        {
            try
            {
                if (File.Exists(PathFor(slot)))
                {
                    SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
                    if (d != null) return d;
                }
            }
            catch (Exception e) { Debug.LogWarning("Save load failed: " + e.Message); }
            return new SaveData();
        }

        public static void Save(int slot, SaveData data)
        {
            try { File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        public static void Delete(int slot)
        {
            try { if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot)); }
            catch (Exception e) { Debug.LogWarning("Delete failed: " + e.Message); }
        }
    }
}
