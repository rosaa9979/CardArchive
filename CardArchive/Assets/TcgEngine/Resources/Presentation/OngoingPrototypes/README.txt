OngoingAura

Prefab: Assets/TcgEngine/Prefabs/FX/OngoingAura.prefab
Controller: Assets/TcgEngine/Animations/OngoingAura.controller
Clips: OngoingAuraSelf.anim and OngoingAuraEnemy.anim (2-second infinite loops).
Source: AuraSoft.png; imported single sprites: AuraSoftFrames/AuraSoft_00..31.png.

Owner convention
Self = green (0.025, 0.68, 0.12); Enemy = orange (1, 0.5, 0.025).
Both states animate sprite frames and a constant renderer tint with opacity 1.0.
All FX factories accept ownerPlayerId and delegate to FXTool.SetOwner / FXOwnerState.
The helper selects Self/Enemy through GameClient.GetPlayerID(), refreshes viewpoint
or ownership changes, and does not restart an unchanged state. Controllers without
these states retain their authored behaviour. FXSetting.PlayOwnerState is a
compatibility wrapper for the same helper, not a separate FX-specific mechanism.
If no owner is supplied, the authored default is preserved (neutral/menu FX).

Fixed footprint
Each frame is 256 x 256 with 192 pixels per unit and a centred pivot.
Root scale is (2.197,2.197,1); visible radius is below 1.348 world units.
The pulse remains around its own unit and has no distance or tile-span controls.
Place under an anchor with unit scale and the same orientation as the board;
parent scaling will also scale the aura. GameObject layer: UI (5), matching the unit camera pass. Sorting: Default / -9 (UnderCard).

Persistent decorative lifetime
Assign the prefab to AbilityData.ongoing_fx on an Ongoing ability.
Makoto Statue uses OG_attack_m3.ongoing_fx; its CardData.idle_fx is unassigned.
BoardCardFX delegates to OngoingAbilityFX using the published display snapshot.
One instance per ability is retained while the caster is on board, still has the
ability and is not silenced. Different abilities remain independent. Removal,
transformation, silence, view destruction and session reset clear stale instances.
This is a source-ability indicator, independent of whether any target is present;
it does not execute trigger/target conditions or apply gameplay effects in the UI.
Creation uses FXTool.DoPersistentFX; it preserves the fixed world footprint under
the scaled card anchor and never reserves a presentation marker or hold.
Do not spawn via FXTool.DoFX/ScheduleStateFX: those paths manage finite cues
and presentation reservations. This prefab has presentation_impact=false,
no FXPlayback, no Impact/Continue/Complete events and no automatic destruction.
The looping decoration never owns a gameplay result or blocks a presentation
boundary. State-bearing one-shot FX keep their existing Impact/Complete path.

Asset editing
Edit OngoingAura.prefab's Transform scale in the Inspector to change its size.
The current scale is (2.197,2.197,1). No runtime code enforces a numeric aura size.
FXTool.DoPersistentFX preserves the prefab's authored world footprint when it
attaches the instance to a scaled board-card anchor. The one-off asset generator
has been retired; the prefab, controller, clips and sprite assets are standalone.

Ground and ripple presentation
OngoingAuraGround.mat uses a moving inner gradient, transparent at the centre and stronger toward each ring (max opacity 0.32).
Animator material._RingPhase drives two thin expanding outline rings (opacity 0.85).
FullRect sprite meshes keep the persistent disc stable between frames.
The 2-second phase curve runs at state speed 0.75 (2.667-second cycle), shared by Self/Enemy; no independent clock or gameplay logic.

Optional presentation loop synchronization
FXSetting.synchronize_loop opts a persistent FX into PresentationLoopClock.
FXSetting.loop_period is a full cycle at normal replay speed; this aura uses 2.6666667 seconds.
All opted-in instances with the same period share phase, even when spawned later.
The clock uses scaled Unity time, so ReplayDirector pause and speed apply once.
GameClient creation, presentation baseline reset and destruction reset its origin.
Per-step PresentationClock.Begin/Reset and progression gates do not drive this clock.
FXLoopSync binds only through DoPersistentFX and only accepts one-layer, event-free looping Animators.
One-shot FX factories do not bind it, even if the prefab flag is enabled.
Disable synchronization to retain ordinary per-instance playback.

Spawn animation event
Ongoing ability decorations remain hidden until the linked spawn FX completes.
To choose an earlier start, add one StartOngoing AnimationEvent to the spawn clip
on the Animator GameObject that owns FXPlayback. No parameter is required.
Without this optional event, the existing Complete event starts ongoing.
Impact still controls card/result publication; StartOngoing never publishes state.
Spawn callbacks are registered before Arm, including delayed and baseline spawns.
Reset and re-entry invalidate stale spawn gates. Removal/OnKill and card parenting are unchanged.
Validation: TcgEngine/FX/Validate Ongoing Spawn (Play Mode).
