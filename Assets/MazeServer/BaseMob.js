// Общий класс мобов
class BaseMob {
    constructor(id, x, z, archetype, config = {}) {
        this.id = id;
        this.archetype = archetype;
        this.x = x;
        this.y = 0.0;
        this.z = z;
        this.yaw = 0;
        this.maxHp = config.maxHp || 100;
        this.hp = this.maxHp;
        this.speed = config.speed || 3.5;
        this.turnSpeed = config.turnSpeed || 200;
        this.state = 'PATROL';
        this.targetPlayerId = null;
        this.isDead = false;
        this.path = [];
        this.lastPathCalcTime = 0;
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        throw new Error('Метод update() не реализован для ' + this.archetype);
    }

    steerTowards(targetX, targetZ, dt) {
        const dx = targetX - this.x;
        const dz = targetZ - this.z;
        const dist = Math.hypot(dx, dz);
        if (dist > 0.05) {
            const targetYaw = Math.atan2(dx, dz) * 180 / Math.PI;
            let diff = (targetYaw - this.yaw) % 360;
            if (diff > 180) diff -= 360;
            if (diff < -180) diff += 360;
            const step = this.turnSpeed * dt;
            if (Math.abs(diff) <= step) this.yaw = targetYaw;
            else this.yaw += Math.sign(diff) * step;
        }
        return dist;
    }

    moveForward(dt) {
        const rad = this.yaw * Math.PI / 180;
        this.x += Math.sin(rad) * this.speed * dt;
        this.z += Math.cos(rad) * this.speed * dt;
    }

    takeDamage(damage, hitDirX, hitDirZ, attacker, broadcast) {
        if (this.isDead) return false;
        this.hp = Math.max(0, this.hp - damage);
        broadcast({ type: 'MOB_HIT', mobId: this.id, hp: this.hp, damage: damage });
        if (this.hp <= 0) {
            this.isDead = true;
            this.state = 'DEAD';
            broadcast({ type: 'MOB_DIED', mobId: this.id,
                killerId: attacker ? attacker.state.id : null });
            return true;
        }
        return false;
    }

    getStateSnapshot() {
        return {
            id: this.id, archetype: this.archetype,
            x: Number(this.x.toFixed(2)), y: this.y, z: Number(this.z.toFixed(2)),
            yaw: Number(this.yaw.toFixed(1)),
            hp: this.hp, maxHp: this.maxHp, state: this.state
        };
    }
}
module.exports = BaseMob;
