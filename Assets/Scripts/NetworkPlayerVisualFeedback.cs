using System.Collections;
using UnityEngine;

public class NetworkPlayerVisualFeedback : MonoBehaviour
{
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color hitColor = Color.red;
    private Material targetMaterial;
    private Color originalColor;
    private Coroutine hitCoroutine;

    private void Awake()
    {
        if (bodyRenderer == null) return;
        targetMaterial = bodyRenderer.material;
        originalColor = targetMaterial.color;
    }

    public void PlayHitFeedback()
    {
        if (targetMaterial == null) return;
        if (hitCoroutine != null) StopCoroutine(hitCoroutine);
        hitCoroutine = StartCoroutine(FeedbackRoutine());
    }

    private IEnumerator FeedbackRoutine()
    {
        targetMaterial.color = hitColor;
        yield return new WaitForSeconds(0.12f);
        targetMaterial.color = originalColor;
        hitCoroutine = null;
    }

    private void OnDestroy()
    {
        if (targetMaterial != null) Destroy(targetMaterial);
    }
}
