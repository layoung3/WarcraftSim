'use strict';
const assert=require('node:assert/strict'),vm=require('node:vm');
const {environment,part,plain,source}=require('./solo-dps-loading-contract.test.cjs');
function env(){
 const e=environment();
 vm.runInContext(part('function buildCharacter(){','function body(){')+part('function body(){','// Strict historical/manual fixed-Cat contract.')+part('const talentTrees=','async function validateTalentSelection()'),e.c);
 e.c.validateRemovedPriorityTargets=()=>{};
 for(const id of ['classKey','spec','race','rotation','petFamily','demonType','rogueMainHandPoison','rogueOffHandPoison'])e.elements[id].tagName='SELECT';
 for(const id of ['seed','duration','targetHealth','iterations','rogueTargetArmor','rogueMainHandWeaponDamage','rogueMainHandSpeedSeconds'])e.elements[id].tagName='INPUT';
 e.responses.set('/api/forever/character/preview-rogue',request=>({effectiveStats:{values:{energy:100,health:2273,'attack-power':210}},attackPower:210,maximumEnergy:100,startingEnergy:request.startingEnergyPercent,maximumHealth:2273,baseline:{race:request.character.race},sourceStatus:'historical-provisional',modelingNotes:['partial'],rotation:{entries:[{abilityKey:'sinister-strike',isEnabled:true,priority:1,conditions:[]}]}}));
 e.elements.classKey.value='rogue';e.elements.spec.value='combat';e.elements.race.value='human';e.elements.race.options=[{value:'human',textContent:'Human'}];
 return e;
}
(async()=>{
 const e=env();await e.c.loadPreset();
 assert.equal(e.c.loadedSpec,'combat');assert.equal(e.c.request.talents.combatPointsSpent,31);assert.equal(e.c.request.mainHandWeaponType,'sword');assert.equal(e.c.request.bothWeaponsDaggers,false);
 assert.equal(e.elements.status.dataset.buildState,'input-required');for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,true,id);assert.equal(e.panels['.workspace'].inert,false);assert.equal(e.elements.provisional.disabled,false);
 await e.c.previewCombatRogue();assert.equal(e.c.combatPreviewMatches(),true);assert.equal(e.elements.status.dataset.buildState,'ready');
 const saved=plain(e.c.saveConfig());const before=plain(e.c.request);
 const held=e.hold('/api/forever/character/preview-rogue'),work=e.c.previewCombatRogue();await held.started;e.elements.rogueMainHandWeaponDamage.value='177';held.release();await work;assert.equal(e.c.combatPreviewMatches(),false,'stale flat receipt cannot enable different raw controls');
 await e.c.previewCombatRogue();assert.equal(e.c.combatPreviewMatches(),true);
 e.elements.rogueMainHandSpeedSeconds.value='';e.elements.seed.value=' ';e.elements.provisional.checked=false;e.c.buildDirty=true;
 const raw=()=>Object.fromEntries(['classKey','spec','race','rogueMainHandWeaponDamage','rogueMainHandSpeedSeconds','seed','provisional'].map(id=>[id,{value:e.elements[id].value,checked:e.elements[id].checked}]));
 const draft=raw(),request=plain(e.c.request);const bad=plain(saved);bad.talents.ranks.unknown=0;
 await assert.rejects(()=>e.c.applyConfig(bad),/outside the current class catalog/);assert.deepEqual(raw(),draft);assert.deepEqual(plain(e.c.request),request);assert.equal(e.c.buildDirty,true);assert.equal(e.elements.status.dataset.buildState,'input-required');assert.equal(e.panels['.workspace'].inert,false);assert.equal(e.elements.provisional.disabled,false);
 assert.throws(()=>e.c.requirePreparedBuild(),/Combat inputs/);assert.equal(e.elements.exportBuild.disabled,true);
 await e.c.applyConfig(saved);await e.c.previewCombatRogue();assert.deepEqual(plain(e.c.saveConfig()),saved);assert.equal(e.c.combatPreviewMatches(),true);
 e.elements.rogueMainHandWeaponDamage.value='199';e.c.buildDirty=true;const cancelled=plain(e.c.saveConfig());
 e.elements.spec.value='assassination';await e.elements.spec.listeners.change();assert.equal(e.elements.presetDialog.open,true);assert.equal(e.elements.spec.value,'combat');await e.elements.cancelPreset.listeners.click();assert.deepEqual(plain(e.c.saveConfig()),cancelled);
 e.elements.spec.value='assassination';await e.elements.spec.listeners.change();await e.elements.confirmPreset.listeners.click();assert.equal(e.c.loadedSpec,'assassination');assert.equal(e.c.request.mainHandWeaponType,'dagger');assert.equal(e.c.request.bothWeaponsDaggers,true);assert.equal(e.c.request.talents.assassinationPointsSpent,38);
 await e.c.applyConfig(saved);await e.c.previewCombatRogue();await e.elements.exportBuild.listeners.click();assert.deepEqual(plain(e.downloads.at(-1).data),saved);
 assert.deepEqual(before.talents.ranks,saved.talents.ranks);
 console.log('Combat production loaders/import/rollback: 31-point swords, flat request readiness, editable invalid controls, dirty cancel/confirm, exact raw failed-import restoration and recovery passed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={env};
