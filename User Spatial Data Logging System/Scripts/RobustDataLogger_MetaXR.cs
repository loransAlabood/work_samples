using System;
using UnityEngine;

/// <summary>
/// Samples head, hand, and gaze transforms during an XR session and writes one row per
/// tracked object. The requested sampling frequency is limited by Unity's frame rate.
/// </summary>
public class RobustDataLogger_MetaXR : MonoBehaviour
{
    [Header("Tracked Objects")]
    public Transform centerEye;
    public Transform leftHandRoot;
    public Transform rightHandRoot;
    public Transform leftEyeGaze;
    public Transform rightEyeGaze;

    [Header("Logging Parameters")]
    [Min(1)] public int logFrequencyInFPS = 30;
    [Min(0.1f)] public float writeFrequencyInSeconds = 1f;

    private Transform leftIndexTip, leftThumbTip, rightIndexTip, rightThumbTip;
    private string currentGazeSource = "Unknown";
    private float eyeConfidence = -1f;
    private float nextSampleTime, nextWriteTime;
    private BufferedCsvWriter writer;

    public bool IsLoggingActive { get; private set; }
    public int PendingRows => writer == null ? 0 : writer.PendingCount;

    private void OnEnable()
    {
        if (CentralEventSystem.Instance != null)
            CentralEventSystem.Instance.OnGazeSourceChanged += UpdateGazeSource;
    }

    private void OnDisable()
    {
        if (CentralEventSystem.Instance != null)
            CentralEventSystem.Instance.OnGazeSourceChanged -= UpdateGazeSource;
        StopLogging();
    }

    private void UpdateGazeSource(GazeSource source, float confidence)
    {
        currentGazeSource = source == GazeSource.Eye ? "Eye Gaze" :
            source == GazeSource.Head ? "Head Gaze" : "Unknown";
        eyeConfidence = source == GazeSource.Eye ? confidence : -1f;
    }

    private void Update()
    {
        if (!IsLoggingActive) return;
        if (Time.unscaledTime >= nextSampleTime)
        {
            ResolveJointReferencesIfNeeded();
            LogTrackedObjects();
            nextSampleTime = Time.unscaledTime + 1f / Mathf.Max(1, logFrequencyInFPS);
        }
        if (Time.unscaledTime >= nextWriteTime)
        {
            writer.RequestFlush();
            nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
        }
    }

    /// <summary>Starts a new output file. Returns false if file creation fails.</summary>
    public bool TryStartLogging()
    {
        if (IsLoggingActive) return true;
        if (writer != null && writer.PendingCount > 0)
        {
            Debug.LogError("Previous session has unflushed records; retry FlushBuffer before starting another file.");
            return false;
        }
        try
        {
            writer = new BufferedCsvWriter(
                System.IO.Path.Combine(Application.persistentDataPath,
                    "UserData_MetaXR_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".csv"),
                "Time,Obj,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,EyeConfidence,GazeSource");
            nextSampleTime = Time.unscaledTime;
            nextWriteTime = Time.unscaledTime + Mathf.Max(0.1f, writeFrequencyInSeconds);
            IsLoggingActive = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Spatial logger could not open an output file: " + ex.Message);
            return false;
        }
    }

    // Retained for existing Unity events and other callers.
    public void StartLogging() => TryStartLogging();

    /// <summary>Stops sampling and attempts to commit all buffered records.</summary>
    public void StopLogging()
    {
        if (!IsLoggingActive) return;
        IsLoggingActive = false;
        FlushBuffer();
    }

    private void OnApplicationQuit() => FlushBuffer();

    /// <summary>Synchronizes with any in-flight background write before returning.</summary>
    public void FlushBuffer()
    {
        if (writer != null && !writer.Flush())
            Debug.LogError("Spatial data flush failed; " + writer.PendingCount + " records remain in memory: " + writer.LastError);
    }

    private void ResolveJointReferencesIfNeeded()
    {
        if (leftIndexTip == null) leftIndexTip = FindDeepChild(leftHandRoot, "Hand_IndexTip");
        if (leftThumbTip == null) leftThumbTip = FindDeepChild(leftHandRoot, "Hand_ThumbTip");
        if (rightIndexTip == null) rightIndexTip = FindDeepChild(rightHandRoot, "Hand_IndexTip");
        if (rightThumbTip == null) rightThumbTip = FindDeepChild(rightHandRoot, "Hand_ThumbTip");
    }

    private void LogTrackedObjects()
    {
        // All rows in this sample share one capture timestamp, regardless of disk-write time.
        string stamp = CsvFormat.Timestamp(DateTime.UtcNow);
        LogTransform(centerEye, "CenterEye", stamp);
        LogTransform(leftHandRoot, "LeftHandRoot", stamp);
        LogTransform(leftIndexTip, "LeftIndexTip", stamp);
        LogTransform(leftThumbTip, "LeftThumbTip", stamp);
        LogTransform(rightHandRoot, "RightHandRoot", stamp);
        LogTransform(rightIndexTip, "RightIndexTip", stamp);
        LogTransform(rightThumbTip, "RightThumbTip", stamp);
        LogTransform(leftEyeGaze, "LeftEyeGaze", stamp, eyeConfidence);
        LogTransform(rightEyeGaze, "RightEyeGaze", stamp, eyeConfidence);
    }

    private void LogTransform(Transform tracked, string name, string stamp, float confidence = -1f)
    {
        string[] fields = { stamp, name, "null", "null", "null", "null", "null", "null", "null",
            CsvFormat.Number(confidence, "F2"), currentGazeSource };
        if (tracked != null)
        {
            Vector3 pos = tracked.position;
            Quaternion rot = tracked.rotation;
            fields[2] = CsvFormat.Number(pos.x); fields[3] = CsvFormat.Number(pos.y);
            fields[4] = CsvFormat.Number(pos.z); fields[5] = CsvFormat.Number(rot.x);
            fields[6] = CsvFormat.Number(rot.y); fields[7] = CsvFormat.Number(rot.z);
            fields[8] = CsvFormat.Number(rot.w);
        }
        writer.Enqueue(CsvFormat.Row(fields));
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == targetName) return child;
        return null;
    }
}
