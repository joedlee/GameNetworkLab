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

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(AuthenticatedPlayerId);

    public string AuthToken { get; private set; }
    public string AuthenticatedPlayerId { get; private set; }
    public string AuthenticatedUsername { get; private set; }

    [SerializeField]
    private string baseUrl = "http://127.0.0.1:5000";

    [SerializeField]
    private TextMeshProUGUI statusText;

    [Serializable]
    private class CredentialsRequest
    {
        public string Username;
        public string Password;
    }

    [Serializable]
    private class SaveOwnScoreRequest
    {
        public int Score;
    }

    [Serializable]
    private class ScoreResponse
    {
        public string playerId;
        public int score;
        public string lastSavedAt;
    }

    [Serializable]
    private class AuthResponse
    {
        public string playerId;
        public string username;
        public string token;
    }

    [Serializable]
    private class MessageResponse
    {
        public string message;
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

    public async UniTask<bool> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        return await AuthenticateAsync(
            "/auth/register",
            username,
            password,
            "회원가입",
            cancellationToken);
    }

    public async UniTask<bool> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        return await AuthenticateAsync(
            "/auth/login",
            username,
            password,
            "로그인",
            cancellationToken);
    }

    public async UniTask LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            ClearAuthentication();
            SetStatus("이미 로그아웃 상태입니다.");
            return;
        }

        string token = AuthToken;

        DetachAuthenticatedAccountFromLocalPlayer();

        using UnityWebRequest request =
            new UnityWebRequest(
                $"{baseUrl.TrimEnd('/')}/auth/logout",
                UnityWebRequest.kHttpVerbPOST);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Authorization",
            $"Bearer {token}");

        try
        {
            await request.SendWebRequest().ToUniTask(
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        ClearAuthentication();
        SetStatus("로그아웃 완료");
    }

    public async UniTask<PlayerScoreData> LoadAccountScoreAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using UnityWebRequest request =
            UnityWebRequest.Get(
                $"{baseUrl.TrimEnd('/')}/account/score");

        request.SetRequestHeader(
            "Authorization",
            $"Bearer {token}");

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
                $"계정 점수 조회 실패 | HTTP {request.responseCode} | " +
                $"{request.downloadHandler.text}");
            return null;
        }

        return ParseScoreResponse(
            request.downloadHandler.text);
    }

    public async UniTask<PlayerScoreData> SaveAccountScoreAsync(
        string token,
        int score,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        SaveOwnScoreRequest saveData =
            new SaveOwnScoreRequest
            {
                Score = score
            };

        string json =
            JsonUtility.ToJson(saveData);

        using UnityWebRequest request =
            new UnityWebRequest(
                $"{baseUrl.TrimEnd('/')}/account/score",
                UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        request.SetRequestHeader(
            "Authorization",
            $"Bearer {token}");

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
                $"계정 점수 저장 실패 | HTTP {request.responseCode} | " +
                $"{request.downloadHandler.text}");
            return null;
        }

        return ParseScoreResponse(
            request.downloadHandler.text);
    }

    public void AttachAuthenticatedAccountToLocalPlayer()
    {
        if (!IsAuthenticated)
            return;

        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            return;
        }

        NetworkScorePlayer scorePlayer =
            manager.LocalClient.PlayerObject
                .GetComponent<NetworkScorePlayer>();

        if (scorePlayer == null)
            return;

        scorePlayer.UseAuthenticatedAccount(
            AuthenticatedPlayerId,
            AuthToken);
    }

    public void DetachAuthenticatedAccountFromLocalPlayer()
    {
        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            return;
        }

        NetworkScorePlayer scorePlayer =
            manager.LocalClient.PlayerObject
                .GetComponent<NetworkScorePlayer>();

        if (scorePlayer == null)
            return;

        scorePlayer.ClearAuthenticatedAccount();
    }

    public void SaveCurrentPlayerScore()
    {
        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            SetStatus("현재 플레이어가 없습니다.");
            return;
        }

        if (!IsAuthenticated)
        {
            SetStatus("먼저 로그인하세요.");
            return;
        }

        NetworkScorePlayer scorePlayer =
            manager.LocalClient.PlayerObject
                .GetComponent<NetworkScorePlayer>();

        if (scorePlayer == null)
        {
            SetStatus("NetworkScorePlayer를 찾을 수 없습니다.");
            return;
        }

        scorePlayer.RequestSaveToDatabase();
        SetStatus("현재 계정 점수 DB 저장 요청");
    }

    private async UniTask<bool> AuthenticateAsync(
        string path,
        string username,
        string password,
        string actionName,
        CancellationToken cancellationToken)
    {
        username =
            username?.Trim() ?? string.Empty;

        password ??= string.Empty;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            SetStatus(
                $"{actionName} 실패: 사용자 이름과 비밀번호를 입력하세요.");
            return false;
        }

        CredentialsRequest credentials =
            new CredentialsRequest
            {
                Username = username,
                Password = password
            };

        string json =
            JsonUtility.ToJson(credentials);

        using UnityWebRequest request =
            new UnityWebRequest(
                $"{baseUrl.TrimEnd('/')}{path}",
                UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        SetStatus($"{actionName} 중...");

        try
        {
            await request.SendWebRequest().ToUniTask(
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            SetStatus(
                $"{actionName} 실패: 서버 연결 오류");
            Debug.LogException(exception);
            return false;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            string serverMessage =
                TryGetMessage(
                    request.downloadHandler.text);

            if (string.IsNullOrWhiteSpace(serverMessage))
            {
                serverMessage =
                    request.responseCode == 401
                        ? "사용자 이름 또는 비밀번호가 올바르지 않습니다."
                        : $"HTTP {request.responseCode}";
            }

            SetStatus(
                $"{actionName} 실패: {serverMessage}");

            return false;
        }

        AuthResponse response =
            JsonUtility.FromJson<AuthResponse>(
                request.downloadHandler.text);

        if (response == null ||
            string.IsNullOrWhiteSpace(response.playerId) ||
            string.IsNullOrWhiteSpace(response.token))
        {
            SetStatus(
                $"{actionName} 실패: 응답 해석 실패");
            return false;
        }

        AuthenticatedPlayerId =
            response.playerId;

        AuthenticatedUsername =
            response.username;

        AuthToken =
            response.token;

        SetStatus(
            $"{actionName} 성공: {AuthenticatedUsername}");

        Debug.Log(
            $"계정 인증 성공 | " +
            $"Username={AuthenticatedUsername}, " +
            $"PlayerId={AuthenticatedPlayerId}");

        AttachAuthenticatedAccountToLocalPlayer();

        return true;
    }

    private PlayerScoreData ParseScoreResponse(
        string json)
    {
        ScoreResponse response =
            JsonUtility.FromJson<ScoreResponse>(
                json);

        if (response == null ||
            string.IsNullOrWhiteSpace(response.playerId))
        {
            Debug.LogError(
                $"점수 응답 해석 실패: {json}");
            return null;
        }

        return new PlayerScoreData
        {
            PlayerId = response.playerId,
            Score = response.score,
            LastSavedAt = response.lastSavedAt
        };
    }

    private string TryGetMessage(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            MessageResponse response =
                JsonUtility.FromJson<MessageResponse>(
                    json);

            return response?.message ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ClearAuthentication()
    {
        AuthToken = null;
        AuthenticatedPlayerId = null;
        AuthenticatedUsername = null;
    }

    private void SetStatus(
        string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message);
    }
}
