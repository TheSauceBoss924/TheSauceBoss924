using System.Collections.Generic;
using UnityEngine;

// The RoomTrigger script (formerly RoomManager) goes on a trigger collider covering each room/level, same as before.
// It only reports "the player entered/left me". The WorldStreamer in the Core scene decides which room is current,
// and looks the room up in the WorldMap to find its biome and neighbors.
//
// Per room, set in the inspector:
//   - roomId: must match the room's id in the WorldMap. Leave empty to use the scene name, which is all you need
//     once every room has its own scene. Only fill it in while several rooms share one scene.
//   - roomBounds: the camera confiner shape (defaults to this object's collider if left empty)
//   - spawnPoint (optional): where the player gets placed when teleporting/respawning into this room
[RequireComponent(typeof(Collider2D))]
public class RoomTrigger : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private Collider2D roomBounds;
    [SerializeField] private Transform spawnPoint;

    // Every RoomTrigger in every loaded scene, so the WorldStreamer can look across rooms without Find calls.
    private static readonly List<RoomTrigger> _all = new();
    public static IReadOnlyList<RoomTrigger> All => _all;

    // Increments on every room entry, so when two room triggers overlap the most recently entered one wins.
    private static int _enterCounter;

    private Collider2D _trigger;

    // Counted instead of a bool in case the player has more than one collider tagged "Player".
    private int _playerContacts;

    public string RoomId => string.IsNullOrEmpty(roomId) ? gameObject.scene.name : roomId;
    public Collider2D Bounds => roomBounds;
    public bool PlayerInside => _playerContacts > 0;
    public int EnterOrder { get; private set; }
    public Vector2 SpawnPosition => spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)roomBounds.bounds.center;

    // Clears the statics when entering play mode with domain reload turned off (Enter Play Mode Options),
    // otherwise rooms from the last play session would still be in the list.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
        _enterCounter = 0;
    }

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (roomBounds == null)
        {
            roomBounds = _trigger;
        }
    }

    private void OnEnable()
    {
        _all.Add(this);

        if (WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.RegisterRoom(this);
        }
    }

    private void OnDisable()
    {
        _all.Remove(this);

        // Unloading a scene doesn't reliably send OnTriggerExit2D, so forget the player ourselves.
        _playerContacts = 0;

        if (WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.UnregisterRoom(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerContacts++;
        if (_playerContacts == 1)
        {
            EnterOrder = ++_enterCounter;

            if (WorldStreamer.Instance != null)
            {
                WorldStreamer.Instance.RefreshCurrentRoom();
            }
        }
    }

    // Exits matter as much as enters: if two room triggers overlap and the player walks back into the older one
    // without ever leaving it, no enter fires, so the current room has to be re-picked when they leave the newer one.
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _playerContacts == 0) return;

        _playerContacts--;
        if (_playerContacts == 0 && WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.RefreshCurrentRoom();
        }
    }
}
