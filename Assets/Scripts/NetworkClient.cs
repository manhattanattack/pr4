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
public class PlayerStateData
{
    public string id;
    public string name;
    public float x, y, z;
    public float yaw, pitch;
    public bool crouch;
    public int hp, maxHp, score;
    public bool isDead;
}

[Serializable]
public class SnapshotPacket
{
    public string type;
    public PlayerStateData[] players;
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

public class NetworkClient : MonoBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject localPlayerPrefab;
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

    private WebSocket websocket;
    private string myClientId;
    private GameObject localPlayerInstance;
    private LocalPlayerController localController;
    private readonly Dictionary<string, NetworkPlayer> remotePlayers = new Dictionary<string, NetworkPlayer>();

    private float sendTimer;
    private float respawnCountdown;
    private bool waitingForRespawn;
    private const float SendInterval = 0.05f;

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
                if (markersManager != null && localPlayerInstance != null)
                    markersManager.RegisterLocalPlayer(localPlayerInstance.transform);
                break;
            }

            case "SNAPSHOT":
            {
                SnapshotPacket snapshot = JsonUtility.FromJson<SnapshotPacket>(json);
                if (snapshot.players != null) ProcessWorldSnapshot(snapshot);
                break;
            }

            case "PLAYER_LEFT":
                RemoveRemotePlayer(JsonUtility.FromJson<PlayerLeftPacket>(json).id);
                break;

            case "HIT_EVENT":
            {
                HitEventPacket hit = JsonUtility.FromJson<HitEventPacket>(json);
                if (hit.targetId == myClientId)
                {
                    if (damageFlash != null) damageFlash.TriggerFlash();
                    if (combatHUD != null) combatHUD.UpdateHealth(hit.currentHp);
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
                    }
                    if (localController != null) localController.enabled = true;
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
        localPlayerInstance = Instantiate(localPlayerPrefab, spawnPos, Quaternion.identity);
        localController = localPlayerInstance.GetComponent<LocalPlayerController>();
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
    private async void SendRangedAttackRequest() => await SendSimpleRequest("FIRE_RANGED");

    private async System.Threading.Tasks.Task SendSimpleRequest(string type)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(JsonUtility.ToJson(new SimpleRequest { type = type }));
    }

    private async void SendMovementUpdate()
    {
        if (localController == null || websocket == null || websocket.State != WebSocketState.Open) return;
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
        if (projectileManager != null) projectileManager.ClearAll();
    }
}
