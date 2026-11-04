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
