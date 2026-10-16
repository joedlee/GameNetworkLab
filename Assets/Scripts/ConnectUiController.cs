using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiController : MonoBehaviour
{
    [SerializeField] private Button connectButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        // 처음에는 다시 시도 버튼을 숨깁니다.
        retryButton.gameObject.SetActive(false);
    }

    public void StartHostFromButton()
    {
        StartHostAsync().Forget();
    }

    public void RetryHostFromButton()
    {
        StartHostAsync().Forget();
    }

    private async UniTask StartHostAsync()
    {
        if (NetworkManager.Singleton == null)
        {
            statusText.text = "NetworkManager 없음";
            retryButton.gameObject.SetActive(true);
            return;
        }

        // 이미 Host 또는 Client가 실행 중이면 다시 시작하지 않습니다.
        if (NetworkManager.Singleton.IsListening)
        {
            statusText.text = "이미 네트워크가 실행 중입니다.";
            retryButton.gameObject.SetActive(false);
            return;
        }

        connectButton.interactable = false;
        retryButton.gameObject.SetActive(false);

        statusText.text = "Host 시작 중...";

        bool started = NetworkManager.Singleton.StartHost();

        if (!started)
        {
            statusText.text = "Host 시작 실패";

            connectButton.interactable = true;
            retryButton.gameObject.SetActive(true);

            return;
        }

        await UniTask.Yield(
            cancellationToken: this.GetCancellationTokenOnDestroy());

        statusText.text = "Host 시작 완료";

        connectButton.interactable = true;
        retryButton.gameObject.SetActive(false);
    }
}