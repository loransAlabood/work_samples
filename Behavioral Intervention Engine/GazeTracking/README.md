# Gaze tracking and interaction

Turns raw eye and head tracking into a small set of stable events: *the participant started or stopped looking at portal X* and *started or stopped looking at the avatar*. It is a data source for the rest of the application. It does not decide when interventions happen, and it does not write data to storage.

```text
EyeGazeTracker ──┐                                       ┌─> portal look started / ended
                 ├─> GazeManager (picks the source) ─┐   │
HeadGazeTracker ─┘                                   ▼   │
                                 Eye/Head interaction trackers ──> RaycastUtils
                                                     │
                                                     ▼
                                           GazeInteractionTracker ──> avatar look started / ended
                                          (debounce, handoff, events)
```

## Choosing the source

`GazeManager` selects **eye** gaze when the SDK reports eye tracking available, both eyes are tracked, and the mean confidence of the two eyes is at least the threshold (0.6 by default). Otherwise it falls back to **head** gaze, using the headset's center-eye pose. One threshold, owned by `GazeManager`, drives the decision, so the downstream trackers never choose a source on their own.

The source is re-evaluated every frame because the SDK integration has no reliable availability event. A source-change notification is published only when the source actually changes.

## From samples to events

1. The active adapter (`EyeGazeInteractionTracker` or `HeadGazeInteractionTracker`) casts the gaze ray each frame. The adapter for the inactive source returns early, so there is only one event stream.
2. `RaycastUtils` finds the nearest portal and reports whether an avatar collider was hit. Portal and avatar results are independent. It uses a reusable hit buffer, falls back to an allocating query if that buffer fills, and picks the portal by hit distance because the non-allocating query does not sort hits. Rays are limited to 7 m.
3. `GazeInteractionTracker` feeds each result through a `DebouncedValue`. A new target must appear in a number of consecutive frames (3 by default) before it becomes the confirmed target. A return to the confirmed target cancels the pending change.
4. Only a confirmed change produces an event, and a switch from portal A to portal B emits the end of A before the start of B.

### Sensor handoff

When the source changes, open look intervals are closed under the old source, all pending debounce state is reset, and counting restarts under the new source. A gaze duration therefore never spans two sensors, and losing eye tracking is treated as an interaction boundary.

## Other pieces

- `GazeInteractionTracker` also answers direct queries for the application's onboarding and training sequence (`IsUserLookingPortal`, `IsUserLookingAtZone`). These use the current ray immediately, without waiting for debounced events.
- `GazeDataVisualizer` draws eye and head rays for debugging. It is optional and does not feed interaction detection or logging.

## Files

| File | Responsibility |
| --- | --- |
| `GazeManager.cs` | Source selection, source-change notification, visualization mode requests. |
| `EyeGazeTracker.cs` | Eye ray and confidence from the Meta SDK. |
| `HeadGazeTracker.cs` | Head ray from the center-eye anchor. |
| `EyeGazeInteractionTracker.cs`, `HeadGazeInteractionTracker.cs` | Per-source spatial sampling only. |
| `GazeInteractionTracker.cs` | Debouncing, source handoff, portal and avatar events, training queries. |
| `DebouncedValue.cs` | Generic consecutive-sample debouncer (unit-tested). |
| `RaycastUtils.cs` | Shared portal and avatar raycast. |
| `GazeDataVisualizer.cs` | Debug visualization of gaze rays. |

## Limitations

- No hysteresis on the confidence threshold: confidence hovering near 0.6 can switch sources repeatedly, and each switch closes the current look interval.
- Debounce counts are in frames, so the effective time window depends on the frame rate.
- Raycast layer masks are shared static state across all callers.
- The code depends on the Meta XR SDK, scene objects and the application's central event system, which are not included. Only `DebouncedValue` has tests.
