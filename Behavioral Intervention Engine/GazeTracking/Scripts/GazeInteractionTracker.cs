using System;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Converts per-frame sensor hits into start/end events for portal and avatar attention.
/// GazeManager selects the source; this class filters unstable hits and maintains event state.
/// Events are passed to the application-wide event system, not written to storage here.
/// </summary>
[RequireComponent(typeof(GazeManager), typeof(EyeGazeTracker), typeof(HeadGazeTracker))]
public class GazeInteractionTracker : MonoBehaviour
{
    [Header("Raycast layers")]
    [SerializeField] private LayerMask portalLayerMask;
    [SerializeField] private LayerMask avatarLayerMask;

    // Thresholds are measured in frames, not seconds. Behavior therefore depends on update rate.
    [Header("Debounce (consecutive frames)")]
    [Min(1)] [SerializeField] private int avatarDebounceFrames = 3;
    [Min(1)] [SerializeField] private int avatarBlockingDebounceFrames = 3;
    [Min(1)] [SerializeField] private int portalDebounceFrames = 3;

    [Header("Training zones")]
    [SerializeField] private Collider playerZone;
    [SerializeField] private Collider portalZone;
    [SerializeField] private Collider avatarZone;

    private GazeManager gazeManager;
    private EyeGazeTracker eyeTracker;
    private HeadGazeTracker headTracker;
    private GazeSource processedSource = GazeSource.None;

    // Confirmed interaction states are kept separately from raw per-frame raycast results.
    private DebouncedValue<Portal> portalState;
    private DebouncedValue<bool> avatarState;
    private DebouncedValue<bool> blockingState;

    private void Awake()
    {
        gazeManager = GetComponent<GazeManager>();
        eyeTracker = GetComponent<EyeGazeTracker>();
        headTracker = GetComponent<HeadGazeTracker>();
        portalState = new DebouncedValue<Portal>(null, portalDebounceFrames);
        avatarState = new DebouncedValue<bool>(false, avatarDebounceFrames);
        blockingState = new DebouncedValue<bool>(false, avatarBlockingDebounceFrames);
        RaycastUtils.ConfigureMasks(portalLayerMask, avatarLayerMask);
    }

    /// <summary>Accepts an eye-ray hit; ignored unless eye gaze is the selected source.</summary>
    /// <remarks>The availability argument remains for existing callers; GazeManager owns that decision.</remarks>
    public void ProcessEyeGazeInteraction(Portal eyePortal, bool eyeAvatarHit, bool usableEyeAvailable)
    {
        ProcessFrame(GazeSource.Eye, eyePortal, eyeAvatarHit);
    }

    /// <summary>Accepts a head-ray hit; ignored unless head gaze is the selected source.</summary>
    /// <remarks>The availability argument remains for existing callers; GazeManager owns that decision.</remarks>
    public void ProcessHeadGazeInteraction(Portal headPortal, bool headAvatarHit, bool usableEyeAvailable)
    {
        ProcessFrame(GazeSource.Head, headPortal, headAvatarHit);
    }

    // Only confirmed state changes produce events. The raw hit is not itself a look event.
    private void ProcessFrame(GazeSource source, Portal portal, bool avatarHit)
    {
        GazeSource selected = gazeManager.CurrentSource;
        if (selected != processedSource)
            ChangeSource(selected);

        if (source != selected)
            return;

        Portal previousPortal = portalState.Value;
        if (portalState.Observe(portal))
        {
            // Close the previous interval before opening a new one, including A -> B switches.
            if (previousPortal != null)
                EmitPortal(previousPortal, source, PortalEventType.LookEnded);
            if (portalState.Value != null)
                EmitPortal(portalState.Value, source, PortalEventType.LookStarted);
        }

        if (avatarState.Observe(avatarHit))
        {
            if (avatarState.Value)
                CentralEventSystem.Instance.InvokeOnAvatarLookStarted(source);
            else
                CentralEventSystem.Instance.InvokeOnAvatarLookEnded(source);
        }

        // Retained as an internal state for systems that may use obstruction detection later.
        blockingState.Observe(avatarHit && portal != null);
    }

    // Close intervals at the sensor boundary so a gaze duration never spans two different sources.
    private void ChangeSource(GazeSource next)
    {
        // A source handoff is a boundary: don't combine consecutive samples from different sensors.
        if (processedSource != GazeSource.None)
        {
            if (portalState.Value != null)
                EmitPortal(portalState.Value, processedSource, PortalEventType.LookEnded);
            if (avatarState.Value)
                CentralEventSystem.Instance.InvokeOnAvatarLookEnded(processedSource);
        }

        portalState.Reset(null);
        avatarState.Reset(false);
        blockingState.Reset(false);
        processedSource = next;
    }

    // Keep event payload construction in one place so start/end use the same portal metadata.
    private static void EmitPortal(Portal portal, GazeSource source, PortalEventType type)
    {
        var data = new SpawnedPortalData
        {
            portalSlot = portal.Slot,
            portalChoiceIndex = portal.ChoiceIndex,
            label = new FixedString64Bytes(portal.Label ?? ""),
            gazeSource = source,
            eventType = type
        };
        if (type == PortalEventType.LookStarted)
            CentralEventSystem.Instance.InvokeOnOnPortalLookStarted(data);
        else
            CentralEventSystem.Instance.InvokeOnOnPortalLookEnded(data);
    }

    private void OnDisable()
    {
        if (portalState != null && processedSource != GazeSource.None)
            ChangeSource(GazeSource.None);
    }

    private void GetCurrentRay(out Vector3 origin, out Vector3 direction)
    {
        if (gazeManager.CurrentSource == GazeSource.Eye)
        {
            origin = eyeTracker.EyeOrigin;
            direction = eyeTracker.AverageDirection;
        }
        else
        {
            origin = headTracker.HeadOrigin;
            direction = headTracker.HeadDirection;
        }
    }

    // Training queries use the current ray directly, without waiting for debounced events.
    // These queries are used by the application's onboarding/training sequence.
    /// <summary>Checks the current gaze ray against a portal label during training.</summary>
    public bool IsUserLookingPortal(string portalLabel)
    {
        GetCurrentRay(out Vector3 origin, out Vector3 direction);
        RaycastUtils.PerformRaycast(origin, direction, out _, out Portal portal);
        return portal != null && string.Equals(portal.Label, portalLabel, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks whether the current gaze ray intersects one of the configured training zones.</summary>
    public bool IsUserLookingAtZone(ZoneType zoneType)
    {
        Collider target = GetColliderForZone(zoneType);
        if (target == null) return false;
        GetCurrentRay(out Vector3 origin, out Vector3 direction);
        return target.Raycast(new Ray(origin, direction), out _, 20f);
    }

    private Collider GetColliderForZone(ZoneType type)
    {
        return type switch
        {
            ZoneType.PlayerZone => playerZone,
            ZoneType.PortalZone => portalZone,
            ZoneType.AvatarZone => avatarZone,
            _ => null
        };
    }
}
