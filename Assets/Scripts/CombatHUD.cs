using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    [Header("Индикаторы здоровья")]
    [SerializeField] private Slider localHealthSlider;
    [SerializeField] private TextMeshProUGUI localHealthText;
    [Header("Счетчик побед")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [Header("Экран гибели")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI respawnTimerText;

    private TextMeshProUGUI spawnShieldText;

    public void UpdateSpawnShield(float remaining)
    {
        if (spawnShieldText == null && localHealthText != null)
        {
            spawnShieldText = Instantiate(localHealthText, localHealthText.transform.parent);
            spawnShieldText.gameObject.name = "SpawnShieldCountdown";
            spawnShieldText.rectTransform.anchoredPosition += new Vector2(0f, -32f);
            spawnShieldText.fontSize = 18;
            spawnShieldText.color = Color.cyan;
        }
        if (spawnShieldText == null) return;
        bool active = remaining > 0.01f;
        spawnShieldText.gameObject.SetActive(active);
        if (active) spawnShieldText.text = $"ЩИТ: {remaining:F1} сек";
    }

    public void UpdateHealth(int currentHp, int maxHp = 100)
    {
        if (localHealthSlider != null)
        {
            localHealthSlider.minValue = 0;
            localHealthSlider.maxValue = maxHp;
            localHealthSlider.value = Mathf.Clamp(currentHp, 0, maxHp);
        }
        if (localHealthText != null)
            localHealthText.text = $"{Mathf.Max(0, currentHp)} / {maxHp}";
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = $"Побед: {score}";
    }

    public void ShowDeathScreen(bool isDead, float respawnTime = 0f)
    {
        if (deathPanel != null) deathPanel.SetActive(isDead);
        if (isDead) UpdateRespawnTimer(respawnTime);
    }

    public void UpdateRespawnTimer(float remainingTime)
    {
        if (respawnTimerText != null && deathPanel != null && deathPanel.activeSelf)
            respawnTimerText.text = $"Возрождение через: {Mathf.CeilToInt(Mathf.Max(0f, remainingTime))} сек...";
    }
}
