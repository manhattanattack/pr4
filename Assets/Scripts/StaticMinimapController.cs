using UnityEngine;

public class StaticMinimapController : MonoBehaviour
{
    [Header("Управление видимостью")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private GameObject mapContentContainer;

    [Header("Ссылки на объекты")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private RectTransform playerMarkerUI;
    [SerializeField] private RectTransform mapBackgroundUI;

    [Header("Границы 3D-лабиринта в мировых координатах")]
    [SerializeField] private Vector2 worldBottomLeftCorner = new Vector2(0f, 0f);
    [SerializeField] private Vector2 worldTopRightCorner = new Vector2(42f, 42f);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            mapContentContainer.SetActive(!mapContentContainer.activeSelf);
    }

    private void LateUpdate() // вызов в конце кадра
    {
        // не рисуем если выключена
        if (!mapContentContainer.activeSelf) return;
        // проверка на назначенные объектвы
        if (playerTransform == null || playerMarkerUI == null || mapBackgroundUI == null) return;

        UpdateMarkerPosition();
        UpdateMarkerRotation();
    }

    private void UpdateMarkerPosition() 
    {
        float playerX = playerTransform.position.x;
        float playerZ = playerTransform.position.z;

        // берем доли между началом и концом. координата 21 - доля 0.5, 42 - 1 и тд,
        // также ограничивает до 1 так что выйти за пределы нельзя
        float normalizedX = Mathf.InverseLerp(worldBottomLeftCorner.x, worldTopRightCorner.x, playerX);
        float normalizedY = Mathf.InverseLerp(worldBottomLeftCorner.y, worldTopRightCorner.y, playerZ);

        float mapWidth = mapBackgroundUI.rect.width;
        float mapHeight = mapBackgroundUI.rect.height;

        // поч вычитаем 0.5 не понял до сих пор
        float uiX = (normalizedX - 0.5f) * mapWidth;
        float uiY = (normalizedY - 0.5f) * mapHeight;

        playerMarkerUI.anchoredPosition = new Vector2(uiX, uiY);
    }

    private void UpdateMarkerRotation()
    {
        // поворот по y
        float playerRotationY = playerTransform.eulerAngles.y;
        playerMarkerUI.localRotation = Quaternion.Euler(0f, 0f, -playerRotationY);
    }
}