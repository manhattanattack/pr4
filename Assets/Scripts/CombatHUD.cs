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
    private TextMeshProUGUI weaponText;
    private TextMeshProUGUI pickupText;
    private float pickupMessageUntil;

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

    public void UpdateWeapon(string weapon, int shotgunAmmo, int smgAmmo, int railgunAmmo, int rocketAmmo)
    {
        if (weaponText == null && scoreText != null)
        {
            weaponText = Instantiate(scoreText, scoreText.transform.parent);
            weaponText.name = "WeaponHUD";
            weaponText.rectTransform.anchoredPosition += new Vector2(0f, -60f);
            weaponText.rectTransform.sizeDelta = new Vector2(520f, 80f);
            weaponText.fontSize = 18f;
            weaponText.color = new Color(0.9f, 0.96f, 1f);
        }
        if (weaponText == null) return;
        string current = "БЛАСТЕР";
        if (weapon == "shotgun") current = "ДРОБОВИК";
        else if (weapon == "smg") current = "ПП";
        else if (weapon == "railgun") current = "РЕЛЬСОТРОН";
        else if (weapon == "rocket") current = "РАКЕТНИЦА";

        int ammo = -1;
        if (weapon == "shotgun") ammo = shotgunAmmo;
        else if (weapon == "smg") ammo = smgAmmo;
        else if (weapon == "railgun") ammo = railgunAmmo;
        else if (weapon == "rocket") ammo = rocketAmmo;
        string amount = ammo < 0 ? "∞" : ammo.ToString();
        weaponText.text = "ПКМ: " + current + "  [" + amount + "]" +
            "\n1 Бластер  2 Дробовик  3 ПП  4 Рельсотрон  5 Ракетница";
    }

    public void ShowWeaponPickup(string weapon)
    {
        if (pickupText == null && scoreText != null)
        {
            pickupText = Instantiate(scoreText, scoreText.transform.parent);
            pickupText.name = "WeaponPickupNotice";
            pickupText.rectTransform.anchoredPosition += new Vector2(0f, -143f);
            pickupText.rectTransform.sizeDelta = new Vector2(500f, 45f);
            pickupText.fontSize = 20;
            pickupText.color = Color.yellow;
        }
        if (pickupText == null) return;
        string name = weapon == "shotgun" ? "ДРОБОВИК" :
            weapon == "smg" ? "ПИСТОЛЕТ-ПУЛЕМЁТ" :
            weapon == "rocket" ? "РАКЕТНИЦА" : "РЕЛЬСОТРОН";
        pickupText.text = "ПОДОБРАНО: " + name;
        pickupMessageUntil = Time.unscaledTime + 2.5f;
        pickupText.gameObject.SetActive(true);
    }

    public void ShowPowerup(string kind)
    {
        if (pickupText == null && scoreText != null)
        {
            pickupText = Instantiate(scoreText,scoreText.transform.parent);
            pickupText.name = "ChaosPowerupNotice";
            pickupText.rectTransform.anchoredPosition += new Vector2(0f,-143f);
            pickupText.fontSize = 20;
        }
        if (pickupText == null) return;
        pickupText.text = kind == "speed" ? "БОНУС: СКОРОСТЬ 10 СЕК" :
            kind == "damage" ? "БОНУС: ДВОЙНОЙ УРОН 8 СЕК" : "БОНУС: +50 БРОНИ";
        pickupMessageUntil = Time.unscaledTime + 2.5f;
        pickupText.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (pickupText != null && Time.unscaledTime > pickupMessageUntil)
            pickupText.gameObject.SetActive(false);
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
