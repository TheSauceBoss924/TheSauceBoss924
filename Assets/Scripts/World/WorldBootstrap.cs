// The whole file is wrapped in #if UNITY_EDITOR, so it only exists inside the Unity Editor.
// When you make a build of the game, Unity skips everything between #if UNITY_EDITOR and #endif,
// so this script isn't even compiled into the game. Players never run it.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

// WORLD BOOTSTRAP
//
// PURPOSE:
// This is a testing convenience for the editor. It lets you open ANY room (or biome) scene on its own and press Play,
// without having to open the Core scene first.
//
// WHY IT'S NEEDED:
// Once the game is split into scenes, the Player, the cameras, the CameraTarget, the UI and the WorldStreamer all live
// ONLY in the Core scene. A room scene like "Room_03" only has its tilemaps, enemies and RoomTrigger.
// So if you open Room_03 by itself and press Play, there is no player, no camera (black screen) and no WorldStreamer.
// Without this script you would have to open Core, press Play, and walk all the way to Room_03 every time you want to test it.
//
// WHAT HAPPENS WHEN YOU PRESS PLAY IN A ROOM SCENE:
//   1. Unity loads the scene you have open (e.g. Room_03)
//   2. This script runs once, sees there is no WorldStreamer anywhere, and loads Core on top of Room_03
//   3. Core's WorldStreamer starts up, sees the player isn't standing in any room, and teleports them
//      to Room_03's spawn point (see WorldStreamer.Start)
//   4. The WorldStreamer then loads Room_03's biome background and neighbor rooms like normal
//   So you end up playing in Room_03 with everything set up, as if you had walked there.
//
// WHEN IT DOES NOTHING:
//   - When you press Play in the Core scene (a WorldStreamer already exists, so Core doesn't need loading)
//   - When there is no scene called "Core" in the build scene list yet (e.g. right now, while everything is still in one scene)
//   - In builds of the game (see the #if UNITY_EDITOR at the top)
//
// It isn't required for the system to work. If this file were deleted, everything would still work,
// you'd just have to open Core yourself before pressing Play.
//
// It is a static class (not a MonoBehaviour), so it does NOT go on a GameObject. Unity finds and runs it automatically
// because of the [RuntimeInitializeOnLoadMethod] attribute below.
public static class WorldBootstrap
{
    // Name of the scene holding the player, camera, camera target and WorldStreamer.
    // If the Core scene ends up with a different name, change it here.
    private const string CoreSceneName = "Core";

    // [RuntimeInitializeOnLoadMethod] tells Unity to call this method by itself when the game starts. No GameObject needed.
    // AfterSceneLoad means it runs once, right after the first scene has finished loading and its objects have run Awake().
    // That timing matters: by then any WorldStreamer in the opened scene already exists, so the check below can find it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadCoreIfMissing()
    {
        // If a WorldStreamer already exists, Core (or whatever scene holds the WorldStreamer) is already loaded,
        // so there is nothing to do. This is the normal case when you press Play from the Core scene.
        if (Object.FindAnyObjectByType<WorldStreamer>() != null) return;

        // If there is no scene called "Core" in the build scene list, there is nothing to load.
        // This keeps it from throwing errors before Core exists (while everything is still in one scene).
        if (!Application.CanStreamedLevelBeLoaded(CoreSceneName)) return;

        // Load Core ON TOP of the scene that's already open. Additive means "add it alongside",
        // instead of the default, which would unload the room you opened and replace it with Core.
        SceneManager.LoadScene(CoreSceneName, LoadSceneMode.Additive);
    }
}
#endif
