using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RigidbodyPlayerMovement : MonoBehaviour, INoiseSource
{
    [Header("Параметры скорости")]
    [Tooltip("Скорость обычного шага/бега (м/с)")]
    [SerializeField] private float walkSpeed = 4.5f;
    [Tooltip("Скорость скрытного перемещения на LeftShift (м/с)")]
    [SerializeField] private float sneakSpeed = 2.0f;

    [Header("Управление обзором")]
    [Tooltip("Чувствительность вращения камеры мышью")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [Tooltip("Ссылка на Transform дочерней камеры игрока")]
    [SerializeField] private Transform cameraTransform;

    [Header("Сенсорная система (Шум для NPC)")]
    [Tooltip("Радиус слышимости обычного шага (в метрах)")]
    [SerializeField] private float normalNoiseRadius = 6.0f;

    private Rigidbody rb;
    private Vector2 inputAxes;
    private bool isSneaking;
    private float verticalCameraRotation = 0f;

    public Vector3 Position => transform.position;
    public float NoiseRadius { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // TODO 1
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Update()
    {
        ReadInput();
        HandleMouseLook();
        UpdateNoiseRadius();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void ReadInput()
    {
        // TODO 2
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        inputAxes = new Vector2(horizontal, vertical).normalized; // нормализация это чтоб не суммировалась скорость как я пон
        isSneaking = Input.GetKey(KeyCode.LeftShift);
    }

    private void HandleMouseLook()
    {
        // TODO 3
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // поворот капсулы за мышью
        transform.Rotate(0f, mouseX, 0f);

        verticalCameraRotation -= mouseY;
        // ограничение чтоб камера не переворачивалась
        verticalCameraRotation = Mathf.Clamp(verticalCameraRotation, -80f, 80f);

        if (cameraTransform != null)
        {
            // теперь камере угол задаем
            cameraTransform.localRotation = Quaternion.Euler(verticalCameraRotation, 0f, 0f);
        }
    }

    private void ApplyMovement()
    {
        // TODO 4
        float targetSpeed = isSneaking ? sneakSpeed : walkSpeed;
        Vector3 moveDirection = transform.right * inputAxes.x + transform.forward * inputAxes.y;
        Vector3 horizontalVelocity = moveDirection * targetSpeed;

        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    private void UpdateNoiseRadius()
    {
        // TODO 5
        bool isMoving = inputAxes.sqrMagnitude > 0.01f;

        if (!isMoving || isSneaking)
        {
            NoiseRadius = 0f;
        }
        else
        {
            NoiseRadius = normalNoiseRadius;
        }
    }

    // чтоб внутри редактора рисовалась сфера звука
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, NoiseRadius);
    }
}