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
//   - fires EventHandler.OnBiomeChanged when the player crosses into a different biome
//   - switches each biome's gameplay objects (BiomeContent) on while the player is in or next to that biome, and off otherwise
//   - keeps only what's needed loaded, using the WorldMap to know how rooms connect:
//       * the current room's scene, plus the scenes of its neighbor rooms (so the player can walk straight
//         into them, their triggers have to exist before the player gets there)
//       * the current biome's background scene (shown)
//       * the background scene of any neighboring room in another biome (loaded but hidden by BiomeBackground,
//         so crossing a biome border doesn't pop in)
//     Every other room/biome scene in the WorldMap gets unloaded.
// Rooms that are all in one big scene still work: every room there shares one scene, so nothing gets streamed,
// and it just handles the confiner and biome changes like RoomManager did.
public class WorldStreamer : MonoBehaviour
{
    public static WorldStreamer Instance { get; private set; }

    [SerializeField] private WorldMap worldMap;
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private CameraTarget cameraTarget;
    [SerializeField] private Rigidbody2D playerRb;

    [Tooltip("Room id to load on startup if the player isn't already standing in a room (e.g. when the game boots straight into Core).")]
    [SerializeField] private string startRoomId;

    public WorldMap Map => worldMap;
    public RoomTrigger CurrentRoom { get; private set; }
    public WorldMap.RoomEntry CurrentRoomEntry { get; private set; }
    public BiomeType? CurrentBiome { get; private set; }
    public BiomeProfile CurrentBiomeProfile => CurrentBiome.HasValue ? GetProfile(CurrentBiome.Value) : null;

    // Scenes that should be loaded right now, most important first.
    private readonly List<string> _desiredScenes = new();
    // Scenes that failed to load (typo, or missing from the build scene list), so we don't retry them every frame.
    private readonly HashSet<string> _invalidScenes = new();
    // Room ids we've already warned about missing from the WorldMap, so the warning only shows once.
    private readonly HashSet<string> _unmappedRooms = new();

    // Biomes whose gameplay objects (BiomeContent) are switched on: the current biome plus the biome of any neighboring room,
    // so a neighboring biome is already running before the player crosses into it.
    private readonly HashSet<BiomeType> _activeBiomes = new();
    private readonly HashSet<BiomeType> _nextActiveBiomes = new();
    public IReadOnlyCollection<BiomeType> ActiveBiomes => _activeBiomes;
    // False until the player's first room is known. Until then BiomeContent leaves everything as it is in the scene.
    public bool HasActiveBiomes { get; private set; }

    private string _teleportScene;
    private Coroutine _worker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
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

        if (worldMap == null)
        {
            Debug.LogError("WorldStreamer has no WorldMap assigned, so it can only update the camera confiner. Nothing will be streamed and biomes won't change.", this);
        }
#if UNITY_EDITOR
        else
        {
            worldMap.Validate();
        }
#endif

        if (confiner == null)
        {
            confiner = FindAnyObjectByType<CinemachineConfiner2D>();
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
            TeleportTo(RoomTrigger.All[0].RoomId);
        }
        else if (!string.IsNullOrEmpty(startRoomId))
        {
            TeleportTo(startRoomId);
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
    public void TeleportTo(string roomId, Vector2? position = null, float facingDirection = 1f)
    {
        StartCoroutine(Teleport(roomId, position, facingDirection));
    }

    // Same as TeleportTo, but can be yielded on from your own coroutine (e.g. between a fade out and fade in).
    // It finishes once the room, its biome background and its neighbors are all loaded.
    public IEnumerator Teleport(string roomId, Vector2? position = null, float facingDirection = 1f)
    {
        string roomScene = GetSceneOfRoom(roomId);
        if (roomScene == null)
        {
            Debug.LogError($"Can't teleport into room '{roomId}', it isn't in the WorldMap and isn't loaded.", this);
            yield break;
        }
        if (!CanLoad(roomScene)) yield break;

        // Only wait when the room still has to load. If it's already loaded, everything below happens this same frame,
        // which matters for respawning (the player shouldn't spend a frame still standing in the death zone)
        if (!IsLoaded(roomScene))
        {
            _teleportScene = roomScene;
            UpdateStreaming();
            yield return new WaitUntil(() => IsLoaded(roomScene) || _invalidScenes.Contains(roomScene));
            _teleportScene = null;
        }

        RoomTrigger room = FindLoadedRoom(roomId);
        if (room == null)
        {
            Debug.LogError($"Can't teleport into room '{roomId}', there's no RoomTrigger with that id in scene '{roomScene}'.", this);
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

    // Instantly moves the player to a position, e.g. respawning at a checkpoint (used by CheckpointManager.RespawnPlayer).
    // Finds the room containing the position, so the camera bounds, biome, biome background and biome gameplay objects are
    // all switched to that spot before the camera snaps. Happens in the same frame when the room is already loaded.
    public void TeleportToPosition(Vector2 position, float facingDirection = 1f)
    {
        RoomTrigger room = FindLoadedRoomContaining(position);
        if (room != null)
        {
            TeleportTo(room.RoomId, position, facingDirection);
            return;
        }

        // Not inside any room: still move the player and snap the camera, and the room triggers take over from there
        Debug.LogWarning($"Teleport position {position} isn't inside any loaded room.", this);
        if (playerRb != null)
        {
            playerRb.transform.position = position;
            playerRb.position = position;
            playerRb.linearVelocity = Vector2.zero;
        }
        if (cameraTarget != null)
        {
            cameraTarget.SnapToPlayer(facingDirection);
        }
    }

    // Testing helper (used by DevModeManager): unloads the room the player is in and loads it again, so its enemies
    // and everything else in it are reset. The player stays where they are.
    // Only works once the room has its own scene, since the scene with the WorldStreamer in it is never unloaded.
    public void ReloadCurrentRoom()
    {
        StartCoroutine(ReloadCurrentRoomRoutine());
    }

    private IEnumerator ReloadCurrentRoomRoutine()
    {
        if (CurrentRoom == null) yield break;

        Scene roomScene = CurrentRoom.gameObject.scene;
        if (roomScene == gameObject.scene)
        {
            Debug.LogWarning($"Can't reload room '{CurrentRoom.RoomId}' on its own, it's in the same scene as the WorldStreamer. Reload the whole scene instead.", this);
            yield break;
        }

        string roomId = CurrentRoom.RoomId;
        Vector2 position = playerRb != null ? playerRb.position : CurrentRoom.SpawnPosition;

        // Freeze the player so they don't fall while the room's floor is gone.
        bool wasSimulated = playerRb != null && playerRb.simulated;
        if (playerRb != null)
        {
            playerRb.simulated = false;
        }

        // Let any load in progress finish first, so the worker and this don't both touch the scene at once.
        yield return new WaitUntil(() => _worker == null);
        yield return SceneManager.UnloadSceneAsync(roomScene);
        yield return Teleport(roomId, position);

        if (playerRb != null)
        {
            playerRb.simulated = wasSimulated;
        }
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
        if (worldMap != null && !worldMap.TryGetRoom(room.RoomId, out _) && _unmappedRooms.Add(room.RoomId))
        {
            Debug.LogWarning($"Room '{room.RoomId}' (scene '{room.gameObject.scene.name}') isn't in the WorldMap. Its camera bounds still work, but its biome and neighbors are unknown.", room);
        }
    }

    public void UnregisterRoom(RoomTrigger room)
    {
        if (room != CurrentRoom) return;

        CurrentRoom = null;
        CurrentRoomEntry = null;
        RefreshCurrentRoom();
        UpdateStreaming();
    }

    public BiomeProfile GetProfile(BiomeType biome) => worldMap != null ? worldMap.GetBiome(biome) : null;

    private void SetCurrentRoom(RoomTrigger room)
    {
        if (room == CurrentRoom) return;
        CurrentRoom = room;

        if (confiner != null && room.Bounds != null && confiner.BoundingShape2D != room.Bounds)
        {
            confiner.BoundingShape2D = room.Bounds;
            confiner.InvalidateBoundingShapeCache();
        }

        // A room missing from the map keeps the previous biome rather than guessing one.
        CurrentRoomEntry = worldMap != null && worldMap.TryGetRoom(room.RoomId, out WorldMap.RoomEntry entry) ? entry : null;
        if (CurrentRoomEntry != null && CurrentBiome != CurrentRoomEntry.biome)
        {
            CurrentBiome = CurrentRoomEntry.biome;
            BiomeBackground.ShowOnly(CurrentRoomEntry.biome);
            EventHandler.InvokeBiomeChanged(CurrentRoomEntry.biome);
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
        if (CurrentRoomEntry == null) return;

        AddBiomeBackground(CurrentRoomEntry.biome);

        _nextActiveBiomes.Clear();
        _nextActiveBiomes.Add(CurrentRoomEntry.biome);

        foreach (string neighborId in worldMap.GetNeighbors(CurrentRoomEntry.Id))
        {
            if (!worldMap.TryGetRoom(neighborId, out WorldMap.RoomEntry neighbor)) continue;

            AddDesired(neighbor.scene);
            _nextActiveBiomes.Add(neighbor.biome);

            // Neighbor in a different biome: preload that biome's background (hidden) ahead of the border.
            if (neighbor.biome != CurrentRoomEntry.biome)
            {
                AddBiomeBackground(neighbor.biome);
            }
        }

        UpdateActiveBiomes();
    }

    // Switches BiomeContent groups on and off, only when the set of active biomes actually changes
    private void UpdateActiveBiomes()
    {
        if (HasActiveBiomes && _activeBiomes.SetEquals(_nextActiveBiomes)) return;

        _activeBiomes.Clear();
        _activeBiomes.UnionWith(_nextActiveBiomes);
        HasActiveBiomes = true;
        BiomeContent.ApplyAll(_activeBiomes);
    }

    private void AddBiomeBackground(BiomeType biome)
    {
        BiomeProfile profile = GetProfile(biome);
        if (profile != null)
        {
            AddDesired(profile.backgroundScene);
        }
    }

    private void AddDesired(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || _desiredScenes.Contains(sceneName) || !CanLoad(sceneName)) return;
        _desiredScenes.Add(sceneName);
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
                    // Unity refused (e.g. it's the only scene left), so stop trying to unload it.
                    MarkInvalid(toUnload);
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
        if (CurrentRoom == null || worldMap == null) return null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            // Only scenes the WorldMap knows about are ever unloaded, and never the scene this streamer is in (Core).
            if (scene.isLoaded
                && scene != gameObject.scene
                && worldMap.ContainsScene(scene.name)
                && !_desiredScenes.Contains(scene.name)
                && !_invalidScenes.Contains(scene.name))
            {
                return scene.name;
            }
        }
        return null;
    }

    // The map is the source of truth, but a loaded room that's missing from it can still be teleported into.
    private string GetSceneOfRoom(string roomId)
    {
        if (worldMap != null && worldMap.TryGetRoom(roomId, out WorldMap.RoomEntry entry))
        {
            return entry.scene;
        }

        RoomTrigger loaded = FindLoadedRoom(roomId);
        return loaded != null ? loaded.gameObject.scene.name : null;
    }

    private static RoomTrigger FindLoadedRoomContaining(Vector2 position)
    {
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            if (room.Contains(position)) return room;
        }
        return null;
    }

    private static RoomTrigger FindLoadedRoom(string roomId)
    {
        foreach (RoomTrigger room in RoomTrigger.All)
        {
            if (room.RoomId == roomId) return room;
        }
        return null;
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
            Debug.LogError($"Scene '{sceneName}' couldn't be loaded or unloaded. Check the name in the WorldMap matches, and that the scene is in the build scene list.", this);
        }
    }
}
