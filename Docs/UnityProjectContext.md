# Loot Goblin prototype context
Unity 6000.6.0f1, URP 17.6.0, Input System 1.20.0 (new input only). Fresh template; no existing gameplay. Existing Player/Move action is reused. No packages added. Unity MCP currently targets another project (Dominion); do not use it to mutate this project.

Scene: Assets/Scenes/LootGoblin.unity. One runtime component builds on serialized primitive geometry; it owns movement, simple enemy steering, stop-to-attack, loot and five room resets. No player damage. R restarts. The runtime Tick(Vector2,float) is also the bounded smoke-test entry point.

Slime addition (2026-09-15): LootGoblinRun loads Assets/Prototype/Resources/Slime.prefab once at startup and uses it for all existing room spawns. SlimeMotion supplies grounded hop timing, idle wobble, a compressed wind-up followed by a visual bump, and a 0.32-second death collapse. The run retains steering, targeting, loot and progression ownership. Dying enemies cannot be targeted and remain counted until collapse completes. The original capsule is retained as a fallback if the prefab is absent.

Player health addition (2026-09-15): PlayerHealth is attached to the runtime-built player and owns 100 HP, a 0.75-second post-hit invulnerability window, and a small scale punch. The Slime lunge exposes a one-tick attack contact; LootGoblinRun applies 25 damage only when that lunge is within contact distance. Health persists across normal rooms, while Restart restores full health. At zero health the run enters Failed, freezes Tick processing, and the existing R restart path restores the run. The IMGUI debug HUD displays current health and RUN FAILED when appropriate.

Validation: Loot Goblin/Run Smoke Test passed with slime animation checks and the existing complete five-room/20-loot/restart checks. Logs/LootGoblinValidation.txt contains results; Logs/LootGoblinPortrait.png shows visibility from the gameplay camera. Unity compiled successfully. GameObjectInspector MissingReferenceException/SerializedObjectNotCreatableException messages appeared during script reload; their cause is unresolved. The baseline separately contained player-reference and rig errors. No slime runtime errors occurred in the passing smoke test. No player build or device test was run.

Baseline on 2026-09-15: ProjectSettings/ProjectSettings.asset and QualitySettings.asset already modified by the user; preserve both. SampleScene and template assets are preserved. EditorBuildSettings is changed only to make LootGoblin the playable build scene.

