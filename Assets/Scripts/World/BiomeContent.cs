using System.Collections.Generic;
using UnityEngine;

// The BiomeContent script goes on a parent object in the level scene that holds one biome's gameplay objects:
// moving platforms, fans, enemies, hazards, switches and doors. Put those objects under its `content` child.
// The WorldStreamer switches `content` on while the player is in that biome or in a room next to it, and off otherwise,
// so far-away biomes don't move, update, run physics or render. Switched-off objects keep their state and carry on
// from where they were when they come back on.
//
// Do NOT put these under a BiomeContent: tiles/the Grid, room triggers, checkpoints, the player, camera, managers or UI,
// or anything that must exist in every biome. Those stay always on.
//
// Until the player's first room is known (or if there is no WorldStreamer in the scene), `content` is left exactly as it
// is saved in the scene, so leave it ticked (active) in the scene.
public class BiomeContent : MonoBehaviour
{
    [SerializeField] private BiomeType biome;
    [SerializeField] private GameObject content;

    private static readonly List<BiomeContent> _all = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
    }

    // Called by the WorldStreamer whenever the set of active biomes changes
    public static void ApplyAll(IReadOnlyCollection<BiomeType> activeBiomes)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            _all[i].Apply(activeBiomes);
        }
    }

    private void OnEnable()
    {
        _all.Add(this);

        WorldStreamer streamer = WorldStreamer.Instance;
        if (streamer != null && streamer.HasActiveBiomes)
        {
            Apply(streamer.ActiveBiomes);
        }
    }

    private void OnDisable()
    {
        _all.Remove(this);
    }

    private void Apply(IReadOnlyCollection<BiomeType> activeBiomes)
    {
        if (content == null || content == gameObject) return;

        bool active = false;
        foreach (BiomeType activeBiome in activeBiomes)
        {
            if (activeBiome == biome)
            {
                active = true;
                break;
            }
        }

        if (content.activeSelf != active)
        {
            content.SetActive(active);
        }
    }
}
