using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Firebase;
using Firebase.Database;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Captures hand/head and task-object poses plus discrete grab/release/snap events.
/// A recording chooses either local CSV files or Firebase Realtime Database; this component
/// is a slice of a larger XR spelling application and depends on its SessionTracker.
/// </summary>
public class Logger : MonoBehaviour
{
    public Transform CenterEye, LeftHandOVR, RightHandOVR;

    public static bool ShouldWriteToFile { get; private set; }
    public static bool ShouldWriteToCloud { get; private set; }
    public static List<Transform> lettersAndBases;

    [Header("Pose sampling")]
    [Min(0.1f)] public float frequency = 10f;

    private Transform leftIndexTip, leftThumbTip, rightIndexTip, rightThumbTip;
    private float elapsedSinceSample;
    private static bool isTracking;
    private static string bodyPosesPath, goPosesPath, eventFilePath;
    private static string cloudSessionKey;
    private static DatabaseReference reference;
    private static SessionTracker sessionTracker;
    private static bool firebaseReady;
    private static Logger instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one Logger instance may control a recording in this scene.");
            enabled = false;
            return;
        }
        instance = this;
        sessionTracker = FindAnyObjectByType<SessionTracker>();
    }

    private async void Start()
    {
        await InitializeFirebaseAsync();
    }

    private async Task InitializeFirebaseAsync()
    {
        try
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogWarning($"Firebase unavailable: {status}. Local logging remains available.");
                return;
            }
            reference = FirebaseDatabase.DefaultInstance.RootReference;
            firebaseReady = reference != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Firebase initialization failed: {ex.Message}");
        }
    }

    private void FixedUpdate()
    {
        if (!isTracking) return;
        // Sample rate follows elapsed fixed-timestep time, not an assumed 50 Hz physics rate.
        elapsedSinceSample += Time.fixedDeltaTime;
        float period = 1f / Mathf.Max(0.1f, frequency);
        if (elapsedSinceSample < period) return;
        elapsedSinceSample %= period;
        LogPoses();
    }

    /// <summary>Starts a cloud-only recording if session context and Firebase are ready.</summary>
    public static void StartTrackingOnCloud()
    {
        if (!CanRecord() || isTracking) return;
        if (!firebaseReady || reference == null)
        {
            Debug.LogWarning("Cloud recording requested before Firebase is ready.");
            return;
        }
        StartRecording(false);
    }

    /// <summary>Starts a local-only recording under Application.persistentDataPath.</summary>
    public static void StartTrackingOnFile()
    {
        if (!CanRecord() || isTracking) return;
        StartRecording(true);
    }

    private static bool CanRecord()
    {
        if (instance == null || sessionTracker == null)
        {
            Debug.LogWarning("Logger and SessionTracker must be initialized before recording.");
            return false;
        }
        string user = sessionTracker.GetUserID();
        return !string.IsNullOrWhiteSpace(user) && user != "Researcher";
    }

    private static void StartRecording(bool local)
    {
        string key = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string basePath = Path.Combine(Application.persistentDataPath, "TrackData-" + key);
        if (local)
        {
            try
            {
                Directory.CreateDirectory(basePath);
                bodyPosesPath = Path.Combine(basePath, "bodyPoses.csv");
                goPosesPath = Path.Combine(basePath, "goPoses.csv");
                eventFilePath = Path.Combine(basePath, "events.csv");
                File.WriteAllText(bodyPosesPath, "timestamp,Poses\n");
                File.WriteAllText(goPosesPath, "timestamp,Poses\n");
                File.WriteAllText(eventFilePath, "timestamp,event\n");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not create recording files: {ex.Message}");
                return;
            }
        }
        // A unique recording key prevents successive sessions from sharing the same cloud folder.
        cloudSessionKey = key;
        ShouldWriteToFile = local;
        ShouldWriteToCloud = !local;
        instance.elapsedSinceSample = 0f;
        isTracking = true;
        Debug.Log($"Recording started ({(local ? "local" : "cloud")}, session {key}).");
    }

    /// <summary>Stops acquiring samples. Outstanding cloud writes are not awaited by this method.</summary>
    public static void StopTracking()
    {
        if (!isTracking) return;
        AppendLog("Recording stopped");
        isTracking = false;
        ShouldWriteToFile = false;
        ShouldWriteToCloud = false;
    }

    /// <summary>
    /// Receives labels emitted by the larger project's grab-event handler and attaches
    /// the current spelling target to the interaction record.
    /// </summary>
    public static void LogInteractionEvent(string objectName, string eventType)
    {
        if (!isTracking || sessionTracker == null) return;
        string word = sessionTracker.GetCurrentWord();
        string target = sessionTracker.TryGetCurrentLetter(out char letter) ? letter.ToString() : string.Empty;
        InteractionRecordFormatter.Classify(objectName, eventType, target, out string action, out string correctness);
        var entry = new InteractionRecord
        {
            type = action, objectName = objectName ?? string.Empty,
            timeUtc = DateTime.UtcNow.ToString("o"), timestampMs = GetUnixTimeMilliseconds(),
            phase = sessionTracker.GetCurrentPhase(), word = word,
            targetLetter = target, correctness = correctness
        };
        string json = JsonUtility.ToJson(entry);
        WriteToFile(eventFilePath, entry.timestampMs.ToString(), json);
        _ = WriteToCloudAsync(json, "Events");
    }

    private void LogPoses()
    {
        if (leftIndexTip == null) leftIndexTip = FindDeepChild(LeftHandOVR, "Hand_IndexTip");
        if (leftThumbTip == null) leftThumbTip = FindDeepChild(LeftHandOVR, "Hand_ThumbTip");
        if (rightIndexTip == null) rightIndexTip = FindDeepChild(RightHandOVR, "Hand_IndexTip");
        if (rightThumbTip == null) rightThumbTip = FindDeepChild(RightHandOVR, "Hand_ThumbTip");

        long timestamp = GetUnixTimeMilliseconds();
        string utc = DateTime.UtcNow.ToString("o");
        var body = new PoseFrame
        {
            timestampMs = timestamp, timeUtc = utc,
            poses = new[]
            {
                CapturePose("CenterEye", CenterEye), CapturePose("LeftHandRoot", LeftHandOVR),
                CapturePose("LeftIndexTip", leftIndexTip), CapturePose("LeftThumbTip", leftThumbTip),
                CapturePose("RightHandRoot", RightHandOVR), CapturePose("RightIndexTip", rightIndexTip),
                CapturePose("RightThumbTip", rightThumbTip)
            }
        };
        string bodyJson = JsonUtility.ToJson(body);
        WriteToFile(bodyPosesPath, timestamp.ToString(), bodyJson);
        _ = WriteToCloudAsync(bodyJson, "BodyPoses");

        if (lettersAndBases == null) return;
        var poses = new List<TrackedPose>();
        foreach (Transform item in lettersAndBases)
        {
            if (item != null && item.gameObject.activeSelf)
                poses.Add(CapturePose(item.name, item));
        }
        string objectsJson = JsonUtility.ToJson(new ObjectPoseFrame
        {
            timestampMs = timestamp, timeUtc = utc, objects = poses.ToArray()
        });
        WriteToFile(goPosesPath, timestamp.ToString(), objectsJson);
        _ = WriteToCloudAsync(objectsJson, "goPoses");
    }

    private static TrackedPose CapturePose(string name, Transform transformValue)
    {
        return new TrackedPose
        {
            name = name, available = transformValue != null,
            position = transformValue == null ? Vector3.zero : transformValue.position,
            rotation = transformValue == null ? Quaternion.identity : transformValue.rotation
        };
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        var queue = new Queue<Transform>();
        queue.Enqueue(parent);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current.name == name) return current;
            if (current.name == "BindPoses") continue;
            foreach (Transform child in current) queue.Enqueue(child);
        }
        return null;
    }

    private static void WriteToFile(string path, string timestamp, string payload)
    {
        if (!ShouldWriteToFile || string.IsNullOrEmpty(path)) return;
        try
        {
            string row = InteractionRecordFormatter.EscapeCsv(timestamp) + "," +
                         InteractionRecordFormatter.EscapeCsv(payload) + "\n";
            File.AppendAllText(path, row);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Local recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes serialized JSON to Firebase under a unique recording key. Firebase rules must
    /// restrict access to the intended research clients; this code does not implement auth.
    /// </summary>
    public static void WriteToCloud(string msg, string group)
    {
        _ = WriteToCloudAsync(msg, group);
    }

    private static async Task WriteToCloudAsync(string json, string group)
    {
        if (!ShouldWriteToCloud) return;
        if (!firebaseReady || reference == null || sessionTracker == null) return;
        try
        {
            // Firebase RTDB forbids . # $ [ ] / inside path segments.
            string user = SanitizeFirebaseKey(sessionTracker.GetUserID());
            await reference.Child("sessions").Child(user).Child(cloudSessionKey)
                .Child(group).Push().SetRawJsonValueAsync(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Firebase write failed ({group}): {ex.Message}");
        }
    }

    private static string SanitizeFirebaseKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        foreach (char invalid in new[] {'.', '#', '$', '[', ']', '/'})
            value = value.Replace(invalid, '_');
        return value;
    }

    private static void AppendLogBase(string message, bool write, bool cloud)
    {
        if (!isTracking) return;
        var item = new SessionLogRecord
        {
            timeUtc = DateTime.UtcNow.ToString("o"), timestampMs = GetUnixTimeMilliseconds(),
            message = message
        };
        string json = JsonUtility.ToJson(item);
        if (write) WriteToFile(eventFilePath, item.timestampMs.ToString(), json);
        if (cloud) _ = WriteToCloudAsync(json, "Logs");
    }

    public static void AppendLog(string msg, bool write = true, bool cloud = true)
    {
        Debug.Log(msg);
        AppendLogBase(msg, write, cloud);
    }

    public static void AppendLogWarning(string msg, bool write = true, bool cloud = true)
    {
        Debug.LogWarning(msg);
        AppendLogBase("Warning: " + msg, write, cloud);
    }

    public static void AppendLogError(string msg, bool write = true, bool cloud = true)
    {
        Debug.LogError(msg);
        AppendLogBase("Error: " + msg, write, cloud);
    }

    public static string GetUnixTime() => GetUnixTimeMilliseconds().ToString();
    private static long GetUnixTimeMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [PunRPC] public void StopDataRecording() => StopTracking();
    [PunRPC] public void StartTrackingOnCloudRPC() => StartTrackingOnCloud();
    [PunRPC] public void StartTrackingOnFileRPC() => StartTrackingOnFile();

    public void OnStartTrackingOnCloudRPCPressed() => BroadcastRecordingRpc(nameof(StartTrackingOnCloudRPC));
    public void OnStartTrackingOnFileRPCPressed() => BroadcastRecordingRpc(nameof(StartTrackingOnFileRPC));
    public void OnStopTrackingRPCPressed() => BroadcastRecordingRpc(nameof(StopDataRecording));

    private void BroadcastRecordingRpc(string method)
    {
        PhotonView view = PhotonView.Get(this);
        if (view == null) { Debug.LogError("Missing PhotonView on Logger object."); return; }
        view.RPC(method, RpcTarget.All);
    }

    /// <summary>Optional one-second delayed start used during integration testing.</summary>
    public IEnumerator StartTrackingWithDelay()
    {
        yield return new WaitForSeconds(1f);
        StartTrackingOnFile();
    }

    /// <summary>Compatibility entry point; uses the destination last selected by the caller.</summary>
    public static void StartTracking()
    {
        if (ShouldWriteToCloud) StartTrackingOnCloud();
        else StartTrackingOnFile();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        StopTracking();
        instance = null;
        sessionTracker = null;
    }
}
