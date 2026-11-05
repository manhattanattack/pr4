using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkPlayer : MonoBehaviour
{
    [Header("Ссылки на узлы")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private Transform bodyMesh;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Slider healthSlider;

    [Header("Сглаживание сети")]
    [SerializeField] private float smoothSpeed = 23f;

    [Header("Состояние жизни")]
    [SerializeField] private GameObject nameCanvasObject;
    [SerializeField] private NetworkPlayerVisualFeedback visualFeedback;

    private Vector3 targetPosition;
    private float targetYaw;
    private float targetPitch;
    private bool targetCrouch;
    private bool isDead;

    private GameObject shieldIndicator;
    private int appliedColorIndex = -1;

    public Color VisualSkinColor => SkinColors.Get(appliedColorIndex >= 0 ? appliedColorIndex : 0);

    public void SetSkinColor(int colorIndex)
    {
        if (appliedColorIndex == colorIndex) return;
        appliedColorIndex = colorIndex;
        Color color = SkinColors.Get(colorIndex);
        if (visualFeedback != null)
            visualFeedback.SetBaseColor(color);
        else if (bodyMesh != null)
        {
            Renderer renderer = bodyMesh.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }
    }

    private void Awake()
    {
        shieldIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shieldIndicator.name = "SpawnShieldIndicator";
        shieldIndicator.transform.SetParent(transform, false);
        shieldIndicator.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        shieldIndicator.transform.localScale = new Vector3(0.62f, 0.035f, 0.62f);
        Collider col = shieldIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Renderer r = shieldIndicator.GetComponent<Renderer>();
        if (r != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) r.material = new Material(shader);
            r.material.color = Color.cyan;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        shieldIndicator.SetActive(false);
    }

    public void SetSpawnShield(float seconds)
    {
        if (shieldIndicator != null)
            shieldIndicator.SetActive(seconds > 0.01f && !isDead);
    }

    public void Initialize(string nickname, Vector3 startPos, int initialHp = 100)
    {
        if (nicknameText != null) nicknameText.text = nickname;
        Teleport(startPos);
        UpdateHealth(initialHp);
        SetDeadState(false);
    }

    public void Teleport(Vector3 pos)
    {
        targetPosition = pos;
        transform.position = pos;
    }

    public void UpdateHealth(int currentHp)
    {
        if (healthSlider != null) healthSlider.value = Mathf.Clamp(currentHp, 0, 100);
    }

    public void OnHitTaken(int currentHp)
    {
        UpdateHealth(currentHp);
        if (visualFeedback != null) visualFeedback.PlayHitFeedback();
    }

    public void SetDeadState(bool dead)
    {
        if (isDead == dead) return;
        isDead = dead;
        if (nameCanvasObject != null) nameCanvasObject.SetActive(!dead);
        if (bodyMesh != null) bodyMesh.gameObject.SetActive(!dead);
        if (dead)
        {
            transform.rotation = Quaternion.Euler(90f, targetYaw, 0f);
            transform.position = new Vector3(transform.position.x, 0.2f, transform.position.z);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
        }
    }

    public void UpdateNetworkState(Vector3 pos, float yaw, float pitch, bool crouch)
    {
        targetPosition = pos;
        targetYaw = yaw;
        targetPitch = pitch;
        targetCrouch = crouch;
    }

    private void Update()
    {
        if (isDead) return;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetYaw, 0f), Time.deltaTime * smoothSpeed);
        if (headPivot != null)
            headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.Euler(targetPitch, 0f, 0f), Time.deltaTime * smoothSpeed);
        if (bodyMesh != null)
        {
            float targetScaleY = targetCrouch ? 0.5f : 1f;
            Vector3 s = bodyMesh.localScale;
            s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * smoothSpeed);
            bodyMesh.localScale = s;
        }
    }
}
