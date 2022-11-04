using UnityEngine;
using UnityEngine.Serialization;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [SerializeField]
    private float m_MouseSensitivity = 400f;
    [SerializeField]
    private float m_MovementSpeed = 2.927f;
    [SerializeField]
    private float m_MovementBackwardSpeed = 1.125f;
    [SerializeField]
    private float m_MovementHorizontalSpeed = 1.4f;
    [SerializeField]
    private Transform m_PlayerCamera = null;
    [SerializeField]
    private bool m_MoveWithMouse = true;

    private CharacterController m_CharacterController;
    private float m_XRotation = 0f;

    private Animator m_animator;

    void Start()
    {
#if ENABLE_INPUT_SYSTEM
        Debug.Log("The FirstPersonController uses the legacy input system. Please set it in Project Settings");
        m_MoveWithMouse = false;
#endif
        if (m_MoveWithMouse)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        m_CharacterController = GetComponent<CharacterController>();
        m_animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        Look();
        Move();
    }

    private void Look()
    {
        Vector2 lookInput = GetLookInput();

        m_XRotation -= lookInput.y;
        m_XRotation = Mathf.Clamp(m_XRotation, -90f, 90f);

        m_PlayerCamera.localRotation = Quaternion.Euler(m_XRotation, 0, 0);
        transform.Rotate(Vector3.up * lookInput.x, Space.World);
    }

    private void Move()
    {
        Vector3 movementInput = GetMovementInput();

        Vector3 move = transform.right * movementInput.x * m_MovementHorizontalSpeed;
        if (movementInput.z >= 0)
            move += transform.forward * movementInput.z * m_MovementSpeed;
        else
            move += transform.forward * movementInput.z * m_MovementBackwardSpeed;
        
        m_animator.SetFloat("movingSpeedLeftRight", movementInput.x);
        m_animator.SetFloat("movingSpeedFrontBack", movementInput.z);

        m_CharacterController.Move(move * Time.deltaTime);
    }

    private Vector2 GetLookInput()
    {
        float mouseX = 0;
        float mouseY = 0;
        if (m_MoveWithMouse)
        {
            mouseX = Input.GetAxis("Mouse X") * m_MouseSensitivity * Time.deltaTime;
            mouseY = Input.GetAxis("Mouse Y") * m_MouseSensitivity * Time.deltaTime;
        }
        return new Vector2(mouseX, mouseY);
    }

    private Vector3 GetMovementInput()
    {
        float x = 0;
        float z = 0;
        if (m_MoveWithMouse)
        {
            x = Input.GetAxis("Horizontal");
            z = Input.GetAxis("Vertical");
        }

        return new Vector3(x, 0, z);
    }
}
