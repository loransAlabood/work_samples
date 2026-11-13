using UnityEngine;

/// <summary>Provides a head-forward ray when eye tracking cannot drive interactions.</summary>
/// <remarks>The center-eye anchor is assigned in the inspector; its world-space
/// position and forward vector are sampled by the head-gaze interaction component.</remarks>
public class HeadGazeTracker : MonoBehaviour
{
    [SerializeField] private Transform centerEyeAnchor;

    public Vector3 HeadDirection => centerEyeAnchor.forward;
    public Vector3 HeadOrigin => centerEyeAnchor.position;
}