# Unity 6.6 compatibility cleanup

Target editor: **6000.6.2f1**, URP **17.6.0**.

## Removed unused assets

- `Assets/Graph And Chart - Lite Edition` and its folder metadata.
- `Assets/Live2D` (Cubism SDK, editor tools, models) and its folder metadata.

Before removal, scanned 1,613 asset GUIDs against retained Assets, Packages and
ProjectSettings serialized assets, metadata and source. No external references
were found. The audit is in `Logs/Unity66/external-references.json` (local only).

## Compatibility fixes

- EPO outline: exclude the legacy URP compatibility-mode `Execute` callback and
  camera-target helpers on Unity 6.6; retain the existing Render Graph path.
- EPO inspectors: use `Shader.GetProperty*` and
  `UnityEngine.Rendering.ShaderPropertyType` instead of deprecated `ShaderUtil`
  equivalents. Range limits still use the minimum and maximum, and serialized
  property-type values remain compatible.
- Remove the unused `UnityEngine.Rendering.UI` import from `BSlotIndicatorUI`.
- Replace singleton lookups with `FindAnyObjectByType` and collection lookups
  with the unsorted `FindObjectsByType` overload. Mulligan presentation uses
  sibling order explicitly instead of the retired instance-ID order.
- Re-save legacy importer metadata/assets and physics settings flagged as below
  Unity 6.6's minimum serialization version, using Unity's own serialization API.
  The targeted upgrade covered 178 paths; Unity also updated its graphics and
  quality settings schemas while loading/rendering.
- Remove two unreferenced TMP HDRP shader graphs from this URP project, and five
  orphaned material-version subassets in EPO demo materials. Preserve the material
  objects and their URP version data.
- Update the TMP mobile distance-field shader's deprecated
  `enable_d3d11_debug_symbols` pragma to `enable_debug_symbols`.

## Deliberate analyzer exceptions

These exceptions do not represent an API migration and are scoped to individual
fields or methods, rather than project-wide warning suppression:

- `UAC1009`/`UAC1015`: Game's HashSets and Player's dictionary are network/replay
  state, not Unity scene serialization. Marking them `NonSerialized` would drop
  authoritative state from existing network snapshots and replay capture.
- `UAC0023`: retain BinaryFormatter in the existing network packet codec and
  `.user` save-file reader/writer for compatibility. Replacing it requires a
  coordinated network protocol and saved-data migration. This deprecated
  dependency remains technical debt; a clean warning log does not mean it was
  replaced.

The migration is committed separately from replay samples and video editing
source. Unrelated gameplay/effect-test edits remain in the working tree.

## Validation

- Replay codec/gameplay validation: PASS (`Logs/Unity66/compile2.log`).
- EPO Render Graph rendering: PASS for Low, Medium, High and Ultra; each image
  contains 688 outline pixels and 7,056 object pixels, with no captured rendering
  warnings/errors (`Logs/Unity66/rendering.log`,
  `Logs/Unity6Warnings/rendering/result.txt`).
- Real-scene replay playback: PASS for both player viewpoints, zero errors
  (`Logs/Unity66/playback.log`,
  `Library/ReplayValidation/replay-playback-validation.txt`).
- Final Windows x64 build: PASS, exit code 0, no C# compiler warnings/errors,
  shader compiler warnings, missing-script warnings or obsolete serialization
  version diagnostics (`Logs/Unity66/build-final.log`). Output:
  `Builds/Unity66/CardArchive.exe`.
- Unity's first import/build can emit licensing handshake retries and asset-cache
  eviction diagnostics; these are separate from the project/compiler fixes.
