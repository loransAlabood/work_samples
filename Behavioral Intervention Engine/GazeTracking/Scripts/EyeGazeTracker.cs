using UnityEngine;

/// <summary>Exposes a combined eye-gaze ray and tracking quality from the Meta SDK.</summary>
/// <remarks>GazeManager decides whether the reported data is usable for interaction.
/// Position and direction are derived from the two eye transforms in world space.</remarks>
public class EyeGazeTracker : MonoBehaviour
{
    [SerializeField] private OVREyeGaze leftEye;
    [SerializeField] private OVREyeGaze rightEye;

    public bool IsOVREyeTrackingAvailable => OVRPlugin.eyeTrackingEnabled;
    public bool HasEyeTracking => leftEye != null && rightEye != null &&
                                  leftEye.EyeTrackingEnabled && rightEye.EyeTrackingEnabled;
    /// <summary>Mean confidence reported by the two eyes; zero when binocular tracking is unavailable.</summary>
    public float Confidence => HasEyeTracking ? (leftEye.Confidence + rightEye.Confidence) * 0.5f : 0f;
    /// <summary>Normalized average of the two eye-forward vectors in world space.</summary>
    public Vector3 AverageDirection => HasEyeTracking
        ? (leftEye.transform.forward + rightEye.transform.forward).normalized : transform.forward;
    /// <summary>Midpoint between tracked eye positions in world space.</summary>
    public Vector3 EyeOrigin => HasEyeTracking
        ? (leftEye.transform.position + rightEye.transform.position) * 0.5f : transform.position;

    /// <summary>Checks SDK availability, both eye trackers, and the required confidence threshold.</summary>
    public bool IsUsable(float minimumConfidence) =>
        IsOVREyeTrackingAvailable && HasEyeTracking && Confidence >= minimumConfidence;

    // Kept for existing application integrations; GazeManager owns the threshold used for source selection.
    public bool UsableEyeTrackingIsAvailable => IsUsable(0.6f);

    /// <summary>Requests eye tracking from the SDK; source selection is updated by GazeManager.</summary>
    public bool TryStartEyeTracking()
    {
        bool started = OVRPlugin.StartEyeTracking();
        if (started && IsOVREyeTrackingAvailable)
            Debug.Log("Eye tracking started.");
        else
            Debug.LogWarning("Eye tracking unavailable.");
        // GazeManager publishes the source-change event on its next update.
        return started && IsOVREyeTrackingAvailable;
    }

    public void ForceRecheck()
    {
        if (!IsOVREyeTrackingAvailable)
            TryStartEyeTracking();
    }
}
