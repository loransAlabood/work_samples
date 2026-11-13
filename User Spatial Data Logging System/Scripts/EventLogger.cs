using System;
using UnityEngine;

/// <summary>
/// Captures application events during a logging session. Subscriptions are installed once
/// and held while this component lives; recording is gated by IsLoggingActive.
/// </summary>
public class EventLogger : MonoBehaviour
{
    [Header("Logging Parameters")]
    [Min(0.1f)] public float writeFrequencyInSeconds = 1f;
    private float nextWriteTime;
    private BufferedCsvWriter writer;
    private bool subscribed;
    public bool IsLoggingActive { get; private set; }
    public int PendingRows => writer == null ? 0 : writer.PendingCount;
    public Action OnLoggingStarted;
    public Action OnLoggingStopped;

    private void Update()
    {
        if (IsLoggingActive && Time.unscaledTime >= nextWriteTime)
        {
            writer.RequestFlush();
            nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
        }
    }

    /// <summary>Creates a new event file and activates event capture.</summary>
    public bool TryStartLogging()
    {
        if (IsLoggingActive) return true;
        if (writer != null && writer.PendingCount > 0)
        {
            Debug.LogError("Previous session has unflushed records; retry FlushBuffer before starting another file.");
            return false;
        }
        if (CentralEventSystem.Instance == null)
        {
            Debug.LogError("Event logger requires CentralEventSystem.");
            return false;
        }
        try
        {
            writer = new BufferedCsvWriter(
                System.IO.Path.Combine(Application.persistentDataPath,
                    "EventLog_MetaXR_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".csv"),
                "Time,Event,Arguments");
        }
        catch (Exception ex)
        {
            Debug.LogError("Event logger could not open an output file: " + ex.Message);
            return false;
        }
        // Subscribe once to prevent a second start/stop cycle from duplicating callbacks.
        // See README: these subscriptions assume the event system shares this scene's lifetime.
        if (!subscribed) { SubscribeToEvents(); subscribed = true; }
        nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
        IsLoggingActive = true;
        OnLoggingStarted?.Invoke();
        return true;
    }

    public void StartLogging() => TryStartLogging();

    /// <summary>Stops accepting records and waits for any current file write to finish.</summary>
    public void StopLogging()
    {
        if (!IsLoggingActive) return;
        IsLoggingActive = false;
        FlushBuffer();
        OnLoggingStopped?.Invoke();
    }

    private void OnDisable() => StopLogging();
    private void OnApplicationQuit() => FlushBuffer();

    public void FlushBuffer()
    {
        if (writer != null && !writer.Flush())
            Debug.LogError("Event log flush failed; " + writer.PendingCount + " records remain: " + writer.LastError);
    }

    private void SubscribeToEvents()
    {
        var es = CentralEventSystem.Instance;
        es.OnGameSequenceWasStarted += () => Log("OnGameSequenceWasStarted");
        es.OnHumanPlayerChoiceSelected += (index) => Log("OnHumanPlayerChoiceSelected", index.ToString());
        es.OnPlaybackStarted += () => Log("OnPlaybackStarted");
        es.OnPlaybackFinished += () => Log("OnPlaybackFinished");
        es.OnChoiceRead += (index, text) => Log("OnChoiceRead", $"Index:{index},Text:{text}");
        es.OnStoryIsReady += () => Log("OnStoryIsReady");
        es.OnCheckpointChanged += (index) => Log("OnCheckpointChanged", index.ToString());
        es.OnAIScoreChanged += (s1, s2, turn, rand, varPerc, maxRand) => Log("OnAIScoreChanged", $"S1:{s1},S2:{s2},Turn:{turn},RandUsed:{rand},Var%:{varPerc:F2},MaxRand:{maxRand}");
        es.OnAIChoiceMade += (index) => Log("OnAIChoiceMade", index.ToString());
        es.OnAvatarIsMoving += () => Log("OnAvatarIsMoving");
        es.OnAvatarFinishedMovement += () => Log("OnAvatarFinishedMovement");
        es.OnTrainingModeIsReady += () => Log("OnTrainingModeIsReady");
        es.OnTrainingHasStarted += () => Log("OnTrainingHasStarted");
        es.OnTrainingIsCompleted += () => Log("OnTrainingIsCompleted");
        es.OnTrainingStepTypeChanged += (type) => Log("OnTrainingStepTypeChanged", type.ToString());
        es.OnPlayerTurnStatusChanged += (status) => Log("OnPlayerTurnStatusChanged", status.ToString());
        es.OnPlayerEnteredZone += () => Log("OnPlayerEnteredZone");
        es.OnPlayerExitedZone += () => Log("OnPlayerExitedZone");
        es.OnPlayerSpatialStatusChanged += (state) => Log("OnPlayerSpatialStatusChanged", state.ToString());
        es.OnGazeSourceChanged += (src, conf) => Log("OnGazeSourceChanged", $"Source:{src},Confidence:{conf:F2}");
        es.OnStartVisualizingGazeData += (src, conf) => Log("OnStartVisualizingGazeData", $"Source:{src},Confidence:{conf:F2}");
        es.OnStopVisualizingGazeData += () => Log("OnStopVisualizingGazeData");
        es.OnAvatarLookStarted += (src) => Log("OnAvatarLookStarted", src.ToString());
        es.OnAvatarLookEnded += (src) => Log("OnAvatarLookEnded", src.ToString());
        es.OnPortalLookStarted += (data) => Log("OnPortalLookStarted", data.ToString());
        es.OnPortalLookEnded += (data) => Log("OnPortalLookEnded", data.ToString());
        es.OnPlayerStateChanged += () => Log("OnPlayerStateChanged");
        es.OnPortalSpawned += (data) => Log("OnPortalSpawned", data.ToString());
        es.OnPortalsCleared += () => Log("OnPortalsCleared");
        es.OnVoiceModelSelected += (model, id) => Log("OnVoiceModelSelected", $"Model:{model},ID:{id}");
    }

    private void Log(string eventName, string args = "")
    {
        if (!IsLoggingActive || writer == null) return;
        writer.Enqueue(CsvFormat.Row(CsvFormat.Timestamp(DateTime.UtcNow), eventName, args));
    }
}
