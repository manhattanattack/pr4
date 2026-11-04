using System.Collections;
using UnityEngine;

public class MeleeAnimator : MonoBehaviour
{
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private float swingAngle = 60f;
    [SerializeField] private float swingSpeed = 12f;

    private bool isSwinging;
    private Quaternion initialRotation;

    private void Awake()
    {
        if (weaponHolder != null) initialRotation = weaponHolder.localRotation;
    }

    public void PlaySwing()
    {
        if (!isSwinging && weaponHolder != null)
            StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;
        Quaternion swingRotation = initialRotation * Quaternion.Euler(swingAngle, 0f, -swingAngle / 2f);
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * swingSpeed);
            weaponHolder.localRotation = Quaternion.Slerp(initialRotation, swingRotation, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * swingSpeed * 0.7f);
            weaponHolder.localRotation = Quaternion.Slerp(swingRotation, initialRotation, t);
            yield return null;
        }
        weaponHolder.localRotation = initialRotation;
        isSwinging = false;
    }
}
