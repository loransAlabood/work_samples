using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Rechecks incoming triggers against the current spatial, timing and task context.</summary>
public class TriggerInterpreter : MonoBehaviour
{

    EIPlayerObserver eIPlayerOserver;
    public float TimeAllowedToReturnToBase = 5.0f;

    IEEventSystem IEES;


    private void Start()
    {
        eIPlayerOserver = GetComponent<EIPlayerObserver>();
        IEES = FindObjectOfType<IEEventSystem>();
        if (eIPlayerOserver == null || IEES == null)
        {
            Debug.LogError("EIPlayerOserver or IEEventSystem component not found in the scene.");
            return;
        }
        IEES.OnRaiseTrigger += HandleTriggerRaised;
    }
    
    private void OnDestroy()
    {
        if (IEES != null)
        {
            IEES.OnRaiseTrigger -= HandleTriggerRaised;
        }
    }

    private void HandleTriggerRaised(TriggerData data)
    {
        Debug.Log($"[Trigger Interpreter] Trigger received: {data.UserScenarioType} at {data.Timestamp}");
        var (scenario, result) = InterpretAndInvestigate(data);
        Debug.Log($"[Trigger Interpreter] Trigger interpreted: {scenario.Description} at {scenario.TriggeredAt}");
        IEES.InvokeOnTriggerInvestigated(scenario, result);
    }


    // Investigate and interpret incoming trigger
    public (InterventionScenario, TriggerInvestigationResult) InterpretAndInvestigate(TriggerData data)
    {
        InterventionScenario scenario = new()
        {
            UserScenarioType = data.UserScenarioType,
            Description = MapDescription(data.UserScenarioType),
            TriggeredAt = data.Timestamp,
            OccurrenceCount = 1
        };

        TriggerInvestigationResult investigation = new()
        {
            UserScenarioType = data.UserScenarioType,
            InvestigationSummary = "Default: no investigation.",
            ImmediateInterventionNeeded = true
        };

        switch (data.UserScenarioType)
        {
            case UserScenarioType.DidNotMoveInTime:
                investigation = InvestigateTimeTrigger(data);
                break;
            case UserScenarioType.NotOptimalPositionForTooLong:
                investigation = InvestigateSpatialTrigger(data);
                break;
            case UserScenarioType.Missed_2_Or_3_PortalsThisRound:
            case UserScenarioType.Missed_2_Or_3_Portals_For_2_Round:
                investigation = InvestigateGazeTrigger(data);
                break;
            case UserScenarioType.UnreasonableDecisions:
                investigation = InvestigateDecisionTrigger(data);
                break;
        }

        return (scenario, investigation);
    }


    private string MapDescription(UserScenarioType scenarioType)
    {
        return scenarioType switch
        {
            UserScenarioType.DidNotMoveInTime => "User didn't make a move within allowed time.",
            UserScenarioType.NotOptimalPositionForTooLong => "User is not in optimal position.",
            UserScenarioType.Missed_2_Or_3_PortalsThisRound => "User missed at least 2 portals this round.",
            UserScenarioType.Missed_2_Or_3_Portals_For_2_Round => "User missed at least 2 portals for two consecutive rounds",
            UserScenarioType.UnreasonableDecisions => "User made repeated unreasonable decisions.",
            _ => "Unknown scenario."
        };
    }


    private TriggerInvestigationResult InvestigateTimeTrigger(TriggerData data)
    {
        bool escalate = true;
        string summary = "Exceeded allowed selection time.";

        if (eIPlayerOserver.TimeSinceInstruction < eIPlayerOserver.ResponseTimeThreshold)
        {
            escalate = false;
            summary = "User still within time limit to make selection.";
        }

        return new TriggerInvestigationResult
        {
            UserScenarioType = data.UserScenarioType,
            ImmediateInterventionNeeded = escalate,
            InvestigationSummary = summary,
            SpatialState = eIPlayerOserver.GetSpatialState(),
            TurnStatus = eIPlayerOserver.GetTurnStatus(),
            TaskContext = eIPlayerOserver.DetermineCurrentTaskContext(),
            Metrics = new Dictionary<string, object>
            {
                { "TimeOutOfOptimalZone", eIPlayerOserver.TimeOutOfOptimalZone },
                { "AllowedOutOfZoneTime", eIPlayerOserver.OutOfOptimalZoneTimeThreshold }
            }
        };
    }

    private TriggerInvestigationResult InvestigateSpatialTrigger(TriggerData data)
    {
        PlayerSpatialState spatial = eIPlayerOserver.GetSpatialState();
        PlayerTurnStatus status = data.PlayerTurnStatus;
        float timeOut = eIPlayerOserver.TimeOutOfOptimalZone;
        float threshold = eIPlayerOserver.OutOfOptimalZoneTimeThreshold;

        bool escalate = false;
        string summary = "";

        if (timeOut <= threshold)
        {
            summary = "Still within allowed time out of optimal zone.";
        }
        else
        {
            // Escalate based on stage + location/orientation
            switch (status)
            {
                case PlayerTurnStatus.Human_Turn_Started:
                    if (spatial != PlayerSpatialState.InZone_FacingPortals)
                    {
                        escalate = true;
                        summary = "Player not in optimal position at start of turn.";
                    }
                    break;

                case PlayerTurnStatus.Human_Made_Choice:
                    if (spatial == PlayerSpatialState.NotInZone_FacingPortals)
                    {
                        escalate = true;
                        summary = "Player stuck in portal zone after selection.";
                    }
                    else if (spatial == PlayerSpatialState.InZone_NotFacingPortals)
                    {
                        escalate = true;
                        summary = "Player returned but not facing portals.";
                    }
                    break;

                case PlayerTurnStatus.Human_Turn_Ended:
                    // You might skip here if turn over resets the risk
                    break;

                default:
                    escalate = true;
                    summary = "Spatial misalignment detected outside normal bounds.";
                    break;
            }
        }

        return new TriggerInvestigationResult
        {
            UserScenarioType = data.UserScenarioType,
            ImmediateInterventionNeeded = escalate,
            InvestigationSummary = summary,
            SpatialState = eIPlayerOserver.GetSpatialState(),
            TurnStatus = eIPlayerOserver.GetTurnStatus(),
            TaskContext = eIPlayerOserver.DetermineCurrentTaskContext(), 
            Metrics = new Dictionary<string, object>
            {
                { "TimeOutOfOptimalZone", eIPlayerOserver.TimeOutOfOptimalZone },
                { "AllowedOutOfZoneTime", eIPlayerOserver.OutOfOptimalZoneTimeThreshold }
            }
        };
    }



    private TriggerInvestigationResult InvestigateGazeTrigger(TriggerData data)
    {
        // For Gaze triggers, we always escalate immediately — no investigation logic needed
        return new TriggerInvestigationResult
        {
            UserScenarioType = data.UserScenarioType,
            ImmediateInterventionNeeded = true,
            InvestigationSummary = "Gaze-based trigger fired.",
            Metrics = null // Or new Dictionary<string, object>() if you prefer keeping structure consistent
        };
    }


    private TriggerInvestigationResult InvestigateDecisionTrigger(TriggerData data)
    {
        bool escalate = !eIPlayerOserver.AreDecisionsReasonable;
        string summary = escalate
            ? "User's decisions appear random and inconsistent."
            : "Decisions seem logical despite previous flags.";

        return new TriggerInvestigationResult
        {
            UserScenarioType = data.UserScenarioType,
            ImmediateInterventionNeeded = escalate,
            InvestigationSummary = summary,
            Metrics = new Dictionary<string, object>
            {
                { "RecentDecisionConsistency", eIPlayerOserver.AreDecisionsReasonable }
            }
        };
    }
}