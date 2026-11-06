using System;
using UnityEngine;

// Принимает только резкий физический взмах клинком; сервер проверяет попадания и кулдаун.
public class VRMeleeWeapon : MonoBehaviour
{
    [SerializeField] private float minSwingSpeed = 2.2f;
    [SerializeField] private float swingCooldown = 0.6f;

    private Vector3 previousPosition;
    private bool hasPosition;
    private bool wasFast;
    private float lastSwingTime = -10f;

    public event Action OnValidSwingDetected;
    public float CurrentSpeed { get; private set; }

    private void OnEnable() { hasPosition = false; wasFast = false; }

    private void Update()
    {
        Vector3 current = transform.position;
        if (!hasPosition) { previousPosition = current; hasPosition = true; return; }
        CurrentSpeed = Vector3.Distance(current, previousPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        previousPosition = current;
        bool isFast = CurrentSpeed >= minSwingSpeed;
        // Отправляем один удар на начало резкого взмаха; иначе пустые триггеры будут стрелять без жеста.
        if (isFast && !wasFast) TrySwing();
        wasFast = isFast;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger && CurrentSpeed >= minSwingSpeed) TrySwing();
    }

    private void TrySwing()
    {
        if (Time.time - lastSwingTime < swingCooldown) return;
        lastSwingTime = Time.time;
        OnValidSwingDetected?.Invoke();
    }
}
