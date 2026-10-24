using UnityEngine;
using UnityEngine.InputSystem;

public class CapsuleMovement : MonoBehaviour
{
    public float moveSpeed = 1F;

    private Rigidbody rigidBody;
    private PlayerControls controls;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    void Start() {
        rigidBody = GetComponent<Rigidbody>();
    }

    void Update() {
        float forward = controls.Player.Forward.ReadValue<float>();
        float backward = controls.Player.Backward.ReadValue<float>();
        float left = controls.Player.Left.ReadValue<float>();
        float right = controls.Player.Right.ReadValue<float>();

        float moveX = right - left;
        float moveZ = forward - backward;

        /*
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;
        }
        */

        Vector3 movement = new Vector3(moveX, 0f, moveZ).normalized;

        rigidBody.linearVelocity = new Vector3(movement.x * moveSpeed, rigidBody.linearVelocity.y, movement.z * moveSpeed);
    }
}
