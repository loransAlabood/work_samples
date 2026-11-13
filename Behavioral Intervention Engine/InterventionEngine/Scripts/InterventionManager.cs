using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public enum InterventionEngineStatus
{
    Enabled,
    Disabled,
    Paused,
    Resume

}


/// <summary>Tracks active scenarios, selects rule-based responses and handles outcome-driven escalation.</summary>
public class InterventionManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] public InterventionExecutor interventionExecutor;
    [SerializeField] private IELogger logger;
    [SerializeField] private IEEventSystem IEES;
    [SerializeField] private EIPlayerObserver eIPlayerObserver;
    private const int MaxEscalationLevel = 3;

    // Tracks the active state of each scenario (e.g. escalation level, whether resolved, etc.)
    private Dictionary<UserScenarioType, InterventionScenarioState> activeScenarios;

    // Resolves which intervention should be used given a scenario and its escalation level
    private InterventionPolicyResolver resolver;

    public InterventionEngineStatus interventionEngineStatus = InterventionEngineStatus.Disabled;

    public void TriggerEmergencyCancelAll() => EmergencyCancelAll();

    public event Action<InterventionLifecycleEvent> OnInterventionLifecycleEvent;

    private void Start()
    {
        if (interventionExecutor == null || IEES == null)
        {
            Debug.LogError("InterventionManager: Missing dependencies.");
            return;
        }

        activeScenarios = new Dictionary<UserScenarioType, InterventionScenarioState>();
        IEES.OnTriggerInvestigated += HandleInterpretedTrigger;
        IEES.OnInterventionEngineStatusChanged += UpdatedEngingeStatus;
        IEES.OnInterventionRetryRequested += QueueInterventionRetry;

        // Initialize the resolver (this could be data-driven or hardcoded)
        resolver = new InterventionPolicyResolver();
    }

    
    private void OnDestroy()
    {
        if (IEES != null)
        {
            IEES.OnTriggerInvestigated -= HandleInterpretedTrigger;
            IEES.OnInterventionEngineStatusChanged -= UpdatedEngingeStatus;
            IEES.OnInterventionRetryRequested -= QueueInterventionRetry;
        }
    }



    private void UpdatedEngingeStatus(InterventionEngineStatus status)
    {
        interventionEngineStatus = status;
        Debug.Log("Intervention Engine Status changed to " + interventionEngineStatus.ToString());
    }
    /// <summary>
    /// Called by the interpreter once a trigger has been analyzed and confirmed to require action.
    /// </summary>
    private void HandleInterpretedTrigger(InterventionScenario scenario, TriggerInvestigationResult result)
    {
        Debug.Log($"[InterventionManager] Handling scenario: {scenario.UserScenarioType} at {scenario.TriggeredAt}");

        if (!result.ImmediateInterventionNeeded)
            return; // Skip handling if it's not urgent or conclusive

        HandleScenario(scenario, result);
    }



    /// <summary>Evaluates a confirmed scenario unless the engine is inactive or a previous outcome is pending.</summary>
    public void HandleScenario(InterventionScenario interventionScenario, TriggerInvestigationResult result)
    {
        if (!InterventionAdmission.CanStart(interventionEngineStatus))
        {
            Debug.LogWarning("[InterventionManager] Engine is not enabled; trigger ignored.");
            return;
        }

        UserScenarioType userScenarioType = interventionScenario.UserScenarioType;

        // Create scenario state if first time
        if (!activeScenarios.ContainsKey(userScenarioType))
        {
            activeScenarios[userScenarioType] = new InterventionScenarioState
            {
                Scenario = interventionScenario,
                CurrentStage = InterventionStage.Initial,
                AwaitingOutcome = false,
                EscalationCount = 0,
                IsResolved = false,
            };

            FireLifecycleEvent(userScenarioType, 0, InterventionStage.Initial, InterventionOutcome.Pending, "TriggerReceived", "First scenario initialization");
        }

        InterventionScenarioState state = activeScenarios[userScenarioType];

        // Global exhaustion check (safe guard!)
        if (state.IsResolved || state.EscalationCount > MaxEscalationLevel)
        {
            Debug.LogWarning("[InterventionManager] Scenario resolved or escalation exhausted. Ignoring further triggers.");
            state.IsResolved = true;
            ClearIntervention(userScenarioType);
            return;
        }

        // Skip if awaiting outcome already
        if (state.AwaitingOutcome) return;

        // Refresh state
        state.SpatialState = result.SpatialState;
        state.TurnStatus = result.TurnStatus;
        state.TaskContext = result.TaskContext;

        if (state.TaskContext == TaskContextType.Unknown)
        {
            Debug.LogWarning("[InterventionEngine] Received UNKNOWN task context.");
            return;
        }

        Intervention intervention = DecideIntervention(state);

        if (intervention == null)
        {
            Debug.Log($"[InterventionManager] No valid intervention found. Escalating...");

            state.EscalationCount++;
            FireLifecycleEvent(userScenarioType, state.EscalationCount, state.CurrentStage, InterventionOutcome.Pending, "Escalation", "Escalation incremented");

            // Global exhaustion double-check inside HandleScenario:
            if (state.EscalationCount > MaxEscalationLevel)
            {
                Debug.LogWarning("[InterventionManager] Escalation exhausted — scenario resolved.");
                state.IsResolved = true;
                ClearIntervention(userScenarioType);
                return;
            }
            else
            {
                // DO NOT RECURSE — simply wait for next external trigger.
                IEES.InvokePrivateOnInterventionRetryRequest(state);
                return;
            }
        }

        // Set before launching: a monitor may report its outcome immediately.
        state.AwaitingOutcome = true;
        FireLifecycleEvent(userScenarioType, state.EscalationCount, state.CurrentStage, InterventionOutcome.Pending, "InterventionStarted", intervention.Description ?? "");
        StartCoroutine(interventionExecutor.ExecuteIntervention(state, intervention));
    }

    public void OnOutcomeReceived(InterventionScenarioState state, InterventionOutcome outcome)
    {
        UserScenarioType userScenarioType = state.Scenario.UserScenarioType;

        // Ignore callbacks from retired states, canceled monitors, or duplicate reports.
        if (!activeScenarios.TryGetValue(userScenarioType, out var currentState) ||
            !ReferenceEquals(currentState, state) || !state.AwaitingOutcome)
            return;

        state.AwaitingOutcome = false;

        state.SpatialState = eIPlayerObserver.GetSpatialState();
        state.TurnStatus = eIPlayerObserver.GetTurnStatus();
        state.TaskContext = eIPlayerObserver.DetermineCurrentTaskContext();

        FireLifecycleEvent(userScenarioType, state.EscalationCount, state.CurrentStage, outcome, "OutcomeReceived", "");

        switch (outcome)
        {
            case InterventionOutcome.Success:
            case InterventionOutcome.EarlyExit:
            case InterventionOutcome.EmergencyCancelled:
                state.IsResolved = true;
                logger.LogOutcome(userScenarioType.ToString(), outcome != InterventionOutcome.EmergencyCancelled);
                interventionExecutor.OnInterventionResolvedByScenario(userScenarioType);
                ClearIntervention(userScenarioType, outcome);
                break;

            case InterventionOutcome.Failure:
                state.EscalationCount++;
                logger.LogOutcome(userScenarioType.ToString(), false);
                interventionExecutor.OnInterventionResolvedByScenario(userScenarioType);
                IEES.InvokePrivateOnInterventionRetryRequest(state);
                break;

            case InterventionOutcome.ContextDrift:
                logger.LogOutcome(userScenarioType.ToString(), false);
                interventionExecutor.OnInterventionResolvedByScenario(userScenarioType);
                ClearIntervention(userScenarioType, outcome);
                break;
        }
    }


    private void QueueInterventionRetry(InterventionScenarioState state)
    {
        if (!InterventionAdmission.CanStart(interventionEngineStatus)) return;
        StartCoroutine(HandleRetryAfterDelay(state));
    }
    private IEnumerator HandleRetryAfterDelay(InterventionScenarioState state)
    {
        yield return null;  // Defer to avoid entering scenario handling during an outcome callback.
        if (!InterventionAdmission.CanStart(interventionEngineStatus)) yield break;
        if (!activeScenarios.TryGetValue(state.Scenario.UserScenarioType, out var active) ||
            !ReferenceEquals(active, state) || state.AwaitingOutcome) yield break;
        HandleScenario(state.Scenario, BuildCurrentTriggerResult(state));
    }
    private TriggerInvestigationResult BuildCurrentTriggerResult(InterventionScenarioState state)
    {
        return new TriggerInvestigationResult
        {
            UserScenarioType = state.Scenario.UserScenarioType,
            ImmediateInterventionNeeded = true,
            InvestigationSummary = "Retry after failure",
            SpatialState = state.SpatialState,
            TurnStatus = state.TurnStatus,
            TaskContext = state.TaskContext,
            Metrics = new Dictionary<string, object>()
        };
    }
    private void ClearIntervention(UserScenarioType userScenarioType, InterventionOutcome outcome = InterventionOutcome.Failure)
    {
        if (activeScenarios.ContainsKey(userScenarioType))
            activeScenarios.Remove(userScenarioType);
        FireLifecycleEvent(userScenarioType, 0, InterventionStage.Resolved, outcome, "Resolved", "Scenario cleared");

    }


    /// <summary>
    /// Looks up or computes the correct intervention for a given scenario and stage.
    /// </summary>
    private Intervention DecideIntervention(InterventionScenarioState state)
    {
        Intervention intervention = resolver.Resolve(
            state.Scenario.UserScenarioType,
            state.EscalationCount,
            state.SpatialState,
            state.TurnStatus,
            state.TaskContext);
        if (intervention == null)
        {
            Debug.LogWarning("[InterventionManager] Resolver returned NULL intervention — skipping execution.");
            return null;
        }
        else
        {
            return intervention;
        }
    }

    private void FireLifecycleEvent(UserScenarioType scenario, int escalationCount, InterventionStage stage, InterventionOutcome outcome, string action, string comments = "")
    {
        OnInterventionLifecycleEvent?.Invoke(new InterventionLifecycleEvent
        {
            Timestamp = DateTime.Now,
            Scenario = scenario,
            EscalationCount = escalationCount,
            Stage = stage,
            Outcome = outcome,
            Action = action,
            Comments = comments
        });
    }



#if UNITY_EDITOR
    [Button(" Emergency Cancel Everything")]
#endif
    public void EmergencyCancelAll()
    {
        IEES.InvokeOnEmergencyCancelAllActiveInterventions();
        // Monitors normally report cancellation. Clear any scenarios without an active monitor as well.
        interventionExecutor.CancelAllActiveInterventions();
        foreach (var scenario in activeScenarios.Keys.ToList())
            ClearIntervention(scenario, InterventionOutcome.EmergencyCancelled);
        FireLifecycleEvent(UserScenarioType.Unknown, 0, InterventionStage.Resolved, InterventionOutcome.EmergencyCancelled, "EmergencyCancelAll", "All interventions forcibly cancelled");


    }

}

