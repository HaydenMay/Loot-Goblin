# Production Foundation

The production gameplay scene is `Assets/Production/Scenes/DungeonRun_Production.unity`. It contains a simple ground plane, a directional light, the canonical Player prefab, and a follow camera. The existing `Assets/Scenes/LootGoblin.unity` remains the prototype/BuildLab scene and is still available as the second enabled build scene.

## Player structure

`Assets/Production/Prefabs/Player.prefab` owns the `CharacterController`, `ProductionPlayerController`, and `PlayerWeaponAttachment`. Its nested `LootGoblinCharacterPresentation.prefab` is the shared visual-only character setup. That prefab uses the corrected Humanoid runtime rig, one Animator/Avatar, and `WeaponSocket_R`; it omits the test Hammer node from the character instance.

Future character preview screens can instantiate `LootGoblinCharacterPresentation.prefab` directly and call `PrepareForPreview()` on its presentation component. This keeps the same rig, avatar, and animation controller available without adding gameplay movement or colliders to the preview.

## Movement and animation

The Player consumes the existing `Player/Move`, `Player/Attack`, and `Player/Sprint` actions from `Assets/InputSystem_Actions.inputactions`. Movement is world-relative, rotates the player toward its current direction, and continues during the attack. Root motion is disabled because the CharacterController owns movement.

`Assets/Production/Animation/Production_Player.controller` contains `Idle`, `Walk`, and `Hammer_Overhead_Smash_Test`. `MoveSpeed` selects Idle or Walk; the `Attack` trigger enters the Hammer state from locomotion. The Hammer state exits at normalized time 1 and returns to Idle or Walk according to the current movement input. Repeated attack input is ignored until the attack state has exited.

No valid Run clip was found in the current Loot Goblin assets, so the controller has no Run state and sprint input currently leaves the player at walk speed.

## Weapons

`ProductionWeaponDefinition` stores a weapon type, weapon prefab, Animator Controller, attack state name, and local socket transform. `Assets/Production/Weapons/Hammer_Overhead_Smash_Test.asset` selects the separate `Assets/Production/Prefabs/TemporaryHammer.prefab` and production controller. `PlayerWeaponAttachment.Equip()` attaches a definition to `WeaponSocket_R` and applies its animation controller. This is the foundation for future weapon data; it does not implement a full equipment flow.

## Validation evidence

- `Logs/ProductionFoundationBuild.txt` records the production asset checks and the unchanged BuildLab SHA-256.
- `Logs/ProductionFoundationRuntimeValidation.txt` records the Play Mode checks run against an isolated copy of the project.

The runtime check observed 1.70-unit character height, 1.60 units of movement, camera follow offset error of 0.14 units, one complete Hammer attack at normalized time 1.05, locomotion return, socket attachment, and finite backpack bounds with 102 valid bones. It also confirmed one valid Humanoid Avatar, one Animator, and one `CC_Base_Hip` skeleton root.
