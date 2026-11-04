using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageFlash : MonoBehaviour
{
    [SerializeField] private Image flashImage;
    [SerializeField] private float maxAlpha = 0.45f;
    [SerializeField] private float flashDuration = 0.25f;
    private Coroutine flashCoroutine;

    public void TriggerFlash()
    {
        if (flashImage == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Color color = flashImage.color;
        color.a = maxAlpha;
        flashImage.color = color;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(maxAlpha, 0f, Mathf.Clamp01(elapsed / flashDuration));
            flashImage.color = color;
            yield return null;
        }
        color.a = 0f;
        flashImage.color = color;
        flashCoroutine = null;
    }
}
