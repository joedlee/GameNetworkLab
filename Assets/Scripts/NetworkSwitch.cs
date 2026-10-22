using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class NetworkSwitch : NetworkBehaviour
{
    [SerializeField]
    private NetworkDoor targetDoor;

    [SerializeField]
    private Vector3 detectionPadding =
        new Vector3(0.05f, 0.2f, 0.05f);

    private readonly HashSet<ulong> playersOnSwitch =
        new();

    private readonly Collider[] overlapBuffer =
        new Collider[16];

    private BoxCollider switchCollider;
    private bool pressed;

    private void Awake()
    {
        switchCollider =
            GetComponent<BoxCollider>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        RefreshSwitchState();
    }

    private void FixedUpdate()
    {
        if (!IsServer ||
            !IsSpawned ||
            switchCollider == null)
        {
            return;
        }

        RefreshSwitchState();
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!IsServer)
            return;

        TryRegisterPlayer(
            other);

        ApplyPressedState(
            playersOnSwitch.Count > 0);
    }

    private void OnTriggerStay(
        Collider other)
    {
        if (!IsServer)
            return;

        TryRegisterPlayer(
            other);
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (!IsServer)
            return;

        NetworkScorePlayer player =
            other.GetComponentInParent<NetworkScorePlayer>();

        if (player == null)
            return;

        playersOnSwitch.Remove(
            player.OwnerClientId);

        ApplyPressedState(
            playersOnSwitch.Count > 0);
    }

    private void RefreshSwitchState()
    {
        playersOnSwitch.Clear();

        Bounds bounds =
            switchCollider.bounds;

        Vector3 halfExtents =
            bounds.extents +
            detectionPadding;

        int count =
            Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                overlapBuffer,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

        for (int i = 0;
             i < count;
             i++)
        {
            Collider candidate =
                overlapBuffer[i];

            if (candidate == null ||
                candidate == switchCollider)
            {
                continue;
            }

            TryRegisterPlayer(
                candidate);
        }

        ApplyPressedState(
            playersOnSwitch.Count > 0);
    }

    private void TryRegisterPlayer(
        Collider other)
    {
        NetworkScorePlayer player =
            other.GetComponentInParent<NetworkScorePlayer>();

        if (player == null ||
            !player.IsSpawned)
        {
            return;
        }

        playersOnSwitch.Add(
            player.OwnerClientId);
    }

    private void ApplyPressedState(
        bool shouldBePressed)
    {
        if (pressed ==
            shouldBePressed)
        {
            return;
        }

        pressed =
            shouldBePressed;

        if (targetDoor == null)
        {
            Debug.LogWarning(
                "NetworkSwitch: targetDoor가 연결되어 있지 않습니다.",
                this);

            return;
        }

        if (pressed)
        {
            targetDoor.OpenDoor();

            Debug.Log(
                $"NetworkSwitch ON | Players={playersOnSwitch.Count}",
                this);
        }
        else
        {
            targetDoor.CloseDoor();

            Debug.Log(
                "NetworkSwitch OFF | Players=0",
                this);
        }
    }
}
