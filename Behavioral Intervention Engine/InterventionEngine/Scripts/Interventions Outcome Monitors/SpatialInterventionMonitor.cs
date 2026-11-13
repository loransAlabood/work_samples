using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Monitors spatial interventions to determine whether the user corrects their position after a prompt.
/// </summary>
public class SpatialInterventionMonitor : MonoBehaviour, IInterventionOutcomeMonitor
{
    [Header("Dependencies")]
    EIPlayerObserver eIPlayerObserver;
    private InterventionManager interventionManager;
    private IEEventSystem IEES;

    [Tooltip("Time (in seconds) to wait before evaluating success.")]
    [SerializeField] private float defaultDelay = 10f;

    private Dictionary<UserScenarioType, Coroutine> monitorRoutines = new();
    private Dictionary<UserScenarioType, InterventionScenarioState> activeScenarios = new();

    void Start()
    {
        eIPlayerObserver = GetComponent<EIPlayerObserver>();
        interventionManager = GetComponent<InterventionManager>();
        IEES = GetComponent<IEEventSystem>();

        if (eIPlayerObserver == null || interventionManager == null || IEES == null)
        {
            Debug.LogError("[Spatial Intervention Monitor] Can't find EIPlayerObserver or InterventionManager or IEES");
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
        {
            Debug.Log($"[SpatialInterventionMonitor] Skipping monitor; already running for: {scenario}");
            return;
        }

        activeScenarios.Add(scenario, state);  //  dictionary insert

        Coroutine routine = StartCoroutine(MonitorOutcome(state));
        monitorRoutines[scenario] = routine;
    }

    private IEnumerator MonitorOutcome(InterventionScenarioState state)
    {
        yield return new WaitForSeconds(defaultDelay);

        var currentContext = eIPlayerObserver.DetermineCurrentTaskContext();
        var TaskContextAtInterventionTime = state.TaskContext;
        InterventionOutcome outcome;

        //  If context has changed, log it for debugging
        if (currentContext != TaskContextAtInterventionTime)
        {
            Debug.Log($"[SpatialMonitor] Context changed from {TaskContextAtInterventionTime} → {currentContext} during monitoring. Evaluating with original context.");
            outcome = InterventionOutcome.ContextDrift;
        }
        else
        {
            Debug.Log($"[SpatialMonitor] Spatial={state.SpatialState}, Turn={state.TurnStatus}, Context={currentContext}");
            bool isSuccess = DidInterventionResolveProblem(state);
            outcome = isSuccess ? InterventionOutcome.Success : InterventionOutcome.Failure;
            Debug.Log($"[SpatialMonitor] Outcome check for {state.Scenario.UserScenarioType}: {(isSuccess ? "Success" : "Failure")}");

        }
        //  Cleanup
        activeScenarios.Remove(state.Scenario.UserScenarioType);


        interventionManager.OnOutcomeReceived(state, outcome);
    }


    private bool DidInterventionResolveProblem(InterventionScenarioState state)
    {
        var context = state.TaskContext;
        var spatial = eIPlayerObserver.GetSpatialState();
        var turn = eIPlayerObserver.GetTurnStatus();

        switch (context)
        {
            //if the context was get the user to be in position during instruction,
            //Then their current spatial status should be in zone and facing portals
            case TaskContextType.ListeningToInstruction:
                return spatial == PlayerSpatialState.InZone_FacingPortals;

            // if the user was in at a stage of making a selection, 
            // Then they either should be in zone facing portals to make a selection, 
            // or have already moved and make a choice.
            case TaskContextType.WaitingForSelection:
                // Either facing the portals in zone OR made a choice and moved forward
                return spatial == PlayerSpatialState.InZone_FacingPortals ||
                       turn == PlayerTurnStatus.Human_Made_Choice;

            // if the user context was to return to base. 
            // Then they should be in zone, and either facing portals or not, or they return and ended their turn.
            case TaskContextType.ReturningToBase:
                // Either returning or already back
                return spatial == PlayerSpatialState.InZone_NotFacingPortals ||
                        spatial == PlayerSpatialState.InZone_FacingPortals ||
                       turn == PlayerTurnStatus.Human_Turn_Ended;

            case TaskContextType.FreeExploration:
                // Just make sure they’re back and facing the right way
                return spatial == PlayerSpatialState.InZone_FacingPortals;

            default:
                return false;
        }
    }


    public void HandleEmergencyCancel()
    {
        Debug.Log("[Spatial Intervention Monitor] Emergency cancel triggered.");

        foreach (var scenarioType in activeScenarios.Keys.ToList())
        {
            if (monitorRoutines.TryGetValue(scenarioType, out var coroutine))
            {
                StopCoroutine(coroutine);
                monitorRoutines.Remove(scenarioType);
            }

            var state = activeScenarios[scenarioType];
            interventionManager.OnOutcomeReceived(state, InterventionOutcome.EmergencyCancelled);
        }

        activeScenarios.Clear();
    }



}


