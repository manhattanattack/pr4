using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class LocalPlayerController : MonoBehaviour
{
    [Header("Параметры скорости")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float crouchSpeed = 3f;

    [Header("Параметры прыжка и гравитации")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -19.62f;

    [Header("Параметры обзора")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform headPivot;

    [Header("Параметры приседания")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchingHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Боевые компоненты")]
    [SerializeField] private MeleeAnimator meleeAnimator;

    public event Action OnAttackInputPressed;
    public event Action OnRangedAttackInputPressed;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float pitchAngle = 0f;
    private bool isCrouching = false;

    public bool IsCrouching => isCrouching;
    public float PitchAngle => pitchAngle;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleRotation();
        HandleCrouch();
        HandleMovement();
        HandleCombatInput();
    }

    private void HandleCombatInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (meleeAnimator != null) meleeAnimator.PlaySwing();
            OnAttackInputPressed?.Invoke();
        }
        if (Input.GetMouseButtonDown(1))
            OnRangedAttackInputPressed?.Invoke();
    }

    private void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Вращение тела вокруг вертикальной оси Y
        transform.Rotate(Vector3.up * mouseX);

        // Наклон головы вверх и вниз вокруг оси X
        pitchAngle -= mouseY;
        pitchAngle = Mathf.Clamp(pitchAngle, -80f, 80f);

        if (headPivot != null)
        {
            headPivot.localEulerAngles = new Vector3(pitchAngle, 0f, 0f);
        }
    }

    private void HandleCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl);
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;

        // Плавная смена высоты коллайдера
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0f, controller.height / 2f, 0f);
    }

    private void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        float inputX = Input.GetAxis("Horizontal");
        float inputZ = Input.GetAxis("Vertical");
        Vector3 moveDir = transform.right * inputX + transform.forward * inputZ;

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;
        controller.Move(moveDir * currentSpeed * Time.deltaTime);

        // Прыжок
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }
}