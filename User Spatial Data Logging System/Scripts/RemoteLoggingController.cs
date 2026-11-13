using System;
using UnityEngine;

/// <summary>Entry point for session logging commands received from the remote dashboard.</summary>
public class RemoteLoggingController : MonoBehaviour
{
    public LoggingManager loggingManager;
    public Action OnRemoteStartRequested;
    public Action OnRemoteStopRequested;

    /// <summary>Requests a logging start; repeated start commands are ignored.</summary>
    public void RemoteStartLogging()
    {
        if (loggingManager == null) { Debug.LogError("LoggingManager is missing."); return; }
        if (loggingManager.IsLoggingActive) return;
        loggingManager.StartAllLogging();
        if (loggingManager.IsLoggingActive) OnRemoteStartRequested?.Invoke();
    }

    /// <summary>Requests a logging stop; repeated stop commands are ignored.</summary>
    public void RemoteStopLogging()
    {
        if (loggingManager == null) { Debug.LogError("LoggingManager is missing."); return; }
        if (!loggingManager.IsLoggingActive) return;
        loggingManager.StopAllLogging();
        OnRemoteStopRequested?.Invoke();
    }
}
