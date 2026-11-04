using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MobSnapshotData
{
    public string id;
    public string archetype;
    public float x, y, z;
    public float yaw;
    public int hp;
    public int maxHp;
    public string state;
}

public class ClientMobManager : MonoBehaviour
{
    [SerializeField] private GameObject mobPrefab;
    private readonly Dictionary<string, NetworkMobVisual> activeMobs =
        new Dictionary<string, NetworkMobVisual>();

    public void ProcessMobSnapshots(MobSnapshotData[] mobs)
    {
        if (mobs == null || mobPrefab == null) return;

        HashSet<string> receivedIds = new HashSet<string>();
        foreach (var data in mobs)
        {
            receivedIds.Add(data.id);
            Vector3 pos = new Vector3(data.x, data.y, data.z);
            if (!activeMobs.ContainsKey(data.id))
            {
                GameObject obj = Instantiate(mobPrefab, pos, Quaternion.Euler(0f, data.yaw, 0f));
                NetworkMobVisual visual = obj.GetComponent<NetworkMobVisual>();
                if (visual == null) { Destroy(obj); continue; }
                visual.Setup(data.archetype, pos, data.hp, data.maxHp);
                activeMobs.Add(data.id, visual);
            }
            else
            {
                activeMobs[data.id].UpdateNetworkState(pos, data.yaw);
                activeMobs[data.id].UpdateHealth(data.hp, data.maxHp);
            }
        }

        List<string> toRemove = new List<string>();
        foreach (string id in activeMobs.Keys)
            if (!receivedIds.Contains(id)) toRemove.Add(id);
        foreach (string id in toRemove) RemoveMob(id);
    }

    public void TriggerCountdown(string id, float duration)
    {
        if (activeMobs.TryGetValue(id, out NetworkMobVisual mob))
            mob.StartCountdownFlash(duration);
    }

    public void TriggerArmor(string id)
    {
        if (activeMobs.TryGetValue(id, out NetworkMobVisual mob))
            mob.FlashArmor();
    }

    public void TriggerPrepare(string id)
    {
        if (activeMobs.TryGetValue(id, out NetworkMobVisual mob))
            mob.PrepareAttack();
    }

    public void UpdateMobHp(string id, int hp)
    {
        // Полный HP также придёт в следующем SNAPSHOT.
        if (activeMobs.TryGetValue(id, out NetworkMobVisual mob))
            mob.UpdateHealth(hp, 250);
    }

    public void RemoveMob(string id)
    {
        if (!activeMobs.TryGetValue(id, out NetworkMobVisual mob)) return;
        if (mob != null) Destroy(mob.gameObject);
        activeMobs.Remove(id);
    }
}
