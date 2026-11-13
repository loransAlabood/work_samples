using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>Runs intervention effects and connects each scenario to its outcome monitor.</summary>
public class InterventionExecutor : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private AvatarInterventionHandler avatarInterventionHandler;
    [SerializeField] private VisualEffectsManager visualEffectsManager;
    [SerializeField] private ElevenLabsTTSRuntime TTSRuntime; // Assuming this exists
    [SerializeField] private ParticipantNameTTSManager nameTTSManager;
    [SerializeField] private TimeInterventionMonitor timeMonitor;
    [SerializeField] private SpatialInterventionMonitor spatialMonitor;
    [SerializeField] private TrivialGazeMonitor gazeMonitor;
    [SerializeField] private InterventionManager interventionManager;
    [SerializeField] private GazeInterventionHandler gazeInterventionHandler;
    [SerializeField] private GameStartManager gameStartManager;
    [SerializeField] private TurnManager turnManager;
    private IEEventSystem IEES;

    [Header("Local Values")]
    // private Dictionary<Intervention, Coroutine> activeInterventions = new();
    private Dictionary<UserScenarioType, (Intervention intervention, Coroutine coroutine)> activeInterventions = new();
    private Dictionary<UserScenarioType, Intervention> interventionByScenario = new();
    

    void Start()
    {
        IEES = GetComponent<IEEventSystem>();
        timeMonitor = GetComponent<TimeInterventionMonitor>();
        if (IEES == null)
        {
            Debug.LogError("InterventionExecutor: IEEventSystem component not found in the scene.");
            return;
        }
        IEES.OnSpecificInterventionCancelled += HandleCancelledIntervention;
    }

    void OnDestroy()
    {
        if (IEES != null) IEES.OnSpecificInterventionCancelled -= HandleCancelledIntervention;
    }


    public IEnumerator ExecuteIntervention(InterventionScenarioState state, Intervention intervention)
    {
        if (intervention == null)
        {
            Debug.LogError("[Executor] Received NULL intervention!");
            yield break;
        }

        if (intervention.Description == null)
        {
            Debug.LogError("[Executor] Intervention.Description is NULL!");
        }

        Debug.Log("[Intervention Executor] Just received an intervention of type " + intervention.Description);

        // Register before starting the monitor: it can resolve the scenario during execution.
        TrackScenarioMapping(state.Scenario.UserScenarioType, intervention);
        

        float compensationDelay = intervention.Nature == InterventionNature.Verbal ? 4f : 2f;
        StartMonitorInterventionOutcome(state, intervention, compensationDelay);


        // Execution and outcome monitoring are independent; either may finish first.
        Coroutine execution = StartCoroutine(ExecuteInternal(intervention));


        // locally track active interventions, and map the scenario of the this intervention for resolving purposes.
        
        activeInterventions[state.Scenario.UserScenarioType] = (intervention, execution);
        yield break;
    }


    private void StartMonitorInterventionOutcome(InterventionScenarioState state, Intervention intervention, float compensationDelay)
    {
        UserScenarioType userScenarioType = state.Scenario.UserScenarioType;

        switch (userScenarioType)
        {
            case UserScenarioType.DidNotMoveInTime:
                timeMonitor.StartMonitoring(state, intervention, compensationDelay);
                break;

            case UserScenarioType.NotOptimalPositionForTooLong:
                spatialMonitor.StartMonitoring(state, intervention, compensationDelay);
                break;

            case UserScenarioType.Missed_2_Or_3_PortalsThisRound:
            case UserScenarioType.Missed_2_Or_3_Portals_For_2_Round:
                gazeMonitor.StartMonitoring(state, intervention);
                break;

        }

        //TODO: Must trigger an event here with some relevant information. Currently passing TriggerType, which might not be the best.

    }

    private void HandleCancelledIntervention(Intervention intervention)
    {
        Debug.Log($"[Executor] Cancelling an active intervention earlier: {intervention.Description}");

        // Look up which scenario this intervention belongs to:
        // Do not use FirstOrDefault().Key here: the first enum value may be a valid scenario.
        foreach (var pair in interventionByScenario.ToList())
        {
            if (!ReferenceEquals(pair.Value, intervention)) continue;
            OnInterventionResolvedByScenario(pair.Key);
            break;
        }
    }


    private IEnumerator ExecuteVerbal(VerbalInterventionType type)
    {
        Debug.Log($"[Executor] Executing verbal intervention type: {type}");

        switch (type)
        {
            case VerbalInterventionType.RemindScanAllPortals:
                    yield return StartCoroutine(TTSRuntime.PlayInterventionClip(isTrainingClip: false));
                break;

            case VerbalInterventionType.RepeatInstruction:
                    yield return RepeatLastInstruction();
                break;

            case VerbalInterventionType.RepeatUsingName:
                yield return StartCoroutine(PlayChainedNameClips());
                break;

            case VerbalInterventionType.FacePortals:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Face_Portals));
                break;

            case VerbalInterventionType.FacePortalsWithName:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Name_Face_Portals));
                break;

            case VerbalInterventionType.ReturnToZone:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Return_To_Zone));
                break;

            case VerbalInterventionType.ReturnToZoneWithName:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Name_Return_To_Zone));
                break;

            case VerbalInterventionType.ReturnToZoneToRestart:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Restart_From_Zone));
                break;

            case VerbalInterventionType.FacePlayerZone:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Face_Player_Zone));
                break;

            case VerbalInterventionType.FacePlayerZoneWithName:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Name_Face_Player_Zone));
                break;

            case VerbalInterventionType.First_Generic_Positive_Preferred:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Generic_Positive_Preferred));
                break;

            case VerbalInterventionType.Second_Generic_Positive_WithName:
                yield return StartCoroutine(TTSRuntime.PlayPromptClip(PromptTTS.Generic_Positive_Second_WithName));
                break;

            case VerbalInterventionType.PauseTalking:
                // Optional delay — implement if needed
                break;

            default:
                Debug.LogWarning($"Unknown verbal intervention type: {type}");
                break;
        }

        IEES.InvokeOnInterventionFired(TriggerSourceType.Time); // Same note as earlier — fix to use context
    }

    private IEnumerator RepeatLastInstruction()
    {
        if (gameStartManager.StoryModeIsActive)
        {
            yield return turnManager.ReplayCurrentCheckpointClipsOnly();
        }
        else if (gameStartManager.StoryModeIsActive == false)
        {
            yield return StartCoroutine(TTSRuntime.PlayInterventionClip(isTrainingClip: true));
        }
        else
        {
            Debug.Log("[Executor] received a instruction replay, but it is neither story mode nor training");
            yield break;
        }
        
    }
    
       private IEnumerator PlayChainedNameClips()
    {
        Debug.Log("[InterventionExecutor] Playing chained name clips...");
        yield return StartCoroutine(TTSRuntime.PlayNamedClip(nameTTSManager.GetNamePromptIndex("retry")[0]));
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(RepeatLastInstruction());
        Debug.Log("[InterventionExecutor] Finished playing chained name clips.");
    }

    private IEnumerator ExecuteVisual(Intervention intervention)
    {
        var type = intervention.VisualInterventionType.Value;
        var sub = intervention.VisualSubCategory.Value;

        Debug.Log($"[Visual Executor] Executing {sub} → {type} on target: {intervention.VisualTargetType}");

        if (sub == VisualSubCategory.SystemGenerated)
        {
            switch (type)
            {

                case VisualInterventionType.DrawPath:
                    {

                        visualEffectsManager.StartHighlightIntervention(intervention.SystemPathTarget.Value);
                        break;
                    }

                default:
                    Debug.LogWarning($"[Visual Executor] Unhandled visual type: {type}");
                    break;
            }
        }
        else if (sub == VisualSubCategory.AvatarBehaviour)
        {
            switch (type)
            {
                case VisualInterventionType.DemoAction:
                    yield return StartCoroutine(avatarInterventionHandler.StartInterventionAvatar(intervention.VisualTargetType.Value));
                    break;

                default:
                    Debug.LogWarning($"[Avatar Visual] Unhandled visual type: {type}");
                    break;
            }
        }

        // No coroutine to wait on for path, only for avatar.
        yield return null;
    }



    private IEnumerator ExecuteReTraining(ReTrainingInterventionType type)
    {
        switch (type)
        {
            case ReTrainingInterventionType.MovementTraining:
                Debug.LogWarning("The player arrived to the Movement Retraining Intervention level ");
                break;

            case ReTrainingInterventionType.AssociationTraining:
                Debug.LogWarning("The player arrived to the Association Training Intervention level ");
                break;

            case ReTrainingInterventionType.GazeTraining:
                yield return StartCoroutine (gazeInterventionHandler.StartGazeTrainingIntervention());
                break;

            default:
                Debug.LogWarning($"Unhandled retraining type: {type}");
                break;
        }
    }


 

    private IEnumerator ExecuteInternal(Intervention intervention)
    {
        switch (intervention.Nature)
        {
            case InterventionNature.Verbal:
                if (intervention.VerbalInterventionType.HasValue)
                    yield return StartCoroutine(ExecuteVerbal(intervention.VerbalInterventionType.Value));
                else
                    Debug.LogWarning("Missing VerbalType in intervention.");
                break;

            case InterventionNature.Visual:
                if (intervention.VisualInterventionType.HasValue && intervention.VisualSubCategory.HasValue)
                    yield return StartCoroutine(ExecuteVisual(intervention));
                else
                    Debug.LogWarning("Missing VisualType or SubCategory in intervention.");
                break;

            case InterventionNature.ReTraining:
                if (intervention.ReTrainingInterventionType.HasValue)
                    yield return StartCoroutine(ExecuteReTraining(intervention.ReTrainingInterventionType.Value));
                else
                    Debug.LogWarning("Missing ReTrainType in intervention.");
                break;

            default:
                Debug.LogWarning("Unknown intervention nature.");
                break;
        }

    }


    /// <summary>
    /// Helper methods
    /// This to clear up a resolved intervention. It takes an intervention type cos this script 
    /// tracks active interventions only
    /// </summary>
    /// <param name="intervention"></param>
    private void HandleInterventionCleanup(Intervention intervention)
    {
        if (intervention == null )
        {
            Debug.LogWarning("Cannot clean up a null intervention.");
            return;
        }

        // Deal with the action side of the intervention. 
        switch (intervention.Nature)
        {
            case InterventionNature.Verbal:
                if (TTSRuntime.IsPlaying)
                {
                    TTSRuntime.StopPlayback();
                }
                break;

            case InterventionNature.Visual:
                if (intervention.VisualInterventionType == VisualInterventionType.DrawPath)
                {
                    visualEffectsManager.ClearPath();
                }
                else if (intervention.VisualInterventionType == VisualInterventionType.DemoAction)
                {
                    avatarInterventionHandler.StopCurrentAvatarIntervention();
                }
                else
                {
                    Debug.LogWarning(" [Executor] Unknown Visual intervention type, can't cancel");
                }
                break;

            case InterventionNature.ReTraining:

                if (intervention.ReTrainingInterventionType == ReTrainingInterventionType.GazeTraining)
                {
                    gazeInterventionHandler.EndGazeTrainingIntervention();
                }
                else
                {
                    Debug.LogWarning("Unknown retraining type.");
                }
                break;
        }

    }


private void TrackScenarioMapping(UserScenarioType type, Intervention intervention)
    {
        // If we already have an active intervention for this scenario, stop and clean it
        if (activeInterventions.TryGetValue(type, out var previousPair))
        {
            var (_, oldCoroutine) = previousPair;
            StopCoroutine(oldCoroutine);
            HandleInterventionCleanup(previousPair.intervention);
            activeInterventions.Remove(type);
        }

        // Always update the interventionByScenario map for completeness
        interventionByScenario[type] = intervention;
    }




    public void OnInterventionResolvedByScenario(UserScenarioType type)
    {
        if (!activeInterventions.TryGetValue(type, out var interventionPair))
        {
            interventionByScenario.Remove(type);
            return;
        }

        var (intervention, coroutine) = interventionPair;

        StopCoroutine(coroutine);
        activeInterventions.Remove(type);
        interventionByScenario.Remove(type);

        HandleInterventionCleanup(intervention);
    }

   




    ////
    /// Emergency canceling methods
    /// 

    public void CancelAllActiveInterventions()
    {
        foreach (var type in activeInterventions.Keys.ToList())
        {
            OnInterventionResolvedByScenario(type);
        }
    }




    



}
