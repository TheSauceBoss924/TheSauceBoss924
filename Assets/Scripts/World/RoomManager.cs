using UnityEngine;
using Unity.Cinemachine;

// The RoomManager script is responsible for managing the camera confiner when the player enters a new room/level.
// It should be attached to a trigger collider that defines the entrance to each room,
// and it will update the CinemachineConfiner2D component with the new room's bounds when the player enters.
// When making new levels/rooms, make sure to add a new trigger collider with this script and set the confiner and room bounds in the inspector (for now)
// Once we have our big "open-world" level, we can probably automate this process more by having the script find the confiner and room bounds automatically based on the scene hierarchy or tags.

// Each room trigger also carries a BiomeType tag. Since RoomManager is one instance per room
// rather than a single central manager, biome tracking lives in a static field shared across
// all instances — that way any trigger can tell whether the biome actually changed, not just the room.
public class RoomManager : MonoBehaviour
{
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private Collider2D roomBounds;
    [SerializeField] private BiomeType biome;

    // Shared across every RoomManager instance in the scene, tracks whichever biome
    // the player is currently in, regardless of which room trigger last fired.
    private static BiomeType? _currentBiome;

    // Fired only when a room transition also crosses a biome boundary.
    // Listeners (music, lighting, background) subscribe to this rather than
    // RoomManager needing to know about any of them directly.
    public static event System.Action<BiomeType> OnBiomeChanged;

    private void Awake()
    {
        if (confiner == null)
        {
            confiner = GameObject.FindAnyObjectByType<CinemachineConfiner2D>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && confiner.BoundingShape2D != roomBounds)
        {
            confiner.BoundingShape2D = roomBounds;
            confiner.InvalidateBoundingShapeCache();

            if (_currentBiome != biome)
            {
                _currentBiome = biome;
                OnBiomeChanged?.Invoke(biome);
            }
        }
    }
}
