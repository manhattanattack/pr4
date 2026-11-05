const { WebSocketServer } = require('ws');

const PORT = 7777;
const TICK_RATE = 20; // 20 раз в секунду (каждые 50 мс)

const COLS = 24;
const ROWS = 24;
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
    // Центральная площадь, четыре открытых двора и сквозные дороги.
    carveOpenRoom(grid, 8, 8, 8, 8);
    for (const r of [2, rows - 7])
        for (const c of [2, cols - 7]) carveOpenRoom(grid, r, c, 5, 5);
    for (let r = 0; r < rows - 1; r++) {
        for (const c of [11, 12]) {
            grid[r][c].south = false;
            grid[r + 1][c].north = false;
        }
    }
    for (let c = 0; c < cols - 1; c++) {
        for (const r of [11, 12]) {
            grid[r][c].east = false;
            grid[r][c + 1].west = false;
        }
    }

    // 3. Формирование кольцевых проходов (Braid): часть тупиков пробиваем
    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            let wallCount = 0;
            if (grid[r][c].north) wallCount++;
            if (grid[r][c].south) wallCount++;
            if (grid[r][c].east) wallCount++;
            if (grid[r][c].west) wallCount++;

            if (wallCount === 3 && Math.random() < 0.72) {
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

// Укрытия: одинаковые координаты на сервере и в Unity.
function makeCover() {
    const cover = [];
    const origin = -(COLS * CELL_SIZE) / 2;
    function add(c,r,sx,sz,height,kind) {
        cover.push({x:origin+(c+0.5)*CELL_SIZE,z:origin+(r+0.5)*CELL_SIZE,
            sx,sz,height,kind});
    }
    for (const c of [9,14])
        for (const r of [9,14]) add(c,r,2.4,2.4,2.1,'crate');
    for (const c of [10,13])
        for (const r of [11,12]) add(c,r,2.7,1.0,1.3,'barrier');
    for (const [c,r] of [[3,3],[5,5],[18,3],[20,5],[3,18],[5,20],[18,18],[20,20]])
        add(c,r,2.1,2.1,1.6,'crate');
    for (const [c,r] of [[11,4],[12,19],[4,11],[19,12],[8,12],[15,11],[11,8],[12,15]])
        add(c,r,2.8,1.1,1.1,'barrier');
    return cover;
}
const arenaCover = makeCover();

// Точки появления распределены по четырём дворам.
const spawnCells = [[4,4],[5,4],[19,4],[20,4],[4,19],[5,19],
    [19,19],[20,19],[4,5],[19,5],[4,20],[19,20]];
function selectSpawn() {
    const half = COLS * CELL_SIZE / 2;
    const points = spawnCells.map(([c,r])=>({
        x:-half+(c+0.5)*CELL_SIZE,z:-half+(r+0.5)*CELL_SIZE
    }));
    const alive = Array.from(players.values()).filter(p=>!p.state.isDead);
    if (!alive.length) return points[Math.floor(Math.random()*points.length)];
    let bestDistance = -1, best = [];
    for (const point of points) {
        const d = Math.min(...alive.map(p=>Math.hypot(p.state.x-point.x,p.state.z-point.z)));
        if(d > bestDistance+0.01){ bestDistance=d;best=[point]; }
        else if(Math.abs(d-bestDistance)<0.01)best.push(point);
    }
    return best[Math.floor(Math.random()*best.length)];
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
const HEAD_RADIUS = 0.34;
const HEADSHOT_MULTIPLIER = 2;
const SPAWN_SHIELD_MS = 3000;
const activeProjectiles = new Map();

// Chaos: four server-controlled jump pads in the open courtyards.
const jumpPads = [
    {x:-32,z:-32,dx:1,dz:1}, {x:32,z:-32,dx:-1,dz:1},
    {x:-32,z:32,dx:1,dz:-1}, {x:32,z:32,dx:-1,dz:-1}
];
const jumpPadCooldowns = new Map();
function updateJumpPads() {
    const now = Date.now();
    for (const [id, session] of players) {
        const p = session.state;
        if (p.isDead || now < (jumpPadCooldowns.get(id) || 0)) continue;
        for (const pad of jumpPads) {
            if (Math.hypot(p.x-pad.x,p.z-pad.z)>1.7 || Math.abs(p.y)>1.2) continue;
            jumpPadCooldowns.set(id,now+1600);
            broadcast({type:'PLAYER_IMPULSE',playerId:id,x:pad.dx*7,y:15,z:pad.dz*7});
            break;
        }
    }
}


// Оружие лежит на карте и периодически появляется снова.
const WEAPONS = {
    blaster: { damage: 20, cooldown: 500, speed: 18, life: 3000, ammo: -1, pellets: 1, head: 2 },
    shotgun: { damage: 8, cooldown: 1000, speed: 32, life: 1100, ammo: 18, pellets: 8, head: 1.5 },
    smg: { damage: 9, cooldown: 125, speed: 45, life: 1600, ammo: 120, pellets: 1, head: 2 },
    railgun: { damage: 70, cooldown: 1800, speed: 90, life: 1500, ammo: 10, pellets: 1, head: 2 },
    rocket: { damage: 65, cooldown: 1100, speed: 24, life: 3200, ammo: 8, pellets: 1, head: 1 }
};

const weaponPickups = [
    {id: 'gun_shot_1', weapon: 'shotgun', x: 0, z: 0, availableAt: 0, respawnMs: 25000},
    {id: 'gun_shot_2', weapon: 'shotgun', x: 0, z: 12, availableAt: 0, respawnMs: 25000},
    {id: 'gun_smg_1', weapon: 'smg', x: -34, z: 34, availableAt: 0, respawnMs: 25000},
    {id: 'gun_smg_2', weapon: 'smg', x: 34, z: -34, availableAt: 0, respawnMs: 25000},
    {id: 'gun_rail_1', weapon: 'railgun', x: -38, z: -30, availableAt: 0, respawnMs: 35000},
    {id: 'gun_rail_2', weapon: 'railgun', x: 38, z: 30, availableAt: 0, respawnMs: 35000},
    {id: 'gun_rocket_1', weapon: 'rocket', x: -12, z: 0, availableAt: 0, respawnMs: 30000},
    {id: 'gun_rocket_2', weapon: 'rocket', x: 12, z: 0, availableAt: 0, respawnMs: 30000}
];

function pickupSnapshots() {
    const now = Date.now();
    return weaponPickups.map(p => ({
        id: p.id, weapon: p.weapon, x: p.x, z: p.z,
        available: now >= p.availableAt
    }));
}

function updateWeaponPickups() {
    const now = Date.now();
    for (const pickup of weaponPickups) {
        if (now < pickup.availableAt) continue;
        for (const session of players.values()) {
            if (session.state.isDead) continue;
            const dist = Math.hypot(session.state.x - pickup.x, session.state.z - pickup.z);
            if (dist > 1.8 || Math.abs(session.state.y) > 2.0) continue;
            const key = pickup.weapon + 'Ammo';
            session.state[key] = Math.min(WEAPONS[pickup.weapon].ammo,
                session.state[key] + WEAPONS[pickup.weapon].ammo);
            session.state.weapon = pickup.weapon;
            pickup.availableAt = now + pickup.respawnMs;
            broadcast({type:'WEAPON_ACQUIRED', playerId:session.state.id,
                weapon:pickup.weapon, ammo:session.state[key], pickupId:pickup.id});
            break;
        }
    }
}

function chooseWeapon(playerId, weapon) {
    const session = players.get(playerId);
    if (!session || session.state.isDead || !WEAPONS[weapon]) return;
    if (weapon !== 'blaster' && session.state[weapon + 'Ammo'] <= 0) return;
    session.state.weapon = weapon;
}


// Генерация карты при запуске сервера
const globalWalls = generateArenaMaze(COLS, ROWS, CELL_SIZE);

// Только колонны на концах существующих стен: открытые площади свободны.
function makePillarSites(walls) {
    const sites = new Set();
    const origin = -(COLS * CELL_SIZE) / 2;
    function add(x,z) {
        const c = Math.round((x-origin)/CELL_SIZE);
        const r = Math.round((z-origin)/CELL_SIZE);
        if(c>=0 && c<=COLS && r>=0 && r<=ROWS)sites.add(c+':'+r);
    }
    for(const w of walls) {
        if(w.rot===0) {
            add(w.x-CELL_SIZE/2,w.z);
            add(w.x+CELL_SIZE/2,w.z);
        } else {
            add(w.x,w.z-CELL_SIZE/2);
            add(w.x,w.z+CELL_SIZE/2);
        }
    }
    return sites;
}
const pillarSites = makePillarSites(globalWalls);

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
    const selected = selectSpawn();
    const spawnX = selected.x;
    const spawnZ = selected.z;

    const initialPlayerState = {
        id: clientId,
        name: 'Player',
        colorIndex: 0,
        x: spawnX,
        y: 0.0,
        z: spawnZ,
        yaw: 0.0,
        pitch: 0.0,
        crouch: false,
        hp: 100,
        maxHp: 100,
        isDead: false,
        score: 0,
        deaths: 0,
        shieldSeconds: 3,
        weapon: 'blaster',
        shotgunAmmo: 0, smgAmmo: 0, railgunAmmo: 0, rocketAmmo: 0,
        speedMultiplier: 1, doubleDamageUntil: 0, speedUntil: 0, armor: 0
    };

    players.set(clientId, { ws, state: initialPlayerState, lastAttackTime: 0, lastRangedTime: 0,
        spawnProtectedUntil: Date.now() + SPAWN_SHIELD_MS });
    console.log(`[ПОДКЛЮЧЕНИЕ] Клиент ${clientId} (IP: ${clientIp}) вошел в сессию`);

    // 3. Отправка клиенту структуры лабиринта и точки спавна
    ws.send(JSON.stringify({
        type: 'INIT_WORLD',
        id: clientId,
        spawnPos: [initialPlayerState.x, initialPlayerState.y, initialPlayerState.z],
        hp: initialPlayerState.hp,
        shieldSeconds: 3,
        walls: globalWalls,
        cover: arenaCover, cols: COLS, rows: ROWS, cellSize: CELL_SIZE,
        weaponPickups: pickupSnapshots(), jumpPads, powerupPickups: powerupSnapshots()
    }));

    // 4. Прием пакетов от Unity-клиента
    ws.on('message', (messageRaw) => {
        try {
            const data = JSON.parse(messageRaw.toString());

            // Регистрация никнейма из LobbyScene
            if (data.type === 'JOIN') {
                const session = players.get(clientId);
                if (session) {
                    // Ник и скин выбираются в меню, но сервер проверяет значения.
                    const wantedName = typeof data.name === 'string' ? data.name.trim() : '';
                    session.state.name = wantedName.replace(/[<>]/g, '').slice(0, 20) || 'Player';
                    const color = Number(data.colorIndex);
                    session.state.colorIndex = Number.isInteger(color) && color >= 0 && color < 6 ? color : 0;
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

            if (data.type === 'SELECT_WEAPON') chooseWeapon(clientId, data.weapon);
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
        jumpPadCooldowns.delete(clientId);
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
    updateProjectiles(dt);
    updateWeaponPickups();
    updateJumpPads();
    updatePowerups();
    if (players.size === 0) return;

    const now = Date.now();
    for (const session of players.values()) {
        session.state.shieldSeconds = Math.max(0, (session.spawnProtectedUntil - now) / 1000);
    }
    const snapshot = {
        type: 'SNAPSHOT',
        players: Array.from(players.values()).map(p => p.state),
        weaponPickups: pickupSnapshots(), powerupPickups: powerupSnapshots()
    };

    broadcast(snapshot);
}, 1000 / TICK_RATE);

function broadcast(packet) {
    const raw = JSON.stringify(packet);
    for (const {ws} of players.values()) if(ws.readyState === 1)ws.send(raw);
}

// ===== БЛИЖНИЙ БОЙ =====
function handleMeleeAttack(attackerId) {
    const attacker = players.get(attackerId);
    if (!attacker || attacker.state.isDead) return;
    const now = Date.now();
    if (now - attacker.lastAttackTime < MELEE_COOLDOWN_MS) return;
    attacker.lastAttackTime = now;
    // Щит нужен для выхода со спавна, а не для неуязвимой атаки.
    attacker.spawnProtectedUntil = 0;
    attacker.state.shieldSeconds = 0;
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
            if (segmentBlocked(attacker.state, target.state)) continue;
            const headshot = attacker.state.pitch < -25;
            applyDamage(attacker, target, headshot ? 40 : MELEE_DAMAGE,
                headshot ? 'head' : 'body');
            return;
        }
    }


}

// ===== УРОН, ФРАГИ И ВОЗРОЖДЕНИЕ =====
function applyDamage(attacker, target, damage, hitZone = 'body') {
    if (!attacker || !target || target.state.isDead) return;
    if (Date.now() < target.spawnProtectedUntil) return;
    if (attacker.state.doubleDamageUntil > Date.now()) damage *= 2;
    const absorbed = Math.min(target.state.armor || 0, damage);
    target.state.armor = Math.max(0,(target.state.armor || 0)-absorbed);
    damage -= absorbed;
    if (damage <= 0) return;
    target.state.hp = Math.max(0, target.state.hp - damage);
    broadcast({
        type: 'HIT_EVENT',
        targetId: target.state.id,
        attackerId: attacker.state.id,
        currentHp: target.state.hp,
        damage,
        hitZone
    });
    console.log(`[УРОН] ${attacker.state.name} -> ${target.state.name}: -${damage} HP (осталось ${target.state.hp})`);
    if (target.state.hp > 0) return;

    target.state.isDead = true;
    target.state.deaths += 1;
    attacker.state.score += 1;
    broadcast({
        type: 'KILL_FEED',
        killerId: attacker.state.id,
        killerName: attacker.state.name,
        victimId: target.state.id,
        victimName: target.state.name,
        hitZone,
        killerColorIndex: attacker.state.colorIndex,
        victimColorIndex: target.state.colorIndex
    });
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
    session.state.weapon = 'blaster';
    session.state.shotgunAmmo = 0;
    session.state.smgAmmo = 0;
    session.state.railgunAmmo = 0;
    session.state.rocketAmmo = 0;
    session.state.speedMultiplier = 1;
    session.state.doubleDamageUntil = 0;
    session.state.speedUntil = 0;
    session.state.armor = 0;
    broadcast({type:'CHAOS_STATUS',playerId:session.state.id,speedMultiplier:1});
    // Пока игрок считается мёртвым, выбираем место относительно остальных.
    const spawn = selectSpawn();
    session.state.isDead = false;
    session.spawnProtectedUntil = Date.now() + SPAWN_SHIELD_MS;
    session.state.shieldSeconds = SPAWN_SHIELD_MS / 1000;
    session.state.x = spawn.x;
    session.state.z = spawn.z;
    session.state.y = 0;
    broadcast({
        type: 'PLAYER_RESPAWN',
        id: session.state.id,
        x: session.state.x,
        y: session.state.y,
        z: session.state.z,
        hp: session.state.hp,
        shieldSeconds: 3
    });
    console.log(`[РЕСПАВН] ${session.state.name} возродился`);
}

// ===== ДАЛЬНИЙ БОЙ =====
function handleRangedAttack(attackerId, shot) {
    const attacker = players.get(attackerId);
    if (!attacker || attacker.state.isDead) return;

    const weapon = WEAPONS[attacker.state.weapon] ? attacker.state.weapon : 'blaster';
    const config = WEAPONS[weapon];
    const now = Date.now();
    if (now - attacker.lastRangedTime < config.cooldown) return;

    const ammoKey = weapon + 'Ammo';
    if (weapon !== 'blaster' && attacker.state[ammoKey] <= 0) {
        attacker.state.weapon = 'blaster';
        return;
    }

    const yaw = attacker.state.yaw * Math.PI / 180;
    const pitch = attacker.state.pitch * Math.PI / 180;
    let origin = { x: attacker.state.x, y: attacker.state.y + 1.5, z: attacker.state.z };
    let dir = {
        x: Math.sin(yaw) * Math.cos(pitch),
        y: -Math.sin(pitch),
        z: Math.cos(yaw) * Math.cos(pitch)
    };

    if (shot) {
        const v = [shot.x, shot.y, shot.z, shot.dx, shot.dy, shot.dz].map(Number);
        const offset = Math.hypot(v[0] - attacker.state.x,
            v[1] - (attacker.state.y + 1.5), v[2] - attacker.state.z);
        const length = Math.hypot(v[3], v[4], v[5]);
        if (v.every(Number.isFinite) && offset <= 2.0 && length > 0.5) {
            origin = { x: v[0], y: v[1], z: v[2] };
            dir = { x: v[3]/length, y: v[4]/length, z: v[5]/length };
        }
    }

    attacker.lastRangedTime = now;
    attacker.spawnProtectedUntil = 0;
    attacker.state.shieldSeconds = 0;
    if (weapon !== 'blaster') {
        attacker.state[ammoKey]--;
        if (attacker.state[ammoKey] === 0) attacker.state.weapon = 'blaster';
    }

    // У дробовика несколько дробинок с серверным разбросом.
    const horizontalLength = Math.hypot(dir.x, dir.z);
    const rightX = horizontalLength > 0.001 ? dir.z / horizontalLength : 1;
    const rightZ = horizontalLength > 0.001 ? -dir.x / horizontalLength : 0;

    for (let i = 0; i < config.pellets; i++) {
        let dx = dir.x, dy = dir.y, dz = dir.z;
        if (weapon === 'shotgun') {
            const spreadH = (Math.random() - 0.5) * 0.24;
            const spreadV = (Math.random() - 0.5) * 0.24;
            dx += rightX * spreadH;
            dy += spreadV;
            dz += rightZ * spreadH;
            const size = Math.hypot(dx, dy, dz);
            dx /= size; dy /= size; dz /= size;
        }
        const p = {
            id: 'proj_' + Math.random().toString(36).slice(2, 12),
            ownerId: attackerId,
            weapon,
            damage: config.damage,
            headMultiplier: config.head,
            speed: config.speed,
            life: config.life,
            x: origin.x, y: origin.y, z: origin.z,
            dx, dy, dz,
            createdAt: now
        };
        activeProjectiles.set(p.id, p);
        broadcast({
            type: 'SPAWN_PROJECTILE', id: p.id, weapon,
            x: p.x, y: p.y, z: p.z,
            dx: p.dx, dy: p.dy, dz: p.dz,
            speed: p.speed
        });
    }
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
    if (!pillarSites.has(c+':'+r)) return false;

    const pillarX = offsetX + c * CELL_SIZE;
    const pillarZ = offsetZ + r * CELL_SIZE;
    return Math.abs(p.x - pillarX) <= 0.4 &&
        Math.abs(p.z - pillarZ) <= 0.4;
}

function projectileHitsCover(p) {
    for (const box of arenaCover) {
        if (Math.abs(p.x-box.x) <= box.sx/2 &&
            Math.abs(p.z-box.z) <= box.sz/2 &&
            p.y >= 0 && p.y <= box.height) return true;
    }
    return false;
}
function pointHitsWall(p) {
    if (projectileHitsPillar(p) || projectileHitsCover(p)) return true;
    for (const wall of globalWalls) {
        const dx = wall.rot === 0 ? CELL_SIZE/2 : 0.2;
        const dz = wall.rot === 0 ? 0.2 : CELL_SIZE/2;
        if (Math.abs(p.x-wall.x)<=dx && Math.abs(p.z-wall.z)<=dz &&
            p.y >= 0 && p.y <= 3) return true;
    }
    return false;
}
function segmentBlocked(a,b) {
    const count = Math.ceil(Math.hypot(b.x-a.x,b.z-a.z)/0.2);
    for(let i=1;i<count;i++) {
        const t=i/count;
        if(pointHitsWall({x:a.x+(b.x-a.x)*t,y:1.3,z:a.z+(b.z-a.z)*t}))
            return true;
    }
    return false;
}
function updateProjectiles(dt) {
    const now = Date.now();
    for (const [id, p] of activeProjectiles) {
        if (now - p.createdAt > (p.life || PROJECTILE_LIFETIME_MS)) {
            destroyProjectile(id, 'timeout');
            continue;
        }

        // Дробим полёт на короткие отрезки: при 20 тиках/с
        // один шаг в 0.9 м иначе может перескочить стену толщиной 0.4 м.
        const speed = p.speed || PROJECTILE_SPEED;
        const steps = Math.max(1, Math.ceil(speed * dt / 0.15));
        const distancePerStep = speed * dt / steps;
        let destroyed = false;

        for (let step = 0; step < steps && !destroyed; step++) {
            p.x += p.dx * distancePerStep;
            p.y += p.dy * distancePerStep;
            p.z += p.dz * distancePerStep;

            if (p.y <= 0.1 || p.y >= 10.0) {
                if (p.weapon === 'rocket') explodeRocket(p);
                destroyProjectile(id, 'floor');
                break;
            }

            for (const wall of globalWalls) {
                const halfX = wall.rot === 0 ? 2.0 : 0.2;
                const halfZ = wall.rot === 0 ? 0.2 : 2.0;
                if (p.x >= wall.x - halfX && p.x <= wall.x + halfX &&
                    p.z >= wall.z - halfZ && p.z <= wall.z + halfZ &&
                    p.y <= 3.0) {
                    if (p.weapon === 'rocket') explodeRocket(p);
                    destroyProjectile(id, 'wall');
                    destroyed = true;
                    break;
                }
            }
            if (destroyed) break;

            if (projectileHitsPillar(p)) {
                if (p.weapon === 'rocket') explodeRocket(p);
                destroyProjectile(id, 'pillar');
                destroyed = true;
                break;
            }
            if (projectileHitsCover(p)) {
                if (p.weapon === 'rocket') explodeRocket(p);
                destroyProjectile(id, 'cover');
                destroyed = true;
                break;
            }

            for (const [targetId, target] of players) {
                if (targetId === p.ownerId || target.state.isDead) continue;
                const dx = target.state.x - p.x, dz = target.state.z - p.z;
                const crouch = target.state.crouch;
                const headY = target.state.y + (crouch ? 0.82 : 1.70);
                const headHit = Math.hypot(dx,p.y-headY,dz) <= HEAD_RADIUS;
                // Не расширяем тело на голову, иначе headshot засчитывается как body.
                const bodyBottom = target.state.y + (crouch ? 0.05 : 0.15);
                const bodyTop = target.state.y + (crouch ? 0.67 : 1.38);
                const bodyHit = Math.hypot(dx,dz) <= PLAYER_HIT_RADIUS &&
                    p.y >= bodyBottom && p.y <= bodyTop;
                if (headHit || bodyHit) {
                    const attacker = players.get(p.ownerId);
                    if (p.weapon === 'rocket') { explodeRocket(p); destroyProjectile(id,'player'); destroyed = true; break; }
                    if (attacker) applyDamage(attacker,target,
                        headHit ? Math.round((p.damage || RANGED_DAMAGE) * (p.headMultiplier || HEADSHOT_MULTIPLIER)) : (p.damage || RANGED_DAMAGE),
                        headHit ? 'head' : 'body');
                    destroyProjectile(id,'player');
                    destroyed = true;
                    break;
                }
            }
            if (destroyed) break;


        }
    }
}

// Server-authoritative splash damage and launch. Self damage does not grant a frag.
function explodeRocket(p) {
    const attacker = players.get(p.ownerId);
    if (!attacker) return;
    broadcast({type:'EXPLOSION',x:p.x,y:p.y,z:p.z,radius:5});
    for (const [id,target] of players) {
        if (target.state.isDead) continue;
        const dx=target.state.x-p.x, dy=target.state.y+1-p.y, dz=target.state.z-p.z;
        const dist=Math.hypot(dx,dy,dz);
        if (dist>=5 || segmentBlocked({x:p.x,z:p.z},{x:target.state.x,z:target.state.z})) continue;
        const scale=1-dist/5;
        const damage=Math.round(65*scale);
        if (id === p.ownerId) {
            if (Date.now() >= target.spawnProtectedUntil) target.state.hp=Math.max(1,target.state.hp-Math.round(damage*0.4));
        } else if (damage>0) applyDamage(attacker,target,damage,'explosion');
        const planar=Math.max(0.1,Math.hypot(dx,dz));
        broadcast({type:'PLAYER_IMPULSE',playerId:id,x:dx/planar*scale*9,y:Math.max(5,scale*15),z:dz/planar*scale*9});
    }
}

const powerups = [
    {id:'boost_speed',kind:'speed',x:0,z:-16,availableAt:0,respawnMs:30000},
    {id:'boost_damage',kind:'damage',x:0,z:16,availableAt:0,respawnMs:30000},
    {id:'boost_armor',kind:'armor',x:16,z:0,availableAt:0,respawnMs:30000}
];
function powerupSnapshots() {
    const now=Date.now();
    return powerups.map(p=>({id:p.id,kind:p.kind,x:p.x,z:p.z,available:now>=p.availableAt}));
}
function updatePowerups() {
    const now=Date.now();
    for(const [id,session] of players) {
        const p=session.state;
        if(p.speedMultiplier!==1 && now>=p.speedUntil) {
            p.speedMultiplier=1;
            broadcast({type:'CHAOS_STATUS',playerId:id,speedMultiplier:1});
        }
    }
    for(const pickup of powerups) {
        if(now<pickup.availableAt)continue;
        for(const [id,session] of players) {
            const p=session.state;
            if(p.isDead || Math.abs(p.y)>2 || Math.hypot(p.x-pickup.x,p.z-pickup.z)>1.8)continue;
            pickup.availableAt=now+pickup.respawnMs;
            if(pickup.kind==='speed') {
                p.speedUntil=now+10000;p.speedMultiplier=1.5;
                broadcast({type:'CHAOS_STATUS',playerId:id,speedMultiplier:1.5});
            } else if(pickup.kind==='damage')p.doubleDamageUntil=now+8000;
            else p.armor=Math.min(50,(p.armor||0)+50);
            broadcast({type:'POWERUP_ACQUIRED',playerId:id,kind:pickup.kind});
            break;
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
