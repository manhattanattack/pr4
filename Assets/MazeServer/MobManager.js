const TankMob = require('./TankMob');
const SprinterMob = require('./SprinterMob');
const BlindMob = require('./BlindMob');
const KamikazeMob = require('./KamikazeMob');

class MobManager {
    constructor() { this.mobs = new Map(); }

    initSpawns() {
        this.mobs.clear();
        this.mobs.set('mob_tank', new TankMob('mob_tank', 0, 0));
        this.mobs.set('mob_sprinter', new SprinterMob('mob_sprinter', -10, 10));
        this.mobs.set('mob_blind', new BlindMob('mob_blind', 10, -10));
        this.mobs.set('mob_kami', new KamikazeMob('mob_kami', 8, 8));
        console.log('[ИИ] Спавн инициализирован. Активно мобов: ' + this.mobs.size);
    }

    emitNoiseEvent(x, z, noiseLevel) {
        for (const mob of this.mobs.values()) {
            if (mob instanceof BlindMob) mob.onNoiseDetected(x, z, noiseLevel);
        }
    }

    applyDamageToMob(mobId, damage, hitDirX, hitDirZ, attacker, broadcast) {
        const mob = this.mobs.get(mobId);
        if (mob && !mob.isDead) {
            const died = mob.takeDamage(damage, hitDirX, hitDirZ, attacker, broadcast);
            if (died) {
                // Пример лута: один предмет с типом архетипа моба.
                broadcast({ type: 'MOB_LOOT', mobId: mob.id, lootType: mob.archetype,
                    x: mob.x, y: 0.4, z: mob.z });
            }
        }
    }

    update(dt, players, walls, navNodes, broadcast, helpers) {
        for (const mob of this.mobs.values()) {
            if (!mob.isDead) mob.update(dt, players, walls, navNodes, broadcast, helpers);
        }
    }

    getSnapshots() {
        const list = [];
        for (const mob of this.mobs.values()) {
            if (!mob.isDead) list.push(mob.getStateSnapshot());
        }
        return list;
    }
}
module.exports = MobManager;
