using System;
using UnityEngine;

/// <summary>
/// Coordinates spatial, application-event, and intervention logging for one session.
/// The start operation rolls back if any configured logger fails to initialize.
/// </summary>
public class LoggingManager : MonoBehaviour
{
    [Header("Logger references")]
    public RobustDataLogger_MetaXR spatialLogger;
    public EventLogger eventLogger;
    public InterventionLogger interventionLogger;

    [Header("Status")]
    public bool isLoggingActive;
    public bool IsLoggingActive => isLoggingActive;
    public Action OnLoggingStarted;
    public Action OnLoggingStopped;

    private void OnEnable()
    {
        if (CentralEventSystem.Instance == null) return;
        CentralEventSystem.Instance.OnTrainingHasStarted += StartAllLogging;
        CentralEventSystem.Instance.OnGameSequenceWasStarted += StartAllLogging;
    }

    private void OnDisable()
    {
        if (CentralEventSystem.Instance != null)
        {
            CentralEventSystem.Instance.OnTrainingHasStarted -= StartAllLogging;
            CentralEventSystem.Instance.OnGameSequenceWasStarted -= StartAllLogging;
        }
        StopAllLogging();
    }

#if UNITY_EDITOR
    [ContextMenu("Start logging")]
    public void StartLoggingFromEditor() => StartAllLogging();
#endif

    /// <summary>Starts all streams, or stops the ones already started if initialization fails.</summary>
    public void StartAllLogging()
    {
        if (isLoggingActive) return;
        if (spatialLogger == null || eventLogger == null || interventionLogger == null)
        {
            Debug.LogError("LoggingManager requires all three logger references.");
            return;
        }

        bool spatialStarted = spatialLogger.TryStartLogging();
        bool eventStarted = spatialStarted && eventLogger.TryStartLogging();
        bool interventionStarted = eventStarted && interventionLogger.TryStartLogging();
        if (!interventionStarted)
        {
            // Do not report an active session when one CSV stream did not start.
            spatialLogger.StopLogging();
            eventLogger.StopLogging();
            interventionLogger.StopLogging();
            Debug.LogError("Logging start failed; all started streams were stopped.");
            return;
        }
        isLoggingActive = true;
        OnLoggingStarted?.Invoke();
    }

    /// <summary>Stops every stream and completes any outstanding file flush.</summary>
    public void StopAllLogging()
    {
        if (!isLoggingActive) return;
        if (spatialLogger != null) spatialLogger.StopLogging();
        if (eventLogger != null) eventLogger.StopLogging();
        if (interventionLogger != null) interventionLogger.StopLogging();
        isLoggingActive = false;
        OnLoggingStopped?.Invoke();
    }

    public void RemoteStartLogging() => StartAllLogging();
}
