using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkLauncher : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI statusText;

    private const string HostAddress = "127.0.0.1";
    private const ushort HostPort = 7777;

    private NetworkManager manager;
    private bool callbacksRegistered;

    private void Start()
    {
        RegisterCallbacks();
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();
    }

    public void StartHost()
    {
        RegisterCallbacks();

        if (manager == null || manager.IsListening)
            return;

        UnityTransport transport =
            manager.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            HostAddress,
            HostPort,
            HostAddress);

        bool started = manager.StartHost();

        statusText.text =
            started ? "Host 시작" : "Host 시작 실패";
    }

    public void StartClient()
    {
        RegisterCallbacks();

        if (manager == null || manager.IsListening)
            return;

        UnityTransport transport =
            manager.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            HostAddress,
            HostPort);

        bool started = manager.StartClient();

        statusText.text =
            started ? "Client 연결 시도" : "Client 시작 실패";
    }

    private void RegisterCallbacks()
    {
        if (callbacksRegistered)
            return;

        manager =
            NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnServerStarted += HandleServerStarted;
        manager.OnClientConnectedCallback += HandleClientConnected;
        manager.OnClientDisconnectCallback += HandleClientDisconnected;

        callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!callbacksRegistered || manager == null)
            return;

        manager.OnServerStarted -= HandleServerStarted;
        manager.OnClientConnectedCallback -= HandleClientConnected;
        manager.OnClientDisconnectCallback -= HandleClientDisconnected;

        callbacksRegistered = false;
    }

    private void HandleServerStarted()
    {
        Debug.Log(
            $"{Timestamp()} HostStarted ClientId={NetworkManager.ServerClientId}");
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (manager == null)
            return;

        if (manager.IsServer &&
            clientId == NetworkManager.ServerClientId)
        {
            return;
        }

        Debug.Log(
            $"{Timestamp()} ClientConnected ClientId={clientId}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log(
            $"{Timestamp()} ClientDisconnected ClientId={clientId}");
    }

    private static string Timestamp()
    {
        return DateTimeOffset.Now.ToString("O");
    }
}
