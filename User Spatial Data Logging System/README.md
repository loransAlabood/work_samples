# XR Session Data Logging

Selected source files from a larger Unity/Meta XR research application. The subsystem records spatial tracking samples, application events, and intervention lifecycle changes into separate CSV files. It is presented as a code sample, not a runnable Unity project.

The three streams are started and stopped together by `LoggingManager`. The researcher dashboard can request the same operations through `RemoteLoggingController`. Output goes to `Application.persistentDataPath` and is written locally, not transmitted to the dashboard.

![Realtime eye and head gaze spatial data file sample](Supplementary%20materials/raw_gaze_data.png)

![Gaze collision event data file sample](Supplementary%20materials/gaze_event_data.png)

![User interaction and system events data file sample](Supplementary%20materials/User_and_system_events.png)

## Components

| Source | Responsibility |
| --- | --- |
| `RobustDataLogger_MetaXR.cs` | Periodically samples tracked transforms: head, hand roots, fingertip joints and gaze transforms. |
| `EventLogger.cs` | Captures selected application events from `CentralEventSystem`. |
| `InterventionLogger.cs` | Captures intervention lifecycle events published by `InterventionManager`. |
| `InterventionLifecycleEvent.cs` | Defines the intervention event record. |
| `LoggingManager.cs` | Starts and stops the three logging streams as a session. |
| `RemoteLoggingController.cs` | Exposes the session logging controls to the network bridge. |
| `BufferedCsvWriter.cs` | Owns queued writes and serializes periodic and stop-time flushes. |
| `CsvFormat.cs` | CSV escaping, UTC timestamp and invariant numeric formatting. |

`IELogger.cs` was excluded. It is an unused stub containing no logging implementation.

## Data flow

```text
XR transforms --------------------> Spatial logger ------> spatial CSV
CentralEventSystem events --------> Event logger --------> events CSV
InterventionManager lifecycle ----> Intervention logger -> intervention CSV
                                       ^
LoggingManager / dashboard ----------- start/stop control
```

Each logger has its own writer and file. Periodic writes run on a worker thread. `StopLogging()` synchronizes with the same writer lock, preventing concurrent file appends. Records are only removed from memory after a successful append. The writer reports failures so they can be inspected and retried; see limitations below.

### Output

- Spatial header: `Time,Obj,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,EyeConfidence,GazeSource`
- Application event header: `Time,Event,Arguments`
- Intervention header: `Timestamp,Scenario,EscalationCount,Stage,Outcome,Action,Comments`

All new timestamps are formatted as UTC ISO 8601 with milliseconds. Numbers use invariant formatting; fields containing commas, quotes, or newlines use CSV quoting. Null transforms retain the original literal `null` position/rotation convention.

## External dependencies

This sample depends on Unity, `CentralEventSystem`, `GazeSource`, `InterventionManager`, `UserScenarioType`, `InterventionStage`, and `InterventionOutcome`, all defined elsewhere in the application. The center-eye, hand, and gaze transforms are assigned in the Unity Inspector. Joint names used for discovery (`Hand_IndexTip` and `Hand_ThumbTip`) come from the project's rig hierarchy.

To integrate these files, keep the public `StartLogging`, `StopLogging`, `FlushBuffer`, and remote-control methods available to existing callers. For newly written code, the `TryStartLogging` methods report whether file initialization succeeded.

## Sampling and timing

The spatial logger targets a configured sampling rate, 30 Hz by default, but samples on Unity's main thread. If frame rate drops, samples are skipped; no interpolation or catch-up sampling is performed. Object rows in one sample share a UTC capture timestamp. Separate loggers use their own clocks at the moment events occur, so the files are **not** a transactionally synchronized multi-stream recording.

## Portfolio scope

These files are extracted from an experimental XR application. The complete XR project, data processing pipeline and participant data are not included. No participant records or research datasets are published with this code sample.
