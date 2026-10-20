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

    public void DisconnectClient()
    {
        RegisterCallbacks();

        if (manager == null || !manager.IsListening)
        {
            if (statusText != null)
                statusText.text = "연결된 Client가 없습니다.";

            return;
        }

        if (manager.IsHost || manager.IsServer)
        {
            if (statusText != null)
                statusText.text = "Host에서는 Client 연결 해제를 사용할 수 없습니다.";

            Debug.LogWarning(
                "Client 연결 해제 요청 무시: 현재 인스턴스는 Host/Server입니다.");
            return;
        }

        if (!manager.IsClient)
        {
            if (statusText != null)
                statusText.text = "현재 인스턴스는 Client가 아닙니다.";

            return;
        }

        Debug.Log(
            $"{Timestamp()} LocalClientShutdownRequested ClientId={manager.LocalClientId}");

        if (statusText != null)
            statusText.text = "Client 연결 해제";

        manager.Shutdown();
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
