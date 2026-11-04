const BaseMob = require('./BaseMob');

class TankMob extends BaseMob {
    constructor(id, x, z) {
        super(id, x, z, 'tank', { maxHp: 250, speed: 2.2, turnSpeed: 110 });
        this.attackRange = 2.0;
        this.damage = 35;
        this.lastAttackTime = 0;
        this.isPreparingAttack = false;
        this.prepareTimer = 0;
    }

    takeDamage(damage, hitDirX, hitDirZ, attacker, broadcast) {
        if (this.isDead) return false;
        const yawRad = this.yaw * Math.PI / 180;
        const forwardX = Math.sin(yawRad);
        const forwardZ = Math.cos(yawRad);
        const dot = forwardX * hitDirX + forwardZ * hitDirZ;
        // hitDir направлен ОТ игрока к мобу.
        // Если игрок находится перед танком, броня держит удар.
        if (dot <= 0.2) {
            broadcast({ type: 'MOB_ARMOR_BLOCKED', mobId: this.id, x: this.x, z: this.z });
            return false;
        }
        return super.takeDamage(damage, hitDirX, hitDirZ, attacker, broadcast);
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        if (this.isDead) return;
        const now = Date.now();
        let target = null;
        let minDist = Infinity;
        for (const player of players.values()) {
            if (player.state.isDead) continue;
            const dx = player.state.x - this.x;
            const dz = player.state.z - this.z;
            const dist = Math.hypot(dx, dz);
            if (dist > 0.001 && dist < minDist && dist <= 12.0) {
                const yawRad = this.yaw * Math.PI / 180;
                const fx = Math.sin(yawRad), fz = Math.cos(yawRad);
                if (fx * dx / dist + fz * dz / dist >= 0.707) {
                    minDist = dist;
                    target = player;
                }
            }
        }

        if (target) {
            this.targetPlayerId = target.state.id;
            const dist = this.steerTowards(target.state.x, target.state.z, dt);
            if (dist <= this.attackRange) {
                this.state = 'ATTACK';
                if (!this.isPreparingAttack && now - this.lastAttackTime > 2200) {
                    this.isPreparingAttack = true;
                    this.prepareTimer = 0.8;
                    broadcast({ type: 'MOB_PREPARE_ATTACK', mobId: this.id, duration: 0.8 });
                }
                if (this.isPreparingAttack) {
                    this.prepareTimer -= dt;
                    if (this.prepareTimer <= 0) {
                        this.isPreparingAttack = false;
                        this.lastAttackTime = now;
                        target.state.hp = Math.max(0, target.state.hp - this.damage);
                        const yawRad = this.yaw * Math.PI / 180;
                        target.state.x += Math.sin(yawRad) * 3.5;
                        target.state.z += Math.cos(yawRad) * 3.5;
                        broadcast({ type: 'HIT_EVENT', targetId: target.state.id,
                            attackerId: this.id, currentHp: target.state.hp, damage: this.damage });
                        helpers.checkDeath(target, this.id);
                    }
                }
            } else {
                this.isPreparingAttack = false;
                this.state = 'CHASE';
                this.moveForward(dt);
            }
        } else {
            this.isPreparingAttack = false;
            this.state = 'PATROL';
        }
    }
}
module.exports = TankMob;
