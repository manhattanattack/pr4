// Automated headless smoke tests for the server. No Unity or npm dependency needed.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(__dirname + '/server.js', 'utf8');

function createGame() {
    let now = 500000;
    let tick;
    let server;
    const timers = [];
    const errors = [];

    class MockWSS {
        constructor() { server = this; this.handlers = {}; }
        on(name, fn) { this.handlers[name] = fn; }
    }
    class MockClient {
        constructor() { this.handlers = {}; this.sent = []; this.readyState = 1; }
        on(name, fn) { this.handlers[name] = fn; }
        send(message) { this.sent.push(JSON.parse(message)); }
        sendToServer(packet) { this.handlers.message(JSON.stringify(packet)); }
        all(type) { return this.sent.filter(packet => packet.type === type); }
        last(type) { return this.all(type).slice(-1)[0]; }
    }
    class FakeDate extends Date {
        static now() { return now; }
    }

    vm.runInNewContext(source, {
        require(name) {
            assert.equal(name, 'ws');
            return { WebSocketServer: MockWSS };
        },
        Date: FakeDate,
        setInterval(fn) { tick = fn; },
        setTimeout(fn, ms) { timers.push({at: now + ms, fn, done: false}); },
        console: { log() {}, error(error) { errors.push(String(error)); } }
    }, { filename: 'server.js' });

    function connect() {
        const client = new MockClient();
        server.handlers.connection(client, {socket: {remoteAddress: 'test'}});
        return {client, id: client.last('INIT_WORLD').id};
    }
    function move(player, x, y, z) {
        player.client.sendToServer({type: 'MOVE', x, y, z, yaw: 0, pitch: 0, crouch: false});
    }
    function advance(ms) {
        for (let i = 0; i < Math.ceil(ms / 50); i++) {
            now += 50;
            tick();
            for (const timer of timers) {
                if (timer.done || timer.at > now) continue;
                timer.done = true;
                timer.fn();
            }
        }
    }
    function state(player) {
        return player.client.last('SNAPSHOT').players.find(p => p.id === player.id);
    }
    function fireDown(player, x, z) {
        player.client.sendToServer({type: 'FIRE_RANGED',
            x, y: 1.5, z, dx: 0, dy: -1, dz: 0});
    }
    return {connect, move, advance, state, fireDown, errors};
}

function test(name, action) {
    try {
        action();
        console.log('PASS ' + name);
    } catch (error) {
        console.error('FAIL ' + name, error);
        process.exitCode = 1;
    }
}

test('new world contains rocket pickups, pads, buffs', () => {
    const game = createGame(), a = game.connect();
    const init = a.client.last('INIT_WORLD');
    assert.equal(init.jumpPads.length, 4);
    assert.equal(init.powerupPickups.length, 3);
    assert.equal(init.weaponPickups.filter(p => p.weapon === 'rocket').length, 2);
});

test('jump pad sends authoritative impulse', () => {
    const game = createGame(), a = game.connect();
    game.move(a, -32, 0, -32);
    game.advance(50);
    assert.equal(a.client.last('PLAYER_IMPULSE').playerId, a.id);
});

test('rocket pickup, explosion and knockback', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, -12, 0, 0);
    game.move(b, -11, 0, 2);
    game.advance(3150);
    assert.equal(game.state(a).rocketAmmo, 8);
    game.fireDown(a, -12, 0);
    game.advance(350);
    assert.equal(a.client.all('EXPLOSION').length, 1);
    assert.ok(game.state(b).hp < 100);
    assert.ok(game.state(a).hp < 100);
    assert.ok(a.client.all('PLAYER_IMPULSE').length >= 2);
});

test('spawn shield protects other player, not the attacker who fired', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, -12, 0, 0);
    game.move(b, -11, 0, 2);
    game.advance(50);
    game.fireDown(a, -12, 0);
    game.advance(350);
    assert.equal(game.state(b).hp, 100);
    assert.ok(game.state(a).hp < 100);
});

test('speed boost expires', () => {
    const game = createGame(), a = game.connect();
    game.move(a, 0, 0, -16);
    game.advance(50);
    assert.equal(game.state(a).speedMultiplier, 1.5);
    game.advance(10100);
    assert.equal(game.state(a).speedMultiplier, 1);
});

test('double damage applies to headshot', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 0, 0, 16);
    game.move(b, 0, 0, 2);
    game.advance(3150);
    assert.ok(game.state(a).damageSeconds > 0);
    game.move(a, 0, 0, 0);
    a.client.sendToServer({type: 'FIRE_RANGED',
        x: 0, y: 1.5, z: 0, dx: 0, dy: 0, dz: 1});
    game.advance(500);
    assert.equal(a.client.last('HIT_EVENT').damage, 80);
});

test('armor absorbs damage before health', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 16, 0, 0);
    game.move(b, 0, 0, -3);
    game.advance(3150);
    assert.equal(game.state(a).armor, 50);
    assert.equal(game.state(b).weapon, 'blaster');
    game.move(a, 0, 0, 2);
    game.move(b, 0, 0, 0);
    b.client.sendToServer({type: 'FIRE_RANGED',
        x: 0, y: 1.5, z: 0, dx: 0, dy: 0, dz: 1});
    game.advance(500);
    assert.equal(game.state(a).hp, 100);
    assert.equal(game.state(a).armor, 10);
});

test('explosion death is tagged and respawns after three seconds', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, -12, 0, 0);
    game.move(b, -11, 0, 2);
    game.advance(3150);
    for (let i = 0; i < 5 && !a.client.last('PLAYER_DIED'); i++) {
        game.fireDown(a, -12, 0);
        game.advance(1200);
    }
    const death = a.client.last('PLAYER_DIED');
    assert.ok(death);
    assert.equal(death.hitZone, 'explosion');
    assert.equal(death.victimId, b.id);
    game.advance(3100);
    assert.equal(game.state(b).isDead, false);
    assert.equal(game.state(b).hp, 100);
});

test('E kick deals five damage and launches only the victim', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 0, 0, 0);
    game.move(b, 0, 0, 2);
    game.advance(3150); // spawn protection ends
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 95);
    assert.equal(game.state(a).hp, 100);
    const impulse = a.client.last('PLAYER_IMPULSE');
    assert.ok(impulse);
    assert.equal(impulse.playerId, b.id);
    assert.equal(impulse.y, 7);
    assert.ok(impulse.z > 10);
    assert.equal(a.client.last('KICK_EVENT').victimId, b.id);
});

test('E kick cannot hit behind attacker or outside reach', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 0, 0, 0);
    game.move(b, 0, 0, -2);
    game.advance(3150);
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 100);
    assert.equal(a.client.all('KICK_EVENT').length, 0);
    game.move(b, 0, 0, 4);
    game.advance(1200);
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 100);
    assert.equal(a.client.all('KICK_EVENT').length, 0);
});

test('E kick cooldown ignores a second immediate request', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 0, 0, 0);
    game.move(b, 0, 0, 2);
    game.advance(3150);
    a.client.sendToServer({type: 'KICK_PLAYER'});
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 95);
    assert.equal(a.client.all('KICK_EVENT').length, 1);
    game.advance(1150);
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 90);
    assert.equal(a.client.all('KICK_EVENT').length, 2);
});

test('E kick respects spawn shield on target', () => {
    const game = createGame(), a = game.connect(), b = game.connect();
    game.move(a, 0, 0, 0);
    game.move(b, 0, 0, 2);
    game.advance(50);
    a.client.sendToServer({type: 'KICK_PLAYER'});
    game.advance(50);
    assert.equal(game.state(b).hp, 100);
    assert.equal(a.client.all('KICK_EVENT').length, 0);
    assert.equal(game.state(a).shieldSeconds, 0);
});

test('all projectile speeds match faster arena movement', () => {
    const expected = [
        {weapon: 'blaster', x: null, z: null, speed: 50, pellets: 1},
        {weapon: 'shotgun', x: 0, z: 0, speed: 60, pellets: 8},
        {weapon: 'smg', x: -34, z: 34, speed: 70, pellets: 1},
        {weapon: 'railgun', x: -38, z: -30, speed: 90, pellets: 1},
        {weapon: 'rocket', x: -12, z: 0, speed: 24, pellets: 1}
    ];
    for (const config of expected) {
        const game = createGame(), shooter = game.connect();
        if (config.x !== null) game.move(shooter, config.x, 0, config.z);
        game.advance(50);
        if (config.x !== null)
            assert.equal(game.state(shooter).weapon, config.weapon);
        const state = game.state(shooter);
        game.fireDown(shooter, state.x, state.z);
        const shots = shooter.client.all('SPAWN_PROJECTILE');
        assert.equal(shots.length, config.pellets, config.weapon + ' projectile count');
        for (const shot of shots) {
            assert.equal(shot.weapon, config.weapon);
            assert.equal(shot.speed, config.speed);
        }
    }
});
