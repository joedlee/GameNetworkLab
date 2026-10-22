using Unity.Netcode;
using UnityEngine;

public class NetworkCoin : NetworkBehaviour
{
    [SerializeField]
    private float maxCollectDistance = 2f;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        NetworkScorePlayer player =
            other.GetComponent<NetworkScorePlayer>();

        if (player == null || !player.IsOwner)
            return;

        player.RequestCollectCoin(
            NetworkObjectId);
    }

    public bool TryCollectServer(
        NetworkScorePlayer player)
    {
        if (!IsServer ||
            collected ||
            !IsSpawned ||
            player == null)
        {
            Debug.LogWarning(
                $"Coin 요청 거절: 이미 획득했거나 유효하지 않은 Coin | " +
                $"CoinNetworkObjectId={NetworkObjectId}");

            return false;
        }

        float distance =
            Vector3.Distance(
                player.transform.position,
                transform.position);

        if (distance > maxCollectDistance)
        {
            Debug.LogWarning(
                $"Coin 요청 거절: 거리 초과 | " +
                $"Player={player.OwnerClientId}, " +
                $"CoinNetworkObjectId={NetworkObjectId}, " +
                $"Distance={distance:F2}, " +
                $"Allowed={maxCollectDistance:F2}");

            return false;
        }

        collected = true;

        player.AddScoreFromServer(1);

        Debug.Log(
            $"Coin 요청 승인 | " +
            $"Player={player.OwnerClientId}, " +
            $"CoinNetworkObjectId={NetworkObjectId}, " +
            $"Score={player.Score.Value}");

        NetworkObject.Despawn();

        return true;
    }
}
