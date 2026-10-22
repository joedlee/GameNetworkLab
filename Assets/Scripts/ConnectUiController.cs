using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiController : MonoBehaviour
{
    private const string HostAddress =
        "127.0.0.1";

    private const ushort HostPort =
        7777;

    private const float ClientConnectTimeoutSeconds =
        5f;

    [SerializeField]
    private Button connectButton;

    [SerializeField]
    private Button clientButton;

    [SerializeField]
    private Button retryButton;

    [SerializeField]
    private TextMeshProUGUI statusText;

    private NetworkManager manager;
    private bool callbacksRegistered;
    private bool connectionInProgress;
    private ConnectionMode lastRequestedMode =
        ConnectionMode.None;

    private enum ConnectionMode
    {
        None,
        Host,
        Client
    }

    private void Start()
    {
        manager =
            NetworkManager.Singleton;

        RegisterCallbacks();

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        SetStartButtonsInteractable(true);
        SetStatus(
            $"대기 중 | {HostAddress}:{HostPort}");
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();
    }

    public void StartHostFromButton()
    {
        StartConnectionAsync(
            ConnectionMode.Host)
            .Forget();
    }

    public void StartClientFromButton()
    {
        StartConnectionAsync(
            ConnectionMode.Client)
            .Forget();
    }

    // 기존 씬 UnityEvent 이름을 유지하면서
    // 마지막으로 실패/종료한 역할을 다시 시도합니다.
    public void RetryHostFromButton()
    {
        ConnectionMode retryMode =
            lastRequestedMode == ConnectionMode.None
                ? ConnectionMode.Host
                : lastRequestedMode;

        StartConnectionAsync(
            retryMode)
            .Forget();
    }

    private async UniTaskVoid StartConnectionAsync(
        ConnectionMode mode)
    {
        manager ??=
            NetworkManager.Singleton;

        if (manager == null)
        {
            ShowFailure(
                "NetworkManager 없음",
                mode);

            return;
        }

        RegisterCallbacks();

        if (connectionInProgress)
        {
            SetStatus(
                "이미 연결 시도 중입니다.");

            return;
        }

        if (manager.IsListening)
        {
            string role =
                manager.IsHost
                    ? "Host"
                    : manager.IsClient
                        ? "Client"
                        : "Server";

            SetStatus(
                $"이미 {role}로 네트워크가 실행 중입니다.");

            HideRetry();

            return;
        }

        UnityTransport transport =
            manager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            ShowFailure(
                "UnityTransport 없음",
                mode);

            return;
        }

        lastRequestedMode =
            mode;

        connectionInProgress =
            true;

        SetStartButtonsInteractable(false);
        HideRetry();

        bool started;

        if (mode == ConnectionMode.Host)
        {
            transport.SetConnectionData(
                HostAddress,
                HostPort,
                HostAddress);

            SetStatus(
                $"Host 시작 중... | {HostAddress}:{HostPort}");

            started =
                manager.StartHost();

            if (!started)
            {
                connectionInProgress =
                    false;

                ShowFailure(
                    "Host 시작 실패",
                    mode);

                return;
            }

            await UniTask.Yield(
                cancellationToken:
                    this.GetCancellationTokenOnDestroy());

            connectionInProgress =
                false;

            SetStartButtonsInteractable(false);
            HideRetry();

            SetStatus(
                $"Host 실행 중 | {HostAddress}:{HostPort}");

            return;
        }

        transport.SetConnectionData(
            HostAddress,
            HostPort);

        SetStatus(
            $"Client 연결 중... | {HostAddress}:{HostPort}");

        started =
            manager.StartClient();

        if (!started)
        {
            connectionInProgress =
                false;

            ShowFailure(
                "Client 시작 실패",
                mode);

            return;
        }

        float timeoutAt =
            Time.realtimeSinceStartup +
            ClientConnectTimeoutSeconds;

        while (manager != null &&
               manager.IsListening &&
               !manager.IsConnectedClient &&
               Time.realtimeSinceStartup <
               timeoutAt)
        {
            await UniTask.Yield(
                cancellationToken:
                    this.GetCancellationTokenOnDestroy());
        }

        connectionInProgress =
            false;

        if (manager != null &&
            manager.IsConnectedClient)
        {
            SetStartButtonsInteractable(false);
            HideRetry();

            SetStatus(
                $"Client 연결 완료 | " +
                $"ClientId={manager.LocalClientId} | " +
                $"{HostAddress}:{HostPort}");

            return;
        }

        if (manager != null &&
            manager.IsListening)
        {
            manager.Shutdown();
        }

        ShowFailure(
            "Client 연결 실패 또는 시간 초과",
            mode);
    }

    private void RegisterCallbacks()
    {
        if (callbacksRegistered)
            return;

        manager ??=
            NetworkManager.Singleton;

        if (manager == null)
            return;

        manager.OnServerStarted +=
            HandleServerStarted;

        manager.OnClientConnectedCallback +=
            HandleClientConnected;

        manager.OnClientDisconnectCallback +=
            HandleClientDisconnected;

        callbacksRegistered =
            true;
    }

    private void UnregisterCallbacks()
    {
        if (!callbacksRegistered ||
            manager == null)
        {
            return;
        }

        manager.OnServerStarted -=
            HandleServerStarted;

        manager.OnClientConnectedCallback -=
            HandleClientConnected;

        manager.OnClientDisconnectCallback -=
            HandleClientDisconnected;

        callbacksRegistered =
            false;
    }

    private void HandleServerStarted()
    {
        if (manager == null ||
            !manager.IsHost)
        {
            return;
        }

        connectionInProgress =
            false;

        SetStartButtonsInteractable(false);
        HideRetry();

        SetStatus(
            $"Host 실행 중 | {HostAddress}:{HostPort}");
    }

    private void HandleClientConnected(
        ulong clientId)
    {
        if (manager == null)
            return;

        if (!manager.IsServer &&
            clientId ==
            manager.LocalClientId)
        {
            connectionInProgress =
                false;

            lastRequestedMode =
                ConnectionMode.Client;

            SetStartButtonsInteractable(false);
            HideRetry();

            SetStatus(
                $"Client 연결 완료 | " +
                $"ClientId={clientId} | " +
                $"{HostAddress}:{HostPort}");
        }
    }

    private void HandleClientDisconnected(
        ulong clientId)
    {
        if (manager == null ||
            manager.IsServer)
        {
            return;
        }

        connectionInProgress =
            false;

        lastRequestedMode =
            ConnectionMode.Client;

        SetStartButtonsInteractable(true);
        ShowRetry();

        SetStatus(
            $"Client 연결 종료 | " +
            $"다시 시도 가능 | {HostAddress}:{HostPort}");
    }

    private void ShowFailure(
        string message,
        ConnectionMode mode)
    {
        lastRequestedMode =
            mode;

        connectionInProgress =
            false;

        SetStartButtonsInteractable(true);
        ShowRetry();

        SetStatus(
            $"{message} | 다시 시도 가능");
    }

    private void SetStartButtonsInteractable(
        bool interactable)
    {
        if (connectButton != null)
        {
            connectButton.interactable =
                interactable;
        }

        if (clientButton != null)
        {
            clientButton.interactable =
                interactable;
        }
    }

    private void ShowRetry()
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(
                true);
        }
    }

    private void HideRetry()
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(
                false);
        }
    }

    private void SetStatus(
        string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
