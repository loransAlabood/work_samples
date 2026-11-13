using UnityEngine;

/// <summary>Draws eye and head gaze rays to inspect tracking alignment during development.</summary>
/// <remarks>Visualization is optional and does not contribute to interaction detection or logging.
/// GazeManager supplies both the requested display mode and eye-tracking readiness.</remarks>
public class GazeDataVisualizer : MonoBehaviour
{
    [SerializeField] private float gazeLength = 10f;
    private EyeGazeTracker eyeTracker;
    private HeadGazeTracker headTracker;
    private LineRenderer eyeLine;
    private LineRenderer headLine;
    private GazeVisualizationMode mode = GazeVisualizationMode.None;
    private bool eyeAvailable;

    private void Awake()
    {
        eyeTracker = GetComponent<EyeGazeTracker>();
        headTracker = GetComponent<HeadGazeTracker>();
        eyeLine = CreateLineRenderer("EyeGazeRay", Color.green);
        headLine = CreateLineRenderer("HeadGazeRay", Color.cyan);
        SetMode(GazeVisualizationMode.None, false);
    }

    /// <summary>Applies the requested debug view, hiding the eye ray when eye tracking is unusable.</summary>
    /// <param name="requestedMode">Which sensor rays the operator wants to see.</param>
    /// <param name="usableEye">Whether the eye source currently satisfies the tracking-quality check.</param>
    public void SetMode(GazeVisualizationMode requestedMode, bool usableEye)
    {
        mode = requestedMode;
        eyeAvailable = usableEye;
        if (eyeLine != null)
            eyeLine.enabled = eyeAvailable && (mode == GazeVisualizationMode.EyeOnly || mode == GazeVisualizationMode.EyeAndHead);
        if (headLine != null)
            headLine.enabled = mode == GazeVisualizationMode.HeadOnly || mode == GazeVisualizationMode.EyeAndHead;
    }

    // Draw after frame updates so the rays follow the latest headset and eye poses.
    private void LateUpdate()
    {
        if (eyeLine != null && eyeLine.enabled)
        {
            Vector3 direction = eyeTracker.AverageDirection;
            Vector3 origin = eyeTracker.EyeOrigin + direction * 0.2f + transform.up * 0.05f + transform.right * 0.02f;
            eyeLine.SetPosition(0, origin);
            eyeLine.SetPosition(1, origin + direction * gazeLength);
        }
        if (headLine != null && headLine.enabled)
        {
            Vector3 direction = headTracker.HeadDirection;
            Vector3 origin = headTracker.HeadOrigin + Vector3.up * 0.05f + direction * 0.15f;
            headLine.SetPosition(0, origin);
            headLine.SetPosition(1, origin + direction * gazeLength);
        }
    }

    // Each line owns a material instance, released in OnDestroy.
    private LineRenderer CreateLineRenderer(string lineName, Color color)
    {
        GameObject obj = new GameObject(lineName);
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.positionCount = 2;
        line.startWidth = 0.02f;
        line.endWidth = 0.01f;
        line.startColor = color;
        line.endColor = color;
        line.enabled = false;
        return line;
    }

    private void OnDestroy()
    {
        // The line objects own their materials; Unity does not dispose instantiated materials automatically.
        if (eyeLine != null) Destroy(eyeLine.material);
        if (headLine != null) Destroy(headLine.material);
    }
}
