'use strict';
// Real HTTP preview/rotation/strict execution parity. Synthetic observations are
// test inputs, not current-client evidence or a claim of class certification.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const BASE=process.env.CP174_BASE_URL||'http://127.0.0.1:5142';
const origin=new URL(BASE);assert.ok(origin.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(origin.hostname));
const out=path.resolve(process.env.CP174_REPORT_DIR||'acceptance-target-semantics-cloud-20261110/enhancement-solo-ui','http');fs.mkdirSync(out,{recursive:true});
let checks=0;
async function call(route,label,input,status=200){
 const body=input===undefined?undefined:JSON.stringify(input);
 const r=await fetch(BASE+route,{method:body===undefined?'GET':'POST',headers:body===undefined?undefined:{'content-type':'application/json'},body,redirect:'error',signal:AbortSignal.timeout(30000)});
 const data=await r.json();fs.writeFileSync(path.join(out,label+'.json'),JSON.stringify({sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',scope:'software-only; synthetic observations; not current-client certified',request:input,status:r.status,response:data},null,2));
 assert.equal(r.status,status,label+': '+JSON.stringify(data));checks++;return data;
}
// CombatEvent exposes exactly five per-run GUID identity fields. Their owners
// allocate fresh GUIDs (AbilityExecutionState, AutoAttackState, AuraInstance,
// AbsorbManager and ForcedTargetState). Canonicalize first-seen identities,
// retaining nulls, repeated references, every other field and original ordering.
// Reuse the established anchor-aware oracle: complete parsed simulation response,
// only actual generated nonzero event GUIDs renamed; literals and joins stay exact.
const {canonical:canonicalSimulation}=require('./solo-dps-combat-browser-support.cjs');
const envelope=r=>({classKey:'shaman',specialization:'enhancement',itemizedTalentMode:true,itemizedTalentRanks:r.talents.ranks,enhancement:r});
(async()=>{
 const capability=await call('/api/forever/shared/capabilities/shaman?specialization=enhancement','enhancement-capability');
 assert.equal(capability.classKey,'shaman');assert.deepEqual(capability.specializations,['enhancement']);
 assert.equal(capability.executesThroughCurrentUnifiedEndpoint,true);assert.equal(capability.hasRotationPreview,true);
 assert.equal(capability.hasExecutableCustomRotation,false);assert.equal(capability.hasSeededBatchResults,false);
 assert.deepEqual(capability.encounterControls,['target-health','target-armor','player-maximum-health','duration','seed','main-hand-weapon','dual-wield','off-hand-weapon','hand-imbues','ability-toggles']);
 assert.match(capability.knownRestrictions.join(' '),/0\/36\/0.*15 unspent/);
 const elemental=await call('/api/forever/shared/capabilities/shaman','elemental-capability-unchanged');assert.deepEqual(elemental.specializations,['elemental']);
 const preset=await call('/api/forever/talents/shaman/enhancement/default-itemized','enhancement-default-itemized');
 assert.deepEqual(preset.ranks,JSON.parse(fs.readFileSync(path.join(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/shaman-enhancement.json'))).ranks);
 assert.equal(preset.totalRankedPoints,36);assert.equal(51-preset.totalRankedPoints,15);
 assert.equal(preset.provenance.sourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');
 assert.match(preset.provenance.statement,/15 unspent/);

 assert.equal(Object.values(preset.ranks).reduce((a,b)=>a+b),36);assert.equal(Object.keys(preset.ranks).length,12);
 const catalog=await call('/api/forever/talents/shaman/enhancement','enhancement-modeled-catalog');
 assert.deepEqual(catalog.talents.map(x=>x.key).sort(),[...Object.keys(preset.ranks),'convection','concussion','reverberation'].sort());
 for(const key of ['convection','concussion','reverberation'])assert.equal(catalog.talents.find(x=>x.key===key).tree,'elemental');
 assert.ok(!catalog.talents.some(x=>x.key==='call-of-flame'));
 const r=await call('/api/sim/enhancement-shaman/defaults','defaults');
 Object.assign(r,{durationSeconds:20,inputLatencyMs:0,mainHandImbue:'none',offHandImbue:'none',useWindfuryWeapon:false,useWindfuryTotem:false,maximumHealth:4700});
 r.character.race='orc';r.character.equipment={items:[]};r.character.talents={selections:[]};r.character.buffs={selections:[]};
 r.talents={elementalPointsSpent:0,enhancementPointsSpent:36,restorationPointsSpent:0,ranks:preset.ranks};
 for(const key of Object.keys(r.combatItems))if(key.endsWith('Charges'))r.combatItems[key]=0;
 r.level60Baseline={race:'orc',classKey:'shaman',strength:100,agility:100,stamina:100,intellect:100,spirit:100,observedUnbuffedMeleeAttackPower:700,observedUnbuffedMaximumMana:2000,observedUnbuffedMaximumHealth:3000,clientBuild:'synthetic',sourceReference:'synthetic-ui-http-test-not-live-data',recordedAtUtc:'2026-11-10T00:00:00Z'};
 async function parity(label,input){
  const p=await call('/api/forever/character/preview-enhancement',label+'-preview',input);
  const rotation=await call('/api/forever/rotation/preview',label+'-rotation',envelope(input));
  const execution=await call('/api/forever/character/simulate-level60',label+'-run',envelope(input));const sim=execution.sim;
  for(const key of ['attackPower','spellPower','maximumMana','maximumHealth'])assert.equal(p[key],sim[key],label+' '+key);
  assert.ok(rotation.entries.length>0,'Real engine rotation required');
  assert.equal(rotation.execution,'EnhancementShamanSimRunner.BuildRotation');
  assert.deepEqual(p.rotation.entries.map(x=>({abilityKey:x.abilityKey,isEnabled:x.isEnabled,priority:x.priority,conditions:x.conditions})),rotation.entries);
  assert.equal(execution.talentSourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');
  assert.equal(execution.talentSourceStatus,'historical-provisional-not-current-client-certified');
  assert.equal(sim.run.summary.actorSummaries.shaman.startingResources.mana,p.maximumMana);
  return {p,sim};
 }
 const base=await parity('observed-health',r);assert.equal(base.p.maximumHealth,3000);assert.equal(base.p.attackPower,810);assert.equal(base.p.maximumMana,2150);assert.equal(base.p.spellPower,33);
 const again=await parity('repeat',r);for(const key of ['damageDone','dps','endingMana','maximumMana','maximumHealth','damageByAbility','combatItemUses'])assert.deepEqual(again.sim[key],base.sim[key],key+' deterministic repeat');
 // The unchanged starting preview is also the baseline for optional legal
 // cross-tree allocations. Selection/import roundtrips are JSON transport here;
 // native controls and downloaded reports are exercised by the browser suite.
 for(const [label,added,total] of [['46',{convection:5,concussion:5},46],['51',{convection:5,concussion:5,reverberation:5},51]]){
  const selected=structuredClone(r);selected.talents.elementalPointsSpent=total-36;Object.assign(selected.talents.ranks,added);
  const imported=JSON.parse(JSON.stringify(selected));assert.deepEqual(imported,selected);
  const legal=await call('/api/forever/talents/shaman/enhancement/validate-itemized','cross-tree-'+label+'-validation',imported.talents);
  assert.equal(legal.pointsSpent,total);assert.equal(legal.remaining,51-total);
  const result=await parity('cross-tree-'+label,imported);
  for(const key of ['attackPower','spellPower','maximumMana','maximumHealth'])assert.equal(result.p[key],base.p[key],key+' unchanged by cross-tree passives');
  assert.equal(imported.inputLatencyMs,0);
  const direct=await call('/api/sim/enhancement-shaman/run','cross-tree-'+label+'-direct',imported);
  const replay=await call('/api/forever/character/simulate-level60','cross-tree-'+label+'-replay',envelope(imported));
  for(const key of ['damageDone','dps','endingMana','maximumMana','maximumHealth','damageByAbility','combatItemUses']){
   assert.deepEqual(direct[key],result.sim[key],key+' direct/shared same latency');assert.deepEqual(replay.sim[key],result.sim[key],key+' deterministic replay');
  }
  assert.deepEqual(canonicalSimulation(direct),canonicalSimulation(result.sim));assert.deepEqual(canonicalSimulation(replay.sim),canonicalSimulation(result.sim));
 }
 const lower=structuredClone(r);delete lower.talents.ranks['rage-of-the-farseer'];lower.talents.enhancementPointsSpent=35;
 const lowerValidation=await call('/api/forever/talents/shaman/enhancement/validate-itemized','legal-35-allocation',lower.talents);
 assert.equal(lowerValidation.valid,true);assert.equal(lowerValidation.pointsSpent,35);assert.equal(lowerValidation.remaining,16);
 assert.deepEqual(envelope(lower).itemizedTalentRanks,lower.talents.ranks);
 const lowerRun=await parity('legal-35-run',lower);assert.ok(lowerRun.sim.damageDone>0,'Legally unspent points must execute');
 const fallback=structuredClone(r);delete fallback.level60Baseline.observedUnbuffedMaximumHealth;
 assert.equal((await parity('manual-health-fallback',fallback)).p.maximumHealth,4700);
 const changed=structuredClone(r);Object.assign(changed,{targetArmor:0,dualWield:true,weaponMinDamage:170,weaponMaxDamage:220,weaponSpeedSeconds:2.6,offHandWeaponMinDamage:100,offHandWeaponMaxDamage:130,offHandWeaponSpeedSeconds:1.8,mainHandImbue:'rockbiter',offHandImbue:'flametongue'});
 const dual=await parity('dual-wield-manual',changed);assert.notEqual(dual.sim.damageDone,base.sim.damageDone);
 for(const imbue of ['none','windfury','flametongue','frostbrand','rockbiter']){const x=structuredClone(changed);x.mainHandImbue=imbue;await parity('main-hand-'+imbue,x);}
 for(const [label,mutate] of [['negative-armor',x=>x.targetArmor=-1],['reversed-weapon',x=>x.weaponMinDamage=x.weaponMaxDamage+1],['zero-health',x=>x.maximumHealth=0],['offhand-windfury',x=>x.offHandImbue='windfury'],['gear',x=>x.character.equipment.items=[{slotKey:'main-hand',itemId:19102}]]]){
  const x=structuredClone(changed);mutate(x);await call('/api/forever/character/preview-enhancement',label+'-preview-rejected',x,400);await call('/api/forever/character/simulate-level60',label+'-run-rejected',envelope(x),400);
 }
 fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({status:'passed',checks,sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',scope:'software acceptance, not live-client certification'},null,2));console.log('Enhancement UI HTTP gates passed: '+checks);
})().catch(error=>{console.error(error);process.exitCode=1;});
