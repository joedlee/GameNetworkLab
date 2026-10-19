using System;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class GameDataApiClient : MonoBehaviour
{
    [SerializeField]
    private string baseUrl = "http://127.0.0.1:5000";

    [SerializeField]
    private string testPlayerId = "test-player-002";

    [SerializeField]
    private int testSaveScore = 300;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private bool testOnStart = true;

    [Serializable]
    private class SaveScoreRequest
    {
        public string PlayerId;
        public int Score;
    }

    [Serializable]
    private class ScoreResponse
    {
        public string playerId;
        public int score;
        public string lastSavedAt;
    }

    private void Start()
    {
        if (testOnStart)
            LoadTestScoreAsync().Forget();
    }

    public void LoadTestScore()
    {
        LoadTestScoreAsync().Forget();
    }

    public void SaveTestScore()
    {
        SaveTestScoreAsync().Forget();
    }

    private async UniTask LoadTestScoreAsync()
    {
        if (string.IsNullOrWhiteSpace(testPlayerId))
        {
            SetStatus("DB 조회 실패: PlayerId 없음");
            return;
        }

        SetStatus("DB 점수 조회 중...");

        string url =
            $"{baseUrl.TrimEnd('/')}/score/{UnityWebRequest.EscapeURL(testPlayerId)}";

        using UnityWebRequest request = UnityWebRequest.Get(url);

        try
        {
            await request.SendWebRequest().ToUniTask(
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            SetStatus($"DB 조회 실패: {exception.Message}");
            Debug.LogException(exception);
            return;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            SetStatus($"DB 조회 실패: HTTP {request.responseCode}");
            Debug.LogError(
                $"GameDataServer 조회 실패\nURL: {url}\n응답: {request.downloadHandler.text}");
            return;
        }

        ScoreResponse response =
            JsonUtility.FromJson<ScoreResponse>(request.downloadHandler.text);

        if (response == null || string.IsNullOrWhiteSpace(response.playerId))
        {
            SetStatus("DB 조회 실패: 응답 해석 실패");
            Debug.LogError($"GameDataServer 응답 해석 실패: {request.downloadHandler.text}");
            return;
        }

        PlayerScoreData data = new PlayerScoreData
        {
            PlayerId = response.playerId,
            Score = response.score,
            LastSavedAt = response.lastSavedAt
        };

        SetStatus($"DB 조회 성공: {data.PlayerId} / {data.Score}점");

        Debug.Log(
            $"Unity -> C# Server -> PostgreSQL 조회 성공 | " +
            $"PlayerId={data.PlayerId}, Score={data.Score}, LastSavedAt={data.LastSavedAt}");
    }

    private async UniTask SaveTestScoreAsync()
    {
        if (string.IsNullOrWhiteSpace(testPlayerId))
        {
            SetStatus("DB 저장 실패: PlayerId 없음");
            return;
        }

        SetStatus($"DB 점수 저장 중... {testSaveScore}점");

        string url = $"{baseUrl.TrimEnd('/')}/score";

        SaveScoreRequest saveData = new SaveScoreRequest
        {
            PlayerId = testPlayerId,
            Score = testSaveScore
        };

        string json = JsonUtility.ToJson(saveData);

        using UnityWebRequest request =
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");

        try
        {
            await request.SendWebRequest().ToUniTask(
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            SetStatus($"DB 저장 실패: {exception.Message}");
            Debug.LogException(exception);
            return;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            SetStatus($"DB 저장 실패: HTTP {request.responseCode}");
            Debug.LogError(
                $"GameDataServer 저장 실패\nURL: {url}\n요청: {json}\n응답: {request.downloadHandler.text}");
            return;
        }

        ScoreResponse response =
            JsonUtility.FromJson<ScoreResponse>(request.downloadHandler.text);

        if (response == null || string.IsNullOrWhiteSpace(response.playerId))
        {
            SetStatus("DB 저장 실패: 응답 해석 실패");
            Debug.LogError($"GameDataServer 응답 해석 실패: {request.downloadHandler.text}");
            return;
        }

        SetStatus($"DB 저장 성공: {response.playerId} / {response.score}점");

        Debug.Log(
            $"Unity -> C# Server -> PostgreSQL 저장 성공 | " +
            $"PlayerId={response.playerId}, Score={response.score}, LastSavedAt={response.lastSavedAt}");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message);
    }
}
