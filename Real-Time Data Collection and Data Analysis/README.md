# User Interaction Analytics & Data Collection

Selected source files from a larger application for practicing word spelling through near hand interactions. The user places letter cubes into target positions. This subsystem records tracked poses and task-context interaction events, with a choice of local CSV storage or Firebase Realtime Database.

![User interacting with the system](Supplementary%20materials/User_in_action_screenshots.jpg)

![Online database real-timel logging](Supplementary%20materials/Firebase_dashboard.png)

![Local data file sample](Supplementary%20materials/Data_file_Sample.png)

**This repository is a subsystem excerpt, not a runnable Unity project.** The parent application supplies the grab/release/snap handler, scene prefabs, interaction objects, user interface, Photon configuration, and Firebase configuration. These excerpts have not been compiled or tested against the original project after the revisions.

## Scope

The recording layer collects two kinds of data:

- **Pose samples:** Head/eye anchor, hand roots, fingertips, and active letter/base/platform transforms. Samples are taken in `FixedUpdate` using elapsed fixed-timestep time rather than an assumed physics frequency.
- **Interaction events:** An event label and manipulated object are joined with the current phase, word, expected letter, and outcome label at capture time. The upstream `GrabEventHandler` defines which interactions generate events.

A `SessionTracker` tracks spelling progress and synchronizes updates through Photon RPCs. The `AddLoggerListener` component registers scene objects and attaches `GrabEventHandler` to snap interactors where missing.

## Data flow

```text
Meta XR / Oculus Interaction objects       Photon session controls
           |                                        |
           v                                        v
 AddLoggerListener / GrabEventHandler         SessionTracker
           |                                        |
           +------------------+---------------------+
                              v
                            Logger
                       /              \
                      v                v
              Local CSV files     Firebase Realtime DB
               (chosen mode)       (chosen mode)
```

## Files

| Script | Responsibility |
| --- | --- |
| `Logger.cs` | Recording lifecycle, pose sampling, event capture, and storage routing |
| `SessionTracker.cs` | Session phase/word/letter state and Photon RPC synchronization |
| `AddLoggerListener.cs` | Finds snap interactors and registers tracked objects |
| `InteractionRecord.cs` | Explicit serializable record types for event and pose payloads |
| `InteractionRecordFormatter.cs` | CSV escaping and event-label normalization |
| `Tests/InteractionRecordFormatterTests.cs` | EditMode tests for small deterministic formatting/classification rules |

## Storage format

Local mode creates a unique `TrackData-<UTC>-<suffix>` directory under `Application.persistentDataPath`, containing `bodyPoses.csv`, `goPoses.csv`, and `events.csv`. Each CSV contains a millisecond timestamp and a correctly quoted JSON payload. The original column names (`timestamp,Poses` and `timestamp,event`) are retained, but the payloads are now valid structured JSON rather than `ToString()` representations.

Cloud mode writes JSON objects to:

```text
sessions/<sanitized-user-id>/<recording-key>/BodyPoses/<push-id>
sessions/<sanitized-user-id>/<recording-key>/goPoses/<push-id>
sessions/<sanitized-user-id>/<recording-key>/Events/<push-id>
sessions/<sanitized-user-id>/<recording-key>/Logs/<push-id>
```

Only one destination is active for a recording. The cloud path and payload format differ from the original implementation; existing consumers must be updated before migrating. Remote records are not retained in an offline queue if a Firebase write fails.


## Limits and verification

- The supplied files reference `GrabEventHandler`, which was not included. Its event labels and semantics must be checked against `InteractionRecordFormatter.Classify` before use.
- Sampling happens in the Unity physics loop; it is not a guaranteed real-time sampling clock. This version emits at most one pose frame per physics step.
- The source is provided as a portfolio excerpt. 
