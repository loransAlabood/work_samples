using UnityEngine;

/// <summary>Shared physics query for portal and avatar intersections along a gaze ray.</summary>
/// <remarks>Uses a reusable hit buffer in the common case. Because non-allocating
/// raycasts do not guarantee hit order, portal selection compares hit distances.</remarks>
public static class RaycastUtils
{
    private const float MaxRayDistance = 7f;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];
    private static LayerMask portalLayerMask;
    private static LayerMask avatarLayerMask;

    /// <summary>Sets the physics layers used by subsequent gaze queries.</summary>
    /// <remarks>The masks are shared across all callers of this static utility.</remarks>
    public static void ConfigureMasks(LayerMask portalMask, LayerMask avatarMask)
    {
        portalLayerMask = portalMask;
        avatarLayerMask = avatarMask;
    }

    /// <summary>Returns whether any avatar collider was hit and the nearest portal hit.</summary>
    /// <remarks>Avatar and portal results are independent; both may be reported for one ray.
    /// Queries beyond MaxRayDistance are excluded.</remarks>
    public static void PerformRaycast(Vector3 origin, Vector3 direction, out bool hitAvatar, out Portal portal)
    {
        hitAvatar = false;
        portal = null;
        int mask = portalLayerMask.value | avatarLayerMask.value;
        if (mask == 0 || direction.sqrMagnitude < 0.000001f) return;

        int count = Physics.RaycastNonAlloc(origin, direction, Hits, MaxRayDistance, mask);
        // RaycastNonAlloc can fill its buffer without returning every hit. Fall back when saturated.
        if (count == Hits.Length)
        {
            RaycastHit[] allHits = Physics.RaycastAll(origin, direction, MaxRayDistance, mask);
            SelectHits(allHits, allHits.Length, out hitAvatar, out portal);
        }
        else
        {
            SelectHits(Hits, count, out hitAvatar, out portal);
        }
    }

    // Inspect all returned hits; RaycastNonAlloc does not sort them by distance.
    private static void SelectHits(RaycastHit[] hits, int count, out bool hitAvatar, out Portal portal)
    {
        hitAvatar = false;
        portal = null;
        float nearestPortalDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            int layerBit = 1 << collider.gameObject.layer;
            if ((layerBit & avatarLayerMask.value) != 0)
                hitAvatar = true;
            if ((layerBit & portalLayerMask.value) == 0 || hits[i].distance >= nearestPortalDistance)
                continue;
            Portal candidate = collider.GetComponent<Portal>();
            if (candidate == null) continue;
            portal = candidate;
            nearestPortalDistance = hits[i].distance;
        }
    }
}
