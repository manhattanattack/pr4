const { WebSocketServer } = require('ws');
const MobManager = require('./MobManager');

const PORT = 7777;
const TICK_RATE = 20; // 20 раз в секунду (каждые 50 мс)

const COLS = 12;
const ROWS = 12;
const CELL_SIZE = 4;

// ===== ГЕНЕРАТОР ЛАБИРИНТА =====

function generateArenaMaze(cols = 12, rows = 12, cellSize = 4) {
    const grid = [];
    for (let r = 0; r < rows; r++) {
        grid[r] = [];
        for (let c = 0; c < cols; c++) {
            grid[r][c] = { north: true, south: true, east: true, west: true, visited: false };
        }
    }

    // 1. Построение базовой связной структуры (обход в глубину)
    const stack = [];
    let current = { r: 0, c: 0 };
    grid[0][0].visited = true;

    while (true) {
        const neighbors = [];
        const { r, c } = current;

        if (r > 0 && !grid[r - 1][c].visited) neighbors.push({ r: r - 1, c, dir: 'north', opp: 'south' });
        if (r < rows - 1 && !grid[r + 1][c].visited) neighbors.push({ r: r + 1, c, dir: 'south', opp: 'north' });
        if (c < cols - 1 && !grid[r][c + 1].visited) neighbors.push({ r, c: c + 1, dir: 'east', opp: 'west' });
        if (c > 0 && !grid[r][c - 1].visited) neighbors.push({ r, c: c - 1, dir: 'west', opp: 'east' });

        if (neighbors.length > 0) {
            const next = neighbors[Math.floor(Math.random() * neighbors.length)];
            grid[current.r][current.c][next.dir] = false;
            grid[next.r][next.c][next.opp] = false;
            grid[next.r][next.c].visited = true;
            stack.push(current);
            current = { r: next.r, c: next.c };
        } else if (stack.length > 0) {
            current = stack.pop();
        } else {
            break;
        }
    }

    // 2. Создание открытых комнат (удаление внутренних перегородок)
    // Центральная арена 3x3 ячейки
    carveOpenRoom(grid, Math.floor(rows / 2) - 1, Math.floor(cols / 2) - 1, 3, 3);
    // Дополнительные открытые зоны 2x2 ячейки
    carveOpenRoom(grid, 1, 1, 2, 2);
    carveOpenRoom(grid, rows - 3, cols - 3, 2, 2);

    // 3. Формирование кольцевых проходов (Braid): часть тупиков пробиваем
    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            let wallCount = 0;
            if (grid[r][c].north) wallCount++;
            if (grid[r][c].south) wallCount++;
            if (grid[r][c].east) wallCount++;
            if (grid[r][c].west) wallCount++;

            if (wallCount === 3 && Math.random() < 0.4) {
                const candidates = [];
                if (r > 0 && grid[r][c].north) candidates.push({ r1: r, c1: c, dir: 'north', r2: r - 1, c2: c, opp: 'south' });
                if (r < rows - 1 && grid[r][c].south) candidates.push({ r1: r, c1: c, dir: 'south', r2: r + 1, c2: c, opp: 'north' });
                if (c < cols - 1 && grid[r][c].east) candidates.push({ r1: r, c1: c, dir: 'east', r2: r, c2: c + 1, opp: 'west' });
                if (c > 0 && grid[r][c].west) candidates.push({ r1: r, c1: c, dir: 'west', r2: r, c2: c - 1, opp: 'east' });

                if (candidates.length > 0) {
                    const pick = candidates[Math.floor(Math.random() * candidates.length)];
                    grid[pick.r1][pick.c1][pick.dir] = false;
                    grid[pick.r2][pick.c2][pick.opp] = false;
                }
            }
        }
    }

    // 4. Перевод в массив мировых координат
    // Строка r растет вместе с Z, поэтому "north" (r - 1) лежит на стороне меньшего Z.
    const walls = [];
    const offsetX = -(cols * cellSize) / 2;
    const offsetZ = -(rows * cellSize) / 2;
    const half = cellSize / 2;

    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            const centerX = offsetX + c * cellSize + half;
            const centerZ = offsetZ + r * cellSize + half;

            if (grid[r][c].north) {
                walls.push({ x: Number(centerX.toFixed(2)), z: Number((centerZ - half).toFixed(2)), rot: 0 });
            }
            if (grid[r][c].west) {
                walls.push({ x: Number((centerX - half).toFixed(2)), z: Number(centerZ.toFixed(2)), rot: 90 });
            }
            if (r === rows - 1 && grid[r][c].south) {
                walls.push({ x: Number(centerX.toFixed(2)), z: Number((centerZ + half).toFixed(2)), rot: 0 });
            }
            if (c === cols - 1 && grid[r][c].east) {
                walls.push({ x: Number((centerX + half).toFixed(2)), z: Number(centerZ.toFixed(2)), rot: 90 });
            }
        }
    }

    return walls;
}

function carveOpenRoom(grid, startR, startC, h, w) {
    for (let r = startR; r < startR + h; r++) {
        for (let c = startC; c < startC + w; c++) {
            if (r + 1 < startR + h) {
                grid[r][c].south = false;
                grid[r + 1][c].north = false;
            }
            if (c + 1 < startC + w) {
                grid[r][c].east = false;
                grid[r][c + 1].west = false;
            }
        }
    }
}

// ===== СЕТЕВАЯ ЧАСТЬ =====

const wss = new WebSocketServer({ port: PORT, host: '0.0.0.0' });

// Таблица активных сессий: clientId -> { ws, state }
const players = new Map();

// Авторитарные параметры боя: урон и попадания считает только сервер.
const MELEE_RANGE = 2.2;
const MELEE_DAMAGE = 25;
const MELEE_COOLDOWN_MS = 600;
const COS_HALF_ANGLE_45 = Math.cos(Math.PI / 4);
const RANGED_DAMAGE = 20;
const PROJECTILE_SPEED = 18.0;
const PROJECTILE_LIFETIME_MS = 3000;
const RANGED_COOLDOWN_MS = 500;
const PLAYER_HIT_RADIUS = 0.7;
const activeProjectiles = new Map();

// Лут общий для всех игроков. Удаляется при подборе или через 20 секунд.
const activeLoot = new Map();
const LOOT_LIFETIME_MS = 20000;
const LOOT_PICKUP_RADIUS = 1.5;

// Генерация карты при запуске сервера
const globalWalls = generateArenaMaze(COLS, ROWS, CELL_SIZE);

// Мобы общие для всех игроков
const mobManager = new MobManager();
mobManager.initSpawns();
const navNodes = [];

const serverHelpers = {
    checkDeath: (target, killerId) => {
        if (target.state.hp > 0 || target.state.isDead) return;
        target.state.isDead = true;
        broadcast({
            type: 'PLAYER_DIED',
            victimId: target.state.id,
            killerId: killerId,
            killerScore: 0
        });
        setTimeout(() => {
            if (players.get(target.state.id) === target)
                respawnPlayer(target);
        }, 3000);
    }
};

// Центр центральной комнаты 3x3 (между колоннами, не внутри них)
const SPAWN_CENTER_X = -(COLS * CELL_SIZE) / 2 + (Math.floor(COLS / 2) - 1 + 1.5) * CELL_SIZE;
const SPAWN_CENTER_Z = -(ROWS * CELL_SIZE) / 2 + (Math.floor(ROWS / 2) - 1 + 1.5) * CELL_SIZE;

console.log('==========================================');
console.log(`[СЕРВЕР] Запущен на порту ${PORT}`);
console.log(`[СЕРВЕР] Карта создана. Сегментов стен: ${globalWalls.length}`);
console.log('Ожидание подключений студентов...');
console.log('==========================================');

wss.on('connection', (ws, req) => {
    // 1. Уникальный ID клиента
    const clientId = 'p_' + Math.random().toString(36).substr(2, 8);
    const clientIp = req.socket.remoteAddress;

    // 2. Точка спавна в центре открытой площадки (небольшой разброс)
    const spawnX = Number((SPAWN_CENTER_X + (Math.random() - 0.5) * 1.6).toFixed(2));
    const spawnZ = Number((SPAWN_CENTER_Z + (Math.random() - 0.5) * 1.6).toFixed(2));

    const initialPlayerState = {
        id: clientId,
        name: 'Player',
        x: spawnX,
        y: 0.0,
        z: spawnZ,
        yaw: 0.0,
        pitch: 0.0,
        crouch: false,
        hp: 100,
        maxHp: 100,
        isDead: false,
        score: 0
    };

    players.set(clientId, { ws, state: initialPlayerState, lastAttackTime: 0, lastRangedTime: 0 });
    console.log(`[ПОДКЛЮЧЕНИЕ] Клиент ${clientId} (IP: ${clientIp}) вошел в сессию`);

    // 3. Отправка клиенту структуры лабиринта и точки спавна
    ws.send(JSON.stringify({
        type: 'INIT_WORLD',
        id: clientId,
        spawnPos: [initialPlayerState.x, initialPlayerState.y, initialPlayerState.z],
        hp: initialPlayerState.hp,
        walls: globalWalls,
        loot: Array.from(activeLoot.values())
    }));

    // 4. Прием пакетов от Unity-клиента
    ws.on('message', (messageRaw) => {
        try {
            const data = JSON.parse(messageRaw.toString());

            // Регистрация никнейма из LobbyScene
            if (data.type === 'JOIN') {
                const session = players.get(clientId);
                if (session) {
                    session.state.name = data.name || 'Anonymous';
                    console.log(`[РЕГИСТРАЦИЯ] Игрок ${clientId} указал имя: "${session.state.name}"`);
                }
            }

            // Синхронизация движения и взгляда
            if (data.type === 'MOVE') {
                const session = players.get(clientId);
                if (session && !session.state.isDead) {
                    const fields = ['x', 'y', 'z', 'yaw', 'pitch'];
                    const values = fields.map(key => Number(data[key]));
                    if (values.every(Number.isFinite)) {
                        const moved = Math.hypot(values[0] - session.state.x,
                            values[2] - session.state.z) > 0.05;
                        [session.state.x, session.state.y, session.state.z,
                         session.state.yaw, session.state.pitch] = values;
                        session.state.crouch = Boolean(data.crouch);
                        if (moved && !session.state.crouch)
                            mobManager.emitNoiseEvent(values[0], values[2], 0.4);
                    }
                }
            }

            if (data.type === 'ATTACK_MELEE') handleMeleeAttack(clientId);
            if (data.type === 'FIRE_RANGED') handleRangedAttack(clientId, data);
        } catch (err) {
            console.error(`[ОШИБКА JSON] от ${clientId}: `, err.message);
        }
    });

    // 5. Отключение клиента
    ws.on('close', () => {
        const session = players.get(clientId);
        const playerName = session ? session.state.name : clientId;
        players.delete(clientId);
        for (const [projectileId, p] of activeProjectiles) {
            if (p.ownerId === clientId) destroyProjectile(projectileId, 'disconnect');
        }
        console.log(`[ОТКЛЮЧЕНИЕ] Игрок "${playerName}" (${clientId}) отключился`);

        // Оповещаем остальных клиентов об удалении аватара
        broadcast({ type: 'PLAYER_LEFT', id: clientId });
    });

    ws.on('error', (err) => {
        console.error(`[ОШИБКА СОКЕТА] ${clientId}: `, err.message);
    });
});

// Игровой цикл: каждые 50 мс рассылаем снапшот мира всем подключенным
setInterval(() => {
    const dt = 1.0 / TICK_RATE;
    mobManager.update(dt, players, globalWalls, navNodes, broadcast, serverHelpers);
    updateProjectiles(dt);
    updateLoot();
    if (players.size === 0) return;

    const snapshot = {
        type: 'SNAPSHOT',
        players: Array.from(players.values()).map(p => p.state),
        mobs: mobManager.getSnapshots()
    };

    broadcast(snapshot);
}, 1000 / TICK_RATE);

function broadcast(packet) {
    if (packet.type === 'MOB_LOOT') {
        const lootId = 'loot_' + packet.mobId;
        packet.lootId = lootId;
        activeLoot.set(lootId, {
            lootId: lootId, lootType: packet.lootType,
            x: packet.x, y: packet.y, z: packet.z,
            createdAt: Date.now()
        });
    }
    const rawData = JSON.stringify(packet);
    for (const { ws } of players.values()) {
        if (ws.readyState === 1) { // WebSocket.OPEN
            ws.send(rawData);
        }
    }
}


// Проверяем автоматический подбор и время жизни кубиков.
function updateLoot() {
    const now = Date.now();
    for (const [lootId, loot] of activeLoot) {
        if (now - loot.createdAt >= LOOT_LIFETIME_MS) {
            activeLoot.delete(lootId);
            broadcast({ type: 'LOOT_REMOVED', lootId: lootId, reason: 'timeout' });
            continue;
        }

        // Небольшая задержка, чтобы лут был виден после смерти моба.
        if (now - loot.createdAt < 500) continue;

        for (const player of players.values()) {
            if (player.state.isDead) continue;
            const dist = Math.hypot(player.state.x - loot.x, player.state.z - loot.z);
            if (dist <= LOOT_PICKUP_RADIUS) {
                activeLoot.delete(lootId);
                broadcast({ type: 'LOOT_REMOVED', lootId: lootId,
                    reason: 'picked', pickedBy: player.state.id });
                console.log('[ЛУТ] ' + player.state.name + ' подобрал ' + loot.lootType);
                break;
            }
        }
    }
}

// ===== БЛИЖНИЙ БОЙ =====
function handleMeleeAttack(attackerId) {
    const attacker = players.get(attackerId);
    if (!attacker || attacker.state.isDead) return;
    const now = Date.now();
    if (now - attacker.lastAttackTime < MELEE_COOLDOWN_MS) return;
    attacker.lastAttackTime = now;
    mobManager.emitNoiseEvent(attacker.state.x, attacker.state.z, 1.0);
    broadcast({ type: 'PLAYER_SWUNG', attackerId });

    const yawRad = attacker.state.yaw * Math.PI / 180;
    const forwardX = Math.sin(yawRad);
    const forwardZ = Math.cos(yawRad);

    // Попадание в первую подходящую цель (не наносим урон сквозь стены).
    for (const [targetId, target] of players) {
        if (targetId === attackerId || target.state.isDead) continue;
        const dx = target.state.x - attacker.state.x;
        const dz = target.state.z - attacker.state.z;
        const distance = Math.hypot(dx, dz);
        if (distance <= 0.001 || distance > MELEE_RANGE) continue;
        const dot = forwardX * dx / distance + forwardZ * dz / distance;
        if (dot >= COS_HALF_ANGLE_45) {
            applyDamage(attacker, target, MELEE_DAMAGE);
            return;
        }
    }

    // Если не попали по игроку, проверяем мобов.
    for (const mob of mobManager.mobs.values()) {
        if (mob.isDead) continue;
        const dx = mob.x - attacker.state.x;
        const dz = mob.z - attacker.state.z;
        const dist = Math.hypot(dx, dz);
        if (dist <= 0.001 || dist > MELEE_RANGE) continue;
        const dot = forwardX * dx / dist + forwardZ * dz / dist;
        if (dot >= COS_HALF_ANGLE_45) {
            mobManager.applyDamageToMob(mob.id, MELEE_DAMAGE,
                dx / dist, dz / dist, attacker, broadcast);
            return;
        }
    }
}

// ===== УРОН, ФРАГИ И ВОЗРОЖДЕНИЕ =====
function applyDamage(attacker, target, damage) {
    if (!attacker || !target || target.state.isDead) return;
    target.state.hp = Math.max(0, target.state.hp - damage);
    broadcast({
        type: 'HIT_EVENT',
        targetId: target.state.id,
        attackerId: attacker.state.id,
        currentHp: target.state.hp,
        damage
    });
    console.log(`[УРОН] ${attacker.state.name} -> ${target.state.name}: -${damage} HP (осталось ${target.state.hp})`);
    if (target.state.hp > 0) return;

    target.state.isDead = true;
    attacker.state.score += 1;
    broadcast({
        type: 'PLAYER_DIED',
        victimId: target.state.id,
        killerId: attacker.state.id,
        killerScore: attacker.state.score
    });
    console.log(`[ГИБЕЛЬ] ${target.state.name} погиб. Побед ${attacker.state.name}: ${attacker.state.score}`);

    setTimeout(() => {
        // За время таймера клиент мог отключиться.
        if (players.get(target.state.id) !== target) return;
        respawnPlayer(target);
    }, 3000);
}

function respawnPlayer(session) {
    session.state.hp = session.state.maxHp;
    session.state.isDead = false;
    session.state.x = Number((SPAWN_CENTER_X + (Math.random() - 0.5) * 8).toFixed(2));
    session.state.z = Number((SPAWN_CENTER_Z + (Math.random() - 0.5) * 8).toFixed(2));
    session.state.y = 0;
    broadcast({
        type: 'PLAYER_RESPAWN',
        id: session.state.id,
        x: session.state.x,
        y: session.state.y,
        z: session.state.z,
        hp: session.state.hp
    });
    console.log(`[РЕСПАВН] ${session.state.name} возродился`);
}

// ===== ДАЛЬНИЙ БОЙ =====
function handleRangedAttack(attackerId, shot) {
    const attacker = players.get(attackerId);
    if (!attacker || attacker.state.isDead) return;
    const now = Date.now();
    if (now - attacker.lastRangedTime < RANGED_COOLDOWN_MS) return;
    attacker.lastRangedTime = now;
    mobManager.emitNoiseEvent(attacker.state.x, attacker.state.z, 1.0);

    const yawRad = attacker.state.yaw * Math.PI / 180;
    const pitchRad = attacker.state.pitch * Math.PI / 180;
    let origin = { x: attacker.state.x, y: attacker.state.y + 1.5, z: attacker.state.z };
    let dir = {
        x: Math.sin(yawRad) * Math.cos(pitchRad),
        y: -Math.sin(pitchRad),
        z: Math.cos(yawRad) * Math.cos(pitchRad)
    };

    if (shot) {
        const values = [shot.x, shot.y, shot.z, shot.dx, shot.dy, shot.dz].map(Number);
        const distance = Math.hypot(values[0] - attacker.state.x,
            values[1] - (attacker.state.y + 1.5), values[2] - attacker.state.z);
        const dirLength = Math.hypot(values[3], values[4], values[5]);
        // Не разрешаем клиенту создать снаряд далеко от своего персонажа.
        if (values.every(Number.isFinite) && distance <= 2.0 && dirLength > 0.5) {
            origin = { x: values[0], y: values[1], z: values[2] };
            dir = { x: values[3] / dirLength,
                y: values[4] / dirLength, z: values[5] / dirLength };
        }
    }

    const p = {
        id: 'proj_' + Math.random().toString(36).slice(2, 10),
        ownerId: attackerId,
        x: origin.x, y: origin.y, z: origin.z,
        dx: dir.x, dy: dir.y, dz: dir.z,
        createdAt: now
    };
    activeProjectiles.set(p.id, p);
    broadcast({
        type: 'SPAWN_PROJECTILE',
        id: p.id,
        x: p.x, y: p.y, z: p.z,
        dx: p.dx, dy: p.dy, dz: p.dz,
        speed: PROJECTILE_SPEED
    });
}

// Колонны в Unity стоят на каждом пересечении сетки, а не в globalWalls.
// Wall_Pillar.prefab: размер по X/Z = 0.8, высота = 3.2, центр Y = 1.5.
function projectileHitsPillar(p) {
    if (p.y < -0.1 || p.y > 3.1) return false;

    const offsetX = -(COLS * CELL_SIZE) / 2;
    const offsetZ = -(ROWS * CELL_SIZE) / 2;
    const c = Math.round((p.x - offsetX) / CELL_SIZE);
    const r = Math.round((p.z - offsetZ) / CELL_SIZE);
    if (c < 0 || c > COLS || r < 0 || r > ROWS) return false;

    const pillarX = offsetX + c * CELL_SIZE;
    const pillarZ = offsetZ + r * CELL_SIZE;
    return Math.abs(p.x - pillarX) <= 0.4 &&
        Math.abs(p.z - pillarZ) <= 0.4;
}

function updateProjectiles(dt) {
    const now = Date.now();
    for (const [id, p] of activeProjectiles) {
        if (now - p.createdAt > PROJECTILE_LIFETIME_MS) {
            destroyProjectile(id, 'timeout');
            continue;
        }

        // Дробим полёт на короткие отрезки: при 20 тиках/с
        // один шаг в 0.9 м иначе может перескочить стену толщиной 0.4 м.
        const steps = Math.max(1, Math.ceil(PROJECTILE_SPEED * dt / 0.15));
        const distancePerStep = PROJECTILE_SPEED * dt / steps;
        let destroyed = false;

        for (let step = 0; step < steps && !destroyed; step++) {
            p.x += p.dx * distancePerStep;
            p.y += p.dy * distancePerStep;
            p.z += p.dz * distancePerStep;

            if (p.y <= 0.1 || p.y >= 10.0) {
                destroyProjectile(id, 'floor');
                break;
            }

            for (const wall of globalWalls) {
                const halfX = wall.rot === 0 ? 2.0 : 0.2;
                const halfZ = wall.rot === 0 ? 0.2 : 2.0;
                if (p.x >= wall.x - halfX && p.x <= wall.x + halfX &&
                    p.z >= wall.z - halfZ && p.z <= wall.z + halfZ &&
                    p.y <= 3.0) {
                    destroyProjectile(id, 'wall');
                    destroyed = true;
                    break;
                }
            }
            if (destroyed) break;

            if (projectileHitsPillar(p)) {
                destroyProjectile(id, 'pillar');
                destroyed = true;
                break;
            }

            for (const [targetId, target] of players) {
                if (targetId === p.ownerId || target.state.isDead) continue;
                const distXZ = Math.hypot(target.state.x - p.x, target.state.z - p.z);
                const distY = Math.abs(target.state.y + 1.0 - p.y);
                if (distXZ <= PLAYER_HIT_RADIUS && distY <= 1.0) {
                    applyDamage(players.get(p.ownerId), target, RANGED_DAMAGE);
                    destroyProjectile(id, 'player');
                    destroyed = true;
                    break;
                }
            }
            if (destroyed) break;

            for (const mob of mobManager.mobs.values()) {
                if (mob.isDead) continue;
                const dist = Math.hypot(mob.x - p.x, mob.z - p.z);
                if (dist > 0.8 || Math.abs(mob.y + 1 - p.y) > 1.0) continue;

                // Направление удара: от игрока к мобу.
                const attacker = players.get(p.ownerId);
                const dx = mob.x - attacker.state.x;
                const dz = mob.z - attacker.state.z;
                const len = Math.hypot(dx, dz);
                if (len > 0.001) {
                    mobManager.applyDamageToMob(mob.id, RANGED_DAMAGE,
                        dx / len, dz / len, attacker, broadcast);
                }
                destroyProjectile(id, 'mob');
                destroyed = true;
                break;
            }
        }
    }
}

function destroyProjectile(id, reason) {
    const p = activeProjectiles.get(id);
    if (!p) return;
    activeProjectiles.delete(id);
    broadcast({
        type: 'DESTROY_PROJECTILE',
        id, x: p.x, y: p.y, z: p.z, reason
    });
}
