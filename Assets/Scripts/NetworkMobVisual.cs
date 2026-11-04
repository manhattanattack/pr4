using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Показывает моба, которого прислал сервер
public class NetworkMobVisual : MonoBehaviour
{
    [Header("Виды мобов")]
    [SerializeField] private GameObject tankMesh;
    [SerializeField] private GameObject sprinterMesh;
    [SerializeField] private GameObject blindMesh;
    [SerializeField] private GameObject kamikazeMesh;

    [Header("Здоровье")]
    [SerializeField] private Slider healthSlider;

    [SerializeField] private float smoothSpeed = 15f;

    private Vector3 targetPos;
    private float targetYaw;

    public void Setup(string archetype, Vector3 spawnPos, int initialHp, int maxHp)
    {
        transform.position = spawnPos;
        targetPos = spawnPos;

        if (tankMesh != null) tankMesh.SetActive(archetype == "tank");
        if (sprinterMesh != null) sprinterMesh.SetActive(archetype == "sprinter");
        if (blindMesh != null) blindMesh.SetActive(archetype == "blind");
        if (kamikazeMesh != null) kamikazeMesh.SetActive(archetype == "kamikaze");

        UpdateHealth(initialHp, maxHp);
    }

    public void UpdateNetworkState(Vector3 pos, float yaw)
    {
        targetPos = pos;
        targetYaw = yaw;
    }

    public void UpdateHealth(int hp, int maxHp)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHp;
            healthSlider.value = hp;
        }
    }

    // Камикадзе мигает перед взрывом
    public void StartCountdownFlash(float duration)
    {
        StartCoroutine(FlashRoutine(duration));
    }

    private IEnumerator FlashRoutine(float duration)
    {
        Renderer r = GetComponentInChildren<Renderer>();
        if (r == null) yield break;

        Material mat = r.material;
        Color original = mat.color;
        float time = 0f;
        while (time < duration)
        {
            mat.color = Mathf.Sin(time * 25f) > 0 ? Color.red : Color.white;
            time += Time.deltaTime;
            yield return null;
        }
        mat.color = original;
    }

    // Когда броня танка блокирует удар - короткая вспышка
    public void FlashArmor()
    {
        StartCoroutine(FlashOnce(Color.cyan, 0.18f));
    }

    public void PrepareAttack()
    {
        StartCoroutine(FlashOnce(Color.yellow, 0.8f));
    }

    private IEnumerator FlashOnce(Color color, float seconds)
    {
        Renderer r = GetComponentInChildren<Renderer>();
        if (r == null) yield break;
        Material mat = r.material;
        Color original = mat.color;
        mat.color = color;
        yield return new WaitForSeconds(seconds);
        mat.color = original;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.Euler(0f, targetYaw, 0f), Time.deltaTime * smoothSpeed);
    }
}
