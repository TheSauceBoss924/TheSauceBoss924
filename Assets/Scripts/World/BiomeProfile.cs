using UnityEngine;

// The BiomeProfile script is a ScriptableObject that defines the properties of a biome in the game.
// It includes the biome type, music, ambient light color, and background sprite.
// This allows for easy customization and management of different biomes in the game.
// If we need any extra properties for a biome, we can add them here. Anything that reacts to biome changes
// (music, lighting, etc.) subscribes to WorldStreamer.OnBiomeChanged and reads WorldStreamer.Instance.CurrentBiomeProfile.
// (These are just placeholders for now, we can add more or change properties as needed.)
// Every biome profile also needs to be added to the WorldStreamer's Biome Profiles list in the Core scene.

// The BiomeProfile ScriptableObject can be created in the Unity Editor by right-clicking in the Project window,
// selecting "Create" -> "Biome Profile", and then filling in the properties in the Inspector.
[CreateAssetMenu(fileName = "New Biome Profile", menuName = "Biome Profile")]
public class BiomeProfile : ScriptableObject
{
    public BiomeType biome;
    public AudioClip music;
    public Color ambientLightColor;
    public Sprite backgroundSprite;

    [Tooltip("Name of the scene with this biome's background (parallax layers, Global Light 2D, post-processing). Must be in the build scene list.")]
    public string backgroundScene;
}
