using System;
using System.Collections;
using System.Collections.Generic;
//using Unity.XR.Oculus;
using UnityEditor;
using UnityEngine;


/// <summary>Collects per-round behavioral observations and raises trigger conditions for the interpreter.</summary>
public class EIPlayerObserver : MonoBehaviour
{

    private TriggerInterpreter triggerInterpreter;
    private StoryContextManager storyContextManager;
    private CentralEventSystem CES;
    private IEEventSystem IEES;

    [Header("Tracking states")]
    public int MissedPortalStreak;
    public bool AreDecisionsReasonable;

    public float ResponseTimeThreshold { get; private set; } = 10f; // seconds
    public float OutOfOptimalZoneTimeThreshold { get; private set; } = 5f; // seconds
    public bool HasMadeSelection { get; private set; } = false;

    private InterventionEngineStatus interventionEngineStatus = InterventionEngineStatus.Disabled;
    public float TimeSinceInstruction { get; private set; }
    public float TimeOutOfOptimalZone { get; private set; }
    public PlayerSpatialState GetSpatialState() => playerSpatialState;
    public PlayerTurnStatus GetTurnStatus() => playerTurnStatus;

    [Header("Local variables")]
    private PlayerSpatialState playerSpatialState;
    [SerializeField] private PlayerTurnStatus playerTurnStatus;
    private InterventionControlLayer interventionControlLayer;
    private float timeInOptimalPositionRecord = 0;
    private Coroutine trackOptimalPositionTimeRecord;

    private TaskContextType currentTaskContextType;

    [Header("Portal Data")]

    private List<int> currentPortalIndices;


    [Header("Session long data")]

    private HashSet<PortalSlot> currentPortalSlots = new();
    private int roundsToTrack = 2;
    private Queue<int> recentPortalMisses = new Queue<int>();

    const int startingRoundIndex = 0;
    private int currentRoundIndex = 0;
    private List<PortalGazeRoundSummary> recentRoundSummaries = new List<PortalGazeRoundSummary>();
    

    private Coroutine trackingSpatialDataTime = null;



    void Start()
    {

        triggerInterpreter = GetComponent<TriggerInterpreter>();
        interventionControlLayer = GetComponent<InterventionControlLayer>();
        IEES = GetComponent<IEEventSystem>();
        CES = CentralEventSystem.Instance;


        if (CES == null || IEES == null)
        {
            Debug.LogError("CentralEventSystem instance or EIEvent System is null. Please ensure it is initialized before PlayerStats.");
            return;
        }
        CES.OnPlayerSpatialStatusChanged += OnPlayerSpatialStatusChanged;
        CES.OnPlayerSpatialStatusChanged += TrackOptimalPositionTimeRecord; // Track optimal position time when spatial status changes    
        CES.OnPlayerTurnStatusChanged += OnPlayerTurnStatusChanged;
        CES.OnPlayerTurnStatusChanged += TrackPlayerGazeQuality;
        CES.OnHumanPlayerChoiceSelected += OnHumanPlayerChoiceSelected;
        CES.OnStoryIsReady += OnStoryIsReady;
        IEES.OnInterventionEngineStatusChanged += UpdatedEngingeStatus;

    }

   

    void OnDestroy()
    {
        if (CES != null || IEES != null)
        {
            CES.OnPlayerSpatialStatusChanged -= OnPlayerSpatialStatusChanged;
            CES.OnPlayerSpatialStatusChanged -= TrackOptimalPositionTimeRecord; // Track optimal position time when spatial status changes    
            CES.OnPlayerTurnStatusChanged -= OnPlayerTurnStatusChanged;
            CES.OnPlayerTurnStatusChanged -= TrackPlayerGazeQuality;
            CES.OnPortalLookStarted -= OnPortalLookStarted;
            CES.OnHumanPlayerChoiceSelected -= OnHumanPlayerChoiceSelected;
            CES.OnStoryIsReady -= OnStoryIsReady;
            IEES.OnInterventionEngineStatusChanged -= UpdatedEngingeStatus;

        }
    }

    private void UpdatedEngingeStatus(InterventionEngineStatus status)
    {
        interventionEngineStatus = status;
    }
    private void OnStoryIsReady()
    {
        // The story Context Manager might take time to be ready, thus, only reference it when event is fired singaling it is ready to be used
        storyContextManager = FindObjectOfType<StoryContextManager>();
    }

    private void SendTriggerRequest(TriggerSourceType sourceType, PlayerTurnStatus playerTurnStatus, UserScenarioType userScenarioType, string details)
    {
        // if the intervention manager is disabled, don't send trigger. 
        if (interventionControlLayer.CurrentInterventionEngineStatus == InterventionEngineStatus.Disabled) return;

        var trigger = new TriggerData
        {
            Timestamp = DateTime.Now,
            PlayerTurnStatus = playerTurnStatus,
            SourceType = sourceType,
            UserScenarioType = userScenarioType,
            Details = details
        };

        IEES.InvokeOnRaiseTrigger(trigger);

    }


    #region Player Spatial Status Tracking
    private void OnPlayerSpatialStatusChanged(PlayerSpatialState state)
    {
        // pass the data to the local var so it is accessible by other methods all the time
        playerSpatialState = state;
        if (interventionEngineStatus == InterventionEngineStatus.Disabled || interventionEngineStatus == InterventionEngineStatus.Paused) return;
        
        // If spatial data received showing that player is in optimal position and there was a tracking coroutine, 
        // stop the tracking and rest values as the player is now back to optimal position. 
        if (playerSpatialState == PlayerSpatialState.InZone_FacingPortals && trackingSpatialDataTime != null)
        {
            StopCoroutine(trackingSpatialDataTime);  // stop counting
            TimeOutOfOptimalZone = 0;                 // reset timer
            trackingSpatialDataTime = null;           // resent coroutine to null so it can be used later. 
        }

        TrackSpatialStatusForThisRound();

    }

    private void TrackSpatialStatusForThisRound()
    {

        if (playerSpatialState == PlayerSpatialState.InZone_FacingPortals) return;

        // Prevent start counting multiple times as the player's out of optimal position can vary. 
        // Note that a bool flag might help here, but having a Coroutine var for trackingSpatialDataTime and assigning
        // its value to the start of the coroutine method give us a reference to this method so we can stop the counter if needed    
        trackingSpatialDataTime ??= StartCoroutine(StartTrackingSpatialStatusTime());   // if trackingSpatialDataTime is null, start the coroutine
    }

    private IEnumerator StartTrackingSpatialStatusTime()
    {
        while (TimeOutOfOptimalZone < OutOfOptimalZoneTimeThreshold)
        {
            TimeOutOfOptimalZone += Time.deltaTime;
            yield return null;
        }

        SendTriggerRequest(TriggerSourceType.Spatial, playerTurnStatus, UserScenarioType.NotOptimalPositionForTooLong, "Player is out of optimal zone for too long");
        TimeOutOfOptimalZone = 0;
        trackingSpatialDataTime = null;

    }



    #endregion


    #region Player response Time Tracking
    private void OnPlayerTurnStatusChanged(PlayerTurnStatus status)
    {
        playerTurnStatus = status;
        if (status == PlayerTurnStatus.Human_Turn_Started)
        {
            HasMadeSelection = false;
            TimeSinceInstruction = 0f; // Reset the timer when the turn starts
            StartCoroutine(WatchPlayerResponseTime()); // Start tracking player response time
        }
        else if (status == PlayerTurnStatus.Human_Turn_Ended)
        {
            TimeSinceInstruction = 0f; // Stop tracking player response time
            StopCoroutine(WatchPlayerResponseTime()); // Stop the timer when the turn ends
        }
        else
        {
            Debug.Log("Player turn status is either Stopped or Stared. Stopping time tracking for now ");
            TimeSinceInstruction = 0f; // Stop tracking player response time
            StopCoroutine(WatchPlayerResponseTime());
        }
    }

    private IEnumerator WatchPlayerResponseTime()
    {

        while (TimeSinceInstruction < ResponseTimeThreshold)
        {
            //Debug.Log($"Player response time: {TimeSinceInstruction:F2} seconds");
            TimeSinceInstruction += Time.deltaTime; // Start the timer when the turn starts
            yield return null;
        }

        bool shouldSendTimeTrigger = ShouldSendTimeTrigger();
        if (shouldSendTimeTrigger)
        {
            Debug.Log("Player took too long to respond, raising a trigger to the interpreter for validation");
            SendTriggerRequest(TriggerSourceType.Time, playerTurnStatus, UserScenarioType.DidNotMoveInTime, "Exceeded reaction time threshold.");

        }
        else
        {
            Debug.Log("Player may have acceded reaction time threshold, but is NOT in optimal position, so no trigger is sent.");
            TimeSinceInstruction = 0f; // Reset the timer if no trigger is sent
            RestartTimeTracking();
        }

    }

    public void RestartTimeTracking()
    {
        StopCoroutine(WatchPlayerResponseTime()); // Stop the timer if no trigger is sent
        StartCoroutine(WatchPlayerResponseTime()); // Start tracking player response time

    }

    
    
    private bool ShouldSendTimeTrigger()
    {
        if (interventionEngineStatus == InterventionEngineStatus.Disabled || interventionEngineStatus == InterventionEngineStatus.Paused)
        {
            return false;
        }
        else
        {
            if (interventionControlLayer.IsGazeRetrainingActive == true)
            {

                return false;
            }
            else
            {
                if (playerSpatialState == PlayerSpatialState.InZone_FacingPortals && timeInOptimalPositionRecord >= ResponseTimeThreshold)
                {
                    return true; // Player is in optimal position and has been there long enough to use an intervention
                }
                else
                {
                    return false; // Player is not in optimal position or has not been there long enough
                }
            }
        }


    }

    /// <summary>
    /// Checks if the user has acted within the response time threshold.
    /// This tells us if the user broke out of stuck state.
    /// </summary>
    /// <returns></returns>
    public bool HasUserActedRecently()
    {
        return TimeSinceInstruction < ResponseTimeThreshold && playerTurnStatus == PlayerTurnStatus.Human_Turn_Started;
    }


    #endregion


    #region Player gaze tracking. 
    private void OnPortalLookStarted(SpawnedPortalData data)
    {
        if (interventionEngineStatus == InterventionEngineStatus.Disabled || interventionEngineStatus == InterventionEngineStatus.Paused) return;
        TrackPortalStateForThisRound(data);
    }

    private void TrackPlayerGazeQuality(PlayerTurnStatus status)
    {
        if (status == PlayerTurnStatus.Human_Turn_Ended)
        {
            OnRoundEnded();
        }
        else if (status == PlayerTurnStatus.Human_Turn_Started)
        {
            GazeTrackingRoundStarted();
        }
        else
        {
            return;
        }
    }

    

    private void TrackPortalStateForThisRound(SpawnedPortalData data)
    {
       
        PortalSlot newSlot = data.portalSlot;

        if (!currentPortalSlots.Contains(newSlot))
        {
            currentPortalSlots.Add(newSlot);
            Debug.Log($"[Observer] User looked at {currentPortalSlots.Count} portals this round.");
        }
    }


    public void OnRoundEnded()
    {
        CES.OnPortalLookStarted -= TrackPortalStateForThisRound;
        int seen = currentPortalSlots.Count;
        int missed = 3 - seen;

        recentPortalMisses.Enqueue(missed);
        if (recentPortalMisses.Count > roundsToTrack)
            recentPortalMisses.Dequeue();

        Debug.Log($"[Observer] Round summary: Seen={seen}, Missed={missed}");

        currentPortalSlots.Clear();
    }

    public void GazeTrackingRoundStarted()
    {
        CES.OnPortalLookStarted += TrackPortalStateForThisRound;

        //  Single round miss trigger
        if (recentPortalMisses.Count >= 1)
        {
            int lastMissed = recentPortalMisses.Peek();

            if (lastMissed >= 2)
            {
                Debug.Log("[Observer] Firing MissedPortalsWarning trigger");
                SendTriggerRequest(
                    TriggerSourceType.Gaze,
                    PlayerTurnStatus.Human_Turn_Started,
                    UserScenarioType.Missed_2_Or_3_PortalsThisRound,
                    "Missed 2 or more portals in previous round"
                );
                return;  // important: warning takes priority
            }
        }

        //  Back-to-back miss trigger
        if (recentPortalMisses.Count == 2)
        {
            int[] misses = recentPortalMisses.ToArray();

            if (misses[0] >= 2 && misses[1] >= 2)
            {
                Debug.Log("[Observer] Firing GazeRetraining trigger");
                SendTriggerRequest(
                    TriggerSourceType.Gaze,
                    PlayerTurnStatus.Human_Turn_Started,
                    UserScenarioType.Missed_2_Or_3_Portals_For_2_Round,
                    "Missed 2 or more portals across two consecutive rounds"
                );
            }
        }
    }


    #endregion

    // #region Player Decision Making Tracking
    private void OnHumanPlayerChoiceSelected(int index)
    {

        HasMadeSelection = true;
        // // First check the portal stats if this round, then check for the previous three rounds for patters. 
        // if (currentPortalSlots.Count == 0)
        // {
        //     SendTriggerRequest(TriggerSourceType.Gaze, playerTurnStatus, UserScenarioType.HasNotLookedAtAnyPortal, "User madea selection without scanning any portals");
        // }
        // CheckPortalStatusForPreviousRounds();
        // currentRoundIndex++;
    }

    // // This will keep checking the last three rounds. It works like a camera loop: adding a clip then removing the last. I will need 
    // // to check and see if that is what I want when to comes to inerpreting these triggers as the same (missed all poratls)
    // // would be counted at least twice! 
    // private void CheckPortalStatusForPreviousRounds()
    // {
    //     PortalGazeRoundSummary thisRoundSummary = new PortalGazeRoundSummary
    //     {
    //         RoundIndex = currentRoundIndex,
    //         NumberOfPortalsLookAt = currentPortalSlots.Count,
    //         PortalsLookAt = currentPortalIndices
    //     };

    //     // Add this round to the tracking list
    //     recentRoundSummaries.Add(thisRoundSummary);

    //     // keep the list length at the required number of rounds to track
    //     if (recentRoundSummaries.Count > roundsToTrack)
    //     {
    //         recentRoundSummaries.RemoveAt(0);

    //     }

    //     Debug.Log($"The user has looked at a total of {thisRoundSummary.NumberOfPortalsLookAt} portals in this round");

    //     ///////////////////////////////////////////////////////////////////////////
    //     /// Search for patters of missing portal gaze in the last three rounds

    //     if (recentRoundSummaries.Count == roundsToTrack)
    //     {

    //         int totalNumberOfMissedPortals = 0;
    //         int numberOfRoundsWithMissings = 0;

    //         foreach (var roundSummary in recentRoundSummaries)
    //         {
    //             if (!roundSummary.AllPortalsLookAt)
    //             {
    //                 totalNumberOfMissedPortals += roundSummary.TotalNumberOfMissed;
    //                 numberOfRoundsWithMissings++;
    //             }
    //         }

    //         // Fire proper triggers based on the found patter. 
    //         if (totalNumberOfMissedPortals == 9)
    //         {
    //             Debug.LogWarning("User missed looking at all portals for the past three rounds");
    //             SendTriggerRequest(TriggerSourceType.Gaze, playerTurnStatus, UserScenarioType.MissedAllPortalsFor3Rounds, "Missed all portals in the past 3 rounds.");

    //         }
    //         else if (totalNumberOfMissedPortals < 9 || totalNumberOfMissedPortals > 3 || numberOfRoundsWithMissings == 3)
    //         {
    //             Debug.Log($"User missed at least one portal per round in the past three rounds. Total of missed portals is :{totalNumberOfMissedPortals}");
    //             SendTriggerRequest(TriggerSourceType.Gaze, playerTurnStatus, UserScenarioType.MissedAtLeastOnePortal, "Missed at least on portal per round in the past 3 rounds.");
    //         }
    //         else if (totalNumberOfMissedPortals < 9 || totalNumberOfMissedPortals > 3 || numberOfRoundsWithMissings < 3)
    //         {
    //             Debug.Log("User missed a few portals but not in every round");
    //         }
    //     }


    //     // Reset current round tracking
    //     currentPortalSlots.Clear();
    //     currentPortalIndices.Clear();

    // }

    //#endregion


    #region Helper Methods

    /// <summary>
    /// Not part of the spatial Status tracking system.
    /// This method is used to track the time player spends in optimal position.
    /// It can be used by other methods or by logger. 
    /// </summary>
    /// <param name="playerSpatialState"></param>
    private void TrackOptimalPositionTimeRecord(PlayerSpatialState playerSpatialState)
    {
        if (playerSpatialState == PlayerSpatialState.InZone_FacingPortals)
        {
            if (trackOptimalPositionTimeRecord == null)
            {
                trackOptimalPositionTimeRecord = StartCoroutine(StartTrackingConsecutiveInOptimalTime());
            }
        }
        else
        {
            if (trackOptimalPositionTimeRecord != null)
            {
                StopCoroutine(trackOptimalPositionTimeRecord);
                trackOptimalPositionTimeRecord = null;
            }
            timeInOptimalPositionRecord = 0;

        }
    }

    private IEnumerator StartTrackingConsecutiveInOptimalTime()
    {
        while (timeInOptimalPositionRecord < 5000) // Arbitrary large number but I need to think of when to stop this coroutine
        {
            timeInOptimalPositionRecord += Time.deltaTime;
            yield return null;
        }

        timeInOptimalPositionRecord = 0; // Reset the timer
    }


    public TaskContextType DetermineCurrentTaskContext()
    {
        {

            var status = GetTurnStatus();
            Debug.Log($"[Observer] Determining context from TurnStatus: {status}");

            if (status == PlayerTurnStatus.Human_Turn_Waiting)
                return TaskContextType.ListeningToInstruction;

            if (status == PlayerTurnStatus.Human_Turn_Started)
                return TaskContextType.WaitingForSelection;

            if (status == PlayerTurnStatus.Human_Made_Choice)
                return TaskContextType.ReturningToBase;

            if (status == PlayerTurnStatus.Human_Turn_Ended)
                return TaskContextType.FreeExploration;

            return TaskContextType.Unknown;
        }
    }

    #endregion

       


}
