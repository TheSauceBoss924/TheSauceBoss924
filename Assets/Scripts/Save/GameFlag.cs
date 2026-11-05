using UnityEngine;

// A GameFlag is one yes/no fact about the player's progress that gets saved, e.g. "intro cutscene played",
// "Fire boss defeated" or "garage door opened". Each flag is its own small asset, so scripts reference it by dragging
// it into a slot in the Inspector instead of typing a name (no typos, and every flag lives in one folder).
//
// Create one with right-click in the Project window -> Create -> Game Flag. Name it after the event, e.g. Flag_IntroPlayed.
// Flags start off in a new save. GameFlags.Set turns one on, GameFlags.IsSet checks it.
//
// The flag is saved by its id, which is the asset's own GUID (filled in automatically). Renaming or moving the asset
// is safe and won't break existing saves. Deleting the asset and making a new one with the same name is a different flag.
[CreateAssetMenu(fileName = "Flag_New", menuName = "Game Flag")]
public class GameFlag : ScriptableObject
{
    [Tooltip("What this flag means, for the team. Not used by the game.")]
    [TextArea]
    [SerializeField] private string description;

    [Tooltip("Save the game as soon as this flag is set, instead of waiting for the next checkpoint. " +
             "Use it for events that must never replay (e.g. a long cutscene or a boss kill). Leave it off for most flags.")]
    [SerializeField] private bool saveImmediately = false;

    [Tooltip("Filled in automatically. The id this flag is saved under.")]
    [SerializeField] private string id;

    // Falls back to the asset name if the id hasn't been filled in yet
    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public bool SaveImmediately => saveImmediately;

#if UNITY_EDITOR
    // Keeps the id equal to the asset's GUID. A duplicated asset gets a new GUID, so copies never share an id.
    private void OnValidate()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(path)) return;

        string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
        if (id != guid)
        {
            id = guid;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
