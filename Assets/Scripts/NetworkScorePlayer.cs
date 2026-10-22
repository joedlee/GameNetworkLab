using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkScorePlayer : NetworkBehaviour
{
    public NetworkVariable<int> Score = new(0);

    [SerializeField]
    private TextMeshProUGUI scoreText;

    private bool databaseReady;
    private bool databaseLoadInProgress;
    private bool saveInProgress;
    private bool hasPendingSave;
    private int pendingScore;

    private string authenticatedPlayerId;
    private string authenticatedToken;

    public override void OnNetworkSpawn()
    {
        Score.OnValueChanged += OnScoreChanged;
        UpdateScoreText(Score.Value);

        if (IsOwner)
        {
            GameDataApiClient api =
                GameDataApiClient.Instance;

            if (api != null &&
                api.IsAuthenticated)
            {
                UseAuthenticatedAccount(
                    api.AuthenticatedPlayerId,
                    api.AuthToken);
            }
            else
            {
                Debug.Log(
                    $"Player {OwnerClientId}: 로그인 전 상태로 생성됨");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        Score.OnValueChanged -= OnScoreChanged;
    }

    public void UseAuthenticatedAccount(
        string playerId,
        string token)
    {
        if (!IsOwner)
            return;

        if (string.IsNullOrWhiteSpace(playerId) ||
            string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        RegisterAuthenticatedSessionRpc(
            playerId,
            token);
    }

    public void ClearAuthenticatedAccount()
    {
        if (!IsOwner)
            return;

        ClearAuthenticatedSessionRpc();
    }

    public void RequestAddScore()
    {
        if (!IsOwner)
            return;

        RequestAddScoreRpc();
    }

    public void RequestCollectCoin(
        ulong coinNetworkObjectId)
    {
        if (!IsOwner)
            return;

        RequestCollectCoinRpc(
            coinNetworkObjectId);
    }

    public void AddScoreFromServer(
        int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        Score.Value += amount;
    }

    public void RequestSaveToDatabase()
    {
        if (!IsOwner)
            return;

        RequestSaveToDatabaseRpc();
    }

    [Rpc(SendTo.Server)]
    private void RegisterAuthenticatedSessionRpc(
        string claimedPlayerId,
        string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        authenticatedPlayerId =
            claimedPlayerId;

        authenticatedToken =
            token;

        databaseReady = false;
        databaseLoadInProgress = false;
        hasPendingSave = false;

        Debug.Log(
            $"계정 세션 등록 | " +
            $"OwnerClientId={OwnerClientId}, " +
            $"ClaimedPlayerId={claimedPlayerId}");

        LoadAuthenticatedScoreAsync().Forget();
    }

    [Rpc(SendTo.Server)]
    private void ClearAuthenticatedSessionRpc()
    {
        authenticatedPlayerId =
            null;

        authenticatedToken =
            null;

        databaseReady = false;
        databaseLoadInProgress = false;
        hasPendingSave = false;

        Debug.Log(
            $"계정 세션 해제 | OwnerClientId={OwnerClientId}");
    }

    [Rpc(SendTo.Server)]
    private void RequestAddScoreRpc()
    {
        Score.Value += 1;

        Debug.Log(
            $"Server가 점수를 {Score.Value}로 변경했습니다.");
    }

    [Rpc(SendTo.Server)]
    private void RequestCollectCoinRpc(
        ulong coinNetworkObjectId)
    {
        if (NetworkManager == null ||
            NetworkManager.SpawnManager == null)
        {
            Debug.LogWarning(
                $"Coin 요청 거절: SpawnManager 없음 | " +
                $"Player={OwnerClientId}, " +
                $"CoinNetworkObjectId={coinNetworkObjectId}");

            return;
        }

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                coinNetworkObjectId,
                out NetworkObject coinObject))
        {
            Debug.LogWarning(
                $"Coin 요청 거절: 잘못된 Coin ID 또는 이미 제거된 Coin | " +
                $"Player={OwnerClientId}, " +
                $"CoinNetworkObjectId={coinNetworkObjectId}");

            return;
        }

        NetworkCoin coin =
            coinObject.GetComponent<NetworkCoin>();

        if (coin == null)
        {
            Debug.LogWarning(
                $"Coin 요청 거절: 대상이 NetworkCoin이 아님 | " +
                $"Player={OwnerClientId}, " +
                $"NetworkObjectId={coinNetworkObjectId}");

            return;
        }

        coin.TryCollectServer(this);
    }

    [Rpc(SendTo.Server)]
    private void RequestSaveToDatabaseRpc()
    {
        if (!HasAuthenticatedSession())
        {
            Debug.LogWarning(
                $"DB 저장 실패: 로그인된 계정이 없습니다. " +
                $"OwnerClientId={OwnerClientId}");
            return;
        }

        if (!databaseReady)
        {
            pendingScore = Score.Value;
            hasPendingSave = true;

            Debug.Log(
                $"DB 저장 대기: 계정 점수 로드 중 / " +
                $"PlayerId={authenticatedPlayerId}, " +
                $"Score={Score.Value}");

            return;
        }

        QueueDatabaseSave(
            Score.Value);
    }

    private void OnScoreChanged(
        int previousValue,
        int newValue)
    {
        UpdateScoreText(newValue);

        if (IsServer &&
            databaseReady &&
            !databaseLoadInProgress &&
            HasAuthenticatedSession())
        {
            QueueDatabaseSave(
                newValue);
        }
    }

    private async UniTask LoadAuthenticatedScoreAsync()
    {
        if (!IsServer ||
            !HasAuthenticatedSession() ||
            databaseLoadInProgress)
        {
            return;
        }

        GameDataApiClient api =
            GameDataApiClient.Instance ??
            FindFirstObjectByType<GameDataApiClient>();

        if (api == null)
        {
            Debug.LogWarning(
                $"계정 점수 로드 실패: GameDataApiClient 없음");
            return;
        }

        databaseLoadInProgress = true;

        string tokenAtStart =
            authenticatedToken;

        int scoreAtLoadStart =
            Score.Value;

        PlayerScoreData data =
            await api.LoadAccountScoreAsync(
                tokenAtStart,
                this.GetCancellationTokenOnDestroy());

        if (tokenAtStart != authenticatedToken)
        {
            databaseLoadInProgress = false;
            return;
        }

        if (data == null)
        {
            databaseLoadInProgress = false;
            databaseReady = false;

            Debug.LogWarning(
                $"계정 점수 불러오기 실패 | " +
                $"OwnerClientId={OwnerClientId}");
            return;
        }

        authenticatedPlayerId =
            data.PlayerId;

        int scoreDeltaDuringLoad =
            Score.Value - scoreAtLoadStart;

        Score.Value =
            data.Score + scoreDeltaDuringLoad;

        databaseReady = true;
        databaseLoadInProgress = false;

        Debug.Log(
            $"계정 점수 불러오기 완료 | " +
            $"PlayerId={authenticatedPlayerId}, " +
            $"SavedScore={data.Score}, " +
            $"AppliedScore={Score.Value}");

        if (scoreDeltaDuringLoad != 0 ||
            hasPendingSave)
        {
            QueueDatabaseSave(
                Score.Value);
        }
    }

    private void QueueDatabaseSave(
        int score)
    {
        if (!HasAuthenticatedSession())
            return;

        pendingScore = score;
        hasPendingSave = true;

        if (!saveInProgress)
            FlushDatabaseSaveAsync().Forget();
    }

    private async UniTask FlushDatabaseSaveAsync()
    {
        if (!HasAuthenticatedSession())
            return;

        GameDataApiClient api =
            GameDataApiClient.Instance ??
            FindFirstObjectByType<GameDataApiClient>();

        if (api == null)
        {
            Debug.LogWarning(
                $"계정 점수 저장 실패: GameDataApiClient 없음");
            return;
        }

        saveInProgress = true;

        try
        {
            while (hasPendingSave &&
                   HasAuthenticatedSession())
            {
                int scoreToSave =
                    pendingScore;

                string tokenForSave =
                    authenticatedToken;

                hasPendingSave = false;

                PlayerScoreData saved =
                    await api.SaveAccountScoreAsync(
                        tokenForSave,
                        scoreToSave,
                        this.GetCancellationTokenOnDestroy());

                if (tokenForSave != authenticatedToken)
                    continue;

                if (saved != null)
                {
                    authenticatedPlayerId =
                        saved.PlayerId;

                    Debug.Log(
                        $"계정 점수 저장 완료 | " +
                        $"PlayerId={authenticatedPlayerId}, " +
                        $"Score={saved.Score}");
                }
            }
        }
        finally
        {
            saveInProgress = false;
        }
    }

    private bool HasAuthenticatedSession()
    {
        return
            !string.IsNullOrWhiteSpace(authenticatedPlayerId) &&
            !string.IsNullOrWhiteSpace(authenticatedToken);
    }

    private void UpdateScoreText(
        int value)
    {
        if (scoreText != null)
            scoreText.text = $"점수: {value}";
    }
}
