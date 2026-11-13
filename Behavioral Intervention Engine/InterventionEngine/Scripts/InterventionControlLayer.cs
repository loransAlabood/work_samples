using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>Enables, pauses and cancels interventions through the engine event channel.</summary>
public class InterventionControlLayer : MonoBehaviour
{
    [Header("Intervention System References")]
    private InterventionManager interventionManager;
    [SerializeField] private GazeInterventionHandler gazeInterventionHandler;
    private IEEventSystem IEES;

    // --- Properties ---
    public InterventionEngineStatus CurrentInterventionEngineStatus = InterventionEngineStatus.Disabled;
    
    public bool IsGazeRetrainingActive;

    void Start()
    {
        interventionManager = GetComponent<InterventionManager>();
        IEES = GetComponent<IEEventSystem>();

        if (interventionManager == null || IEES == null)
        {
            Debug.LogError("[InterventionControlLayer] Can't find InterventionManager or IEES references.");
        }

        IEES.OnRunningGazeRetraining += UpdateGazeRetrainingStatus;
        IEES.OnInterventionEngineStatusChanged += UpdatedEngineSTatus;
    }

   

    void OnDestroy()
    {
        if (IEES != null)
        {
            IEES.OnRunningGazeRetraining -= UpdateGazeRetrainingStatus;
            IEES.OnInterventionEngineStatusChanged -= UpdatedEngineSTatus;
        }
    }

     private void UpdatedEngineSTatus(InterventionEngineStatus status)
    {
        CurrentInterventionEngineStatus = status;
    }
    private void UpdateGazeRetrainingStatus(bool status)
    {
        IsGazeRetrainingActive = status;
    }

    // --- Pause/Resume ---

#if UNITY_EDITOR
    [Button("Pause")]
#endif
    public void PauseInterventions()
    {
        IEES.InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus.Paused);

        Debug.Log("[InterventionControlLayer] Interventions PAUSED.");
    }

#if UNITY_EDITOR
    [Button("Resume")]
#endif
    public void ResumeInterventions()
    {
        IEES.InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus.Resume);
        Debug.Log("[InterventionControlLayer] Interventions RESUMED.");
    }

    // --- Enable/Disable ---
#if UNITY_EDITOR
    [Button("Disable")]
#endif
    public void DisableInterventions()
    {
        IEES.InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus.Disabled);
        Debug.Log("[InterventionControlLayer] Interventions DISABLED (fully inactive).");
    }

#if UNITY_EDITOR
    [Button("Enable")]
#endif
    public void EnableInterventions()
    {
        IEES.InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus.Enabled);
        Debug.Log("[InterventionControlLayer] Interventions ENABLED.");
    }

    // --- Emergency Cancel ---

    public void EmergencyCancelAll()
    {
        interventionManager.TriggerEmergencyCancelAll();
        IEES.InvokeOnInterventionEngineStatusChanged(InterventionEngineStatus.Disabled);
        Debug.Log("[InterventionControlLayer] Emergency cancel triggered. Intervention Engine is Disabled");
    }

    // --- Gaze retraining safe story flow ---

    public IEnumerator WaitForGazeRetrainingIfActive()
    {
        if (!IsGazeRetrainingActive)
            yield break;

        Debug.Log("[InterventionControlLayer] Gaze retraining detected, waiting...");

        yield return new WaitUntil(() => gazeInterventionHandler.HasLookedAtAllPortals);

        Debug.Log("[InterventionControlLayer] Gaze retraining completed. Continuing story.");
    }
}
