using System;
using System.Collections;
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
    public event Action OnKickInputPressed;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private Vector3 externalVelocity;
    private float speedMultiplier = 1f;

    public void ApplyImpulse(Vector3 impulse)
    {
        externalVelocity += new Vector3(impulse.x, 0f, impulse.z);
        verticalVelocity.y = Mathf.Max(verticalVelocity.y, impulse.y);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.5f, 2.5f);
    }

    // Prevent an old rocket impulse or speed buff leaking into the next life.
    public void ResetChaosMotion()
    {
        externalVelocity = Vector3.zero;
        verticalVelocity = Vector3.zero;
        speedMultiplier = 1f;
    }

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

        if (Input.GetKeyDown(KeyCode.E))
        {
            PlayKickAnimation();
            OnKickInputPressed?.Invoke();
        }
    }

    // Small local-only boot. No prefab or multiplayer synchronization needed.
    private void PlayKickAnimation()
    {
        Camera playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) return;
        StartCoroutine(KickAnimation(playerCamera.transform));
    }

    private IEnumerator KickAnimation(Transform cameraTransform)
    {
        GameObject boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boot.name = "KickBoot";
        boot.transform.SetParent(cameraTransform, false);
        boot.transform.localScale = new Vector3(0.28f, 0.18f, 0.48f);
        boot.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        Collider collider = boot.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = boot.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(0.16f, 0.18f, 0.22f);

        Vector3 from = new Vector3(0.23f, -0.47f, 0.50f);
        Vector3 to = new Vector3(0.13f, -0.22f, 1.25f);
        float duration = 0.22f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (boot == null) yield break;
            float t = Mathf.Clamp01(elapsed / duration);
            float punch = Mathf.Sin(t * Mathf.PI);
            boot.transform.localPosition = Vector3.Lerp(from, to, punch);
            yield return null;
        }
        if (boot != null) Destroy(boot);
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

        float currentSpeed = (isCrouching ? crouchSpeed : walkSpeed) * speedMultiplier;
        controller.Move(moveDir * currentSpeed * Time.deltaTime);

        // Прыжок
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move((verticalVelocity + externalVelocity) * Time.deltaTime);
        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 15f * Time.deltaTime);
    }
}