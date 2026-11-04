using System.Collections.Generic;
using UnityEngine;

public class ProjectileManager : MonoBehaviour
{
    [SerializeField] private GameObject visualProjectilePrefab;
    private readonly Dictionary<string, GameObject> activeProjectiles = new Dictionary<string, GameObject>();

    public void SpawnVisualProjectile(string id, Vector3 startPos, Vector3 direction, float speed)
    {
        if (string.IsNullOrEmpty(id) || activeProjectiles.ContainsKey(id) || visualProjectilePrefab == null) return;
        Quaternion rotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        GameObject projectile = Instantiate(visualProjectilePrefab, startPos, rotation);
        VisualProjectile visual = projectile.GetComponent<VisualProjectile>();
        if (visual != null) visual.Setup(direction, speed);
        activeProjectiles[id] = projectile;
    }

    public void DestroyVisualProjectile(string id, Vector3 hitPos)
    {
        if (!activeProjectiles.TryGetValue(id, out GameObject projectile)) return;
        if (projectile != null) Destroy(projectile);
        activeProjectiles.Remove(id);
    }

    public void ClearAll()
    {
        foreach (GameObject projectile in activeProjectiles.Values)
            if (projectile != null) Destroy(projectile);
        activeProjectiles.Clear();
    }

    private void OnDestroy() => ClearAll();
}
