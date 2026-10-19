using Cysharp.Threading.Tasks;
using UnityEngine;

public class AccountAuthOverlay : MonoBehaviour
{
    [SerializeField]
    private GameDataApiClient apiClient;

    [SerializeField]
    private string username = "testuser01";

    private string password = string.Empty;
    private string localMessage = "계정 로그인 대기";
    private bool busy;

    private void Awake()
    {
        if (apiClient == null)
            apiClient = GetComponent<GameDataApiClient>();

        if (apiClient == null)
            apiClient = FindFirstObjectByType<GameDataApiClient>();
    }

    private void OnGUI()
    {
        if (apiClient == null)
            return;

        const float width = 330f;
        const float height = 205f;

        Rect panelRect =
            new Rect(
                Screen.width - width - 20f,
                20f,
                width,
                height);

        GUILayout.BeginArea(
            panelRect,
            GUI.skin.box);

        GUILayout.Label("계정 인증");

        if (apiClient.IsAuthenticated)
        {
            GUILayout.Label(
                $"로그인됨: {apiClient.AuthenticatedUsername}");

            GUILayout.Label(
                $"PlayerId: {ShortPlayerId(apiClient.AuthenticatedPlayerId)}");

            GUILayout.Space(8f);

            GUI.enabled = !busy;

            if (GUILayout.Button(
                "로그아웃",
                GUILayout.Height(32f)))
            {
                LogoutAsync().Forget();
            }

            GUI.enabled = true;
        }
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "아이디",
                GUILayout.Width(70f));

            username =
                GUILayout.TextField(
                    username,
                    32);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "비밀번호",
                GUILayout.Width(70f));

            password =
                GUILayout.PasswordField(
                    password,
                    '*',
                    128);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUI.enabled = !busy;

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "회원가입",
                GUILayout.Height(32f)))
            {
                RegisterAsync().Forget();
            }

            if (GUILayout.Button(
                "로그인",
                GUILayout.Height(32f)))
            {
                LoginAsync().Forget();
            }

            GUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        GUILayout.Space(8f);
        GUILayout.Label(localMessage);

        GUILayout.EndArea();
    }

    private async UniTask RegisterAsync()
    {
        if (busy)
            return;

        busy = true;
        localMessage = "회원가입 중...";

        bool success =
            await apiClient.RegisterAsync(
                username,
                password,
                this.GetCancellationTokenOnDestroy());

        localMessage =
            success
                ? $"회원가입/로그인 완료: {apiClient.AuthenticatedUsername}"
                : "회원가입 실패 - 화면 하단 상태를 확인하세요.";

        if (success)
            password = string.Empty;

        busy = false;
    }

    private async UniTask LoginAsync()
    {
        if (busy)
            return;

        busy = true;
        localMessage = "로그인 중...";

        bool success =
            await apiClient.LoginAsync(
                username,
                password,
                this.GetCancellationTokenOnDestroy());

        localMessage =
            success
                ? $"로그인 완료: {apiClient.AuthenticatedUsername}"
                : "로그인 실패 - 화면 하단 상태를 확인하세요.";

        if (success)
            password = string.Empty;

        busy = false;
    }

    private async UniTask LogoutAsync()
    {
        if (busy)
            return;

        busy = true;
        localMessage = "로그아웃 중...";

        await apiClient.LogoutAsync(
            this.GetCancellationTokenOnDestroy());

        localMessage = "로그아웃 완료";
        busy = false;
    }

    private string ShortPlayerId(
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return "-";

        if (playerId.Length <= 22)
            return playerId;

        return
            $"{playerId[..12]}...{playerId[^7..]}";
    }
}
