using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Мировой HUD на левом запястье. Копирует запечённую карту, рисует 2D-маркеры игроков.
public class VRWristHUD : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private RawImage mapDisplay;
    [SerializeField] private RectTransform markersLayer;

    private MinimapBaker baker;
    private Transform playerTransform;
    private IReadOnlyDictionary<string, NetworkPlayer> remotePlayers;
    private RectTransform ownMarker;
    private readonly Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();

    public void Configure(Slider hp, TextMeshProUGUI text, RawImage map, RectTransform layer)
    {
        healthSlider = hp; healthText = text; mapDisplay = map; markersLayer = layer;
    }

    public void Bind(MinimapBaker mapBaker, Texture mapTexture, Transform local,
        IReadOnlyDictionary<string, NetworkPlayer> remote)
    {
        baker = mapBaker; playerTransform = local; remotePlayers = remote;
        if (mapDisplay != null) mapDisplay.texture = mapTexture;
        if (markersLayer != null && ownMarker == null)
            ownMarker = AddMarker("LocalMarker", new Color(0.22f, 1f, 0.5f), 8f);
    }

    public void UpdateHealth(int hp, int maxHp = 100)
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHp;
            healthSlider.value = Mathf.Clamp(hp, 0, maxHp);
        }
        if (healthText != null) healthText.text = $"{hp} / {maxHp}";
    }

    private RectTransform AddMarker(string name, Color color, float width)
    {
        GameObject marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        marker.transform.SetParent(markersLayer, false);
        Image image = marker.GetComponent<Image>();
        image.color = color;
        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, width);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void Update()
    {
        if (baker == null || baker.LevelBounds.size == Vector3.zero || markersLayer == null) return;
        if (ownMarker != null && playerTransform != null)
            ownMarker.anchoredPosition = Project(playerTransform.position);
        if (remotePlayers == null) return;
        foreach (var pair in remotePlayers)
        {
            if (pair.Value == null) continue;
            if (!markers.TryGetValue(pair.Key, out RectTransform marker))
            {
                marker = AddMarker("Remote_" + pair.Key, new Color(1f, 0.35f, 0.25f), 6f);
                markers.Add(pair.Key, marker);
            }
            marker.anchoredPosition = Project(pair.Value.transform.position);
        }
        var gone = new List<string>();
        foreach (var pair in markers)
            if (!remotePlayers.ContainsKey(pair.Key)) gone.Add(pair.Key);
        foreach (string id in gone)
        {
            if (markers[id] != null) Destroy(markers[id].gameObject);
            markers.Remove(id);
        }
    }

    private Vector2 Project(Vector3 world)
    {
        Bounds b = baker.LevelBounds;
        float size = Mathf.Max(b.size.x, b.size.z) * 1.05f;
        return new Vector2((world.x - b.center.x) / size * markersLayer.rect.width,
            (world.z - b.center.z) / size * markersLayer.rect.height);
    }
}
