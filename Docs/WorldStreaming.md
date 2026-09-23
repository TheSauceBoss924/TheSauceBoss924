# World streaming: biomes and rooms

The world is split into three kinds of scenes, loaded additively on top of each other:

| Scene | How many loaded | What goes in it |
|---|---|---|
| **Core** | Always, never unloaded | Player, Main Camera + CinemachineCamera (with CinemachineConfiner2D), CameraTarget, `WorldStreamer`, UI, EventSystem |
| **Biome background** (e.g. `Biome_Fire`) | The current biome's, plus a hidden one next to a biome border | `BiomeBackground` on the root; parallax layers, Global Light 2D, post-processing Volume and ambient audio under its `content` object |
| **Room** (e.g. `Fire_03`) | The current room and its neighbors | Tilemaps, colliders, enemies, a `RoomTrigger` with its confiner bounds |

The layout of the world lives in one **`WorldMap`** asset. It lists every room's scene, biome and neighbors, plus every biome's profile.

When the player walks into a room, `WorldStreamer`:
- confines the camera to that room,
- looks the room up in the `WorldMap`,
- fires `WorldStreamer.OnBiomeChanged` if the biome changed, and shows that biome's background,
- loads the room's neighbors and the current biome background, plus the hidden background of any neighbor in a different biome,
- unloads every other room and biome scene that's in the map.

Walking between rooms needs no fade or door script. Neighbors are already loaded, so the player walks straight into them.

## Scripts

| Script | Where | Purpose |
|---|---|---|
| `WorldMap` | One asset | Every room (id, scene, biome, neighbors) and every biome profile, turned into dictionaries at runtime |
| `WorldStreamer` | Core | Picks the current room, sets the confiner, fires biome changes, loads and unloads scenes, `TeleportTo` for respawn, save loading and fast travel |
| `RoomTrigger` | Each room | Replaces `RoomManager`. Reports when the player enters or leaves the room |
| `BiomeBackground` | Root of each biome scene | Shows its content only while its biome is current |
| `BiomeProfile` | Asset per biome | Now also holds `backgroundScene` |
| `WorldBootstrap` | Editor only | Pressing Play in a room scene loads Core automatically |
| `CameraTarget` | Core | `SnapToPlayer` now fully resets and cuts the camera after a teleport |
| `RoomTransitionSmoother` | On the CinemachineCamera | Glides the camera into a new room instead of snapping when the confiner switches bounds |
| `SceneSummaryExporter` | Editor only | Tools → Scene Summary: exports a text summary of the open scenes |

## The WorldMap asset

Create it with **Create → World → World Map**, then fill in:

- **Biomes**: drag in every `BiomeProfile`. One per biome.
- **Rooms**: one entry per room:
  - **Id**: leave empty and it uses the scene name. Only fill it in while several rooms share one scene, and put the same id on that room's `RoomTrigger`.
  - **Scene**: the scene the room lives in.
  - **Biome**: which biome it's in.
  - **Neighbors**: ids of rooms you can walk into from here. Each connection only needs listing **once**. If Fire_02 lists Fire_03, then Fire_03 → Fire_02 is added automatically.

Right-click the asset's Inspector header and pick **Validate** to check for duplicate ids, neighbors that don't exist, and biomes with no profile. It also runs automatically every time you press Play. At runtime, a `RoomTrigger` whose id isn't in the map logs a warning.

## Migrating from RoomManager

1. **Keep your existing room triggers.** `RoomManager.cs` was renamed to `RoomTrigger.cs`. In Unity, rename the file in the Project window first so its `.meta` GUID is kept, then replace its contents. Existing components keep their `roomBounds` value. `biome` and `confiner` have moved out: the biome is set in the WorldMap, and the streamer owns the confiner.
2. **Give each room trigger a `roomId`**, e.g. `Fire_01`, while everything is still in one scene.
3. **Create a `WorldMap`.** Add every biome profile. Add a room entry per trigger with the same id, the one scene's name, and its biome. Neighbors can stay empty until you split scenes.
4. **Add a `WorldStreamer`** to a GameObject next to the player and camera. Assign the WorldMap, confiner, camera target and player Rigidbody2D.
5. **Split scenes when you're ready:**
   1. Move the player, cameras, CameraTarget, WorldStreamer, UI and EventSystem into a scene named `Core`.
   2. Move each room into its own scene and clear the `roomId` on its trigger. It will then use the scene name.
   3. Move each biome's backgrounds, global light and volume into its own scene under a `BiomeBackground` content object. Leave the content object **inactive** in the scene.
   4. Add every scene to the build scene list, with Core first.
   5. Update the WorldMap: set each room's scene, clear its id, and fill in neighbors. Also fill in `backgroundScene` on each `BiomeProfile`.
   6. Set `startRoomId` on the WorldStreamer for when the game boots into Core.

## Rules for each scene

- Room and biome scenes must **not** contain a camera, AudioListener, EventSystem, Player or WorldStreamer. Those live only in Core.
- Lighting: keep one Global Light 2D per sorting layer, in the **biome** scene only. Rooms use local lights.
- Keep room scenes light, because they load while the player walks. Put heavy art in the biome scene, which only loads near biome borders.
- Make each room's confiner bounds at least as big as the camera view at maximum fall zoom (`CameraTarget` zooms the ortho size out), or the confiner will clamp hard.
- Scene names and room ids are plain strings. If you rename a scene, update the WorldMap. Validate and the runtime error messages will point at anything that doesn't match.

## Lessons from setting up the PlayGround

- **Room shapes don't have to be boxes.** Where a rectangle would swallow part of a neighboring room (an L-shaped area, or a corridor running under another room), use a **Polygon Collider 2D** (Is Trigger ticked) instead of a Box Collider 2D. Keep **Paths → Size = 1** and set the corner count on **Element 0**. The room trigger and the camera confiner both use the polygon.
- **Every spot the player can stand must be inside some room.** A gap between rooms keeps the previous room's camera bounds, so the player can walk off-screen there.
- **Drag scene objects in from the Hierarchy, not the Project window.** If the WorldStreamer's Confiner or Camera Target points at a prefab asset, it changes the prefab file instead of the scene camera, and nothing happens in game.
- **The CinemachineConfiner2D must be enabled.** Leave its Bounding Shape 2D empty; the WorldStreamer sets it.
- **The confiner's own Damping doesn't soften room switches** (it only smooths corners). `RoomTransitionSmoother` does; tune its Glide Time.
- **Biome background scenes** need `BiomeBackground` on their root, an inactive `Content` child, no camera, and must be in the build scene list. Put the scene's name in the BiomeProfile's **Background Scene**.

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
    yield return WorldStreamer.Instance.Teleport("Fire_03");   // room id; uses the room's spawnPoint
    yield return fader.FadeIn();
}
```

`Teleport` loads the room if needed and moves the player. It resets the camera with no slide, then waits until the biome background and the neighbor rooms are loaded.

## Fixes over the old RoomManager

- **Overlapping triggers:** when walking back into a room whose trigger overlaps the next one, the camera no longer stays stuck on the wrong room. The most recently entered trigger the player is still inside wins.
- **Enter Play Mode Options:** with domain reload turned off, static state and event subscribers are cleared between play sessions.
- **Load order:** there's no `FindAnyObjectByType` in each room, so a room loaded before Core no longer throws.
