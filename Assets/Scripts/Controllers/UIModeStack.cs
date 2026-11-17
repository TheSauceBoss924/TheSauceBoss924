using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Everything that takes the controls away from normal gameplay. Add a new entry here for any new menu or popup.
public enum UIMode
{
    Pause,
    Map,
    PickupPopup,
    Freeze,
    CheckpointMenu
}

// UIModeStack keeps track of every menu, popup or effect that has taken the controls away from normal gameplay,
// and is the ONLY code that switches the player between the "standard" and "ui" ability sets:
//   anything open -> "ui"
//   nothing open  -> "standard"
// Because each mode is tracked separately, closing one only removes that one. Closing the pause menu during a
// snowball freeze leaves the freeze running, instead of handing the controls straight back.
//
// It also owns the canvases it spawns (pause menu, map), so nothing is stored on the shared ability assets,
// and it asks TimeScaleController to stop time while the pause menu or a pickup popup is open.
//
// CONFLICT RULES (all here, not in the abilities):
//   Pause while the map is open        -> the map closes, then the pause menu opens
//   Map while paused                   -> ignored
//   Pause or map during a pickup popup -> ignored (the popup already stops time)
//   Pause during a snowball freeze     -> the pause menu opens on top; closing it leaves the freeze running
//
// Everything is cleared when a new scene replaces the current one (not on additive loads, like biome backgrounds).
public static class UIModeStack
{
    private const string UIAbilitySet = "ui";
    private const string StandardAbilitySet = "standard";

    // Open modes, oldest first
    private static readonly List<UIMode> _stack = new();

    // Canvases this stack spawned, so they can be closed with their mode
    private static readonly Dictionary<UIMode, GameObject> _canvases = new();

    // The player whose ability set is switched. Updated by every call that passes one in
    private static PlayerController _player;

    // Clears everything when entering play mode with domain reload turned off (Enter Play Mode Options)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _stack.Clear();
        _canvases.Clear();
        _player = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    //----------------
    // reading state
    //----------------

    public static bool IsEmpty => _stack.Count == 0;
    public static bool IsOpen(UIMode mode) => _stack.Contains(mode);

    //----------------
    // pause and map (called by PauseAbility, MapAbility and the canvas buttons)
    //----------------

    // Opens the pause menu, or closes it if it's already open
    public static void TogglePause(PlayerController player, GameObject pauseCanvasPrefab)
    {
        if (IsOpen(UIMode.PickupPopup)) return;

        if (IsOpen(UIMode.Pause))
        {
            Close(UIMode.Pause);
            return;
        }

        // the map closes before the pause menu opens, so resuming leaves nothing on screen
        if (IsOpen(UIMode.Map)) Close(UIMode.Map);

        Open(UIMode.Pause, player, pauseCanvasPrefab);
    }

    // Opens the map, or closes it if it's already open. Ignored while paused or during a pickup popup
    public static void ToggleMap(PlayerController player, GameObject mapCanvasPrefab)
    {
        if (IsOpen(UIMode.Pause) || IsOpen(UIMode.PickupPopup)) return;

        if (IsOpen(UIMode.Map))
        {
            Close(UIMode.Map);
            return;
        }

        Open(UIMode.Map, player, mapCanvasPrefab);
    }

    // For the pause menu's Continue button and the map's Exit button
    public static void ClosePause() => Close(UIMode.Pause);
    public static void CloseMap() => Close(UIMode.Map);

    //----------------
    // general use (freeze, pickup popup, checkpoint menu)
    //----------------

    // Opens a mode. If a canvas prefab is given it's spawned, and destroyed again when the mode closes.
    // Does nothing if the mode is already open
    public static void Open(UIMode mode, PlayerController player, GameObject canvasPrefab = null)
    {
        if (player != null) _player = player;
        if (IsOpen(mode)) return;

        _stack.Add(mode);
        if (canvasPrefab != null)
        {
            _canvases[mode] = Object.Instantiate(canvasPrefab);
        }

        Refresh();
    }

    // Closes a mode and its canvas, wherever it is in the stack. Safe to call if it isn't open
    public static void Close(UIMode mode)
    {
        if (_canvases.TryGetValue(mode, out GameObject canvas))
        {
            if (canvas != null) Object.Destroy(canvas);
            _canvases.Remove(mode);
        }

        if (_stack.Remove(mode))
        {
            Refresh();
        }
    }

    // Closes everything. Pass the player to also put them back on the "standard" ability set
    // (used when a player starts in a new scene, and before going back to the main menu)
    public static void Clear(PlayerController player = null)
    {
        foreach (GameObject canvas in _canvases.Values)
        {
            if (canvas != null) Object.Destroy(canvas);
        }
        _canvases.Clear();
        _stack.Clear();

        if (player != null) _player = player;
        Refresh();
    }

    //----------------
    // applying the state
    //----------------

    // Stops time while the pause menu or a pickup popup is open, and puts the player on the right ability set
    private static void Refresh()
    {
        if (IsOpen(UIMode.Pause) || IsOpen(UIMode.PickupPopup))
        {
            TimeScaleController.Request(TimeScaleRequest.Pause, 0f);
        }
        else
        {
            TimeScaleController.Release(TimeScaleRequest.Pause);
        }

        // the player from a previous scene may have been destroyed
        if (_player == null) return;

        string abilitySet = IsEmpty ? StandardAbilitySet : UIAbilitySet;
        if (_player.GetAbilitySet() != abilitySet)
        {
            _player.SwitchAbilitySet(abilitySet);
        }
    }

    // A scene that replaces everything takes its canvases and player with it, so start empty.
    // Additive loads (biome backgrounds, rooms) happen during normal play and are ignored
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            Clear();
        }
    }
}
