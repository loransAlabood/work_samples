# Intervention engine

Watches interaction conditions during a session and responds from explicit rules. It is exploratory and rule-based: **no inference model is used to select interventions.** For the scenario table, escalation ladder, lifecycle diagram, monitors and parameters, see the [repository README](../README.md). This page explains how the pieces connect.

## Path through the engine

```text
EIPlayerObserver ──> TriggerInterpreter ──> InterventionManager ──> InterventionExecutor ──> outcome monitor
 (detect)             (verify context)       (state + escalation)     (perform)                 (check)
                                                  │  ▲                                              │
                                                  ▼  └──────────────── outcome / retry ─────────────┘
                                         InterventionPolicyResolver
                                              (choose what)
```

The observer, interpreter and manager are linked by `IEEventSystem` events. The manager calls the executor directly, and the monitors report their outcome straight back to the manager.

| Stage | Responsibility |
| --- | --- |
| **Observer** (`EIPlayerObserver`, in `IEPlayerObserver.cs`) | Collects per-round observations (spatial state, turn status, timers, portals seen) and raises a trigger when a condition is met. It raises nothing while the engine is disabled. |
| **Interpreter** (`TriggerInterpreter`) | Rechecks the trigger against current context before anything happens, for example "is the participant still within the time limit?". It returns whether an immediate intervention is needed. |
| **Manager** (`InterventionManager`) | Holds one scenario state per scenario type, applies the admission rule, asks the resolver for a response, starts it, and handles the outcome: resolve, clear, or escalate and retry. Escalation is capped. |
| **Resolver** (`InterventionPolicyResolver`) | A pure rule table from scenario, escalation level, spatial state and task context to an `Intervention`. It performs no actions. |
| **Executor** (`InterventionExecutor`) | Performs the requested response (spoken prompt, drawn path, avatar demonstration, retraining) using the application's services, and starts the matching monitor. |
| **Monitors** | Report a short-term outcome (`Success`, `Failure`, `ContextDrift`, `EarlyExit`, `EmergencyCancelled`) back to the manager. |
| **Control layer** (`InterventionControlLayer`) | Enables, pauses, resumes, disables and emergency-cancels the engine, and holds the story flow while gaze retraining is running. |

## Why the resolver is a separate class

Policy is the part most likely to change as the design is refined with experts, and the part most worth testing. The resolver has no scene dependencies, so its rules can be unit-tested directly (three branches are covered in `Tests/EditMode`) and edited without touching execution or state handling.

## Outcome handling

| Outcome | Manager response |
| --- | --- |
| `Success`, `EarlyExit`, `EmergencyCancelled` | Mark resolved, log, clear the scenario. |
| `Failure` | Increase the escalation level and request a retry on the next frame. |
| `ContextDrift` | Log as unresolved and clear the scenario without escalating. |

An outcome is accepted only for the live state object that is still awaiting one. Reports from retired states, canceled monitors or duplicates are ignored.

## Files

| File | Responsibility |
| --- | --- |
| `IEPlayerObserver.cs` | Defines `EIPlayerObserver`: observation and trigger raising. |
| `TriggerInterpreter.cs` | Context check for each trigger. |
| `InterventionManager.cs` | Scenario state, admission, escalation, outcome handling, emergency cancel. |
| `IntervenionPolicyResolver.cs` | Rule table (defines `InterventionPolicyResolver`). |
| `InterventionExecutor.cs` | Performs interventions and starts monitors. |
| `InterventionAdmission.cs` | Which engine states allow a new attempt (unit-tested). |
| `InterventionControlLayer.cs` | Operational state and emergency control. |
| `IEEventSystem.cs` | Event channel for trigger, control and retry signals. |
| `IECoreClasses.cs` | Scenario, intervention and outcome types. |
| `Interventions Outcome Monitors/` | `TimeInterventionMonitor`, `SpatialInterventionMonitor`, `TrivialGazeMonitor` and their interface. |

## Scope

This folder is a subsystem excerpt from a larger Unity scene. The logger, task and turn managers, voice assets, avatar and visual-effects handlers, and scene objects are intentionally absent. Only the admission rule and the resolver branches above are covered by tests. See the repository README for verification status and known limitations.
