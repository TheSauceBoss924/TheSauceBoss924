using System;
using System.Collections.Generic;
using UnityEngine;

// The WorldMap is a ScriptableObject that describes the layout of the whole world in one place:
// every room, which scene it lives in, which biome it belongs to, and which rooms connect to it.
// The WorldStreamer reads it to decide what to load, so it knows a neighbor's biome before that neighbor is even loaded.
//
// Unity can't show or save a Dictionary in the inspector, so the data is edited as lists and turned into
// dictionaries the first time anything asks for it.
//
// Create it in the Project window with "Create" -> "World" -> "World Map", fill in the Biomes and Rooms lists,
// and assign it to the WorldStreamer in the Core scene.
// Right-click the asset's header in the Inspector and pick "Validate" to check it for mistakes.
[CreateAssetMenu(fileName = "World Map", menuName = "World/World Map")]
public class WorldMap : ScriptableObject
{
    [Serializable]
    public class RoomEntry
    {
        [Tooltip("Unique name for this room. Leave empty to use the scene name, which is all you need once every room has its own scene.")]
        public string id;

        [Tooltip("Scene the room lives in. Several rooms can share a scene (e.g. while everything is still in one big scene).")]
        public string scene;

        public BiomeType biome;

        [Tooltip("Ids of the rooms you can walk into from this one. Each connection only needs listing once, the reverse link is added automatically.")]
        public string[] neighbors = Array.Empty<string>();

        public string Id => string.IsNullOrEmpty(id) ? scene : id;
    }

    [Tooltip("One profile per biome. Its backgroundScene is loaded while the player is in (or next to) that biome.")]
    [SerializeField] private BiomeProfile[] biomes = Array.Empty<BiomeProfile>();

    [SerializeField] private List<RoomEntry> rooms = new();

    private Dictionary<string, RoomEntry> _roomsById;
    private Dictionary<string, HashSet<string>> _neighborsById;
    private Dictionary<BiomeType, BiomeProfile> _biomesByType;
    private HashSet<string> _allScenes;

    private static readonly HashSet<string> NoNeighbors = new();

    public bool TryGetRoom(string roomId, out RoomEntry room)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(roomId))
        {
            room = null;
            return false;
        }
        return _roomsById.TryGetValue(roomId, out room);
    }

    public IReadOnlyCollection<string> GetNeighbors(string roomId)
    {
        EnsureBuilt();
        return roomId != null && _neighborsById.TryGetValue(roomId, out HashSet<string> neighbors) ? neighbors : NoNeighbors;
    }

    public BiomeProfile GetBiome(BiomeType biome)
    {
        EnsureBuilt();
        return _biomesByType.TryGetValue(biome, out BiomeProfile profile) ? profile : null;
    }

    // Every room scene and biome background scene in the map. The WorldStreamer only ever unloads these.
    public bool ContainsScene(string sceneName)
    {
        EnsureBuilt();
        return _allScenes.Contains(sceneName);
    }

    private void EnsureBuilt()
    {
        if (_roomsById != null) return;

        _roomsById = new Dictionary<string, RoomEntry>();
        _neighborsById = new Dictionary<string, HashSet<string>>();
        _biomesByType = new Dictionary<BiomeType, BiomeProfile>();
        _allScenes = new HashSet<string>();

        foreach (RoomEntry room in rooms)
        {
            if (room == null || string.IsNullOrEmpty(room.Id)) continue;

            // First entry wins on duplicates, Validate() reports them.
            _roomsById.TryAdd(room.Id, room);

            if (!string.IsNullOrEmpty(room.scene))
            {
                _allScenes.Add(room.scene);
            }

            foreach (string neighbor in room.neighbors ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(neighbor) || neighbor == room.Id) continue;
                Link(room.Id, neighbor);
                Link(neighbor, room.Id);
            }
        }

        foreach (BiomeProfile profile in biomes)
        {
            if (profile == null) continue;

            _biomesByType.TryAdd(profile.biome, profile);

            if (!string.IsNullOrEmpty(profile.backgroundScene))
            {
                _allScenes.Add(profile.backgroundScene);
            }
        }
    }

    private void Link(string from, string to)
    {
        if (!_neighborsById.TryGetValue(from, out HashSet<string> neighbors))
        {
            neighbors = new HashSet<string>();
            _neighborsById[from] = neighbors;
        }
        neighbors.Add(to);
    }

    // Edits in the inspector throw away the dictionaries so they get rebuilt from the new lists.
    private void OnValidate()
    {
        _roomsById = null;
    }

    private void OnEnable()
    {
        _roomsById = null;
    }

    [ContextMenu("Validate")]
    private void ValidateFromMenu()
    {
        if (Validate())
        {
            Debug.Log("[WorldMap] No problems found.", this);
        }
    }

    // Checks the map for mistakes and logs a warning for each one. Returns true if nothing was found.
    // The WorldStreamer runs this when entering Play mode in the editor.
    public bool Validate()
    {
        bool ok = true;
        void Warn(string message)
        {
            Debug.LogWarning($"[WorldMap] {message}", this);
            ok = false;
        }

        var seenIds = new HashSet<string>();
        var usedBiomes = new HashSet<BiomeType>();
        foreach (RoomEntry room in rooms)
        {
            if (room == null) continue;

            if (string.IsNullOrEmpty(room.scene))
            {
                Warn($"Room '{room.Id}' has no scene set.");
            }
            if (string.IsNullOrEmpty(room.Id))
            {
                Warn("A room has neither an id nor a scene set.");
                continue;
            }
            if (!seenIds.Add(room.Id))
            {
                Warn($"Room id '{room.Id}' is used more than once. Only the first one is used.");
            }
            usedBiomes.Add(room.biome);
        }

        foreach (RoomEntry room in rooms)
        {
            if (room == null || room.neighbors == null) continue;

            foreach (string neighbor in room.neighbors)
            {
                if (string.IsNullOrEmpty(neighbor)) continue;
                if (neighbor == room.Id)
                {
                    Warn($"Room '{room.Id}' lists itself as a neighbor.");
                }
                else if (!seenIds.Contains(neighbor))
                {
                    Warn($"Room '{room.Id}' lists neighbor '{neighbor}', but there's no room with that id.");
                }
            }
        }

        var seenBiomes = new HashSet<BiomeType>();
        foreach (BiomeProfile profile in biomes)
        {
            if (profile == null)
            {
                Warn("The Biomes list has an empty slot.");
                continue;
            }
            if (!seenBiomes.Add(profile.biome))
            {
                Warn($"There's more than one profile for biome {profile.biome}. Only the first one is used.");
            }
            if (string.IsNullOrEmpty(profile.backgroundScene))
            {
                Warn($"Biome profile '{profile.name}' has no background scene set.");
            }
        }

        foreach (BiomeType biome in usedBiomes)
        {
            if (!seenBiomes.Contains(biome))
            {
                Warn($"Rooms use biome {biome}, but there's no BiomeProfile for it in the Biomes list.");
            }
        }

        return ok;
    }
}
