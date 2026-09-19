# Unity Project Context
<!-- unity-onboarding:generated:start -->
Analyzed 2026-09-19 at commit 76d8f84f, with pre-existing local modifications.
Project: C:/Users/rlafu/Downloads/PPJ/GAME/CardArchive/CardArchive.

## Confirmed environment
- Unity 6000.0.37f1; URP 17.0.3 with project assets under Assets/TcgEngine/Render.
- Legacy Input Manager (`activeInputHandler: 0`); uGUI 2.0.0.
- Netcode for GameObjects 1.12.0 is actively used by TcgNetwork/GameServer/GameClient.
- Addressables 2.2.2, Test Framework 1.4.5, DOTween and imported Live2D/Spine assets.
- Build scenes: Menu, LoginMenu, Game under Assets/TcgEngine/Resources/Scenes.
- Active scene when inspected: Game, clean, Play Mode stopped. Default editor game settings are Solo/test.
- MainCamera is orthographic, size 5.4, position (0,0,-10), facing +Z.

## Architecture and conventions
- First-party gameplay lives in Assets/TcgEngine/Scripts, principally Assembly-CSharp.
  Editor tools belong in Editor folders. Vendor packages have their own asmdefs.
- Namespace families: TcgEngine, TcgEngine.Gameplay, TcgEngine.Client, TcgEngine.FX.
  Four-space braces and snake_case fields are established conventions.
- CardData/AbilityData/EffectData ScriptableObjects are loaded from Resources.
- GameLogic owns gameplay and resolve ordering; Game/Player/Card hold synchronized state.
  GameServer broadcasts actions; GameClient exposes UnityAction presentation events.
- GameBoardFX.OnAbility spawns AbilityData.board_fx through FXTool.DoFX.
  BoardCardFX handles per-target effects and sound. FXLayer defines established sorting bands.
- Authoritative damage must not be added to VFX scripts. Preserve replay/network rules.
- Existing flow documentation is under docs/; resolve queue checks under Tools/ResolveQueueTests.

## Nonomi effect
- Nonomi_Fire and Nonomi_Fire_Tutorial reference spell_damage1_all_enemy.
- The ability already selects enemy board units and deals 1 damage.
- board_fx now references Prefabs/FX/Nonomi/NonomiBarrageFX.prefab.
- New NonomiBarrageFX is bounded visual-only fan fire, using 3 Particle Systems and an additive shader.
- Editor preview: Tools > Card Archive > FX > Nonomi Barrage Preview.
- See docs/nonomi-barrage.md for tuning, validation and generated serialization changes.

## Tooling and validation
- Unity CLI at C:/Users/rlafu/AppData/Local/Unity/bin/unity.exe.
- Pipeline 0.7.0-exp.1 installed with explicit approval; CardArchive serves port 7800 in this session.
- Always pass --project-path explicitly: another running editor contains the NOVA city project.
  Default mcp__unity tools previously selected NOVA, not CardArchive.
- Confirmed CLI capabilities: status, console, scene listing, eval/run_script, compilation and play control.
- New code compiled; 21 focused Play Mode/isolated-game checks passed, including 3 burst cleanup.
- Existing missing-script warning in CardData.Load predates this work; no new errors observed.
- Full Player build and network match validation remain unverified.

## Constraints and evidence
- No AGENTS.md was found in this project or its parent chain.
- Preserve pre-existing Live2D and ProjectSettings modifications. Do not save unrelated scenes.
- Important sources inspected: ProjectVersion, manifest/lock, GraphicsSettings, EditorBuildSettings,
  GameBoardFX, BoardCardFX, FXTool, FXSetting, GameCamera, GameClient, GameLogic,
  AbilityData, EffectDamage, Card/Player/Game/Slot, both Nonomi card assets and their ability.
<!-- unity-onboarding:generated:end -->
