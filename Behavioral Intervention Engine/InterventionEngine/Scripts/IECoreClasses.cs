using System;
using System.Collections.Generic;

public enum TriggerSourceType
{
    Time,
    Spatial,
    Gaze,
    Decision
}

public enum EIPlayerStat{
    aceeded_reaction_time_threshold,
    unknown
}

public enum InterventionOutcome
{
    Pending,
    Success,
    Failure,
    ContextDrift,
    EarlyExit,
    EmergencyCancelled
}

/// <summary>Defines the scenario, intervention and outcome data exchanged between engine components.</summary>
public class TriggerData
{
    public DateTime Timestamp { get; set; }
    public TriggerSourceType SourceType { get; set; }
    public PlayerTurnStatus PlayerTurnStatus { get; set; }
    public UserScenarioType UserScenarioType { get; set; } // e.g., "DidNotMoveInTime"
    public string Details { get; set; }      // Optional extra info
}

//  2. TriggerInvestigationResult
public class TriggerInvestigationResult
{
    public UserScenarioType UserScenarioType;
    public string InvestigationSummary { get; set; }  // e.g., "Missed 1 portal but decisions reasonable."
    public bool ImmediateInterventionNeeded { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();

    public PlayerSpatialState SpatialState;
    public PlayerTurnStatus TurnStatus;
    public TaskContextType TaskContext;
}

// for logging


//  3. Intervention Taxonomy Enums
public enum InterventionNature
{
    Verbal,
    ReTraining,
    Visual,
    SystemBehaviour
}

public enum VisualSubCategory
{
    SystemGenerated,
    AvatarBehaviour
}

public enum VerbalInterventionType
{
    RepeatInstruction,
    RepeatUsingName,
    RepeatQuestionSlower,
    PauseTalking,

    /// /////////////

    ReturnToZone,
    FacePortals,
    FacePlayerZone,
    ReturnToZoneWithName,
    FacePortalsWithName,
    FacePlayerZoneWithName,

    ReturnToZoneToRestart,


    // Generic prompts for user preference

    First_Generic_Positive_Preferred,
    Second_Generic_Positive_WithName,


    // Gaze related
    RemindScanAllPortals

}

public enum ReTrainingInterventionType
{
    MovementTraining,
    AssociationTraining,
    GazeTraining

}


public enum VisualInterventionType
{
    DrawPath,
    DemoAction
  
}

public enum SystemPathTarget
{
    PlayerZone,
    PlayZone,
    Unknown

}

public enum VisualTargetType
{
    PlayZone_FacingPortals,
    PlayerZone_FacingPortals,
    PlayZone_FacingPlayerZone,
    PlayerZone_NotFacingPortals,
    Avatar_HelpTo_FacePortals,
    Avatar_HelpTo_ReturnToPlayerZone,
    Avatar_HelpTo_MakeSelection,

}


public enum UserScenarioType
{
    Unknown,
    DidNotMoveInTime,
    NotOptimalPositionForTooLong,
    Missed_2_Or_3_PortalsThisRound,
    Missed_2_Or_3_Portals_For_2_Round,
    UnreasonableDecisions
}



// 4. Intervention class
public class Intervention
{
    public InterventionNature Nature { get; set; }

    // Optional, depending on Nature
    public VisualSubCategory? VisualSubCategory { get; set; }
    public VisualInterventionType? VisualInterventionType { get; set; }
    public VisualTargetType? VisualTargetType { get; set; }
    public VerbalInterventionType? VerbalInterventionType { get; set; }
    public ReTrainingInterventionType? ReTrainingInterventionType { get; set; }
    public SystemPathTarget? SystemPathTarget { get; set; }


    public string Description { get; set; }
    public int VariationIndex { get; set; } = 0;
}



//  5. InterventionScenario
public class InterventionScenario
{
    public UserScenarioType UserScenarioType { get; set; }      // e.g., "DidNotMoveInTime"
    public string Description { get; set; }
    public DateTime TriggeredAt { get; set; }
    public int OccurrenceCount { get; set; }
}

//  6. InterventionStage + ScenarioState
public enum InterventionStage
{
    Initial,
    Complex,
    Dictate,
    Resolved
}

public enum TaskContextType
{
    Unknown,
    WaitingForSelection,      // Before selection is made
    ReturningToBase,          // After selection, expected to return
    ListeningToInstruction,   // During narration
    FreeExploration           // Any idle/flexible interaction mode
}


public class InterventionScenarioState
{
    public InterventionScenario Scenario { get; set; }
    public InterventionStage CurrentStage { get; set; } = InterventionStage.Initial;
    public bool AwaitingOutcome { get; set; } = false;
    public int EscalationCount { get; set; } = 0;
    public bool IsResolved { get; set; } = false;
    public PlayerSpatialState SpatialState { get; set; }
    public PlayerTurnStatus TurnStatus { get; set; }
    public TaskContextType TaskContext { get; set; } = TaskContextType.Unknown;
}

// A round is either a training step or a story checkpoint
public class PortalGazeRoundSummary
{

    public int RoundIndex;      // the number of the training step or story checkpoint
    public int MaxNumberOfPortals = 3;
    public int NumberOfPortalsLookAt;
    public List<int> PortalsLookAt;
    public int TotalNumberOfMissed => MaxNumberOfPortals - NumberOfPortalsLookAt;
    public bool AllPortalsLookAt => NumberOfPortalsLookAt == MaxNumberOfPortals; // fancy C# syntex for a property

}


public class InterventionEventData
{
    public DateTime Timestamp { get; internal set; }
    public string ScenarioKey { get; internal set; }
    public InterventionStage Stage { get; internal set; }
    public Intervention Intervention { get; internal set; }
    public string ActionDescription { get; internal set; }
    public InterventionOutcome Outcome { get; internal set; }
    public string Comments { get; internal set; }

}