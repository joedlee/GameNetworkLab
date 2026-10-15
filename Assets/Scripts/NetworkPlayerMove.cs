using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerMove : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float speed = 4f;

    private InputAction moveAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        moveAction = inputActions.FindAction("Player/Move", true);
        moveAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner || moveAction == null)
            return;

        moveAction.Disable();
    }

    private void Update()
    {
        if (!IsOwner || moveAction == null)
            return;

        Vector2 input = moveAction.ReadValue<Vector2>();

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