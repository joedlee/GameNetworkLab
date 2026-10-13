using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class NetworkCharacterPlayer : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private InputAction moveAction;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        bool isHostPlayer =
            OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;

        transform.position =
            new Vector3(isHostPlayer ? -2f : 2f, 0f, 0f);

        controller.enabled = true;

        moveAction =
            new InputAction("Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        moveAction.Enable();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || moveAction == null)
        {
            return;
        }

        Vector2 input =
            moveAction.ReadValue<Vector2>();

        Vector3 direction =
            Vector3.ClampMagnitude(
                new Vector3(input.x, 0f, input.y),
                1f);

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 velocity =
            direction * moveSpeed;

        velocity.y = verticalSpeed;

        controller.Move(
            velocity * Time.deltaTime);

        if (direction.sqrMagnitude > 0f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
            moveAction.Dispose();
            moveAction = null;
        }

        controller.enabled = false;
    }
}