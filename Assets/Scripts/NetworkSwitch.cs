using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSwitch : NetworkBehaviour
{
    [SerializeField]
    private NetworkDoor targetDoor;

    private readonly HashSet<ulong> playersOnSwitch = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        NetworkObject playerNetworkObject =
            other.GetComponent<NetworkObject>();

        NetworkScorePlayer player =
            other.GetComponent<NetworkScorePlayer>();

        if (playerNetworkObject == null || player == null)
            return;

        playersOnSwitch.Add(playerNetworkObject.OwnerClientId);

        if (targetDoor != null)
            targetDoor.OpenDoor();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
            return;

        NetworkObject playerNetworkObject =
            other.GetComponent<NetworkObject>();

        NetworkScorePlayer player =
            other.GetComponent<NetworkScorePlayer>();

        if (playerNetworkObject == null || player == null)
            return;

        playersOnSwitch.Remove(playerNetworkObject.OwnerClientId);

        if (playersOnSwitch.Count == 0 && targetDoor != null)
            targetDoor.CloseDoor();
    }
}