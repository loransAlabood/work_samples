using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Evaluates time-based responses, suspending the response clock when spatial alignment is lost.</summary>
public class TimeInterventionMonitor : MonoBehaviour, IInterventionOutcomeMonitor
{
    [SerializeField] private EIPlayerObserver eIPlayerObserver;
    [SerializeField] private InterventionManager interventionManager;
    [SerializeField] private IEEventSystem IEES;
    [SerializeField] private float checkInterval = 1f; // Poll every 1 second

    [Header("Timing")]
    [SerializeField] private float outcomeDelay = 12f;  //TODO: Make the outcome delays variable and set remotely.


    private Dictionary<UserScenarioType, InterventionScenarioState> activeScenarios = new();
    private Dictionary<UserScenarioType, Coroutine> monitorCoroutines = new(); // optional, for cancellation

    void Start()
    {
        eIPlayerObserver = GetComponent<EIPlayerObserver>();
        interventionManager = GetComponent<InterventionManager>();
        IEES = GetComponent<IEEventSystem>();
        if (eIPlayerObserver == null || interventionManager == null || IEES == null)
        {
            Debug.LogError("TimeInterventionMonitor: EIPlayerObserver OR interventionManager component not found in the scene.");
            return;
        }

        IEES.OnEmergencyCancelAllActiveInterventions += HandleEmergencyCancel;
    }

    void OnDestroy()
    {
        if (IEES != null)
        {
            IEES.OnEmergencyCancelAllActiveInterventions -= HandleEmergencyCancel;
        }
    }
    public void StartMonitoring(InterventionScenarioState state, Intervention intervention, float compensationDelay)
    {
        UserScenarioType scenario = state.Scenario.UserScenarioType;

        if (activeScenarios.ContainsKey(scenario))
            return;

        Coroutine coroutine = StartCoroutine(MonitorOutcome(state, intervention, compensationDelay));
        activeScenarios[scenario] = state;
        monitorCoroutines[scenario] = coroutine;
    }
    private IEnumerator MonitorOutcome(InterventionScenarioState state, Intervention intervention, float compensationDelay)
    {

        float elapsed = 0f;

        // Compensation belongs to this attempt, not the shared configuration field.
        float effectiveDelay = outcomeDelay +
            (intervention.Nature == InterventionNature.Verbal ? compensationDelay : 0f);

        // Check user response every second rather than once by the end of the outcome delay. 
        while (elapsed < effectiveDelay)
        {
            // Check every 1 second
            yield return new WaitForSeconds(1f);
            elapsed += 1f;

            // Time spent out of position does not count toward the response window.
            if (IsUserMisaligned())
            {
                yield return new WaitUntil(() => !IsUserMisaligned());
                elapsed = 0f;
                state.SpatialState = eIPlayerObserver.GetSpatialState();
                state.TurnStatus = eIPlayerObserver.GetTurnStatus();
                state.TaskContext = eIPlayerObserver.DetermineCurrentTaskContext();
            }

            // If player already responded (e.g., made a choice), resolve early

            if (EarlyCheckUserResponseAndResolve(state, intervention))
            {
                yield break;
            }
        }

        FinalCheckAndReport(state, intervention);

    }

    private bool EarlyCheckUserResponseAndResolve(InterventionScenarioState state, Intervention intervention)
    {
        if (eIPlayerObserver.GetTurnStatus() is PlayerTurnStatus.Human_Made_Choice
                or PlayerTurnStatus.Human_Turn_Ended)
        {
            Debug.Log($"[TimeMonitor] Player responded before delay expired. Marking success for {state.Scenario.UserScenarioType}");
            activeScenarios.Remove(state.Scenario.UserScenarioType);
            monitorCoroutines.Remove(state.Scenario.UserScenarioType);
            interventionManager.OnOutcomeReceived(state, InterventionOutcome.EarlyExit);
            //TODO: Previously I had an event that sent signal to the executor to halt the intervention. but now 
            // the Manager itself is handling that by directly communicating with the executor on output.
            // I could leave it as is. Or I could add an event here to tell me that this was an early exit.   
            return true;
        }
        else
        {
            return false;
        }
    }

    private void FinalCheckAndReport(InterventionScenarioState state, Intervention intervention)
{
    InterventionOutcome outcome;

    // Check if player responded normally
    bool userResponded = eIPlayerObserver.GetTurnStatus() is PlayerTurnStatus.Human_Made_Choice
                                                         or PlayerTurnStatus.Human_Turn_Ended;

    #if UNITY_EDITOR
        // In Editor mode, prevent infinite escalation if no response was possible.
        if (!userResponded)
        {
            outcome = InterventionOutcome.EarlyExit;
            Debug.LogWarning("[TimeMonitor] Editor override: EarlyExit triggered to prevent escalation loop.");
        }
        else
        {
            outcome = InterventionOutcome.Success;
        }
    #else
        outcome = userResponded ? InterventionOutcome.Success : InterventionOutcome.Failure;
    #endif

        Debug.Log($"[TimeMonitor] Outcome check for {state.Scenario.UserScenarioType}: {outcome}");

        // Clear this scenario from active tracker before reporting
        activeScenarios.Remove(state.Scenario.UserScenarioType);
        monitorCoroutines.Remove(state.Scenario.UserScenarioType);
        interventionManager.OnOutcomeReceived(state, outcome);
    }


    private bool IsUserMisaligned()
    {
        return eIPlayerObserver.GetSpatialState() != PlayerSpatialState.InZone_FacingPortals;
    }

    public void HandleEmergencyCancel()
    {
        foreach (var kvp in activeScenarios.ToList())
        {
            var scenarioType = kvp.Key;
            var state = kvp.Value;

            // Stop coroutine if tracked
            if (monitorCoroutines.TryGetValue(scenarioType, out var coroutine))
            {
                StopCoroutine(coroutine);
                monitorCoroutines.Remove(scenarioType);
            }

            // Report emergency outcome
            interventionManager.OnOutcomeReceived(state, InterventionOutcome.EmergencyCancelled);
        }

        activeScenarios.Clear();
        Debug.LogWarning("[Time Intervention Monitor] Emergency Cancelling All Active Interventions.");
    }
}
