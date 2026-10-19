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

    private string persistentPlayerId;

    private string PersistentPlayerId =>
        persistentPlayerId;

    public override void OnNetworkSpawn()
    {
        Score.OnValueChanged += OnScoreChanged;
        UpdateScoreText(Score.Value);

        if (IsOwner)
        {
            string localPlayerId =
                LocalPlayerIdentity.GetOrCreatePlayerId();

            RegisterPersistentPlayerIdRpc(localPlayerId);

            Debug.Log(
                $"고정 PlayerId 등록 요청 | " +
                $"OwnerClientId={OwnerClientId}, PlayerId={localPlayerId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        Score.OnValueChanged -= OnScoreChanged;
    }

    public void RequestAddScore()
    {
        if (!IsOwner)
            return;

        RequestAddScoreRpc();
    }

    public void RequestSaveToDatabase()
    {
        if (!IsOwner)
            return;

        RequestSaveToDatabaseRpc();
    }

    [Rpc(SendTo.Server)]
    private void RegisterPersistentPlayerIdRpc(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            Debug.LogWarning(
                $"PlayerId 등록 실패: 빈 값 / OwnerClientId={OwnerClientId}");
            return;
        }

        if (playerId.Length > 128)
        {
            Debug.LogWarning(
                $"PlayerId 등록 실패: 너무 긴 값 / OwnerClientId={OwnerClientId}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(persistentPlayerId))
        {
            if (persistentPlayerId != playerId)
            {
                Debug.LogWarning(
                    $"이미 다른 PlayerId가 등록되어 있습니다. " +
                    $"기존={persistentPlayerId}, 새 값={playerId}");
            }

            return;
        }

        persistentPlayerId = playerId;

        Debug.Log(
            $"고정 PlayerId 등록 완료 | " +
            $"OwnerClientId={OwnerClientId}, PlayerId={PersistentPlayerId}");

        LoadScoreFromDatabaseAsync().Forget();
    }

    [Rpc(SendTo.Server)]
    private void RequestAddScoreRpc()
    {
        Score.Value += 1;

        Debug.Log(
            $"Server가 점수를 {Score.Value}로 변경했습니다.");
    }

    [Rpc(SendTo.Server)]
    private void RequestSaveToDatabaseRpc()
    {
        if (string.IsNullOrWhiteSpace(PersistentPlayerId))
        {
            Debug.LogWarning(
                $"DB 저장 대기: 아직 PlayerId가 등록되지 않았습니다. " +
                $"OwnerClientId={OwnerClientId}");
            return;
        }

        if (!databaseReady)
        {
            pendingScore = Score.Value;
            hasPendingSave = true;

            Debug.Log(
                $"DB 저장 대기: 점수 로드 중 / " +
                $"PlayerId={PersistentPlayerId}, Score={Score.Value}");

            return;
        }

        QueueDatabaseSave(Score.Value);
    }

    private void OnScoreChanged(
        int previousValue,
        int newValue)
    {
        UpdateScoreText(newValue);

        if (IsServer &&
            databaseReady &&
            !databaseLoadInProgress &&
            !string.IsNullOrWhiteSpace(PersistentPlayerId))
        {
            QueueDatabaseSave(newValue);
        }
    }

    private async UniTask LoadScoreFromDatabaseAsync()
    {
        if (!IsServer ||
            string.IsNullOrWhiteSpace(PersistentPlayerId) ||
            databaseLoadInProgress ||
            databaseReady)
        {
            return;
        }

        GameDataApiClient api =
            GameDataApiClient.Instance ??
            FindFirstObjectByType<GameDataApiClient>();

        if (api == null)
        {
            Debug.LogWarning(
                $"DB 로드 건너뜀: GameDataApiClient 없음 / {PersistentPlayerId}");
            databaseReady = true;
            return;
        }

        databaseLoadInProgress = true;

        int scoreAtLoadStart = Score.Value;

        PlayerScoreData data =
            await api.LoadScoreAsync(
                PersistentPlayerId,
                this.GetCancellationTokenOnDestroy());

        if (data == null)
        {
            string legacyPlayerId =
                $"client-{OwnerClientId}";

            PlayerScoreData legacyData =
                await api.LoadScoreAsync(
                    legacyPlayerId,
                    this.GetCancellationTokenOnDestroy());

            if (legacyData != null)
            {
                data =
                    await api.SaveScoreAsync(
                        PersistentPlayerId,
                        legacyData.Score,
                        this.GetCancellationTokenOnDestroy());

                if (data != null)
                {
                    Debug.Log(
                        $"기존 임시 PlayerId 점수를 고정 PlayerId로 이전했습니다. " +
                        $"{legacyPlayerId} -> {PersistentPlayerId}, " +
                        $"Score={legacyData.Score}");
                }
            }
        }

        if (data == null)
        {
            data =
                await api.SaveScoreAsync(
                    PersistentPlayerId,
                    scoreAtLoadStart,
                    this.GetCancellationTokenOnDestroy());
        }

        if (data != null)
        {
            int scoreDeltaDuringLoad =
                Score.Value - scoreAtLoadStart;

            Score.Value =
                data.Score + scoreDeltaDuringLoad;

            Debug.Log(
                $"DB 점수 불러오기 완료 | " +
                $"PlayerId={PersistentPlayerId}, " +
                $"SavedScore={data.Score}, " +
                $"AppliedScore={Score.Value}");

            databaseReady = true;
            databaseLoadInProgress = false;

            if (scoreDeltaDuringLoad != 0 || hasPendingSave)
                QueueDatabaseSave(Score.Value);

            return;
        }

        databaseReady = true;
        databaseLoadInProgress = false;

        Debug.LogWarning(
            $"DB 점수 불러오기 실패. 현재 점수로 계속 진행합니다. " +
            $"PlayerId={PersistentPlayerId}, Score={Score.Value}");
    }

    private void QueueDatabaseSave(int score)
    {
        if (string.IsNullOrWhiteSpace(PersistentPlayerId))
            return;

        pendingScore = score;
        hasPendingSave = true;

        if (!saveInProgress)
            FlushDatabaseSaveAsync().Forget();
    }

    private async UniTask FlushDatabaseSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(PersistentPlayerId))
            return;

        GameDataApiClient api =
            GameDataApiClient.Instance ??
            FindFirstObjectByType<GameDataApiClient>();

        if (api == null)
        {
            Debug.LogWarning(
                $"DB 저장 건너뜀: GameDataApiClient 없음 / {PersistentPlayerId}");
            return;
        }

        saveInProgress = true;

        try
        {
            while (hasPendingSave)
            {
                int scoreToSave = pendingScore;
                hasPendingSave = false;

                PlayerScoreData saved =
                    await api.SaveScoreAsync(
                        PersistentPlayerId,
                        scoreToSave,
                        this.GetCancellationTokenOnDestroy());

                if (saved != null)
                {
                    Debug.Log(
                        $"DB 점수 저장 완료 | " +
                        $"PlayerId={PersistentPlayerId}, Score={saved.Score}");
                }
            }
        }
        finally
        {
            saveInProgress = false;
        }
    }

    private void UpdateScoreText(int value)
    {
        if (scoreText != null)
            scoreText.text = $"점수: {value}";
    }
}
