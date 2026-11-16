"use strict";
const assert=require('node:assert/strict');
const fs=require('node:fs');
const {validateWarrior,validateWarlock}=require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js');
const warrior={preset:'protection',tankMode:true,enableBossAutoAttacks:true,shieldEquipped:true,isTwoHanded:false,dualWield:false,
 mainHandMinimumDamage:80,mainHandMaximumDamage:120,mainHandSpeedSeconds:2,playerBlockPercent:5,playerBlockValue:60};
const empty={items:[]};
assert.equal(validateWarrior(warrior,empty),warrior);
for(const [change,message] of [
 [{tankMode:false},/Protection setup required/],
 [{enableBossAutoAttacks:false},/Boss auto-attacks/],
 [{shieldEquipped:false},/Protection requires a shield/],
 [{isTwoHanded:true},/one-handed/],
 [{dualWield:true},/one-handed/],
 [{playerBlockPercent:101},/0-100/],
 [{playerBlockValue:-1},/nonnegative/],
 [{mainHandSpeedSeconds:0},/swing time/],
 [{mainHandMaximumDamage:50},/weapon damage/],
 [{mainHandMinimumDamage:NaN},/finite/]
])assert.throws(()=>validateWarrior({...warrior,...change},empty),message);
for(const slotKey of ['main-hand','off-hand'])
 assert.throws(()=>validateWarrior(warrior,{items:[{slotKey,itemId:slotKey==='main-hand'?19352:12602}]}),/catalog equipment remains authoritative/);
// Frontend must not fabricate a shield for catalog mode. Server resolves the actual hands.
const catalogRequest={...warrior,shieldEquipped:false};
assert.equal(validateWarrior(catalogRequest,{items:[{slotKey:'main-hand',itemId:19352}]}).shieldEquipped,false);
for(const petType of ['imp','voidwalker','succubus','incubus','felhunter'])
 assert.equal(validateWarlock({enablePet:true,petType}).petType,petType);
for(const petType of ['infernal','doomguard']){
 assert.throws(()=>validateWarlock({enablePet:true,petType}),/temporary-demon/);
 assert.equal(validateWarlock({enablePet:false,petType}).allowTemporaryDemon,undefined);
}
assert.equal(validateWarlock({enablePet:true,petType:'infernal',allowTemporaryDemon:true}).allowDoomguardRitual,undefined);
assert.throws(()=>validateWarlock({enablePet:true,petType:'doomguard',allowTemporaryDemon:true}),/ritual-prerequisites/);
assert.equal(validateWarlock({enablePet:true,petType:'doomguard',allowTemporaryDemon:true,allowDoomguardRitual:true}).petType,'doomguard');
assert.throws(()=>validateWarlock({enablePet:true,petType:'infernal',allowTemporaryDemon:'false'}),/temporary-demon/);
const html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8');
const js=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
assert.ok(html.indexOf('/solo-dps-assumptions.js')<html.indexOf('/solo-dps.js'));
for(const id of ['warriorTank','warriorManualShield','warriorWeaponMode','warriorWeaponMin','warriorWeaponMax','warriorWeaponSpeed','warriorBlockChance','warriorBlockValue','warriorProtectionExample','warlockTemporary','warlockRitual']){
 assert.ok(html.includes('id="'+id+'"'),id);
 assert.ok(js.includes("$('"+id+"')"),id+' wired');
}
assert.ok(js.includes('SoloDpsAssumptions.validateWarrior(r,cfg.equipment)'));
assert.ok(js.includes('SoloDpsAssumptions.validateWarlock(child)'));
assert.ok(js.includes('allowTemporaryDemon:')&&js.includes('c.allowTemporaryDemon===true'));
assert.ok(js.includes('allowDoomguardRitual:')&&js.includes('c.allowDoomguardRitual===true'));
console.log('Protection manual-assumption and seven Warlock demon frontend contracts passed.');
