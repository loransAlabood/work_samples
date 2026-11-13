using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// RPC bridge between the XR session host and the researcher dashboard client.
/// Commands run on the host; status messages and logs are delivered to the dashboard.
/// This is one subsystem of a larger application, not a general-purpose transport layer.
/// </summary>
public class NetworkSharedLogic : NetworkBehaviour
{
    /// <summary>Tag carried by the spawned object that holds this component.</summary>
    public const string BridgeTag = "NetworkPlayerPrefab";

    private const int MaxParticipantNameLength = 64;
    // '|' is the list separator; the rest are unsafe if a name ever reaches a file name or path.
    private static readonly char[] DisallowedNameChars = { '|', '/', '\\', ':', '*', '?', '"', '<', '>' };

    [Header("Session roles (runtime diagnostics)")]
    public bool isXRApp;
    public bool isResearcherTool;

    [Header("Remote control admission")]
    [Tooltip("When enabled, only the configured client ID can send session-control commands.")]
    [SerializeField] private bool restrictCommandsToClientId;
    [SerializeField] private ulong permittedClientId;

    private GameStartManager gameStartManager;
    private TrainingManager trainingManager;
    private InterventionManager interventionManager;
    private ElevenLabsTTSRuntime elevenLabsTTSRuntime;
    private RemoteLoggingController remoteLoggingController;
    private GazeManager gazeManager;
    private ResearcherClientLogManagement clientLogReceiver;
    private ResearcherToolUIManager researcherToolUIManager;
    private ParticipantNameTTSManager participantNameManager;
    private IEEventSystem interventionEvents;

    public override void OnNetworkSpawn()
    {
        isXRApp = IsServer;
        isResearcherTool = IsClient && !IsServer;

        if (IsServer) BindHostReferences();
        if (isResearcherTool) BindDashboardReferences();
    }

    public override void OnNetworkDespawn()
    {
        isXRApp = false;
        isResearcherTool = false;
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Finds this client's bridge. Prefers the bridge on the local client's own player object,
    /// so a dashboard does not bind to another client's copy; falls back to the tagged object.
    /// </summary>
    public static bool TryGetLocalBridge(out NetworkSharedLogic bridge)
    {
        bridge = null;
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsClient) return false;

        NetworkObject localPlayer = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
        if (localPlayer != null) bridge = localPlayer.GetComponent<NetworkSharedLogic>();

        if (bridge == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(BridgeTag);
            if (tagged != null) bridge = tagged.GetComponent<NetworkSharedLogic>();
        }
        return bridge != null && bridge.IsSpawned;
    }

    private void BindHostReferences()
    {
        gameStartManager = SceneLookup.Find<GameStartManager>();
        trainingManager = SceneLookup.Find<TrainingManager>();
        interventionManager = SceneLookup.Find<InterventionManager>();
        elevenLabsTTSRuntime = SceneLookup.Find<ElevenLabsTTSRuntime>();
        remoteLoggingController = SceneLookup.Find<RemoteLoggingController>();
        gazeManager = SceneLookup.Find<GazeManager>();
        participantNameManager = SceneLookup.Find<ParticipantNameTTSManager>();
        interventionEvents = SceneLookup.Find<IEEventSystem>();
        if (gameStartManager == null)
            Debug.LogWarning("Network bridge: GameStartManager missing in host scene.");
    }

    private void BindDashboardReferences()
    {
        researcherToolUIManager = SceneLookup.Find<ResearcherToolUIManager>();
        if (researcherToolUIManager != null)
            researcherToolUIManager.RegisterNetworkSharedLogic(this);
        else
            Debug.LogWarning("Network bridge: dashboard UI manager not yet available.");
    }

    /// <summary>
    /// Require a connected sender. Optionally restrict commands to a configured client ID.
    /// The ID filter is an operational restriction, not authentication. Use a trusted LAN
    /// or an authenticated connection-approval scheme for sensitive deployments.
    /// </summary>
    private bool AllowRemoteCommand(ServerRpcParams rpcParams)
    {
        if (!IsServer || NetworkManager == null) return false;
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(sender)) return false;
        if (!restrictCommandsToClientId || sender == permittedClientId) return true;
        Debug.LogWarning($"Network bridge: command rejected from client {sender}.");
        return false;
    }

    private static bool IsValidParticipantName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxParticipantNameLength) return false;
        if (name.IndexOfAny(DisallowedNameChars) >= 0) return false;
        foreach (char c in name)
            if (char.IsControl(c)) return false;
        return true;
    }

    #region Dashboard-to-host commands
    // RequireOwnership is false because the dashboard may call an object whose owner is the host
    // (for example, a scene-owned bridge). Authorization is handled by AllowRemoteCommand.
    [ServerRpc(RequireOwnership = false)]
    public void StartTrainingServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && trainingManager != null)
            trainingManager.OnStartTrainingButton();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectStoryServerRpc(int storyIndex, ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gameStartManager != null && storyIndex >= 0)
            gameStartManager.SelectStoryUI(storyIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void LoadSelectedStoryServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gameStartManager != null)
            gameStartManager.LoadSelectedStoryButton();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartGameServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gameStartManager != null)
            gameStartManager.StartGameButton();
    }

    /// <summary>
    /// The name reaches text-to-speech and the participant cache, so it is length-limited and
    /// must not contain the list separator, path characters or control characters.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetParticipantServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        if (!AllowRemoteCommand(rpcParams) || participantNameManager == null) return;
        string trimmed = name == null ? null : name.Trim();
        if (!IsValidParticipantName(trimmed))
        {
            Debug.LogWarning("Network bridge: participant name rejected (empty, too long, or contains a disallowed character).");
            return;
        }
        participantNameManager.SetCurrentParticipant(trimmed);
    }

    /// <summary>
    /// The client ID in the request is checked against the actual RPC sender so one dashboard
    /// cannot request a response addressed to another client. The reply is sent only to the
    /// requester. Names are pipe-delimited for compatibility with the existing dashboard.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestParticipantListServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!AllowRemoteCommand(rpcParams) || clientId != sender || participantNameManager == null)
            return;
        var names = participantNameManager.GetAllCachedParticipantNames();
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        };
        SendParticipantListClientRpc(string.Join("|", names), target);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetVoiceServerRpc(bool isFemale, ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && elevenLabsTTSRuntime != null)
            elevenLabsTTSRuntime.SetVoiceActor(isFemale);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartDataLoggingServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && remoteLoggingController != null)
            remoteLoggingController.RemoteStartLogging();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetInterventionEngineStateServerRpc(InterventionEngineStatus newStatus, ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && interventionEvents != null)
            interventionEvents.InvokeOnInterventionEngineStatusChanged(newStatus);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CancelAllInterventionsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && interventionManager != null)
            interventionManager.TriggerEmergencyCancelAll();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReCheckEyeTrackingServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gazeManager != null) gazeManager.ReCheckEyeTracking();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartEyeAndHeadVisualizationServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gazeManager != null) gazeManager.StartEyeAndHeadVisualization();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartEyeVisualizationServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gazeManager != null) gazeManager.StartEyeVisualization();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartHeadVisualizationServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gazeManager != null) gazeManager.StartHeadVisualization();
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopGazeVisualizationServerRpc(ServerRpcParams rpcParams = default)
    {
        if (AllowRemoteCommand(rpcParams) && gazeManager != null) gazeManager.StopGazeVisualization();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestServerToDisconnectMeServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!AllowRemoteCommand(rpcParams)) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        // A client may disconnect only itself, never a caller-supplied target ID.
        if (sender != NetworkManager.ServerClientId) NetworkManager.DisconnectClient(sender);
    }
    #endregion

    #region Host-to-dashboard notifications
    /// <summary>Delivered only to the clients named in <paramref name="clientRpcParams"/>.</summary>
    [ClientRpc]
    public void SendParticipantListClientRpc(string serializedNames, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer || !TryGetDashboardUI()) return;
        var names = string.IsNullOrEmpty(serializedNames)
            ? new List<string>() : serializedNames.Split('|').ToList();
        researcherToolUIManager.PopulateParticipantList(names);
    }

    /// <summary>Broadcast to every connected dashboard.</summary>
    [ClientRpc]
    public void SendLogToClientClientRpc(string logMessage)
    {
        if (IsServer) return;
        if (clientLogReceiver == null)
            clientLogReceiver = SceneLookup.Find<ResearcherClientLogManagement>();
        if (clientLogReceiver != null) clientLogReceiver.AddRemoteLog(logMessage);
    }

    [ClientRpc]
    public void ReceivePlayerStatusDataClientRpc(PlayerStatusData data)
    {
        if (!IsServer && TryGetDashboardUI()) researcherToolUIManager.PlayerDataReceiver(data);
    }

    [ClientRpc]
    public void ReceiveTrainingClientRpc(TrainingStatusData data)
    {
        if (!IsServer && TryGetDashboardUI()) researcherToolUIManager.TrainingDataReciever(data);
    }

    [ClientRpc]
    public void ReceivePortalDataClientRpc(SpawnedPortalData data)
    {
        if (!IsServer && TryGetDashboardUI()) researcherToolUIManager.PortalDataReciever(data);
    }

    private bool TryGetDashboardUI()
    {
        if (researcherToolUIManager == null)
            researcherToolUIManager = SceneLookup.Find<ResearcherToolUIManager>();
        if (researcherToolUIManager != null) return true;
        Debug.LogWarning("Network bridge: incoming update dropped; dashboard UI is unavailable.");
        return false;
    }
    #endregion
}
