using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The reason something wants time slowed or stopped. Add a new entry here for any new system that changes time.
public enum TimeScaleRequest
{
    Pause,
    HitStop,
    DevSlowMo
}

// TimeScaleController is the ONLY code that sets Time.timeScale. Anything that wants to stop or slow time asks it with
// Request, and hands that back with Release when it's done.
//
// Every system has its own request, so they can't undo each other. For example, if a hit-stop finishes while the game
// is paused, releasing the hit-stop doesn't unpause the game, because the Pause request is still there.
//
// The effective time scale is:
//   0 if any request is 0 (anything stopping time wins)
//   otherwise all the active scales multiplied together (e.g. dev slow-mo 0.25 during a 0.5 slow-down = 0.125)
//   1 if there are no requests
//
// All requests are cleared when a new scene replaces the current one (not when one is loaded additively, like a biome
// background), so nothing can carry a frozen game into the next scene.
public static class TimeScaleController
{
    private static readonly Dictionary<TimeScaleRequest, float> _requests = new();

    // The time scale currently applied
    public static float CurrentScale { get; private set; } = 1f;

    // Clears everything when entering play mode with domain reload turned off (Enter Play Mode Options)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _requests.Clear();
        CurrentScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Asks for time to run at this scale until Release is called with the same key.
    // Calling it again with the same key replaces that key's scale.
    public static void Request(TimeScaleRequest key, float scale)
    {
        _requests[key] = Mathf.Max(0f, scale);
        Apply();
    }

    // Hands back a request. Safe to call even if the key was never requested.
    public static void Release(TimeScaleRequest key)
    {
        if (_requests.Remove(key))
        {
            Apply();
        }
    }

    // True if this key currently has a request
    public static bool IsRequested(TimeScaleRequest key) => _requests.ContainsKey(key);

    // Removes every request and puts time back to normal (scene changes, going back to the main menu)
    public static void ClearAll()
    {
        _requests.Clear();
        Apply();
    }

    private static void Apply()
    {
        float scale = 1f;
        foreach (float requestScale in _requests.Values)
        {
            if (requestScale <= 0f)
            {
                scale = 0f;
                break;
            }
            scale *= requestScale;
        }

        CurrentScale = scale;
        Time.timeScale = scale;
    }

    // Only a scene that replaces everything clears the requests. Additive loads (biome backgrounds, rooms)
    // happen during normal play, and clearing there would cancel a pause or hit-stop part-way through
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            ClearAll();
        }
    }
}
