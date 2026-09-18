# Procedural Skeleton Archer — first pass

Open `Assets/Scenes/LootGoblin.unity` and enter Play Mode. Room 1 contains a Slime and an Archer. No asset generation menu, imported model, animation clip, or manual wiring is required.

## Persistent tuning

Outside Play Mode, select `LOOT GOBLIN - Playable Prototype` and expand **LootGoblinRun > Skeleton Archer**. Save the scene after changing values. The spawned Archer's Inspector is useful for inspection, but runtime edits are not the persistent configuration.

| Inspector field | Default |
| --- | --- |
| Health | 3 |
| Projectile Damage | 15 |
| Preferred Range | 4.6 |
| Retreat Distance | 3 |
| Movement Speed | 1.35 |
| Draw Duration | 1.05 seconds |
| Aim Duration | 0.85 seconds |
| Recoil Duration | 0.28 seconds |
| Projectile Speed | 6 units/second |

Keep Retreat Distance below Preferred Range. The aim stops tracking for its last 0.2 seconds. A reposition attempt is bounded to 1.4 seconds when sight is clear, so a cornered Archer still attacks rather than fleeing forever. Sword hits interrupt the current attack and require another full reload.

## Hierarchy and axes

```
Skeleton Archer (+Z forward, +Y up; gameplay root)
  Pelvis
    Spine / Chest
      Rib cage
      Neck vertebra
        Oversized skull / sockets / orange eyes
      Left upper arm -> forearm -> hand -> curved bow / two string segments
      Right upper arm -> forearm -> hand
      Back quiver / three spare arrows
      Nocked arrow (follows draw hand)
    Left thigh -> shin / foot
    Right thigh -> shin / foot
    Tattered red waist cloth
```

Bone meshes point along local +Y. Two-segment arm solving uses chest-space hand targets and explicit elbow poles. Targets are converted from root axes before solving, so chest twist does not redirect the arrow. Feet use local -Z because the downward leg orientation reverses local Z. Bow limbs are along +Y, with a small outward sweep for head-on readability. The held arrow and projectiles point along local +Z.

Geometry consists of low-sided, flat-shaded pieces combined per moving part/material. Build occurs once per spawn. Projectiles clone the held-arrow mesh references. There are no per-frame mesh rebuilds, physics bodies, imported clips, or skeleton squash/stretch. Meshes and materials are released with their owner.

## Rhythm and integration

`Reposition -> DrawArrow -> Aim -> Fire -> Recovery -> DrawArrow`

Recovery can return to Reposition if range or sight needs correction. Reload raises the draw hand to the back/quiver, reveals an arrow halfway through, and brings it forward. Aim extends the bow and draws the string progressively. Fire releases once, followed by recoil. Death stops firing immediately, clears owned projectiles, and collapses for 0.32 seconds before the run awards one normal loot drop and removes the enemy.

The run retains enemy health, nearest-target selection, sword-hit authority, loot and room progression. Existing spawn slots 1 and 5 (zero-based) become Archers; slot 5 exists only with six or more enemies. Thus rooms 1–4 contain one Archer; room 5 onward contains two. The original `min(Room + 1, 8)` count remains intact.

Arrows move on an immutable straight trajectory. Swept planar collision matches the prototype's XZ gameplay conventions, checks visible obstacle bounds, and consumes the arrow even when player invulnerability rejects damage. Radius is 0.46 units; lifetime is 3.5 seconds, with earlier cleanup on wall bounds, obstacle contact, player contact, owner death, or restart. No homing or repeated damage.

## Validation — 2026-09-17

- `SkeletonArcherValidation.Validate`: 49 checks passed in graphics-enabled Unity Play Mode. Includes repeat rhythm, root stability, hand/quiver reach, bow/arrow alignment, actual movement-input dodge, damage, invulnerability, obstacle impact, interruption, death cleanup, existing sword kill, mixed-room clear, loot, failure and restart.
- Gameplay-camera frames inspected at portrait projection: reload reach, arrow return, aim, release and recoil, plus front/side/back/diagonal aim poses. Pixel crops use the same camera, not an alternate showcase camera. Bow overlap was corrected after the first visual pass; the downward-leg foot axis was also corrected.
- `LootGoblinPrototypeEditor.ValidateDepthBatch`: ten mixed rooms, capped counts, 59 cumulative loot, depth choices, health persistence, Cash Out and restart passed. This room-flow fixture places the player within sword reach and maintains health; it is not an autonomous navigation or balance test.
- Legacy `ValidateBatch` stops at an existing Slime timing mismatch: the test ticks 0.3 seconds and expects a committed lunge, but the unchanged prefab has 0.8-second windup. The failure remains visible; production Slime/sword code and prefab were not changed to satisfy it.
- No new compilation errors or Archer runtime exceptions in the final passing runs. Editor licensing/service and package-import warnings occurred. No new WebGL build, physical mobile session or performance profile was run.
- Six bounded implementation/test runs were used, including the test-fixture correction, test compile correction and legacy regression run. No further animation polish performed.

Evidence (local ignored Logs directory): `SkeletonArcherValidation.txt`, `ArcherValidationEditor.log`, `Archer-*.png`, `ArcherDetail-*.png`, `LootGoblinValidation.txt`, `ArcherDepthEditor.log`, `ArcherLegacySmokeResult.txt`, `ArcherRegressionEditor.log`.

The requested reference attachment was unavailable for the initial implementation and was supplied for the visual follow-up below. Locomotion, elbow solving, collapse and local obstacle avoidance are deliberately simple. This is a procedural prototype, not an anatomical archery rig or a mobile performance certification.

## Supplied-reference visual follow-up — 2026-09-17

Two visual/test iterations, confined to `SkeletonArcherRig.cs` and documentation. The reference's beveled skull, off-white palette, red waist cloth and red quiver fletching replaced the initial spherical skull, yellowish ivory, scarf and upward-facing spare arrowheads. The rib cage now tapers more strongly, has a sternum and diagonal quiver strap, and remains more visible beneath a slightly smaller, raised skull. Arms/hands and bow are chunkier; quiver arrows are stored point-down with red feathers above the rim. Existing joints, limb lengths, hand targets and attack cadence are retained. Only head presentation changes: a small upward face angle improves camera readability and recoil tilts the head upward as in the concept.

The existing 49-check Archer suite passed after both iterations. Final actual-camera captures were inspected for aim, quiver reach, return, fire and recoil, including side/back/diagonal poses. No gameplay, state-machine, projectile collision, spawning, health, damage, loot or room logic was modified in this follow-up. Original source and camera captures are retained in `Logs/ArcherReferenceBefore/`; current captures are `Logs/Archer-*.png` and `Logs/ArcherDetail-*.png`. No further polish pursued.

## Files

- New: `Assets/Prototype/SkeletonArcher.cs`, `SkeletonArcherRig.cs`, `SkeletonArrow.cs`, `Editor/SkeletonArcherValidation.cs`, and Unity-generated metadata.
- Modified: `Assets/Prototype/LootGoblinRun.cs` for spawn, tick, health/death integration and reuse of existing arena collision.
- Modified: `Assets/Prototype/Editor/LootGoblinPrototypeEditor.cs` so room-flow fixtures close distance to ranged enemies, gather their drops, and recognize mixed enemy counts.
- Documentation: this file and `Docs/UnityProjectContext.md`.
