'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { applyVRMove, duckedUnderTankHammer } = require('../vrMovement');

function pose(overrides = {}) {
    return {type:'MOVE_VR', x:0,y:0,z:0, hx:0,hy:1.6,hz:0,
        hyaw:40,hpitch:0,hroll:0, lhx:-0.3,lhy:1.2,lhz:0.2,
        rhx:0.3,rhy:1.2,rhz:0.2, ryaw:0,rpitch:0,...overrides};
}
function fixture() {
    const noise=[];
    return {session:{state:{x:0,y:0,z:0,isDead:false}}, mob:{emitNoiseEvent(...x){noise.push(x);}}, noise};
}
test('6DoF кадр синхронизирует голову и кисти', () => {
    const {session,mob}=fixture();
    assert.equal(applyVRMove(session,pose(),mob,1000),true);
    assert.equal(session.state.hy,1.6);
    assert.equal(session.state.lhx,-0.3);
    assert.equal(session.state.ryaw,0);
    assert.equal(session.state.isVR,true);
});
test('обрыв/NaN и некорректный трекинг отклоняются', () => {
    const {session,mob}=fixture();
    assert.equal(applyVRMove(session,pose({rhy:NaN}),mob,1000),false);
    assert.equal(applyVRMove(session,pose({hy:-2}),mob,1000),false);
    assert.equal(applyVRMove(session,{type:'MOVE_VR',x:0},mob,1000),false);
    assert.equal(session.state.isVR,undefined);
});
test('физическое приседание спасает от молота только VR игрока', () => {
    const {session,mob}=fixture();
    applyVRMove(session,pose({hy:1.05}),mob,1000);
    assert.equal(duckedUnderTankHammer(session.state),true);
    applyVRMove(session,pose({hy:1.11}),mob,1100);
    assert.equal(duckedUnderTankHammer(session.state),false);
    assert.equal(duckedUnderTankHammer({hy:0.5,isVR:false}),false);
});
test('тихо при медленном жесте, громко при махе, шум при беге', () => {
    const {session,mob,noise}=fixture();
    applyVRMove(session,pose(),mob,1000);
    applyVRMove(session,pose({rhx:0.35,x:0.01}),mob,1100);
    assert.equal(noise.length,0);
    applyVRMove(session,pose({rhx:0.7}),mob,1200);
    assert.equal(noise.length,1);
    assert.equal(noise[0][2],1);
    applyVRMove(session,pose({rhx:0.72,x:0.5}),mob,1300);
    assert.equal(noise.at(-1)[2],0.4);
});
