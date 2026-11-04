const BaseMob = require('./BaseMob');

class SprinterMob extends BaseMob {
    constructor(id, x, z) {
        super(id, x, z, 'sprinter', { maxHp: 20, speed: 6.2, turnSpeed: 320 });
        this.zigzagTimer = 0;
        this.zigzagSide = 1;
        this.retreatTimer = 0;
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        if (this.isDead) return;
        if (this.state === 'RETREAT') {
            this.retreatTimer -= dt;
            const rad = this.yaw * Math.PI / 180;
            this.x -= Math.sin(rad) * this.speed * 0.75 * dt;
            this.z -= Math.cos(rad) * this.speed * 0.75 * dt;
            if (this.retreatTimer <= 0) this.state = 'CHASE';
            return;
        }

        let target = null;
        let minDist = Infinity;
        for (const player of players.values()) {
            if (player.state.isDead) continue;
            const dist = Math.hypot(player.state.x - this.x, player.state.z - this.z);
            if (dist < minDist && dist <= 16) {
                minDist = dist;
                target = player;
            }
        }
        if (!target) {
            this.state = 'PATROL';
            return;
        }

        this.state = 'CHASE';
        this.targetPlayerId = target.state.id;
        this.zigzagTimer += dt;
        if (this.zigzagTimer > 0.3) {
            this.zigzagTimer = 0;
            this.zigzagSide *= -1;
        }

        const dx = target.state.x - this.x;
        const dz = target.state.z - this.z;
        const dist = Math.hypot(dx, dz);
        if (dist > 0.001) {
            const perpX = -dz / dist;
            const perpZ = dx / dist;
            const waypointX = target.state.x + perpX * (2.2 * this.zigzagSide);
            const waypointZ = target.state.z + perpZ * (2.2 * this.zigzagSide);
            this.steerTowards(waypointX, waypointZ, dt);
            this.moveForward(dt);
        }
        if (dist <= 1.4) {
            target.state.hp = Math.max(0, target.state.hp - 20);
            broadcast({ type: 'HIT_EVENT', targetId: target.state.id,
                attackerId: this.id, currentHp: target.state.hp, damage: 20 });
            helpers.checkDeath(target, this.id);
            this.state = 'RETREAT';
            this.retreatTimer = 1.2;
        }
    }
}
module.exports = SprinterMob;
