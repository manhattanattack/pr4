using System.Collections.Generic;
using UnityEngine;

public class MinimapMarkersManager : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private MinimapBaker baker;
    [SerializeField] private RectTransform markersContainer; // Объект MarkersLayer в Canvas

    [Header("UI Префабы маркеров")]
    [SerializeField] private GameObject localMarkerPrefab;
    [SerializeField] private GameObject remoteMarkerPrefab;

    private RectTransform localMarkerRect;
    private Transform localPlayerTransform;

    // Реестр сетевых маркеров: ID -> { RectTransform маркера, Transform сетевого игрока }
    private readonly Dictionary<string, RectTransform> remoteMarkers = new Dictionary<string, RectTransform>();
    private readonly Dictionary<string, Transform> remoteTransforms = new Dictionary<string, Transform>();

    public void RegisterLocalPlayer(Transform playerTr)
    {
        localPlayerTransform = playerTr;
        if (localMarkerPrefab != null && markersContainer != null)
        {
            GameObject markerObj = Instantiate(localMarkerPrefab, markersContainer);
            localMarkerRect = markerObj.GetComponent<RectTransform>();
        }
    }

    public void RegisterRemotePlayer(string id, Transform playerTr)
    {
        if (remoteMarkers.ContainsKey(id)) return;

        if (remoteMarkerPrefab != null && markersContainer != null)
        {
            GameObject markerObj = Instantiate(remoteMarkerPrefab, markersContainer);
            remoteMarkers.Add(id, markerObj.GetComponent<RectTransform>());
            remoteTransforms.Add(id, playerTr);
        }
    }

    public void UnregisterRemotePlayer(string id)
    {
        if (remoteMarkers.TryGetValue(id, out RectTransform markerRect))
        {
            Destroy(markerRect.gameObject);
            remoteMarkers.Remove(id);
            remoteTransforms.Remove(id);
        }
    }

    private void Update()
    {
        // Пока схема не запечена, границы равны нулю — координаты не проецируем
        if (baker == null || baker.LevelBounds.size == Vector3.zero) return;

        // 1. Обновление положения и угла поворота локального игрока
        if (localPlayerTransform != null && localMarkerRect != null)
        {
            localMarkerRect.anchoredPosition = WorldToMinimapPosition(localPlayerTransform.position);
            // Поворот стрелочки (инвертируем угол по оси Z для Canvas)
            localMarkerRect.localEulerAngles = new Vector3(0f, 0f, -localPlayerTransform.eulerAngles.y);
        }

        // 2. Обновление точек всех сетевых участников
        foreach (var kvp in remoteTransforms)
        {
            string id = kvp.Key;
            Transform netTr = kvp.Value;

            if (netTr != null && remoteMarkers.TryGetValue(id, out RectTransform markerRect))
            {
                markerRect.anchoredPosition = WorldToMinimapPosition(netTr.position);
            }
        }
    }

    /// <summary>
    /// Перевод мировых координат (X, Z) в координаты UI RectTransform
    /// </summary>
    private Vector2 WorldToMinimapPosition(Vector3 worldPos)
    {
        Bounds b = baker.LevelBounds;
        float maxDim = Mathf.Max(b.size.x, b.size.z) * 1.05f;

        // Нормализация координат от 0.0 до 1.0 относительно центра лабиринта
        float normX = (worldPos.x - b.center.x) / maxDim + 0.5f;
        float normZ = (worldPos.z - b.center.z) / maxDim + 0.5f;

        // Преобразование в пиксельные координаты контейнера с центром в (0, 0)
        float uiX = (normX - 0.5f) * markersContainer.rect.width;
        float uiY = (normZ - 0.5f) * markersContainer.rect.height;

        return new Vector2(uiX, uiY);
    }
}