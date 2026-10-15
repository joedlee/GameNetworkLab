using Unity.Netcode;
using UnityEngine;

public class NetworkDoor : NetworkBehaviour
{
    public NetworkVariable<bool> IsOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField]
    private Vector3 openOffset = new Vector3(0f, 3.5f, 0f);

    private Vector3 closedPosition;

    private void Awake()
    {
        closedPosition = transform.position;
    }

    public override void OnNetworkSpawn()
    {
        IsOpen.OnValueChanged += OnOpenStateChanged;

        // 이미 열린 상태에서 늦게 접속한 Client도 올바른 위치로 맞춤
        ApplyDoorState(IsOpen.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsOpen.OnValueChanged -= OnOpenStateChanged;
    }

    private void OnOpenStateChanged(bool previousValue, bool newValue)
    {
        ApplyDoorState(newValue);
    }

    private void ApplyDoorState(bool open)
    {
        transform.position =
            open
                ? closedPosition + openOffset
                : closedPosition;
    }

    public void OpenDoor()
    {
        if (!IsServer)
            return;

        IsOpen.Value = true;
    }

    public void CloseDoor()
    {
        if (!IsServer)
            return;

        IsOpen.Value = false;
    }
}