'use strict';

// Пакет Quest 3 валидируется целиком: NaN/Infinity и неполные кадры игнорируются.
const VR_FIELDS = ['x','y','z','hx','hy','hz','hyaw','hpitch','hroll',
    'lhx','lhy','lhz','rhx','rhy','rhz','ryaw','rpitch'];
const DUCK_HEIGHT_METERS = 1.1;
const SWING_SPEED_MPS = 2.2;
const RUN_SPEED_MPS = 1.4;

function applyVRMove(session, data, mobManager, now = Date.now()) {
    if (!session || session.state.isDead ||
        !VR_FIELDS.every(field => typeof data[field] === 'number' && Number.isFinite(data[field]))) {
        return false;
    }
    // Неверный трекинг или подмена пакета не должны менять состояние мира.
    if (Math.abs(data.hx) > 3 || data.hy < 0 || data.hy > 3 ||
        Math.abs(data.hz) > 3 || Math.abs(data.lhx) > 3 ||
        Math.abs(data.lhy) > 3 || Math.abs(data.lhz) > 3 ||
        Math.abs(data.rhx) > 3 || Math.abs(data.rhy) > 3 || Math.abs(data.rhz) > 3) {
        return false;
    }

    const s = session.state;
    const lastAt = session.lastVRMoveAt;
    const dt = lastAt == null ? 0 : Math.max(0.02, Math.min(0.25, (now - lastAt) / 1000));
    const bodySpeed = dt ? Math.hypot(data.x - s.x, data.z - s.z) / dt : 0;
    const handSpeed = dt && session.lastRightHand
        ? Math.hypot(data.rhx - session.lastRightHand.x,
            data.rhy - session.lastRightHand.y, data.rhz - session.lastRightHand.z) / dt : 0;

    s.x = data.x; s.y = data.y; s.z = data.z;
    s.hx = data.hx; s.hy = data.hy; s.hz = data.hz;
    s.hyaw = data.hyaw; s.hpitch = data.hpitch; s.hroll = data.hroll;
    s.lhx = data.lhx; s.lhy = data.lhy; s.lhz = data.lhz;
    s.rhx = data.rhx; s.rhy = data.rhy; s.rhz = data.rhz;
    s.ryaw = data.ryaw; s.rpitch = data.rpitch;
    s.yaw = data.hyaw; s.pitch = data.hpitch;
    s.crouch = data.hy < DUCK_HEIGHT_METERS;
    s.isVR = true;

    session.lastVRMoveAt = now;
    session.lastRightHand = {x: data.rhx, y: data.rhy, z: data.rhz};
    // Медленное перемещение не издаёт шума, быстрый взмах слышен за 15 метров.
    if (handSpeed >= SWING_SPEED_MPS) {
        mobManager.emitNoiseEvent(s.x, s.z, 1.0);
    } else if (bodySpeed >= RUN_SPEED_MPS) {
        mobManager.emitNoiseEvent(s.x, s.z, 0.4);
    }
    return true;
}

function duckedUnderTankHammer(state) {
    return state.isVR === true && typeof state.hy === 'number' &&
        Number.isFinite(state.hy) && state.hy < DUCK_HEIGHT_METERS;
}

module.exports = { applyVRMove, duckedUnderTankHammer, DUCK_HEIGHT_METERS, SWING_SPEED_MPS };
