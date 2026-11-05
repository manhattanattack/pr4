using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using NativeWebSocket;

[Serializable]
public class PacketHeader { public string type; }

[Serializable]
public class JumpPadData { public float x, z, dx, dz; }

[Serializable]
public class JumpPadInitPacket { public JumpPadData[] jumpPads; }
[Serializable]
public class PowerupData { public string id, kind; public float x,z; public bool available; }
[Serializable]
public class PowerupSnapshotPacket { public PowerupData[] powerupPickups; }
[Serializable]
public class PowerupAcquiredPacket { public string playerId,kind; }
[Serializable]
public class ExplosionPacket { public float x,y,z,radius; }

[Serializable]
public class ImpulsePacket { public string type; public string playerId; public float x, y, z; }

[Serializable]
public class ChaosStatusPacket { public string type; public string playerId; public float speedMultiplier; }

[Serializable]
public class JoinPacket { public string type = "JOIN"; public string name; public int colorIndex; }

[Serializable]
public class SimpleRequest { public string type; }

[Serializable]
public class SelectWeaponPacket
{
    public string type = "SELECT_WEAPON";
    public string weapon;
}

[Serializable]
public class WeaponAcquiredPacket
{
    public string type;
    public string playerId;
    public string weapon;
    public int ammo;
    public string pickupId;
}

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
    public bool crouch;
    public int hp, maxHp, score;
    public int deaths, colorIndex;
    public string weapon;
    public int shotgunAmmo, smgAmmo, railgunAmmo, rocketAmmo;
    public int armor;
    public float speedSeconds, damageSeconds;
    public float shieldSeconds;
    public bool isDead;
}

[Serializable]
public class SnapshotPacket
{
    public string type;
    public PlayerStateData[] players;
    public WeaponPickupData[] weaponPickups;
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
    public string hitZone;
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
    public string hitZone;
}

[Serializable]
public class RespawnPacket
{
    public string type;
    public string id;
    public float x, y, z;
    public int hp;
    public float shieldSeconds;
}

[Serializable]
public class SpawnProjectilePacket
{
    public string type;
    public string id;
    public float x, y, z;
    public float dx, dy, dz;
    public float speed;
    public string weapon;
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
public class KillFeedPacket
{
    public string type;
    public string killerId, killerName;
    public string victimId, victimName;
    public string hitZone;
    public int killerColorIndex, victimColorIndex;
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
    [SerializeField] private ArenaScoreboard arenaScoreboard;
    [SerializeField] private ArenaWeaponPickups arenaWeaponPickups;

    [Header("Снаряды")]
    [SerializeField] private ProjectileManager projectileManager;

    private readonly Dictionary<string, GameObject> chaosPowerups = new Dictionary<string, GameObject>();
    private readonly List<GameObject> chaosPadVisuals = new List<GameObject>();
    private WebSocket websocket;
    private string myClientId;
    private GameObject localPlayerInstance;
    private LocalPlayerController localController;
    private readonly Dictionary<string, NetworkPlayer> remotePlayers = new Dictionary<string, NetworkPlayer>();

    private float sendTimer;
    private float respawnCountdown;
    private bool waitingForRespawn;
    private const float SendInterval = 0.05f;
    private string currentWeapon = "blaster";
    private float nextSmgShot;

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
            await websocket.SendText(JsonUtility.ToJson(new JoinPacket { name = LobbyManager.Nickname, colorIndex = LobbyManager.SkinColorIndex }));
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
        if (localController != null && localController.enabled)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SendWeaponSelection("blaster");
            if (Input.GetKeyDown(KeyCode.Alpha2)) SendWeaponSelection("shotgun");
            if (Input.GetKeyDown(KeyCode.Alpha3)) SendWeaponSelection("smg");
            if (Input.GetKeyDown(KeyCode.Alpha4)) SendWeaponSelection("railgun");
            if (Input.GetKeyDown(KeyCode.Alpha5)) SendWeaponSelection("rocket");

            // Только ПП стреляет очередью при удержании ПКМ.
            if (currentWeapon == "smg" && Input.GetMouseButton(1) &&
                !Input.GetMouseButtonDown(1) && Time.unscaledTime >= nextSmgShot)
                SendRangedAttackRequest();
        }
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
                if (mazeBuilder != null) mazeBuilder.BuildFromNetworkData(packet.walls, packet.cover, packet.cols, packet.rows, packet.cellSize);
                if (minimapBaker != null && mazeRoot != null) minimapBaker.Bake(mazeRoot);
                if (packet.spawnPos == null || packet.spawnPos.Length < 3) break;
                Vector3 spawnPos = new Vector3(packet.spawnPos[0], packet.spawnPos[1], packet.spawnPos[2]);
                SpawnLocalPlayer(spawnPos);
                CreateJumpPadVisuals(JsonUtility.FromJson<JumpPadInitPacket>(json).jumpPads);
                ApplyPowerups(JsonUtility.FromJson<PowerupSnapshotPacket>(json).powerupPickups);
                if (arenaWeaponPickups != null) arenaWeaponPickups.ApplySnapshot(packet.weaponPickups);
                if (combatHUD != null) combatHUD.UpdateSpawnShield(packet.shieldSeconds);
                if (markersManager != null && localPlayerInstance != null)
                    markersManager.RegisterLocalPlayer(localPlayerInstance.transform);
                break;
            }

            case "SNAPSHOT":
            {
                SnapshotPacket snapshot = JsonUtility.FromJson<SnapshotPacket>(json);
                if (arenaWeaponPickups != null) arenaWeaponPickups.ApplySnapshot(snapshot.weaponPickups);
                ApplyPowerups(JsonUtility.FromJson<PowerupSnapshotPacket>(json).powerupPickups);
                if (snapshot.players != null)
                {
                    ProcessWorldSnapshot(snapshot);
                    if (arenaScoreboard != null)
                        arenaScoreboard.SetPlayers(snapshot.players, myClientId);
                }
                break;
            }

            case "PLAYER_IMPULSE":
            {
                ImpulsePacket impulse = JsonUtility.FromJson<ImpulsePacket>(json);
                if (impulse.playerId == myClientId && localController != null)
                    localController.ApplyImpulse(new Vector3(impulse.x, impulse.y, impulse.z));
                break;
            }

            case "CHAOS_STATUS":
            {
                ChaosStatusPacket status = JsonUtility.FromJson<ChaosStatusPacket>(json);
                if (status.playerId == myClientId && localController != null)
                    localController.SetSpeedMultiplier(status.speedMultiplier);
                break;
            }

            case "POWERUP_ACQUIRED":
            {
                PowerupAcquiredPacket powerup = JsonUtility.FromJson<PowerupAcquiredPacket>(json);
                if (powerup.playerId == myClientId && combatHUD != null)
                    combatHUD.ShowPowerup(powerup.kind);
                break;
            }

            case "EXPLOSION":
            {
                ExplosionPacket explosion = JsonUtility.FromJson<ExplosionPacket>(json);
                CreateExplosionEffect(new Vector3(explosion.x,explosion.y,explosion.z),explosion.radius);
                break;
            }

            case "PLAYER_LEFT":
                RemoveRemotePlayer(JsonUtility.FromJson<PlayerLeftPacket>(json).id);
                break;

            case "KILL_FEED":
            {
                KillFeedPacket kill = JsonUtility.FromJson<KillFeedPacket>(json);
                if (arenaScoreboard != null) arenaScoreboard.AddKill(kill);
                break;
            }

            case "WEAPON_ACQUIRED":
            {
                WeaponAcquiredPacket pickup = JsonUtility.FromJson<WeaponAcquiredPacket>(json);
                if (pickup.playerId == myClientId && combatHUD != null)
                    combatHUD.ShowWeaponPickup(pickup.weapon);
                break;
            }

            case "HIT_EVENT":
            {
                HitEventPacket hit = JsonUtility.FromJson<HitEventPacket>(json);
                if (hit.targetId == myClientId)
                {
                    if (damageFlash != null) damageFlash.TriggerFlash();
                    if (combatHUD != null) combatHUD.UpdateHealth(hit.currentHp);
                    if (localPlayerInstance != null &&
                        (hit.knockbackX != 0f || hit.knockbackZ != 0f))
                    {
                        CharacterController cc = localPlayerInstance.GetComponent<CharacterController>();
                        if (cc != null)
                            cc.Move(new Vector3(hit.knockbackX, 0f, hit.knockbackZ));
                    }
                    Debug.Log($"[БОЙ] Урон {hit.damage} ({hit.hitZone}). HP: {hit.currentHp}");
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
                    if (localPlayerInstance != null)
                        ChaosRagdoll.Spawn(localPlayerInstance.transform.position,
                            localPlayerInstance.transform.rotation,
                            SkinColors.Get(LobbyManager.SkinColorIndex), died.hitZone == "explosion");
                    waitingForRespawn = true;
                    respawnCountdown = 3f;
                    if (combatHUD != null)
                    {
                        combatHUD.UpdateHealth(0);
                        combatHUD.ShowDeathScreen(true, respawnCountdown);
                    }
                    if (localController != null)
                    {
                        localController.ResetChaosMotion();
                        localController.enabled = false;
                    }
                    if (combatHUD != null)
                    {
                        combatHUD.UpdateSpawnShield(0f);
                        combatHUD.UpdateChaosStatus(0, 0f, 0f);
                    }
                }
                else if (remotePlayers.TryGetValue(died.victimId, out NetworkPlayer remoteVictim))
                {
                    ChaosRagdoll.Spawn(remoteVictim.transform.position,
                        remoteVictim.transform.rotation,
                        remoteVictim.VisualSkinColor, died.hitZone == "explosion");
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
                    if (localController != null)
                    {
                        localController.ResetChaosMotion();
                        localController.enabled = true;
                    }
                    waitingForRespawn = false;
                    if (combatHUD != null)
                    {
                        combatHUD.UpdateHealth(respawn.hp);
                        combatHUD.ShowDeathScreen(false);
                        combatHUD.UpdateSpawnShield(respawn.shieldSeconds);
                    }
                }
                else if (remotePlayers.TryGetValue(respawn.id, out NetworkPlayer remote))
                {
                    remote.Teleport(pos);
                    remote.SetDeadState(false);
                    remote.UpdateHealth(respawn.hp);
                    remote.SetSpawnShield(respawn.shieldSeconds);
                    if (markersManager != null) markersManager.RegisterRemotePlayer(respawn.id, remote.transform);
                }
                break;
            }

            case "SPAWN_PROJECTILE":
            {
                SpawnProjectilePacket p = JsonUtility.FromJson<SpawnProjectilePacket>(json);
                if (projectileManager != null)
                    projectileManager.SpawnVisualProjectile(p.id, new Vector3(p.x, p.y, p.z), new Vector3(p.dx, p.dy, p.dz), p.speed, p.weapon);
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
        Transform localBody = localPlayerInstance.transform.Find("BodyMesh");
        if (localBody != null)
        {
            Renderer bodyRenderer = localBody.GetComponent<Renderer>();
            if (bodyRenderer != null)
                bodyRenderer.material.color = SkinColors.Get(LobbyManager.SkinColorIndex);
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
                    combatHUD.UpdateSpawnShield(data.shieldSeconds);
                    combatHUD.UpdateWeapon(data.weapon, data.shotgunAmmo,
                        data.smgAmmo, data.railgunAmmo, data.rocketAmmo);
                    combatHUD.UpdateChaosStatus(data.armor, data.speedSeconds, data.damageSeconds);
                }
                currentWeapon = data.weapon;
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
            np.SetSkinColor(data.colorIndex);
            np.UpdateNetworkState(pos, data.yaw, data.pitch, data.crouch);
            np.UpdateHealth(data.hp);
            np.SetDeadState(data.isDead);
            np.SetSpawnShield(data.shieldSeconds);
        }
    }

    private void RemoveRemotePlayer(string id)
    {
        if (!remotePlayers.TryGetValue(id, out NetworkPlayer player)) return;
        if (markersManager != null) markersManager.UnregisterRemotePlayer(id);
        Destroy(player.gameObject);
        remotePlayers.Remove(id);
    }

    private async void SendWeaponSelection(string weapon)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(JsonUtility.ToJson(new SelectWeaponPacket { weapon = weapon }));
    }

    private async void SendMeleeAttackRequest() => await SendSimpleRequest("ATTACK_MELEE");
    private async void SendRangedAttackRequest()
    {
        if (websocket == null || websocket.State != WebSocketState.Open ||
            localPlayerInstance == null) return;

        nextSmgShot = Time.unscaledTime + 0.125f;
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

    private void ApplyPowerups(PowerupData[] pickups)
    {
        if (pickups == null) return;
        foreach (var pickup in pickups)
        {
            if (string.IsNullOrEmpty(pickup.id)) continue;
            if (!chaosPowerups.TryGetValue(pickup.id, out GameObject marker) || marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "ChaosPowerup_" + pickup.id;
                marker.transform.position = new Vector3(pickup.x,1.1f,pickup.z);
                marker.transform.localScale = Vector3.one * 0.85f;
                var col = marker.GetComponent<Collider>();
                if (col != null) Destroy(col);
                chaosPowerups[pickup.id] = marker;
            }
            // The server may reroll the type when this pickup respawns.
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = pickup.kind == "speed" ? Color.green :
                pickup.kind == "damage" ? Color.red : Color.cyan;
            marker.SetActive(pickup.available);
        }
    }

    private void CreateExplosionEffect(Vector3 position, float radius)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "ChaosExplosion";
        flash.transform.position = position;
        flash.transform.localScale = Vector3.one * Mathf.Max(1f,radius * 0.5f);
        var col = flash.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var renderer = flash.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(1f,0.4f,0.05f);
        Destroy(flash,0.22f);
    }

    private void CreateJumpPadVisuals(JumpPadData[] pads)
    {
        foreach (var old in chaosPadVisuals) if (old != null) Destroy(old);
        chaosPadVisuals.Clear();
        if (pads == null) return;
        foreach (var pad in pads)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ChaosJumpPad";
            marker.transform.position = new Vector3(pad.x, 0.08f, pad.z);
            marker.transform.localScale = new Vector3(2.8f, 0.12f, 2.8f);
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.1f, 1f, 0.5f);
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            chaosPadVisuals.Add(marker);
        }
    }

    private void OnDestroy()
    {
        if (localController != null)
        {
            localController.OnAttackInputPressed -= SendMeleeAttackRequest;
            localController.OnRangedAttackInputPressed -= SendRangedAttackRequest;
        }
        if (projectileManager != null) projectileManager.ClearAll();
        foreach (var pad in chaosPadVisuals) if (pad != null) Destroy(pad);
        foreach (var pickup in chaosPowerups.Values) if (pickup != null) Destroy(pickup);
    }
}
