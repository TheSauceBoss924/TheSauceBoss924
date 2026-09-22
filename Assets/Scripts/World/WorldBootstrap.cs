#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper so you can open any room (or biome) scene and press Play without opening Core first.
// If no WorldStreamer is loaded, it loads the Core scene on top. The WorldStreamer then drops the player
// into the open room at its spawn point and loads the room's biome background and neighbors.
// Does nothing in builds, and does nothing if there's no "Core" scene in the build scene list yet.
public static class WorldBootstrap
{
    // Name of the scene holding the player, camera, camera target and WorldStreamer.
    private const string CoreSceneName = "Core";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadCoreIfMissing()
    {
        if (Object.FindAnyObjectByType<WorldStreamer>() != null) return;
        if (!Application.CanStreamedLevelBeLoaded(CoreSceneName)) return;

        SceneManager.LoadScene(CoreSceneName, LoadSceneMode.Additive);
    }
}
#endif
