using System;
using UnityEngine;

namespace GravityBreak
{
    public enum Role { Hazard, Interact, Safe, Info }

    /// <summary>Accessibility + audio settings, persisted in PlayerPrefs.</summary>
    public static class Settings
    {
        public static bool Colorblind;
        public static bool Captions = true;
        public static float Volume = 0.8f;
        public static event Action Changed;

        public static void Load()
        {
            Colorblind = PlayerPrefs.GetInt("gb_colorblind", 0) == 1;
            Captions = PlayerPrefs.GetInt("gb_captions", 1) == 1;
            Volume = PlayerPrefs.GetFloat("gb_volume", 0.8f);
            AudioListener.volume = Volume;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("gb_colorblind", Colorblind ? 1 : 0);
            PlayerPrefs.SetInt("gb_captions", Captions ? 1 : 0);
            PlayerPrefs.SetFloat("gb_volume", Volume);
            PlayerPrefs.Save();
            AudioListener.volume = Volume;
            if (Changed != null) Changed();
        }
    }

    /// <summary>
    /// Colour roles. Meaning is also carried by shape (hazards are angular, interactables round), so the
    /// colour-blind palette (Okabe-Ito) is a second layer, not the only one.
    /// </summary>
    public static class Palette
    {
        public static Color Get(Role r)
        {
            if (Settings.Colorblind)
            {
                switch (r)
                {
                    case Role.Hazard: return new Color(0.84f, 0.37f, 0.00f);
                    case Role.Interact: return new Color(0.34f, 0.71f, 0.91f);
                    case Role.Safe: return new Color(0.94f, 0.89f, 0.26f);
                    default: return Color.white;
                }
            }
            switch (r)
            {
                case Role.Hazard: return new Color(1.00f, 0.25f, 0.20f);
                case Role.Interact: return new Color(1.00f, 0.80f, 0.20f);
                case Role.Safe: return new Color(0.30f, 0.90f, 0.40f);
                default: return new Color(0.75f, 0.90f, 1.00f);
            }
        }
    }

    /// <summary>Keeps a SpriteRenderer coloured from the live palette.</summary>
    public class PaletteTint : MonoBehaviour
    {
        public Role role = Role.Info;
        public float alpha = 1f;
        SpriteRenderer sr;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void LateUpdate()
        {
            if (sr == null) return;
            Color c = Palette.Get(role);
            c.a = alpha;
            sr.color = c;
        }
    }
}
