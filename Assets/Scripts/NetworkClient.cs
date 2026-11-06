using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using NativeWebSocket;

[Serializable]
public class PacketHeader { public string type; }

[Serializable]
public class JoinPacket { public string type = "JOIN"; public string name; }

[Serializable]
public class SimpleRequest { public string type; }

[Serializable]
public class RangedShotPacket
{
    public string type = "FIRE_RANGED";
    public float x, y, z;
    public float dx, dy, dz;
}

[Serializable]
public class PlayerStateData
{
    public string id;
    public string name;
    public float x, y, z;
    public float yaw, pitch;
    public bool crouch, isVR;
    // 6DoF позы локальны относительно корня игрока (метры/градусы).
    public float hx, hy, hz, hyaw, hpitch, hroll;
    public float lhx, lhy, lhz, rhx, rhy, rhz, ryaw, rpitch;
    public int hp, maxHp, score;
    public bool isDead;
}

[Serializable]
public class VRMovePacket
{
    public string type = "MOVE_VR";
    public float x, y, z, hx, hy, hz, hyaw, hpitch, hroll;
    public float lhx, lhy, lhz, rhx, rhy, rhz, ryaw, rpitch;
}

[Serializable]
public class SnapshotPacket
{
    public string type;
    public PlayerStateData[] players;
    public MobSnapshotData[] mobs;
}

[Serializable]
public class PlayerLeftPacket
{
    public string type;
    public string id;
}

[Serializable]
public class HitEventPacket
{
    public string type;
    public string targetId;
    public string attackerId;
    public int currentHp;
    public int damage;
    public float knockbackX;
    public float knockbackZ;
}

[Serializable]
public class PlayerDiedPacket
{
    public string type;
    public string victimId;
    public string killerId;
    public int killerScore;
}

[Serializable]
public class RespawnPacket
{
    public string type;
    public string id;
    public float x, y, z;
    public int hp;
}

[Serializable]
public class SpawnProjectilePacket
{
    public string type;
    public string id;
    public float x, y, z;
    public float dx, dy, dz;
    public float speed;
}

[Serializable]
public class DestroyProjectilePacket
{
    public string type;
    public string id;
    public float x, y, z;
    public string reason;
}

[Serializable]
public class MobEventPacket
{
    public string type;
    public string mobId;
    public int hp;
    public float duration;
}

[Serializable]
public class MobLootPacket
{
    public string type;
    public string mobId;
    public string lootId;
    public string lootType;
    public float x, y, z;
}

[Serializable]
public class LootRemovedPacket
{
    public string type;
    public string lootId;
    public string reason;
    public string pickedBy;
}

public class NetworkClient : MonoBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject localPlayerPrefab;
    [Header("Quest 3 / OpenXR")]
    [SerializeField] private GameObject vrPlayerPrefab;
    [SerializeField] private bool simulateVRInEditor;
    [SerializeField] private GameObject networkPlayerPrefab;

    [Header("Лабиринт")]
    [SerializeField] private MazeBuilder mazeBuilder;

    [Header("Миникарта")]
    [SerializeField] private MinimapBaker minimapBaker;
    [SerializeField] private MinimapMarkersManager markersManager;
    [SerializeField] private Transform mazeRoot;

    [Header("Боевой интерфейс")]
    [SerializeField] private CombatHUD combatHUD;
    [SerializeField] private DamageFlash damageFlash;

    [Header("Снаряды")]
    [SerializeField] private ProjectileManager projectileManager;

    [Header("Мобы")]
    [SerializeField] private ClientMobManager clientMobManager;

    private WebSocket websocket;
    private string myClientId;
    private GameObject localPlayerInstance;
    private LocalPlayerController localController;
    private VRPlayerController vrLocalController;
    private VRWristHUD wristHUD;
    private readonly Dictionary<string, NetworkPlayer> remotePlayers = new Dictionary<string, NetworkPlayer>();

    private float sendTimer;
    private float respawnCountdown;
    private bool waitingForRespawn;
    private const float SendInterval = 0.05f;
    private bool IsVR => (Application.platform == RuntimePlatform.Android && !Application.isEditor) || simulateVRInEditor;

    public Transform LocalPlayerTransform => localPlayerInstance != null ? localPlayerInstance.transform : null;
    public IReadOnlyDictionary<string, NetworkPlayer> RemotePlayers => remotePlayers;

    private async void Start()
    {
        string serverUrl = $"ws://{LobbyManager.ServerIP}:{LobbyManager.ServerPort}";
        Debug.Log($"[NetworkClient] Подключение к {serverUrl}...");

        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += async () =>
        {
            Debug.Log("[NetworkClient] Соединение открыто! Передача имени из лобби...");
            await websocket.SendText(JsonUtility.ToJson(new JoinPacket { name = LobbyManager.Nickname }));
        };

        websocket.OnMessage += bytes =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            HandleServerMessage(json);
        };
        websocket.OnError += err => Debug.LogError($"[NetworkClient] Ошибка: {err}");
        websocket.OnClose += code => Debug.LogWarning($"[NetworkClient] Соединение закрыто. Код: {code}");

        try { await websocket.Connect(); }
        catch (Exception ex) { Debug.LogError($"[NetworkClient] Не удалось подключиться: {ex.Message}"); }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null) websocket.DispatchMessageQueue();
#endif
        if (waitingForRespawn)
        {
            respawnCountdown -= Time.deltaTime;
            if (combatHUD != null) combatHUD.UpdateRespawnTimer(respawnCountdown);
        }
        if (localPlayerInstance == null || websocket == null || websocket.State != WebSocketState.Open) return;
        sendTimer += Time.deltaTime;
        if (sendTimer >= SendInterval)
        {
            sendTimer = 0f;
            SendMovementUpdate();
        }
    }

    private void HandleServerMessage(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        PacketHeader header;
        try { header = JsonUtility.FromJson<PacketHeader>(json); }
        catch (Exception ex) { Debug.LogWarning($"[NetworkClient] Неверный JSON: {ex.Message}"); return; }
        if (header == null || string.IsNullOrEmpty(header.type)) return;

        switch (header.type)
        {
            case "INIT_WORLD":
            {
                InitWorldPacket packet = JsonUtility.FromJson<InitWorldPacket>(json);
                myClientId = packet.id;
                if (mazeBuilder != null) mazeBuilder.BuildFromNetworkData(packet.walls);
                if (minimapBaker != null && mazeRoot != null) minimapBaker.Bake(mazeRoot);
                if (packet.spawnPos == null || packet.spawnPos.Length < 3) break;
                Vector3 spawnPos = new Vector3(packet.spawnPos[0], packet.spawnPos[1], packet.spawnPos[2]);
                SpawnLocalPlayer(spawnPos);
                if (clientMobManager != null && packet.loot != null)
                {
                    foreach (var loot in packet.loot)
                        clientMobManager.ShowLoot(loot.lootId, loot.lootType,
                            new Vector3(loot.x, loot.y, loot.z));
                }
                if (markersManager != null && localPlayerInstance != null)
                    markersManager.RegisterLocalPlayer(localPlayerInstance.transform);
                if (wristHUD != null)
                    wristHUD.Bind(minimapBaker, minimapBaker != null ? minimapBaker.MapTexture : null,
                        localPlayerInstance.transform, remotePlayers);
                break;
            }

            case "SNAPSHOT":
            {
                SnapshotPacket snapshot = JsonUtility.FromJson<SnapshotPacket>(json);
                if (snapshot.players != null) ProcessWorldSnapshot(snapshot);
                if (clientMobManager != null && snapshot.mobs != null)
                    clientMobManager.ProcessMobSnapshots(snapshot.mobs);
                break;
            }

            case "PLAYER_LEFT":
                RemoveRemotePlayer(JsonUtility.FromJson<PlayerLeftPacket>(json).id);
                break;

            case "MOB_HIT":
            {
                MobEventPacket mobHit = JsonUtility.FromJson<MobEventPacket>(json);
                if (clientMobManager != null) clientMobManager.UpdateMobHp(mobHit.mobId, mobHit.hp);
                break;
            }

            case "MOB_DIED":
            case "MOB_EXPLODED":
            {
                MobEventPacket mobDead = JsonUtility.FromJson<MobEventPacket>(json);
                if (clientMobManager != null) clientMobManager.RemoveMob(mobDead.mobId);
                break;
            }

            case "MOB_ARMOR_BLOCKED":
            {
                MobEventPacket armor = JsonUtility.FromJson<MobEventPacket>(json);
                if (clientMobManager != null) clientMobManager.TriggerArmor(armor.mobId);
                break;
            }

            case "MOB_PREPARE_ATTACK":
            {
                MobEventPacket prepare = JsonUtility.FromJson<MobEventPacket>(json);
                if (clientMobManager != null) clientMobManager.TriggerPrepare(prepare.mobId);
                break;
            }

            case "MOB_COUNTDOWN":
            {
                MobEventPacket countdown = JsonUtility.FromJson<MobEventPacket>(json);
                if (clientMobManager != null)
                    clientMobManager.TriggerCountdown(countdown.mobId, countdown.duration);
                break;
            }

            case "MOB_LOOT":
            {
                MobLootPacket loot = JsonUtility.FromJson<MobLootPacket>(json);
                if (clientMobManager != null)
                    clientMobManager.ShowLoot(loot.lootId, loot.lootType,
                        new Vector3(loot.x, loot.y, loot.z));
                break;
            }

            case "LOOT_REMOVED":
            {
                LootRemovedPacket removed = JsonUtility.FromJson<LootRemovedPacket>(json);
                if (clientMobManager != null)
                    clientMobManager.RemoveLoot(removed.lootId);
                break;
            }

            case "HIT_EVENT":
            {
                HitEventPacket hit = JsonUtility.FromJson<HitEventPacket>(json);
                if (hit.targetId == myClientId)
                {
                    if (damageFlash != null) damageFlash.TriggerFlash();
                    if (combatHUD != null) combatHUD.UpdateHealth(hit.currentHp);
                    if (wristHUD != null) wristHUD.UpdateHealth(hit.currentHp);
                    if (localPlayerInstance != null &&
                        (hit.knockbackX != 0f || hit.knockbackZ != 0f))
                    {
                        CharacterController cc = localPlayerInstance.GetComponent<CharacterController>();
                        if (cc != null)
                            cc.Move(new Vector3(hit.knockbackX, 0f, hit.knockbackZ));
                    }
                    Debug.Log($"[БОЙ] Получен урон: {hit.damage}. Осталось HP: {hit.currentHp}");
                }
                else if (remotePlayers.TryGetValue(hit.targetId, out NetworkPlayer victim))
                    victim.OnHitTaken(hit.currentHp);
                break;
            }

            case "PLAYER_DIED":
            {
                PlayerDiedPacket died = JsonUtility.FromJson<PlayerDiedPacket>(json);
                if (died.victimId == myClientId)
                {
                    waitingForRespawn = true;
                    respawnCountdown = 3f;
                    if (combatHUD != null)
                    {
                        combatHUD.UpdateHealth(0);
                        combatHUD.ShowDeathScreen(true, respawnCountdown);
                    }
                    if (localController != null) localController.enabled = false;
                    if (vrLocalController != null) vrLocalController.enabled = false;
                    if (wristHUD != null) wristHUD.UpdateHealth(0);
                }
                else if (remotePlayers.TryGetValue(died.victimId, out NetworkPlayer remoteVictim))
                {
                    remoteVictim.SetDeadState(true);
                    if (markersManager != null) markersManager.UnregisterRemotePlayer(died.victimId);
                }
                if (died.killerId == myClientId && combatHUD != null)
                    combatHUD.UpdateScore(died.killerScore);
                break;
            }

            case "PLAYER_RESPAWN":
            {
                RespawnPacket respawn = JsonUtility.FromJson<RespawnPacket>(json);
                Vector3 pos = new Vector3(respawn.x, respawn.y, respawn.z);
                if (respawn.id == myClientId)
                {
                    if (localPlayerInstance != null)
                    {
                        CharacterController cc = localPlayerInstance.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        localPlayerInstance.transform.position = pos;
                        if (cc != null) cc.enabled = true;
                        if (vrLocalController != null) vrLocalController.TeleportTo(pos);
                    }
                    if (localController != null) localController.enabled = true;
                    if (vrLocalController != null) vrLocalController.enabled = true;
                    if (wristHUD != null) wristHUD.UpdateHealth(respawn.hp);
                    waitingForRespawn = false;
                    if (combatHUD != null)
                    {
                        combatHUD.UpdateHealth(respawn.hp);
                        combatHUD.ShowDeathScreen(false);
                    }
                }
                else if (remotePlayers.TryGetValue(respawn.id, out NetworkPlayer remote))
                {
                    remote.Teleport(pos);
                    remote.SetDeadState(false);
                    remote.UpdateHealth(respawn.hp);
                    if (markersManager != null) markersManager.RegisterRemotePlayer(respawn.id, remote.transform);
                }
                break;
            }

            case "ATTACK_DODGED":
                Debug.Log("[VR] Физический уворот от удара Танка!");
                break;

            case "SPAWN_PROJECTILE":
            {
                SpawnProjectilePacket p = JsonUtility.FromJson<SpawnProjectilePacket>(json);
                if (projectileManager != null)
                    projectileManager.SpawnVisualProjectile(p.id, new Vector3(p.x, p.y, p.z), new Vector3(p.dx, p.dy, p.dz), p.speed);
                break;
            }

            case "DESTROY_PROJECTILE":
            {
                DestroyProjectilePacket p = JsonUtility.FromJson<DestroyProjectilePacket>(json);
                if (projectileManager != null)
                    projectileManager.DestroyVisualProjectile(p.id, new Vector3(p.x, p.y, p.z));
                break;
            }
        }
    }

    private void SpawnLocalPlayer(Vector3 spawnPos)
    {
        if (localPlayerInstance != null) return;
        if (IsVR)
        {
            GameObject prefab = vrPlayerPrefab != null ? vrPlayerPrefab : Resources.Load<GameObject>("VR_LocalPlayer");
            localPlayerInstance = prefab != null
                ? Instantiate(prefab, spawnPos, Quaternion.identity) : VRRigFactory.Create();
            localPlayerInstance.transform.position = spawnPos;
            vrLocalController = localPlayerInstance.GetComponent<VRPlayerController>();
            wristHUD = localPlayerInstance.GetComponentInChildren<VRWristHUD>(true);
            if (vrLocalController != null)
            {
                vrLocalController.OnMeleeAttackTriggered += SendMeleeAttackRequest;
                vrLocalController.OnRangedShootTriggered += SendRangedAttackRequest;
            }
            // Старый HUD остается источником HP/счёта, но не рисуется перед глазами.
            if (combatHUD != null)
            {
                Canvas screenCanvas = combatHUD.GetComponentInParent<Canvas>();
                if (screenCanvas != null) screenCanvas.enabled = false;
            }
        }
        else
        {
            localPlayerInstance = Instantiate(localPlayerPrefab, spawnPos, Quaternion.identity);
            localController = localPlayerInstance.GetComponent<LocalPlayerController>();
        }
        if (localController != null)
        {
            localController.OnAttackInputPressed += SendMeleeAttackRequest;
            localController.OnRangedAttackInputPressed += SendRangedAttackRequest;
        }
        if (combatHUD != null)
        {
            combatHUD.UpdateHealth(100);
            combatHUD.UpdateScore(0);
            combatHUD.ShowDeathScreen(false);
        }
    }

    private void ProcessWorldSnapshot(SnapshotPacket snapshot)
    {
        if (string.IsNullOrEmpty(myClientId)) return;
        foreach (PlayerStateData data in snapshot.players)
        {
            if (data.id == myClientId)
            {
                if (combatHUD != null)
                {
                    combatHUD.UpdateHealth(data.hp, data.maxHp > 0 ? data.maxHp : 100);
                    combatHUD.UpdateScore(data.score);
                }
                if (wristHUD != null) wristHUD.UpdateHealth(data.hp, data.maxHp > 0 ? data.maxHp : 100);
                continue;
            }
            Vector3 pos = new Vector3(data.x, data.y, data.z);
            if (!remotePlayers.TryGetValue(data.id, out NetworkPlayer np))
            {
                GameObject remoteObj = Instantiate(networkPlayerPrefab, pos, Quaternion.Euler(0f, data.yaw, 0f));
                np = remoteObj.GetComponent<NetworkPlayer>();
                if (np == null) { Destroy(remoteObj); continue; }
                np.Initialize(data.name, pos, data.hp);
                remotePlayers.Add(data.id, np);
                if (markersManager != null && !data.isDead)
                    markersManager.RegisterRemotePlayer(data.id, remoteObj.transform);
            }
            if (data.isVR)
                np.UpdateVRNetworkState(pos,
                    new Vector3(data.hx, data.hy, data.hz),
                    Quaternion.Euler(data.hpitch, data.hyaw, data.hroll),
                    new Vector3(data.lhx, data.lhy, data.lhz),
                    new Vector3(data.rhx, data.rhy, data.rhz),
                    Quaternion.Euler(data.rpitch, data.ryaw, 0f));
            else
                np.UpdateNetworkState(pos, data.yaw, data.pitch, data.crouch);
            np.UpdateHealth(data.hp);
            np.SetDeadState(data.isDead);
        }
    }

    private void RemoveRemotePlayer(string id)
    {
        if (!remotePlayers.TryGetValue(id, out NetworkPlayer player)) return;
        if (markersManager != null) markersManager.UnregisterRemotePlayer(id);
        Destroy(player.gameObject);
        remotePlayers.Remove(id);
    }

    private async void SendMeleeAttackRequest() => await SendSimpleRequest("ATTACK_MELEE");
    private async void SendRangedAttackRequest()
    {
        if (websocket == null || websocket.State != WebSocketState.Open ||
            localPlayerInstance == null) return;

        // В VR снаряд вылетает из правого контроллера по направлению его указателя.
        if (vrLocalController != null && vrLocalController.RightHandTransform != null)
        {
            Transform hand = vrLocalController.RightHandTransform;
            Vector3 from = hand.position + hand.forward * 0.25f;
            Vector3 forward = hand.forward.normalized;
            await websocket.SendText(JsonUtility.ToJson(new RangedShotPacket {
                x = from.x, y = from.y, z = from.z,
                dx = forward.x, dy = forward.y, dz = forward.z
            }));
            return;
        }
        Camera playerCamera = localPlayerInstance.GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            await SendSimpleRequest("FIRE_RANGED");
            return;
        }

        // Стреляем точно по лучу из центра экрана.
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 origin = ray.origin + ray.direction * 0.25f;
        Vector3 dir = ray.direction.normalized;
        RangedShotPacket shot = new RangedShotPacket
        {
            x = origin.x, y = origin.y, z = origin.z,
            dx = dir.x, dy = dir.y, dz = dir.z
        };
        await websocket.SendText(JsonUtility.ToJson(shot));
    }

    private async System.Threading.Tasks.Task SendSimpleRequest(string type)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(JsonUtility.ToJson(new SimpleRequest { type = type }));
    }

    private async void SendMovementUpdate()
    {
        if (websocket == null || websocket.State != WebSocketState.Open) return;
        if (vrLocalController != null)
        {
            Transform tr = vrLocalController.transform;
            Transform head = vrLocalController.HeadTransform;
            Transform left = vrLocalController.LeftHandTransform;
            Transform right = vrLocalController.RightHandTransform;
            if (head == null || left == null || right == null) return;
            Vector3 h = tr.InverseTransformPoint(head.position);
            Vector3 l = tr.InverseTransformPoint(left.position);
            Vector3 r = tr.InverseTransformPoint(right.position);
            Quaternion headRot = Quaternion.Inverse(tr.rotation) * head.rotation;
            Quaternion rightRot = Quaternion.Inverse(tr.rotation) * right.rotation;
            Vector3 he = headRot.eulerAngles;
            Vector3 re = rightRot.eulerAngles;
            VRMovePacket packet = new VRMovePacket {
                x = tr.position.x, y = tr.position.y, z = tr.position.z,
                hx = h.x, hy = h.y, hz = h.z,
                hyaw = he.y, hpitch = he.x, hroll = he.z,
                lhx = l.x, lhy = l.y, lhz = l.z,
                rhx = r.x, rhy = r.y, rhz = r.z,
                ryaw = re.y, rpitch = re.x
            };
            await websocket.SendText(JsonUtility.ToJson(packet));
            return;
        }
        if (localController == null) return;
        Transform tr = localPlayerInstance.transform;
        CultureInfo ci = CultureInfo.InvariantCulture;
        string moveJson = "{\"type\":\"MOVE\"," +
            "\"x\":" + tr.position.x.ToString("F2", ci) + "," +
            "\"y\":" + tr.position.y.ToString("F2", ci) + "," +
            "\"z\":" + tr.position.z.ToString("F2", ci) + "," +
            "\"yaw\":" + tr.eulerAngles.y.ToString("F1", ci) + "," +
            "\"pitch\":" + localController.PitchAngle.ToString("F1", ci) + "," +
            "\"crouch\":" + (localController.IsCrouching ? "true" : "false") + "}";
        await websocket.SendText(moveJson);
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.Close();
    }

    private void OnDestroy()
    {
        if (localController != null)
        {
            localController.OnAttackInputPressed -= SendMeleeAttackRequest;
            localController.OnRangedAttackInputPressed -= SendRangedAttackRequest;
        }
        if (vrLocalController != null)
        {
            vrLocalController.OnMeleeAttackTriggered -= SendMeleeAttackRequest;
            vrLocalController.OnRangedShootTriggered -= SendRangedAttackRequest;
        }
        if (projectileManager != null) projectileManager.ClearAll();
    }
}
