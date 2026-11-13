using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Publishes the trigger, intervention-status and lifecycle signals used by this subsystem.</summary>
public class IEEventSystem : MonoBehaviour
{
    public event Action<TriggerData> OnRaiseTrigger; // Used to raise a trigger event with TriggerData

    public Action<InterventionScenario, TriggerInvestigationResult> OnTriggerInvestigated; // Used to notify when a trigger has been investigated
    public Action<TriggerSourceType> onInterventionFired; // Used to notify when an intervention has been fired

    public Action<Intervention> OnSpecificInterventionCancelled;

    public Action OnEmergencyCancelAllActiveInterventions;
    public Action<bool> OnRunningGazeRetraining;

    public Action <InterventionScenarioState> OnInterventionRetryRequested;

    // TO REFACTOR
    public Action<InterventionEngineStatus> OnInterventionEngineStatusChanged;


    ///////////////////////////////////////////////////////

    public void InvokeOnRaiseTrigger(TriggerData triggerData)
    {
        OnRaiseTrigger?.Invoke(triggerData);
    }

    public void InvokeOnTriggerInvestigated(InterventionScenario scenario, TriggerInvestigationResult result)
    {
        OnTriggerInvestigated?.Invoke(scenario, result);
    }

    public void InvokeOnInterventionFired(TriggerSourceType sourceType)
    {
        onInterventionFired?.Invoke(sourceType);
    }

    public void InvokeOnActiveInterventionIsCancelled(Intervention intervention)
    {
        OnSpecificInterventionCancelled?.Invoke(intervention);
    }

    public void InvokeOnEmergencyCancelAllActiveInterventions()
    {
        OnEmergencyCancelAllActiveInterventions?.Invoke();
    }

    public void InvokeOnRunningGazeRetraining(bool status)
    {
        OnRunningGazeRetraining?.Invoke(status);
    }


    // No used? TOREFACTOR
    public void InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus interventionEngineStatus)
    {
        OnInterventionEngineStatusChanged?.Invoke(interventionEngineStatus);
    }

    public void InvokePrivateOnInterventionRetryRequest(InterventionScenarioState interventionScenarioState)
    {
        OnInterventionRetryRequested?.Invoke(interventionScenarioState);
    }
}
