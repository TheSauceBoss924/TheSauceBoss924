# World streaming: biomes and rooms

The world is split into three kinds of scenes, loaded additively on top of each other:

| Scene | How many loaded | What goes in it |
|---|---|---|
| **Core** | Always, never unloaded | Player, Main Camera + CinemachineCamera (with CinemachineConfiner2D), CameraTarget, `WorldStreamer`, UI, EventSystem |
| **Biome background** (e.g. `Biome_Fire`) | The current biome's, plus a hidden one next to a biome border | `BiomeBackground` on the root; parallax layers, Global Light 2D, post-processing Volume and ambient audio under its `content` object |
| **Room** (e.g. `Fire_03`) | The current room and its neighbors | Tilemaps, colliders, enemies, a `RoomTrigger` with its confiner bounds |

When the player walks into a room, `WorldStreamer`:
- confines the camera to that room,
- fires `WorldStreamer.OnBiomeChanged` if the biome changed, and shows that biome's background,
- loads the room's neighbors and the current biome background, plus the hidden background of any neighboring biome,
- unloads every other room and biome scene.

Walking between rooms needs no fade or door script. Neighbors are already loaded, so the player walks straight into them.

## Scripts

| Script | Where | Purpose |
|---|---|---|
| `WorldStreamer` | Core | Picks the current room, sets the confiner, fires biome changes, loads and unloads scenes, `TeleportTo` for respawn, save loading and fast travel |
| `RoomTrigger` | Each room | Replaces `RoomManager`. Reports when the player enters or leaves the room |
| `BiomeBackground` | Root of each biome scene | Shows its content only while its biome is current |
| `BiomeProfile` | Asset per biome | Now also holds `backgroundScene` |
| `WorldBootstrap` | Editor only | Pressing Play in a room scene loads Core automatically |
| `CameraTarget` | Core | `SnapToPlayer` now fully resets and cuts the camera after a teleport |

## Migrating from RoomManager

1. **Keep your existing room triggers.** `RoomManager.cs` was renamed to `RoomTrigger.cs`. In Unity, rename the file in the Project window first so its `.meta` GUID is kept, then replace its contents. Existing components keep their `roomBounds` and `biome` values. The old `confiner` field is gone because the streamer owns the confiner now.
2. **Add a `WorldStreamer`** to a GameObject next to the player and camera. Assign the confiner, camera target, player Rigidbody2D and all your `BiomeProfile` assets.
3. **Nothing else is needed while everything is still in one scene.** Leave `neighborScenes` empty and it works the same way `RoomManager` did, plus the fixes below.
4. **Split scenes when you're ready:**
   1. Move the player, cameras, CameraTarget, WorldStreamer, UI and EventSystem into a scene named `Core`.
   2. Move each room into its own scene.
   3. Move each biome's backgrounds, global light and volume into its own scene under a `BiomeBackground` content object. Leave the content object **inactive** in the scene.
   4. Add every scene to the build scene list, with Core first.
   5. Fill in `backgroundScene` on each `BiomeProfile`, and `neighborScenes` on each `RoomTrigger`.
   6. Set `startRoomScene` on the WorldStreamer for when the game boots into Core.

## Rules for each scene

- Room and biome scenes must **not** contain a camera, AudioListener, EventSystem, Player or WorldStreamer. Those live only in Core.
- Lighting: keep one Global Light 2D per sorting layer, in the **biome** scene only. Rooms use local lights.
- `neighborScenes` should list every room the player can walk into directly. List both directions (A lists B, and B lists A). Otherwise the room behind the player can unload while they're standing in the doorway.
- Keep room scenes light, because they load while the player walks. Put heavy art in the biome scene, which only loads near biome borders.
- Make each room's confiner bounds at least as big as the camera view at maximum fall zoom (`CameraTarget` zooms the ortho size out), or the confiner will clamp hard.
- Scene names are plain strings. If you rename a scene, update the RoomTriggers and BiomeProfiles that point to it. A clear error is logged if a name doesn't match.

## Reacting to biome changes

```csharp
void OnEnable()  => WorldStreamer.OnBiomeChanged += HandleBiomeChanged;
void OnDisable() => WorldStreamer.OnBiomeChanged -= HandleBiomeChanged;

void HandleBiomeChanged(BiomeType biome)
{
    BiomeProfile profile = WorldStreamer.Instance.GetProfile(biome);
    // swap music, tint lights, etc.
}
```

The event also fires for the first room the player is placed in. Anything that subscribes later can read `WorldStreamer.Instance.CurrentBiome`.

## Teleporting (respawn, loading a save, fast travel)

```csharp
IEnumerator Respawn()
{
    yield return fader.FadeOut();
    yield return WorldStreamer.Instance.Teleport("Fire_03");   // uses the room's spawnPoint
    yield return fader.FadeIn();
}
```

`Teleport` loads the room if needed and moves the player. It resets the camera with no slide, then waits until the biome background and the neighbor rooms are loaded.

## Fixes over the old RoomManager

- **Overlapping triggers:** when walking back into a room whose trigger overlaps the next one, the camera no longer stays stuck on the wrong room. The most recently entered trigger the player is still inside wins.
- **Enter Play Mode Options:** with domain reload turned off, static state and event subscribers are cleared between play sessions.
- **Load order:** there's no `FindAnyObjectByType` in each room, so a room loaded before Core no longer throws.
