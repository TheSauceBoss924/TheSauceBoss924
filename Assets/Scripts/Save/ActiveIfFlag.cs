using UnityEngine;

// ActiveIfFlag turns a target object on or off depending on a GameFlag. Use it for parts of the world that change
// after an event, e.g. rubble that appears after a collapse, or a wall that's gone once a boss is defeated.
//
// Put it on a parent object and drag the object to switch into Target (a child, not this object itself,
// otherwise it would switch itself off and could never switch back on).
//   Active When Set ticked   - the target only shows once the flag is on (e.g. rubble after the collapse)
//   Active When Set unticked - the target shows until the flag is on (e.g. the wall before the boss)
//
// It checks the flag every time it's switched on (loading a save, or its biome group coming back on)
// and also reacts straight away when the flag gets set while it's active.
public class ActiveIfFlag : MonoBehaviour
{
    [SerializeField] private GameFlag flag;
    [SerializeField] private GameObject target;
    [SerializeField] private bool activeWhenSet = true;

    private void OnEnable()
    {
        EventHandler.OnGameFlagSet += HandleFlagSet;
        Apply();
    }

    private void OnDisable()
    {
        EventHandler.OnGameFlagSet -= HandleFlagSet;
    }

    private void HandleFlagSet(GameFlag setFlag)
    {
        if (setFlag == flag) Apply();
    }

    private void Apply()
    {
        if (target == null || target == gameObject) return;

        bool active = GameFlags.IsSet(flag) == activeWhenSet;
        if (target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
