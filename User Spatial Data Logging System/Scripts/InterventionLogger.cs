using System;
using UnityEngine;

/// <summary>Records intervention lifecycle notifications as a separate CSV stream.</summary>
public class InterventionLogger : MonoBehaviour
{
    [Header("Logging Parameters")]
    [Min(0.1f)] public float writeFrequencyInSeconds = 1f;
    public InterventionManager interventionManager;
    private float nextWriteTime;
    private BufferedCsvWriter writer;
    public bool IsLoggingActive { get; private set; }
    public int PendingRows => writer == null ? 0 : writer.PendingCount;

    private void Update()
    {
        if (IsLoggingActive && Time.unscaledTime >= nextWriteTime)
        {
            writer.RequestFlush();
            nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
        }
    }

    /// <summary>Opens a new file and subscribes to lifecycle notifications.</summary>
    public bool TryStartLogging()
    {
        if (IsLoggingActive) return true;
        if (writer != null && writer.PendingCount > 0)
        {
            Debug.LogError("Previous session has unflushed records; retry FlushBuffer before starting another file.");
            return false;
        }
        if (interventionManager == null)
        {
            Debug.LogError("Intervention logger requires InterventionManager.");
            return false;
        }
        try
        {
            writer = new BufferedCsvWriter(
                System.IO.Path.Combine(Application.persistentDataPath,
                    "InterventionLog_MetaXR_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".csv"),
                "Timestamp,Scenario,EscalationCount,Stage,Outcome,Action,Comments");
        }
        catch (Exception ex)
        {
            Debug.LogError("Intervention logger could not open an output file: " + ex.Message);
            return false;
        }
        interventionManager.OnInterventionLifecycleEvent += HandleInterventionLifecycleEvent;
        nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
        IsLoggingActive = true;
        return true;
    }

    public void StartLogging() => TryStartLogging();

    /// <summary>Unsubscribes first so no new records arrive while flushing the final batch.</summary>
    public void StopLogging()
    {
        if (!IsLoggingActive) return;
        IsLoggingActive = false;
        if (interventionManager != null)
            interventionManager.OnInterventionLifecycleEvent -= HandleInterventionLifecycleEvent;
        FlushBuffer();
    }

    private void OnDisable() => StopLogging();
    private void OnApplicationQuit() => FlushBuffer();

    public void FlushBuffer()
    {
        if (writer != null && !writer.Flush())
            Debug.LogError("Intervention log flush failed; " + writer.PendingCount + " records remain: " + writer.LastError);
    }

    private void HandleInterventionLifecycleEvent(InterventionLifecycleEvent e)
    {
        if (!IsLoggingActive || writer == null || e == null) return;
        writer.Enqueue(CsvFormat.Row(
            CsvFormat.Timestamp(e.Timestamp), e.Scenario.ToString(), e.EscalationCount.ToString(),
            e.Stage.ToString(), e.Outcome.ToString(), e.Action, e.Comments));
    }
}
