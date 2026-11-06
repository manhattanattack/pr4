using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkPlayer : MonoBehaviour
{
    [Header("Ссылки на узлы")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private Transform bodyMesh;
    [Header("6DoF шлем и руки Quest 3")]
    [SerializeField] private Transform leftHandRoot;
    [SerializeField] private Transform rightHandRoot;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Slider healthSlider;

    [Header("Сглаживание сети")]
    [SerializeField] private float smoothSpeed = 15f;

    [Header("Состояние жизни")]
    [SerializeField] private GameObject nameCanvasObject;
    [SerializeField] private NetworkPlayerVisualFeedback visualFeedback;

    private Vector3 targetPosition;
    private float targetYaw;
    private float targetPitch;
    private bool targetCrouch;
    private bool isDead;
    private bool targetVR;
    private Vector3 baseHeadPosition;
    private Vector3 targetHeadPos, targetLeftHandPos, targetRightHandPos;
    private Quaternion targetHeadRot, targetRightHandRot;

    private void Awake()
    {
        if (headPivot != null) baseHeadPosition = headPivot.localPosition;
        if (leftHandRoot == null) leftHandRoot = MakeHand("LeftHand_Mesh", new Color(0.15f, 0.8f, 1f));
        if (rightHandRoot == null) rightHandRoot = MakeHand("RightHand_Mesh", new Color(1f, 0.72f, 0.2f));
        leftHandRoot.gameObject.SetActive(false);
        rightHandRoot.gameObject.SetActive(false);
    }

    private Transform MakeHand(string handName, Color color)
    {
        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = handName;
        hand.transform.SetParent(transform, false);
        hand.transform.localScale = Vector3.one * 0.12f;
        Collider collider = hand.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        hand.GetComponent<Renderer>().material.color = color;
        return hand.transform;
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
        targetVR = false;
        targetPosition = pos;
        targetYaw = yaw;
        targetPitch = pitch;
        targetCrouch = crouch;
    }

    public void UpdateVRNetworkState(Vector3 bodyPos, Vector3 headPos, Quaternion headRot,
        Vector3 leftPos, Vector3 rightPos, Quaternion rightRot)
    {
        targetVR = true;
        targetPosition = bodyPos;
        targetHeadPos = headPos;
        targetHeadRot = headRot;
        targetLeftHandPos = leftPos;
        targetRightHandPos = rightPos;
        targetRightHandRot = rightRot;
    }

    private void Update()
    {
        if (isDead) return;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            targetVR ? Quaternion.identity : Quaternion.Euler(0f, targetYaw, 0f), Time.deltaTime * smoothSpeed);
        if (headPivot != null)
        {
            headPivot.localPosition = Vector3.Lerp(headPivot.localPosition,
                targetVR ? targetHeadPos : baseHeadPosition, Time.deltaTime * smoothSpeed);
            headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation,
                targetVR ? targetHeadRot : Quaternion.Euler(targetPitch, 0f, 0f),
                Time.deltaTime * smoothSpeed);
        }
        if (leftHandRoot != null)
        {
            leftHandRoot.gameObject.SetActive(targetVR);
            if (targetVR) leftHandRoot.localPosition = Vector3.Lerp(leftHandRoot.localPosition,
                targetLeftHandPos, Time.deltaTime * smoothSpeed);
        }
        if (rightHandRoot != null)
        {
            rightHandRoot.gameObject.SetActive(targetVR);
            if (targetVR)
            {
                rightHandRoot.localPosition = Vector3.Lerp(rightHandRoot.localPosition,
                    targetRightHandPos, Time.deltaTime * smoothSpeed);
                rightHandRoot.localRotation = Quaternion.Slerp(rightHandRoot.localRotation,
                    targetRightHandRot, Time.deltaTime * smoothSpeed);
            }
        }
        if (bodyMesh != null)
        {
            float targetScaleY = !targetVR && targetCrouch ? 0.5f : 1f;
            Vector3 s = bodyMesh.localScale;
            s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * smoothSpeed);
            bodyMesh.localScale = s;
        }
    }
}
