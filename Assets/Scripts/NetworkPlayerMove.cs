using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerMove : NetworkBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float speed = 4f;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            moveAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            moveAction.action.Disable();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0f)
            MoveRequestRpc(input);
    }

    [Rpc(SendTo.Server)]
    private void MoveRequestRpc(Vector2 input)
    {
        Vector3 movement =
            new Vector3(input.x, 0f, input.y)
            * speed
            * Time.deltaTime;

        transform.position += movement;
    }
}