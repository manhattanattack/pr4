const BaseMob = require('./BaseMob');

class BlindMob extends BaseMob {
    constructor(id, x, z) {
        super(id, x, z, 'blind', { maxHp: 130, speed: 2.8, turnSpeed: 170 });
        this.soundTargetPos = null;
        this.isCharging = false;
        this.hearRadius = 15;
    }

    onNoiseDetected(sourceX, sourceZ, noiseLevel) {
        if (this.isDead) return;
        const dist = Math.hypot(sourceX - this.x, sourceZ - this.z);
        if (dist <= this.hearRadius * noiseLevel) {
            this.soundTargetPos = { x: sourceX, z: sourceZ };
            this.state = 'ALERT';
            this.isCharging = true;
            this.speed = 5.6;
        }
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        if (this.isDead) return;
        if (this.soundTargetPos) {
            const dist = this.steerTowards(this.soundTargetPos.x, this.soundTargetPos.z, dt);
            this.moveForward(dt);
            for (const player of players.values()) {
                if (player.state.isDead) continue;
                const d = Math.hypot(player.state.x - this.x, player.state.z - this.z);
                if (d <= 1.3) {
                    player.state.hp = Math.max(0, player.state.hp - 30);
                    broadcast({ type: 'HIT_EVENT', targetId: player.state.id,
                        attackerId: this.id, currentHp: player.state.hp, damage: 30 });
                    helpers.checkDeath(player, this.id);
                    this.soundTargetPos = null;
                    this.isCharging = false;
                    this.speed = 2.8;
                    this.state = 'PATROL';
                    return;
                }
            }
            if (dist <= 0.7) {
                this.soundTargetPos = null;
                this.isCharging = false;
                this.speed = 2.8;
                this.state = 'PATROL';
            }
        } else {
            this.state = 'PATROL';
        }
    }
}
module.exports = BlindMob;
