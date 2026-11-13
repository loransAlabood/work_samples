using UnityEngine;

/// <summary>Collects head-gaze hits while head tracking owns interaction.</summary>
/// <remarks>Uses the center-eye anchor as a head-forward proxy. Filtering and events
/// are handled by GazeInteractionTracker, not by this sampling component.</remarks>
[RequireComponent(typeof(GazeInteractionTracker), typeof(HeadGazeTracker))]
public class HeadGazeInteractionTracker : MonoBehaviour
{
    private GazeInteractionTracker interactionTracker;
    private HeadGazeTracker headTracker;
    private GazeManager gazeManager;

    private void Awake()
    {
        interactionTracker = GetComponent<GazeInteractionTracker>();
        headTracker = GetComponent<HeadGazeTracker>();
        gazeManager = GetComponent<GazeManager>();
    }

    private void Update()
    {
        // Eye and head sampling components must not submit competing interaction hits.
        if (gazeManager.CurrentSource != GazeSource.Head) return;
        RaycastUtils.PerformRaycast(headTracker.HeadOrigin, headTracker.HeadDirection,
            out bool hitAvatar, out Portal portal);
        interactionTracker.ProcessHeadGazeInteraction(portal, hitAvatar, false);
    }
}
