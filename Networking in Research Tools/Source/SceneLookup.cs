using UnityEngine;

/// <summary>
/// Version-tolerant scene lookup. <c>FindObjectOfType</c> is obsolete from Unity 2023.1;
/// <c>FindFirstObjectByType</c> is its replacement and has the same inactive-object behavior.
/// </summary>
internal static class SceneLookup
{
    public static T Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}
