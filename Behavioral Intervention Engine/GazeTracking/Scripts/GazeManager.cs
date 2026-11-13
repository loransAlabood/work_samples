using UnityEngine;

public enum GazeSource { Eye, Head, None }
public enum GazeVisualizationMode { EyeOnly, HeadOnly, EyeAndHead, None }

/// <summary>Selects eye or head gaze as the active interaction source.</summary>
/// <remarks>Head gaze is used when binocular eye tracking is unavailable or below the
/// configured confidence threshold. This class also coordinates optional debug visualization.</remarks>
[RequireComponent(typeof(EyeGazeTracker), typeof(HeadGazeTracker))]
public class GazeManager : MonoBehaviour
{
    // Use one threshold for source selection; downstream trackers do not choose independently.
    [SerializeField, Range(0f, 1f)] private float eyeConfidenceThreshold = 0.6f;
    private EyeGazeTracker eyeTracker;
    private GazeDataVisualizer visualizer;
    private GazeSource currentSource = GazeSource.None;
    private GazeVisualizationMode visualizationMode = GazeVisualizationMode.None;

    public HeadGazeTracker HeadGazeTracker { get; private set; }
    public GazeInteractionTracker GazeInteractionTracker { get; private set; }
    /// <summary>Source currently authorized to drive interaction events.</summary>
    public GazeSource CurrentSource => currentSource;
    public float EyeConfidenceThreshold => eyeConfidenceThreshold;
    /// <summary>Checks readiness using the same threshold as source selection.</summary>
    public bool IsEyeTrackingReady() => eyeTracker != null && eyeTracker.IsUsable(eyeConfidenceThreshold);

    private void Awake()
    {
        eyeTracker = GetComponent<EyeGazeTracker>();
        HeadGazeTracker = GetComponent<HeadGazeTracker>();
        GazeInteractionTracker = GetComponent<GazeInteractionTracker>();
        visualizer = GetComponent<GazeDataVisualizer>();
    }

    private void Start() => RefreshSource();

    // Polling is required here: this SDK integration has no reliable source-availability event.
    private void Update() => RefreshSource();

    // Publish a source-change notification only on transitions, not every frame.
    private void RefreshSource()
    {
        GazeSource selected = IsEyeTrackingReady() ? GazeSource.Eye : GazeSource.Head;
        if (selected != currentSource)
        {
            currentSource = selected;
            CentralEventSystem.Instance?.InvokeOnGazeSourceChanged(
                selected, selected == GazeSource.Eye ? eyeTracker.Confidence : 1f);
        }
        visualizer?.SetMode(visualizationMode, IsEyeTrackingReady());
    }

    [ContextMenu("Force Eye Recheck")]
    public void ReCheckEyeTracking() => eyeTracker.TryStartEyeTracking();

    // Visualization requests are independent of which source drives actual interaction.
    private void SetVisualizationMode(GazeVisualizationMode mode)
    {
        visualizationMode = mode;
        visualizer?.SetMode(mode, IsEyeTrackingReady());

        // Preserve notifications for other components listening to the central event system.
        if (CentralEventSystem.Instance == null) return;
        if (mode == GazeVisualizationMode.None)
        {
            CentralEventSystem.Instance.InvokeOnStopVisualizingGazeData();
            return;
        }
        bool includeEye = IsEyeTrackingReady() && (mode == GazeVisualizationMode.EyeOnly || mode == GazeVisualizationMode.EyeAndHead);
        bool includeHead = mode == GazeVisualizationMode.HeadOnly || mode == GazeVisualizationMode.EyeAndHead;
        if (includeEye)
            CentralEventSystem.Instance.InvokeOnStartVisualizingGazeData(GazeSource.Eye, eyeTracker.Confidence);
        if (includeHead)
            CentralEventSystem.Instance.InvokeOnStartVisualizingGazeData(GazeSource.Head, 1f);
    }

    public void StartEyeAndHeadVisualization() => SetVisualizationMode(GazeVisualizationMode.EyeAndHead);
    public void StartEyeVisualization() => SetVisualizationMode(GazeVisualizationMode.EyeOnly);
    public void StartHeadVisualization() => SetVisualizationMode(GazeVisualizationMode.HeadOnly);
    public void StopGazeVisualization() => SetVisualizationMode(GazeVisualizationMode.None);
}
