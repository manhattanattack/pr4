using System;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

// Quest 3: XR Origin + OpenXR 6DoF, левый стик = 4 м/с, правый = snap turn 45°.
// Собственный CharacterController locomotion не зависит от API устаревшего XRI 2.x.
[RequireComponent(typeof(CharacterController))]
public class VRPlayerController : MonoBehaviour
{
    [Header("6DoF узлы")]
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private VRMeleeWeapon meleeWeapon;

    [Header("Параметры Locomotion")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float snapTurnAmount = 45f;
    [SerializeField] private float snapTurnDeadzone = 0.7f;

    private CharacterController characterController;
    private float fallSpeed;
    private bool turnStickReleased = true;
    private bool triggerReleased = true;
    private readonly List<XRInputSubsystem> xrSystems = new List<XRInputSubsystem>();
    private float lastTrackingAttempt;

    public event Action OnMeleeAttackTriggered;
    public event Action OnRangedShootTriggered;

    public Transform HeadTransform => headCamera;
    public Transform LeftHandTransform => leftHand;
    public Transform RightHandTransform => rightHand;

    public void Configure(Transform head, Transform left, Transform right, VRMeleeWeapon sword)
    {
        headCamera = head; leftHand = left; rightHand = right; meleeWeapon = sword;
        SubscribeMelee();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterController.slopeLimit = 45f;
        characterController.stepOffset = 0.3f;
        characterController.skinWidth = 0.05f;
        characterController.radius = 0.3f;
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
    }

    private void OnEnable() { SubscribeMelee(); }
    private void OnDisable() { if (meleeWeapon != null) meleeWeapon.OnValidSwingDetected -= Swing; }

    private void SubscribeMelee()
    {
        if (!isActiveAndEnabled || meleeWeapon == null) return;
        meleeWeapon.OnValidSwingDetected -= Swing;
        meleeWeapon.OnValidSwingDetected += Swing;
    }

    private void Start() { SetFloorTracking(); }

    private void SetFloorTracking()
    {
        xrSystems.Clear();
        SubsystemManager.GetSubsystems(xrSystems);
        foreach (XRInputSubsystem subsystem in xrSystems)
        {
            if (subsystem.running && subsystem.GetSupportedTrackingOriginModes().HasFlag(TrackingOriginModeFlags.Floor))
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }

    private void Update()
    {
        if (headCamera == null) return;
        if (Time.time - lastTrackingAttempt > 5f) { SetFloorTracking(); lastTrackingAttempt = Time.time; }

        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 move);
        rightDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turn);

        // Правый стик: поворот вокруг фактической позиции головы без сдвига в комнате.
        if (Mathf.Abs(turn.x) < 0.25f) turnStickReleased = true;
        if (turnStickReleased && Mathf.Abs(turn.x) >= snapTurnDeadzone)
        {
            Vector3 headBeforeTurn = headCamera.position;
            transform.Rotate(Vector3.up, Mathf.Sign(turn.x) * snapTurnAmount);
            Vector3 difference = headBeforeTurn - headCamera.position;
            transform.position += new Vector3(difference.x, 0f, difference.z);
            turnStickReleased = false;
        }

        float headHeight = Mathf.Clamp(headCamera.localPosition.y, 0.75f, 2.4f);
        characterController.height = headHeight;
        characterController.center = new Vector3(headCamera.localPosition.x, headHeight * 0.5f,
            headCamera.localPosition.z);

        Vector3 forward = Vector3.ProjectOnPlane(headCamera.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(headCamera.right, Vector3.up).normalized;
        Vector3 horizontal = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f) * moveSpeed;
        if (characterController.isGrounded && fallSpeed < 0f) fallSpeed = -1f;
        fallSpeed += Physics.gravity.y * Time.deltaTime;
        characterController.Move((horizontal + Vector3.up * fallSpeed) * Time.deltaTime);

        rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggered);
        if (!triggered) triggerReleased = true;
        if (triggered && triggerReleased)
        {
            triggerReleased = false;
            OnRangedShootTriggered?.Invoke();
            SendHapticImpulse(0.5f, 0.1f);
        }
    }

    private void Swing()
    {
        OnMeleeAttackTriggered?.Invoke();
        SendHapticImpulse(0.7f, 0.15f);
    }

    public void SendHapticImpulse(float amplitude, float duration)
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid) right.SendHapticImpulse(0u, amplitude, duration);
    }

    public void TeleportTo(Vector3 position)
    {
        characterController.enabled = false;
        transform.position = position;
        fallSpeed = 0f;
        characterController.enabled = true;
    }
}
