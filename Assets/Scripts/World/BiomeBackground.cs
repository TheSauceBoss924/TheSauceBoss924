using System.Collections.Generic;
using UnityEngine;

// The BiomeBackground script goes on the root object of each biome background scene (e.g. "Biome_Fire").
// Everything the biome draws behind the rooms goes under `content`: parallax layers, the Global Light 2D,
// the post-processing Volume, ambient audio, etc.
// The WorldStreamer loads a neighboring biome's background early so crossing a biome border doesn't pop in,
// so `content` stays hidden until its biome is the one the player is actually in.
// Leave `content` inactive in the scene, so nothing on it (like a playOnAwake AudioSource) runs for a frame before it gets hidden.
public class BiomeBackground : MonoBehaviour
{
    [SerializeField] private BiomeType biome;
    [SerializeField] private GameObject content;

    private static readonly List<BiomeBackground> _all = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
    }

    public static void ShowOnly(BiomeType? currentBiome)
    {
        foreach (BiomeBackground background in _all)
        {
            background.Apply(currentBiome);
        }
    }

    private void OnEnable()
    {
        _all.Add(this);
        Apply(WorldStreamer.Instance != null ? WorldStreamer.Instance.CurrentBiome : null);
    }

    private void OnDisable()
    {
        _all.Remove(this);
    }

    private void Apply(BiomeType? currentBiome)
    {
        if (content == null || content == gameObject) return;
        content.SetActive(currentBiome == biome);
    }
}
