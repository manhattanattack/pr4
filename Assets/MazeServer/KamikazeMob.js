const BaseMob = require('./BaseMob');

class KamikazeMob extends BaseMob {
    constructor(id, x, z) {
        super(id, x, z, 'kamikaze', { maxHp: 30, speed: 6.8, turnSpeed: 250 });
        this.countdown = 1.0;
        this.isCountingDown = false;
        this.explosionRadius = 3.5;
        this.explosionDamage = 50;
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        if (this.isDead) return;
        if (this.isCountingDown) {
            this.countdown -= dt;
            if (this.countdown <= 0) this.explode(players, broadcast, helpers);
            return;
        }

        let target = null;
        let minDist = Infinity;
        for (const player of players.values()) {
            if (player.state.isDead) continue;
            const dist = Math.hypot(player.state.x - this.x, player.state.z - this.z);
            if (dist < minDist && dist <= 8.0) {
                minDist = dist;
                target = player;
            }
        }

        if (target) {
            this.state = 'CHASE';
            const dist = this.steerTowards(target.state.x, target.state.z, dt);
            this.moveForward(dt);
            if (dist <= 1.5) {
                this.isCountingDown = true;
                this.state = 'COUNTDOWN';
                broadcast({ type: 'MOB_COUNTDOWN', mobId: this.id, duration: 1.0 });
            }
        } else this.state = 'PATROL';
    }

    explode(players, broadcast, helpers) {
        this.isDead = true;
        this.state = 'DEAD';
        broadcast({ type: 'MOB_EXPLODED', mobId: this.id, x: this.x, y: this.y,
            z: this.z, radius: this.explosionRadius });
        for (const player of players.values()) {
            if (player.state.isDead) continue;
            const dist = Math.hypot(player.state.x - this.x, player.state.z - this.z);
            if (dist <= this.explosionRadius) {
                player.state.hp = Math.max(0, player.state.hp - this.explosionDamage);
                broadcast({ type: 'HIT_EVENT', targetId: player.state.id,
                    attackerId: this.id, currentHp: player.state.hp, damage: this.explosionDamage });
                helpers.checkDeath(player, this.id);
            }
        }
    }
}
module.exports = KamikazeMob;
