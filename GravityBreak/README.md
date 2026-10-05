# Gravity Break (prototype)

Unity 2D prototype for the Gravity Break GDD. Everything is generated in code (levels, sprites, sounds, UI),
so **there are no assets to import and no scene wiring**.

## Run it

1. Unity Hub -> **Add project from disk** -> select this `GravityBreak/` folder. It was authored for
   Unity 6 (6000.0); on 2022.3 LTS pick that editor version instead (the code has `#if` guards for the
   Rigidbody2D velocity rename and the composite-collider API).
2. Unity will install the **Input System** package. When it asks to enable the new input backend, click **Yes**
   (restart). If it doesn't ask: *Edit > Project Settings > Player > Active Input Handling* = **Input System Package (New)** or **Both**.
3. Open or create any scene and press **Play**. `Bootstrap` creates the game automatically. (Optional: put the
   `Bootstrap` component on an empty GameObject yourself.)

## Controls (all remappable in Pause > Settings / Controls)

| Action | Keyboard | Controller |
|---|---|---|
| Move | A / D | Left stick |
| Jump | Space | A / South |
| Rotate gravity | Arrow keys | D-pad |
| Diagonal modifier | Left Shift | Left trigger |
| Interact | E | X / West |
| Gravity Anchor / Pulse | Q / F | LB / RB |
| Pause / Map | Esc / M | Start / Select |

Menus are mouse-driven (IMGUI).

## GDD coverage

| GDD item | Status | Where |
|---|---|---|
| Rotate gravity in real time, custom gravity vector | Done | `GravityManager`, `PlayerController` |
| 90 degree increments, cooldown/recharge | Done | `GravityManager.TryShift` |
| Diagonal shifts (8 directions, unlockable) | Done | `GravityDir`, `PlayerController.HandleGravityInput` |
| Velocity / orientation / camera roll recalculated on shift | Done | world-space velocity kept; `GravityManager.Roll` drives player + `CameraRig` |
| "Floor" is whichever surface is opposite gravity | Done | tangent/fall decomposition in `PlayerController.FixedUpdate` |
| Structural Integrity (debris, hazards, bad landings, collapse drain, 0 = lose) | Done | `StructuralIntegrity`, `SectionRunner` |
| Section collapse timer + lose on timer/integrity | Done | `SectionRunner` |
| 3-step rescue quest, auto-checked, always in HUD | Done | `SectionRunner`, `Interactable`, `GameUI.DrawHud` |
| Bronze/silver/gold per section | Done | `SectionRunner.Win`, thresholds in `LevelLibrary` |
| Unlockable abilities, loadout of up to two | Done | `Abilities`, briefing screen. Fast Rotation, Diagonal Shift, Gravity Anchor, Gravity Pulse |
| Hazards: debris, electrical arcs, venting gas | Done | `Hazards.cs` |
| Station map = level select + save/load, 3 slots | Done | `GameUI.DrawMap`, `SaveSystem` |
| Remappable keyboard + controller | Done | `GameInput` |
| Colour-blind mode (+ shape coding) | Done | `Palette`, `SpriteFactory` shapes |
| Spatial audio cues for blind players | Done (procedural stereo cues) | `AudioCues` |
| Closed captions + visual indicators for deaf players | Done | `Captions`, HUD pings |
| Interactive tutorial (section 1) | Done | `TutorialDirector` |
| Context-sensitive tips between sections | Done | `SectionRunner.BuildTip` |
| 5 sections: Engineering x2, Cargo Bay, Research Lab, Command Deck | Done (small placeholder maps) | `LevelLibrary` |
| Station AI voice | Text only, no voice | `StationAI` |
| Real art, music, ambient audio | Not started (placeholder shapes / beeps) | |
| Gamepad navigation of menus | Not done | menus are mouse-only |

## Tuning

Almost everything is a public field: movement and landing numbers on `PlayerController`, gravity strength and
recharge on `GravityManager`, per-section time limits / medal times / collapse drain / debris rate in
`LevelLibrary`. Levels are ASCII grids in `LevelLibrary.cs` (legend at the top of that file), so adding a
section is adding one entry.

## Caveats

This was written without access to the Unity Editor, so it has **not been compiled or playtested yet**. Expect a
few compile errors or tuning issues on first open; the medal times and collapse numbers in particular are
guesses. `GravityManager.allowOppositeFlip` is `true` (any of the four directions can be selected at any
time); set it to `false` to restrict shifts to adjacent 90 degree rotations, if that is what the GDD means by "adjacent".
