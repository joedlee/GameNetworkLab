using Unity.Netcode;
using UnityEngine;

public class NetworkCoin : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 코인 획득 판정은 Server에서만 합니다.
        if (!IsServer)
            return;

        // 닿은 오브젝트가 Player인지 확인합니다.
        NetworkScorePlayer player =
            other.GetComponent<NetworkScorePlayer>();

        if (player == null)
            return;

        // 점수는 Server에서 변경합니다.
        player.Score.Value += 1;

        Debug.Log(
            $"Coin 획득: Player {player.OwnerClientId}, 점수 {player.Score.Value}");

        // 모든 Client에서 Coin을 제거합니다.
        NetworkObject.Despawn();
    }
}