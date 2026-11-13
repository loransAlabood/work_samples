using System.Collections;
using UnityEngine;

/// <summary>Reports completion or timeout of gaze prompts and retraining.</summary>
public class TrivialGazeMonitor : MonoBehaviour, IInterventionOutcomeMonitor
{
    [SerializeField] GazeInterventionHandler gazeInterventionHandler;
    private InterventionManager interventionManager;
    private IEEventSystem IEES;
    private InterventionScenarioState state;
    private Intervention intervention;
    private Coroutine currentCoroutine;

    [SerializeField] private float timeAllowance = 150f;
    [SerializeField] private float trivialDelayForVerbal = 6f;


    void Start()
    {
        interventionManager = GetComponent<InterventionManager>();
        IEES = GetComponent<IEEventSystem>();
        if (interventionManager == null || IEES == null)
        {
            Debug.LogError("[Gaze Monitor:] Can't reference the Intervention Manager Script OR the IEES");
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

public void StartMonitoring(InterventionScenarioState scenarioState, Intervention interventionData, float delayAfterExecution = 0f)
    {
        state = scenarioState;
        intervention = interventionData;

        if (intervention.Nature == InterventionNature.ReTraining &&
            intervention.ReTrainingInterventionType == ReTrainingInterventionType.GazeTraining)
        {
            currentCoroutine = StartCoroutine(MonitorGazeTrainingRoutine());
        }
        else
        {
            currentCoroutine = StartCoroutine(MonitorVerbalRoutine());
        }

    }

    // This is a special case we are literally just waiting for the narration to finish then fire success. 
    private IEnumerator MonitorVerbalRoutine()
    {
        if (trivialDelayForVerbal > 0f)
            yield return new WaitForSeconds(trivialDelayForVerbal);

        Debug.Log("[TrivialGazeMonitor] Verbal intervention succeeded automatically.");
        interventionManager.OnOutcomeReceived(state, InterventionOutcome.Success);
        Cleanup();
    }

    private IEnumerator MonitorGazeTrainingRoutine()
    {
        float elapsed = 0f;

        while (elapsed < timeAllowance)
        {
            if (gazeInterventionHandler.HasLookedAtAllPortals) // User succeeded during the time allowance 
            {
                Debug.Log("[TrivialGazeMonitor] Gaze retraining completed successfully.");
                interventionManager.OnOutcomeReceived(state, InterventionOutcome.Success);
                Cleanup();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[TrivialGazeMonitor] Gaze retraining timed out.");
        interventionManager.OnOutcomeReceived(state, InterventionOutcome.Failure);
        Cleanup();
    }

    public void StopGazeMonitoring(bool success = true)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        if (success)
        {
            interventionManager.OnOutcomeReceived(state, InterventionOutcome.Success);
        }
        else
        {
            interventionManager.OnOutcomeReceived(state, InterventionOutcome.Failure);
        }

        Cleanup();
    }

    public void HandleEmergencyCancel()
    {
        Debug.Log("[TrivialGazeMonitor] Emergency cancel triggered.");

        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        interventionManager.OnOutcomeReceived(state, InterventionOutcome.EmergencyCancelled);

        Cleanup();
    }


    private void Cleanup()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = null;
    }
}
