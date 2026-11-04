const { WebSocketServer } = require('ws');

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

const wss = new WebSocketServer({ port: PORT });

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

// Генерация карты при запуске сервера
const globalWalls = generateArenaMaze(COLS, ROWS, CELL_SIZE);

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
        walls: globalWalls
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
                        [session.state.x, session.state.y, session.state.z,
                         session.state.yaw, session.state.pitch] = values;
                        session.state.crouch = Boolean(data.crouch);
                    }
                }
            }

            if (data.type === 'ATTACK_MELEE') handleMeleeAttack(clientId);
            if (data.type === 'FIRE_RANGED') handleRangedAttack(clientId);
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
    updateProjectiles(1 / TICK_RATE);
    if (players.size === 0) return;

    const snapshot = {
        type: 'SNAPSHOT',
        players: Array.from(players.values()).map(p => p.state)
    };

    broadcast(snapshot);
}, 1000 / TICK_RATE);

function broadcast(packet) {
    const rawData = JSON.stringify(packet);
    for (const { ws } of players.values()) {
        if (ws.readyState === 1) { // WebSocket.OPEN
            ws.send(rawData);
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
            break;
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
function handleRangedAttack(attackerId) {
    const attacker = players.get(attackerId);
    if (!attacker || attacker.state.isDead) return;
    const now = Date.now();
    if (now - attacker.lastRangedTime < RANGED_COOLDOWN_MS) return;
    attacker.lastRangedTime = now;

    const yawRad = attacker.state.yaw * Math.PI / 180;
    const pitchRad = attacker.state.pitch * Math.PI / 180;
    const p = {
        id: 'proj_' + Math.random().toString(36).slice(2, 10),
        ownerId: attackerId,
        x: attacker.state.x,
        y: attacker.state.y + 1.5,
        z: attacker.state.z,
        dx: Math.sin(yawRad) * Math.cos(pitchRad),
        dy: -Math.sin(pitchRad),
        dz: Math.cos(yawRad) * Math.cos(pitchRad),
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
