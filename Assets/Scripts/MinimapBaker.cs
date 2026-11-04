using UnityEngine;
using UnityEngine.UI;

public class MinimapBaker : MonoBehaviour
{
    [Header("UI Компоненты")]
    [SerializeField] private RawImage minimapDisplay;

    [Header("Параметры снимка")]
    [SerializeField] private int resolution = 512;
    [SerializeField] private Color mapBackgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);

    public Bounds LevelBounds { get; private set; }

    /// <summary>
    /// Вызывается единожды сразу после окончания постройки лабиринта
    /// </summary>
    public void Bake(Transform mazeRoot)
    {
        if (mazeRoot == null || minimapDisplay == null) return;

        // 1. Вычисляем точные габариты лабиринта по всем его рендерерам
        Renderer[] renderers = mazeRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[MinimapBaker] Стены лабиринта не содержат рендереров!");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        LevelBounds = bounds;

        // 2. Создаем временную ортографическую камеру над центром лабиринта
        GameObject camObj = new GameObject("Temp_MinimapCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = mapBackgroundColor;

        // Позиционируем строго над центром уровня и направляем вниз
        camObj.transform.position = new Vector3(bounds.center.x, bounds.max.y + 50f, bounds.center.z);
        camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Размер охвата с небольшим запасом 5%
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.z);
        cam.orthographicSize = (maxDimension / 2f) * 1.05f;

        // 3. Рендерим один кадр в RenderTexture
        RenderTexture rt = new RenderTexture(resolution, resolution, 16);
        cam.targetTexture = rt;
        cam.Render();

        // 4. Копируем результат в постоянную статическую Texture2D
        RenderTexture.active = rt;
        Texture2D staticMapTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        staticMapTexture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        staticMapTexture.Apply();

        // 5. Полное освобождение ресурсов: удаление камеры и буфера
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(camObj);

        // 6. Передаем статическую текстуру в UI
        minimapDisplay.texture = staticMapTexture;

        Debug.Log($"[MinimapBaker] Схема успешно запечена в 2D-текстуру ({resolution}x{resolution}). Временная камера удалена.");
    }
}