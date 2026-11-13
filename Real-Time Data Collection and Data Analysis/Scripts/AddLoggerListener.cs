using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Registers scene objects for pose capture and attaches the application's grab-event
/// bridge to snap interactors. GrabEventHandler is supplied by the parent application.
/// </summary>
public class AddLoggerListener : MonoBehaviour
{
    [SerializeField] private Transform Letters;
    [SerializeField] private Transform Bases;
    [SerializeField] private Transform Platform;

    private void Start()
    {
        foreach (SnapInteractor interactor in GetComponentsInChildren<SnapInteractor>(true))
        {
            // Some scene prefabs already contain the handler. Avoid registering it twice.
            if (interactor.GetComponent<GrabEventHandler>() == null)
                interactor.gameObject.AddComponent<GrabEventHandler>();
        }

        Logger.lettersAndBases = new System.Collections.Generic.List<Transform>();
        AddChildren(Letters);
        AddChildren(Bases);
        if (Platform != null) Logger.lettersAndBases.Add(Platform);
    }

    private static void AddChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
            Logger.lettersAndBases.Add(child);
    }
}
