using UnityEngine;

/// <summary>Collects eye-gaze hits for the currently selected interaction source.</summary>
/// <remarks>Only performs spatial sampling. Debouncing and event emission belong to GazeInteractionTracker.</remarks>
[RequireComponent(typeof(GazeInteractionTracker), typeof(EyeGazeTracker))]
public class EyeGazeInteractionTracker : MonoBehaviour
{
    private GazeInteractionTracker interactionTracker;
    private EyeGazeTracker eyeTracker;
    private GazeManager gazeManager;

    private void Awake()
    {
        interactionTracker = GetComponent<GazeInteractionTracker>();
        eyeTracker = GetComponent<EyeGazeTracker>();
        gazeManager = GetComponent<GazeManager>();
    }

    private void Update()
    {
        // Ignore this sensor while head gaze owns interaction; avoids duplicate event streams.
        if (gazeManager.CurrentSource != GazeSource.Eye) return;
        RaycastUtils.PerformRaycast(eyeTracker.EyeOrigin, eyeTracker.AverageDirection,
            out bool hitAvatar, out Portal portal);
        interactionTracker.ProcessEyeGazeInteraction(portal, hitAvatar, true);
    }
}
