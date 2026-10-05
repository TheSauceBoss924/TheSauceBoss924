using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityBreak
{
    public enum Act
    {
        MoveLeft, MoveRight, Jump,
        GravDown, GravUp, GravLeft, GravRight, Diagonal,
        Interact, Anchor, Pulse, Pause, Map
    }

    /// <summary>
    /// All gameplay input, built in code so it needs no asset. Every action has a keyboard and a
    /// controller binding and both can be remapped at runtime (overrides persist in PlayerPrefs).
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        const string PrefKey = "gb_bindings";
        InputActionMap map;
        InputAction[] actions;
        InputActionRebindingExtensions.RebindingOperation rebinding;

        public bool IsRebinding { get { return rebinding != null; } }

        void Awake()
        {
            Instance = this;
            map = new InputActionMap("Gameplay");
            actions = new InputAction[Enum.GetValues(typeof(Act)).Length];

            Add(Act.MoveLeft, "Move Left", "<Keyboard>/a", "<Gamepad>/leftStick/left");
            Add(Act.MoveRight, "Move Right", "<Keyboard>/d", "<Gamepad>/leftStick/right");
            Add(Act.Jump, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Add(Act.GravDown, "Gravity Down", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");
            Add(Act.GravUp, "Gravity Up", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            Add(Act.GravLeft, "Gravity Left", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            Add(Act.GravRight, "Gravity Right", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");
            Add(Act.Diagonal, "Diagonal Modifier", "<Keyboard>/leftShift", "<Gamepad>/leftTrigger");
            Add(Act.Interact, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Add(Act.Anchor, "Gravity Anchor", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Add(Act.Pulse, "Gravity Pulse", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            Add(Act.Pause, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Add(Act.Map, "Station Map", "<Keyboard>/m", "<Gamepad>/select");

            string json = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { map.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Debug.LogWarning("Could not load saved bindings: " + e.Message); }
            }
            map.Enable();
        }

        void OnDestroy()
        {
            if (rebinding != null) { rebinding.Dispose(); rebinding = null; }
            if (map != null) map.Disable();
            if (Instance == this) Instance = null;
        }

        void Add(Act a, string name, params string[] paths)
        {
            InputAction action = map.AddAction(name, InputActionType.Button);
            for (int i = 0; i < paths.Length; i++) action.AddBinding(paths[i]);
            actions[(int)a] = action;
        }

        public bool Pressed(Act a) { return actions[(int)a].WasPressedThisFrame(); }
        public bool Held(Act a) { return actions[(int)a].IsPressed(); }
        public float Value(Act a) { return actions[(int)a].ReadValue<float>(); }

        // ---- UI helpers -------------------------------------------------------------------------
        public string ActionName(Act a) { return actions[(int)a].name; }
        public int BindingCount(Act a) { return actions[(int)a].bindings.Count; }
        public string BindingLabel(Act a, int index) { return actions[(int)a].GetBindingDisplayString(index); }

        /// <summary>All bindings for an action joined for prompts, e.g. "E / X".</summary>
        public string Label(Act a)
        {
            string s = "";
            for (int i = 0; i < BindingCount(a); i++)
            {
                if (i > 0) s += " / ";
                s += BindingLabel(a, i);
            }
            return s;
        }

        public void StartRebind(Act a, int bindingIndex, Action done)
        {
            if (rebinding != null) return;
            InputAction action = actions[(int)a];
            bool pad = action.bindings[bindingIndex].effectivePath.StartsWith("<Gamepad>");
            action.Disable();
            rebinding = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .WithControlsHavingToMatchPath(pad ? "<Gamepad>" : "<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => FinishRebind(action, done))
                .OnCancel(op => FinishRebind(action, done))
                .Start();
        }

        void FinishRebind(InputAction action, Action done)
        {
            if (rebinding != null) { rebinding.Dispose(); rebinding = null; }
            action.Enable();
            PlayerPrefs.SetString(PrefKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            if (done != null) done();
        }

        public void ResetBindings()
        {
            map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefKey);
        }
    }
}
