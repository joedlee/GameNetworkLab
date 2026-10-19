using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class GameDataApiClient : MonoBehaviour
{
    public static GameDataApiClient Instance { get; private set; }

    [SerializeField]
    private string baseUrl = "http://127.0.0.1:5000";

    [SerializeField]
    private TextMeshProUGUI statusText;

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

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public async UniTask<PlayerScoreData> LoadScoreAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return null;

        string url =
            $"{baseUrl.TrimEnd('/')}/score/{UnityWebRequest.EscapeURL(playerId)}";

        using UnityWebRequest request = UnityWebRequest.Get(url);

        try
        {
            await request.SendWebRequest().ToUniTask(
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }

        if (request.responseCode == 404)
            return null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"GameDataServer 조회 실패\nURL: {url}\n" +
                $"HTTP: {request.responseCode}\n응답: {request.downloadHandler.text}");
            return null;
        }

        ScoreResponse response =
            JsonUtility.FromJson<ScoreResponse>(request.downloadHandler.text);

        if (response == null || string.IsNullOrWhiteSpace(response.playerId))
        {
            Debug.LogError(
                $"GameDataServer 응답 해석 실패: {request.downloadHandler.text}");
            return null;
        }

        return new PlayerScoreData
        {
            PlayerId = response.playerId,
            Score = response.score,
            LastSavedAt = response.lastSavedAt
        };
    }

    public async UniTask<PlayerScoreData> SaveScoreAsync(
        string playerId,
        int score,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return null;

        string url = $"{baseUrl.TrimEnd('/')}/score";

        SaveScoreRequest saveData = new SaveScoreRequest
        {
            PlayerId = playerId,
            Score = score
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
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"GameDataServer 저장 실패\nURL: {url}\n" +
                $"HTTP: {request.responseCode}\n요청: {json}\n" +
                $"응답: {request.downloadHandler.text}");
            return null;
        }

        ScoreResponse response =
            JsonUtility.FromJson<ScoreResponse>(request.downloadHandler.text);

        if (response == null || string.IsNullOrWhiteSpace(response.playerId))
        {
            Debug.LogError(
                $"GameDataServer 응답 해석 실패: {request.downloadHandler.text}");
            return null;
        }

        return new PlayerScoreData
        {
            PlayerId = response.playerId,
            Score = response.score,
            LastSavedAt = response.lastSavedAt
        };
    }

    public async UniTask<PlayerScoreData> LoadOrCreateScoreAsync(
        string playerId,
        int initialScore,
        CancellationToken cancellationToken = default)
    {
        PlayerScoreData existing =
            await LoadScoreAsync(playerId, cancellationToken);

        if (existing != null)
            return existing;

        return await SaveScoreAsync(
            playerId,
            initialScore,
            cancellationToken);
    }

    public void SaveCurrentPlayerScore()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            SetStatus("현재 플레이어가 없습니다.");
            return;
        }

        NetworkScorePlayer scorePlayer =
            manager.LocalClient.PlayerObject.GetComponent<NetworkScorePlayer>();

        if (scorePlayer == null)
        {
            SetStatus("NetworkScorePlayer를 찾을 수 없습니다.");
            return;
        }

        scorePlayer.RequestSaveToDatabase();
        SetStatus("현재 점수 DB 저장 요청");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message);
    }
}
