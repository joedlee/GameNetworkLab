using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiController : MonoBehaviour
{
    [SerializeField] private Button connectButton;
    [SerializeField] private TextMeshProUGUI statusText;

    public void StartHostFromButton()
    {
        StartHostAsync().Forget();
    }

    private async UniTask StartHostAsync()
    {
        if (NetworkManager.Singleton == null)
        {
            statusText.text = "NetworkManager 없음";
            return;
        }

        // 이미 네트워크가 실행 중이면 중복 시작 금지
        if (NetworkManager.Singleton.IsListening)
        {
            statusText.text = "이미 네트워크가 실행 중입니다.";
            return;
        }

        connectButton.interactable = false;
        statusText.text = "Host 시작 중...";

        bool started = NetworkManager.Singleton.StartHost();

        if (!started)
        {
            statusText.text = "Host 시작 실패";
            connectButton.interactable = true;
            return;
        }

        await UniTask.Yield(
            cancellationToken: this.GetCancellationTokenOnDestroy());

        statusText.text = "Host 시작 완료";
        connectButton.interactable = true;
    }
}