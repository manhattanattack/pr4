using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class LocalPlayerController : MonoBehaviour
{
    [Header("Динамичное движение — PvP Chaos")]
    [SerializeField] private float walkSpeed = 9.5f;
    [SerializeField] private float sprintSpeed = 12.5f;
    [SerializeField] private float crouchSpeed = 4.6f;
    [SerializeField] private float groundAcceleration = 65f;
    [SerializeField] private float groundFriction = 20f;
    [SerializeField] private float airAcceleration = 13f;
    [SerializeField] private float maxAirWishSpeed = 11f;
    [SerializeField] private float maxHorizontalSpeed = 21f;

    [Header("Слайд на Ctrl после разбега")]
    [SerializeField] private float slideMinSpeed = 7.5f;
    [SerializeField] private float slideBoost = 2.6f;
    [SerializeField] private float slideDuration = 0.70f;
    [SerializeField] private float slideFriction = 3.5f;
    [SerializeField] private float slideSteering = 7f;
    [SerializeField] private float slideCooldown = 1f;

    [Header("Прыжки и гравитация")]
    [SerializeField] private float jumpHeight = 1.75f;
    [SerializeField] private float gravity = -24f;

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
    private Vector3 horizontalVelocity;
    private Vector3 externalVelocity;
    private float speedMultiplier = 1f;
    private bool isSliding;
    private float slideEndTime;
    private float nextSlideTime;

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
        horizontalVelocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        verticalVelocity = Vector3.zero;
        speedMultiplier = 1f;
        isSliding = false;
        slideEndTime = 0f;
        nextSlideTime = 0f;
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
        // Quake-like acceleration and air steering, while keeping CharacterController
        // for collision (no Rigidbody required on the player prefab).
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        bool grounded = controller.isGrounded;
        bool wantsJump = grounded && Input.GetButton("Jump") && !isCrouching;

        // Raw input gives immediate response when reversing or strafing.
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 wish = transform.right * inputX + transform.forward * inputZ;
        wish = Vector3.ClampMagnitude(wish, 1f);
        Vector3 wishDirection = wish.sqrMagnitude > 0.0001f ? wish.normalized : Vector3.zero;

        if (grounded && !isSliding && Input.GetKeyDown(KeyCode.LeftControl) &&
            horizontalVelocity.magnitude >= slideMinSpeed && Time.time >= nextSlideTime)
        {
            isSliding = true;
            slideEndTime = Time.time + slideDuration;
            nextSlideTime = Time.time + slideCooldown;
            horizontalVelocity += horizontalVelocity.normalized * slideBoost;
        }
        if (isSliding && (!grounded || !isCrouching || Time.time >= slideEndTime))
            isSliding = false;

        float desiredSpeed = (isCrouching ? crouchSpeed :
            Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed) * speedMultiplier;

        if (grounded)
        {
            if (isSliding)
            {
                // Keep momentum while sliding and allow a little steering.
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity, Vector3.zero, slideFriction * dt);
                Accelerate(wishDirection,
                    Mathf.Max(desiredSpeed, horizontalVelocity.magnitude),
                    slideSteering, dt);
            }
            else
            {
                // No friction on the landing frame of an auto-bunny-hop.
                if (!wantsJump)
                    horizontalVelocity = Vector3.MoveTowards(
                        horizontalVelocity, Vector3.zero, groundFriction * dt);
                Accelerate(wishDirection, desiredSpeed, groundAcceleration, dt);
            }
            if (verticalVelocity.y < 0f) verticalVelocity.y = -2f;

            // Holding Space keeps jumping after landing, preserving speed.
            if (wantsJump)
            {
                isSliding = false;
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            // Air strafing changes velocity gradually instead of snapping.
            Accelerate(wishDirection, Mathf.Min(desiredSpeed, maxAirWishSpeed * speedMultiplier),
                airAcceleration, dt);
        }

        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxHorizontalSpeed);
        verticalVelocity.y += gravity * dt;

        CollisionFlags collision = controller.Move(
            (horizontalVelocity + externalVelocity + verticalVelocity) * dt);

        if ((collision & CollisionFlags.Below) != 0 && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;
        if ((collision & CollisionFlags.Above) != 0 && verticalVelocity.y > 0f)
            verticalVelocity.y = 0f;
        // Rocket/pad knockback remains separate from movement acceleration.
        externalVelocity = Vector3.MoveTowards(
            externalVelocity, Vector3.zero, 15f * dt);
    }

    private void Accelerate(Vector3 direction, float wishSpeed, float acceleration, float dt)
    {
        if (direction.sqrMagnitude < 0.001f || wishSpeed <= 0f) return;
        float currentAlongWish = Vector3.Dot(horizontalVelocity, direction);
        float toAdd = wishSpeed - currentAlongWish;
        if (toAdd <= 0f) return;
        float applied = Mathf.Min(toAdd, acceleration * dt);
        horizontalVelocity += direction * applied;
    }
}
