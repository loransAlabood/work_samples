using System.Collections;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Opens the XR host once the training scene reports that it is ready.
/// The dashboard starts separately as a Netcode client.
/// </summary>
public class NetworkStartScript : MonoBehaviour
{
    [SerializeField] private ushort port = 7777;

    [Tooltip("Interface to listen on. 0.0.0.0 accepts connections on every interface; use the host's LAN address to narrow it.")]
    [SerializeField] private string listenAddress = "0.0.0.0";

    [Header("Optional shared-secret admission (off by default)")]
    [Tooltip("Requires 'Connection Approval' to be enabled on the NetworkManager in BOTH applications. A shared secret keeps out accidental peers; it is not encryption.")]
    [SerializeField] private bool requireSessionToken;
    [Tooltip("Set per deployment. Do not commit a real value.")]
    [SerializeField] private string sessionToken;

    private Coroutine subscribeRoutine;
    private bool subscribed;

    private void OnEnable()
    {
        subscribeRoutine = StartCoroutine(SubscribeWhenAvailable());
    }

    private void OnDisable()
    {
        if (subscribeRoutine != null) { StopCoroutine(subscribeRoutine); subscribeRoutine = null; }
        if (subscribed && CentralEventSystem.Instance != null)
            CentralEventSystem.Instance.OnTrainingModeIsReady -= StartHost;
        subscribed = false;
    }

    /// <summary>
    /// The event system may be created after this component is enabled, so wait for it instead
    /// of silently never subscribing. A readiness event raised before this point is not replayed.
    /// </summary>
    private IEnumerator SubscribeWhenAvailable()
    {
        float warnAt = Time.unscaledTime + 5f;
        bool warned = false;
        while (CentralEventSystem.Instance == null)
        {
            if (!warned && Time.unscaledTime > warnAt)
            {
                Debug.LogWarning("NetworkStartScript: CentralEventSystem not available yet; the host will start once it appears.");
                warned = true;
            }
            yield return null;
        }
        CentralEventSystem.Instance.OnTrainingModeIsReady += StartHost;
        subscribed = true;
        subscribeRoutine = null;
    }

    /// <summary>Start a LAN host; repeated readiness events do not start a second session.</summary>
    public void StartHost()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null) { Debug.LogError("NetworkManager not found."); return; }
        if (manager.IsListening) return;
        UnityTransport transport = manager.GetComponent<UnityTransport>();
        if (transport == null) { Debug.LogError("UnityTransport not found."); return; }

        if (requireSessionToken)
        {
            // Fail closed: never start an open host while the operator expects a gated one.
            if (string.IsNullOrEmpty(sessionToken)) { Debug.LogError("Session token required but empty; host not started."); return; }
            if (!manager.NetworkConfig.ConnectionApproval) { Debug.LogError("Session token required but Connection Approval is disabled on the NetworkManager; host not started."); return; }
            manager.ConnectionApprovalCallback = ApproveConnection;
        }

        transport.SetConnectionData("127.0.0.1", port, listenAddress);
        if (!manager.StartHost()) Debug.LogError("Failed to start session host.");
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
                                   NetworkManager.ConnectionApprovalResponse response)
    {
        // The host's own connection is also routed through this callback.
        bool isHost = request.ClientNetworkId == NetworkManager.Singleton.ServerClientId;
        bool tokenMatches = request.Payload != null && Encoding.UTF8.GetString(request.Payload) == sessionToken;
        bool approved = isHost || tokenMatches;
        response.Approved = approved;
        response.CreatePlayerObject = approved;
        response.Pending = false;
        if (!approved) Debug.LogWarning($"Connection from client {request.ClientNetworkId} rejected: invalid session token.");
    }
}
