using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkScorePlayer : NetworkBehaviour
{
    public NetworkVariable<int> Score = new(0);

    [SerializeField]
    private TextMeshProUGUI scoreText;

    public override void OnNetworkSpawn()
    {
        Score.OnValueChanged += OnScoreChanged;

        UpdateScoreText(Score.Value);
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

    [Rpc(SendTo.Server)]
    private void RequestAddScoreRpc()
    {
        Score.Value += 1;

        Debug.Log(
            $"Server가 점수를 {Score.Value}로 변경했습니다.");
    }

    private void OnScoreChanged(
        int previousValue,
        int newValue)
    {
        UpdateScoreText(newValue);
    }

    private void UpdateScoreText(int value)
    {
        if (scoreText != null)
        {
            scoreText.text = $"점수: {value}";
        }
    }
}