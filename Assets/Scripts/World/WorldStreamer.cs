using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// The WorldStreamer lives in the Core scene next to the player, camera and camera target, and there should only be one.
// It took over the job RoomManager used to do from every room, and adds scene streaming on top:
//   - works out which room the player is in from the RoomTriggers they're overlapping
//   - points the CinemachineConfiner2D at that room's bounds
//   - fires OnBiomeChanged when the player crosses into a different biome
//   - keeps only what's needed loaded:
//       * the current room's scene, plus the neighbor scenes listed on its RoomTrigger (so the player can
//         walk straight into them, their triggers have to exist before the player gets there)
//       * the current biome's background scene (shown)
//       * the background scene of any neighboring room in another biome (loaded but hidden by BiomeBackground,
//         so crossing a biome border doesn't pop in)
//     Every other room/biome scene gets unloaded.
// Rooms that are all in one big scene still work: with no neighbor scenes listed nothing gets streamed,
// and it just handles the confiner and biome changes like RoomManager did.
public class WorldStreamer : MonoBehaviour
{
    public static WorldStreamer Instance { get; private set; }

    // Fired only when a room transition also crosses a biome boundary.
    // Listeners (music, lighting, background) subscribe to this rather than
    // the streamer needing to know about any of them directly.
    // It also fires once for the first room the player is placed in; anything subscribing later can read CurrentBiome.
    public static event Action<BiomeType> OnBiomeChanged;

    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private CameraTarget cameraTarget;
    [SerializeField] private Rigidbody2D playerRb;

    [Tooltip("One profile per biome. Its backgroundScene is loaded while the player is in (or next to) that biome.")]
    [SerializeField] private BiomeProfile[] biomeProfiles = Array.Empty<BiomeProfile>();

    [Tooltip("Room scene to load on startup if the player isn't already standing in a room (e.g. when the game boots straight into Core).")]
    [SerializeField] private string startRoomScene;

    public RoomTrigger CurrentRoom { get; private set; }
    public BiomeType? CurrentBiome { get; private set; }
    public BiomeProfile CurrentBiomeProfile => CurrentBiome.HasValue ? GetProfile(CurrentBiome.Value) : null;

    // Scenes that should be loaded right now, most important first.
    private readonly List<string> _desiredScenes = new();
    // Every room/biome scene we've come across. Only these ever get unloaded, so Core is never touched.
    private readonly HashSet<string> _streamableScenes = new();
    // Scenes that failed to load (typo, or missing from the build scene list), so we don't retry them every frame.
    private readonly HashSet<string> _invalidScenes = new();

    private string _teleportScene;
    private Coroutine _worker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnBiomeChanged = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"There are two WorldStreamers loaded ({Instance.gameObject.scene.name} and {gameObject.scene.name}). Only keep the one in the Core scene.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (confiner == null)
        {
            confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        }

        foreach (BiomeProfile profile in biomeProfiles)
        {
            if (profile != null && !string.IsNullOrEmpty(profile.backgroundScene))
            {
                _streamableScenes.Add(profile.backgroundScene);
            }
        }

        // Rooms that were already loaded before we existed (e.g. pressing Play inside a room scene) missed registering.
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            RegisterRoom(room);
        }
    }

    private IEnumerator Start()
    {
        // Give physics a step so any room trigger the player is already standing in gets to report in.
        yield return new WaitForFixedUpdate();
        RefreshCurrentRoom();
        if (CurrentRoom != null) yield break;

        // The player isn't in any room: either the game booted into Core, or we pressed Play inside a room scene
        // and WorldBootstrap loaded Core around it. Either way, put the player somewhere sensible.
        if (RoomTrigger.All.Count > 0)
        {
            TeleportTo(RoomTrigger.All[0].gameObject.scene.name);
        }
        else if (!string.IsNullOrEmpty(startRoomScene))
        {
            TeleportTo(startRoomScene);
        }
    }

    // Unity stops coroutines when the object is disabled, so the worker has to be restarted rather than assumed running.
    private void OnDisable()
    {
        _worker = null;
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            UpdateStreaming();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Instantly moves the player into a room, loading its scene first if needed. Use this for respawning,
    // loading a save or fast travel. Walking between rooms doesn't need it, that's handled by the triggers.
    // position: where to put the player. Leave null to use the room's spawn point.
    public void TeleportTo(string roomScene, Vector2? position = null, float facingDirection = 1f)
    {
        StartCoroutine(Teleport(roomScene, position, facingDirection));
    }

    // Same as TeleportTo, but can be yielded on from your own coroutine (e.g. between a fade out and fade in).
    // It finishes once the room, its biome background and its neighbors are all loaded.
    public IEnumerator Teleport(string roomScene, Vector2? position = null, float facingDirection = 1f)
    {
        if (!CanLoad(roomScene)) yield break;

        _teleportScene = roomScene;
        UpdateStreaming();
        yield return new WaitUntil(() => IsLoaded(roomScene) || _invalidScenes.Contains(roomScene));
        _teleportScene = null;

        RoomTrigger room = FindRoom(roomScene, position);
        if (room == null)
        {
            Debug.LogError($"Can't teleport into '{roomScene}', it has no RoomTrigger in it.", this);
            UpdateStreaming();
            yield break;
        }

        Vector2 target = position ?? room.SpawnPosition;
        if (playerRb != null)
        {
            playerRb.transform.position = target;
            playerRb.position = target;
            playerRb.linearVelocity = Vector2.zero;
        }

        // Set the room directly rather than waiting a physics step for its trigger to fire,
        // so the confiner is already right when the camera snaps.
        SetCurrentRoom(room);

        if (cameraTarget != null)
        {
            cameraTarget.SnapToPlayer(facingDirection);
        }

        yield return new WaitUntil(() => _worker == null);
    }

    // Called by RoomTriggers whenever the player enters or leaves one.
    public void RefreshCurrentRoom()
    {
        RoomTrigger newest = null;
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            if (room.PlayerInside && (newest == null || room.EnterOrder > newest.EnterOrder))
            {
                newest = room;
            }
        }

        // In a gap between triggers (or mid-teleport) keep the last room rather than dropping the confiner.
        if (newest != null)
        {
            SetCurrentRoom(newest);
        }
    }

    public void RegisterRoom(RoomTrigger room)
    {
        if (room.gameObject.scene != gameObject.scene)
        {
            _streamableScenes.Add(room.gameObject.scene.name);
        }

        // A neighbor that just finished loading might be in another biome whose background we should preload.
        UpdateStreaming();
    }

    public void UnregisterRoom(RoomTrigger room)
    {
        if (room == CurrentRoom)
        {
            CurrentRoom = null;
            RefreshCurrentRoom();
        }

        UpdateStreaming();
    }

    public BiomeProfile GetProfile(BiomeType biome)
    {
        foreach (BiomeProfile profile in biomeProfiles)
        {
            if (profile != null && profile.biome == biome)
            {
                return profile;
            }
        }
        return null;
    }

    private void SetCurrentRoom(RoomTrigger room)
    {
        if (room == CurrentRoom) return;
        CurrentRoom = room;

        if (confiner != null && room.Bounds != null && confiner.BoundingShape2D != room.Bounds)
        {
            confiner.BoundingShape2D = room.Bounds;
            confiner.InvalidateBoundingShapeCache();
        }

        if (CurrentBiome != room.Biome)
        {
            CurrentBiome = room.Biome;
            BiomeBackground.ShowOnly(room.Biome);
            OnBiomeChanged?.Invoke(room.Biome);
        }

        UpdateStreaming();
    }

    private void UpdateStreaming()
    {
        RebuildDesiredScenes();

        // isActiveAndEnabled is false while Core itself is being unloaded or the game is quitting.
        if (_worker == null && isActiveAndEnabled)
        {
            _worker = StartCoroutine(StreamingWorker());
        }
    }

    private void RebuildDesiredScenes()
    {
        _desiredScenes.Clear();
        AddDesired(_teleportScene);
        if (CurrentRoom == null) return;

        AddDesired(CurrentRoom.gameObject.scene.name);

        BiomeProfile currentProfile = CurrentBiomeProfile;
        if (currentProfile != null)
        {
            AddDesired(currentProfile.backgroundScene);
        }

        foreach (string neighbor in CurrentRoom.NeighborScenes)
        {
            AddDesired(neighbor);
        }

        // Loaded neighbors in a different biome: preload that biome's background (hidden) ahead of the border.
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            if (room.Biome == CurrentRoom.Biome || !IsNeighborOfCurrentRoom(room)) continue;

            BiomeProfile profile = GetProfile(room.Biome);
            if (profile != null)
            {
                AddDesired(profile.backgroundScene);
            }
        }
    }

    private bool IsNeighborOfCurrentRoom(RoomTrigger room)
    {
        string sceneName = room.gameObject.scene.name;
        foreach (string neighbor in CurrentRoom.NeighborScenes)
        {
            if (neighbor == sceneName) return true;
        }
        return false;
    }

    private void AddDesired(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || _desiredScenes.Contains(sceneName) || !CanLoad(sceneName)) return;

        _desiredScenes.Add(sceneName);
        if (sceneName != gameObject.scene.name)
        {
            _streamableScenes.Add(sceneName);
        }
    }

    // Loads and unloads one scene at a time, re-checking what's wanted after each one. That way a player who
    // changes rooms mid-load never ends up with a scene loaded twice, or unloaded while it's still needed.
    private IEnumerator StreamingWorker()
    {
        while (true)
        {
            string toLoad = FindMissingScene();
            if (toLoad != null)
            {
                yield return SceneManager.LoadSceneAsync(toLoad, LoadSceneMode.Additive);
                if (!IsLoaded(toLoad))
                {
                    MarkInvalid(toLoad);
                }
                continue;
            }

            string toUnload = FindStaleScene();
            if (toUnload != null)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(toUnload);
                if (unload == null)
                {
                    // Unity refused (e.g. it's the only scene left), so stop managing it rather than retrying forever.
                    _streamableScenes.Remove(toUnload);
                    continue;
                }
                yield return unload;
                continue;
            }

            break;
        }

        _worker = null;
    }

    private string FindMissingScene()
    {
        foreach (string sceneName in _desiredScenes)
        {
            if (!IsLoaded(sceneName) && !_invalidScenes.Contains(sceneName))
            {
                return sceneName;
            }
        }
        return null;
    }

    private string FindStaleScene()
    {
        // Don't unload anything until we know where the player is.
        if (CurrentRoom == null) return null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded
                && scene != gameObject.scene
                && _streamableScenes.Contains(scene.name)
                && !_desiredScenes.Contains(scene.name))
            {
                return scene.name;
            }
        }
        return null;
    }

    private RoomTrigger FindRoom(string sceneName, Vector2? position)
    {
        RoomTrigger firstInScene = null;
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            if (room.gameObject.scene.name != sceneName) continue;
            if (position == null || room.Contains(position.Value)) return room;
            if (firstInScene == null) firstInScene = room;
        }
        return firstInScene;
    }

    private static bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

    private bool CanLoad(string sceneName)
    {
        if (_invalidScenes.Contains(sceneName)) return false;
        if (IsLoaded(sceneName) || Application.CanStreamedLevelBeLoaded(sceneName)) return true;

        MarkInvalid(sceneName);
        return false;
    }

    private void MarkInvalid(string sceneName)
    {
        if (_invalidScenes.Add(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' couldn't be loaded. Check the name matches, and that the scene is in the build scene list.", this);
        }
    }
}
