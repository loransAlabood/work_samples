using System.Collections;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds researcher dashboard controls to the spawned RPC bridge.
/// Controls that issue commands are disabled until their network object is ready.
/// </summary>
public class ResearcherToolUI : MonoBehaviour
{
    [SerializeField] private Button connect;
    [SerializeField] private Button startTraining;
    [SerializeField] private Button loadStory;
    [SerializeField] private Button startStory;
    [SerializeField] private TMP_Dropdown dropdown;

    [Tooltip("Optional. Must match the host's session token when it requires one. Do not commit a real value.")]
    [SerializeField] private string sessionToken;

    private NetworkSharedLogic networkSharedLogic;
    private Coroutine bindRoutine;
    private bool controlsBound;

    private void Start()
    {
        if (connect != null) connect.onClick.AddListener(JoinSession);
        SetControlsInteractable(false);
        if (NetworkManager.Singleton == null) { Debug.LogError("NetworkManager not found."); return; }
        NetworkManager.Singleton.OnClientStarted += BeginBinding;
        NetworkManager.Singleton.OnClientStopped += HandleClientStopped;
        if (NetworkManager.Singleton.IsClient) BeginBinding();
    }

    private void BeginBinding()
    {
        if (bindRoutine == null) bindRoutine = StartCoroutine(WaitForNetworkBridge());
    }

    private IEnumerator WaitForNetworkBridge()
    {
        // The network object may spawn after the client-connected callback.
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !FindNetworkPlayerPrefab())
            yield return null;
        bindRoutine = null;
        if (networkSharedLogic == null) yield break;
        if (!controlsBound)
        {
            if (startTraining != null) startTraining.onClick.AddListener(StartTraining);
            if (dropdown != null) dropdown.onValueChanged.AddListener(SelectStory);
            if (loadStory != null) loadStory.onClick.AddListener(LoadStory);
            if (startStory != null) startStory.onClick.AddListener(StartStory);
            controlsBound = true;
        }
        SetControlsInteractable(true);
    }

    private void HandleClientStopped(bool wasHost)
    {
        networkSharedLogic = null;
        if (bindRoutine != null) { StopCoroutine(bindRoutine); bindRoutine = null; }
        SetControlsInteractable(false);
    }

    private void SetControlsInteractable(bool enabledState)
    {
        if (startTraining != null) startTraining.interactable = enabledState;
        if (loadStory != null) loadStory.interactable = enabledState;
        if (startStory != null) startStory.interactable = enabledState;
        if (dropdown != null) dropdown.interactable = enabledState;
    }

    public void JoinSession()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening) return;
        if (!string.IsNullOrEmpty(sessionToken))
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(sessionToken);
        if (!manager.StartClient()) Debug.LogError("Failed to start the dashboard client.");
    }

    public void StartTraining() { if (networkSharedLogic != null) networkSharedLogic.StartTrainingServerRpc(); }

    /// <summary>The first dropdown entry is a placeholder; story indices start at zero on the host.</summary>
    public void SelectStory(int index)
    {
        if (index > 0 && networkSharedLogic != null) networkSharedLogic.SelectStoryServerRpc(index - 1);
    }

    public void LoadStory() { if (networkSharedLogic != null) networkSharedLogic.LoadSelectedStoryServerRpc(); }
    public void StartStory() { if (networkSharedLogic != null) networkSharedLogic.StartGameServerRpc(); }

    /// <summary>Binds this UI to the local client's bridge; name kept for compatibility.</summary>
    public bool FindNetworkPlayerPrefab()
    {
        if (!NetworkSharedLogic.TryGetLocalBridge(out NetworkSharedLogic bridge)) return false;
        networkSharedLogic = bridge;
        return true;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted -= BeginBinding;
            NetworkManager.Singleton.OnClientStopped -= HandleClientStopped;
        }
        if (connect != null) connect.onClick.RemoveListener(JoinSession);
        if (startTraining != null) startTraining.onClick.RemoveListener(StartTraining);
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(SelectStory);
        if (loadStory != null) loadStory.onClick.RemoveListener(LoadStory);
        if (startStory != null) startStory.onClick.RemoveListener(StartStory);
    }
}
