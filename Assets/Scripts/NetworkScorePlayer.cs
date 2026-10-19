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

    private string PersistentPlayerId =>
        $"client-{OwnerClientId}";

    public override void OnNetworkSpawn()
    {
        Score.OnValueChanged += OnScoreChanged;
        UpdateScoreText(Score.Value);

        if (IsServer)
            LoadScoreFromDatabaseAsync().Forget();
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
    private void RequestAddScoreRpc()
    {
        Score.Value += 1;

        Debug.Log(
            $"Server가 점수를 {Score.Value}로 변경했습니다.");
    }

    [Rpc(SendTo.Server)]
    private void RequestSaveToDatabaseRpc()
    {
        QueueDatabaseSave(Score.Value);
    }

    private void OnScoreChanged(
        int previousValue,
        int newValue)
    {
        UpdateScoreText(newValue);

        if (IsServer && databaseReady && !databaseLoadInProgress)
            QueueDatabaseSave(newValue);
    }

    private async UniTask LoadScoreFromDatabaseAsync()
    {
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
            await api.LoadOrCreateScoreAsync(
                PersistentPlayerId,
                scoreAtLoadStart,
                this.GetCancellationTokenOnDestroy());

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

            if (scoreDeltaDuringLoad != 0)
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
        pendingScore = score;
        hasPendingSave = true;

        if (!saveInProgress)
            FlushDatabaseSaveAsync().Forget();
    }

    private async UniTask FlushDatabaseSaveAsync()
    {
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
