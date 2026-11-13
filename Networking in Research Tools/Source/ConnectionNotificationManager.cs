using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Exposes Netcode connection changes as a single event for scene components.
/// Attach beside the NetworkManager; this component does not manage reconnects.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class ConnectionNotificationManager : MonoBehaviour
{
    public static ConnectionNotificationManager Singleton { get; private set; }
    public enum ConnectionStatus { Connected, Disconnected }

    /// <summary>Client ID and connection state, raised by the local NetworkManager.</summary>
    public event Action<ulong, ConnectionStatus> OnClientConnectionNotification;

    private NetworkManager manager;

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Debug.LogError("Duplicate ConnectionNotificationManager found.");
            enabled = false;
            return;
        }
        Singleton = this;
        manager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        if (manager == null) manager = GetComponent<NetworkManager>();
        manager.OnClientConnectedCallback += HandleConnected;
        manager.OnClientDisconnectCallback += HandleDisconnected;
    }

    private void OnDisable()
    {
        if (manager == null) return;
        manager.OnClientConnectedCallback -= HandleConnected;
        manager.OnClientDisconnectCallback -= HandleDisconnected;
    }

    private void OnDestroy()
    {
        if (Singleton == this) Singleton = null;
    }

    private void HandleConnected(ulong id) => OnClientConnectionNotification?.Invoke(id, ConnectionStatus.Connected);
    private void HandleDisconnected(ulong id) => OnClientConnectionNotification?.Invoke(id, ConnectionStatus.Disconnected);
}
