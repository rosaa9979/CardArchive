# Recorded replay samples

- `demo-scenario.json`: scripted Aggro versus Midrange showcase (970 events).
- `four-attack-examples.json`: four attack-rule examples (55 events).

These are completed `ReplayUpload` JSON files (format v1, gzip/base64 payload).
Copy them into `Path.Combine(Application.persistentDataPath, "Replays")`, open
**Tools → Card Archive → Open Replay Tool**, and select the recording in the local
list. The available viewpoints are `root` and `Midrange`.

Keep `Assets/TcgEngine/Resources/Cards/Place/Demo_Makoto_Statue.asset`: the showcase
references that card ID and loads its presentation data during playback.

The one-off `DemoScenarioReplay` / `DemoScenarioScript` generators have been
removed. These recordings remain playable without regenerating the match.
Reusable `ReplayValidation` and `ReplayPlaybackValidation` checks are retained.
See the historical [scenario notes](../../../AIWork/demo-scenario.md).
