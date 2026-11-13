'use strict';
// Real HTTP contracts. Synthetic observations are not current-client evidence.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const BASE=process.env.CP174_BASE_URL||'http://127.0.0.1:5142',origin=new URL(BASE);
assert.ok(origin.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(origin.hostname));
assert.ok(!origin.username&&!origin.password&&!origin.search&&!origin.hash);
const out=path.resolve(process.env.CP174_REPORT_DIR||'acceptance-target-semantics-cloud-20261110/rogue-solo-ui','http');fs.mkdirSync(out,{recursive:true});
let checks=0;
async function call(route,label,input,status=200){
 const body=input===undefined?undefined:JSON.stringify(input);
 const response=await fetch(BASE+route,{method:body===undefined?'GET':'POST',headers:body===undefined?undefined:{'content-type':'application/json'},body,redirect:'error',signal:AbortSignal.timeout(30000)});
 const data=await response.json();fs.writeFileSync(path.join(out,label+'.json'),JSON.stringify({sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',scope:'software acceptance; synthetic observations; not current-client certified',request:input,status:response.status,response:data},null,2));
 assert.equal(response.status,status,label+': '+JSON.stringify(data));checks++;return data;
}
const envelope=r=>({classKey:'rogue',specialization:'assassination',itemizedTalentMode:true,itemizedTalentRanks:r.talents.ranks,rogue:r});
(async()=>{
 const capability=await call('/api/forever/shared/capabilities/rogue?specialization=assassination','rogue-capability');
 assert.deepEqual(capability.specializations,['assassination']);assert.equal(capability.executesThroughCurrentUnifiedEndpoint,true);assert.equal(capability.hasRotationPreview,true);
 for(const flag of ['hasExecutableCustomRotation','hasSeededBatchResults','hasSharedCombatItemProcessor','executesArbitraryCrossTreeModeledTalents'])assert.equal(capability[flag],false,flag);
 const preset=await call('/api/forever/talents/rogue/assassination/default-itemized','rogue-default-itemized');
 const expected={malice:5,ruthlessness:3,'improved-slice-and-dice':3,'improved-expose-armor':2,lethality:5,'vile-poisons':5,'cold-blood':1,'improved-poisons':5,vigor:2,mutilate:1,'seal-fate':5,venom:1};
 assert.deepEqual(preset.ranks,expected);assert.equal(preset.totalRankedPoints,38);assert.equal(preset.unspentPoints,13);
 assert.deepEqual(preset.ranks,JSON.parse(fs.readFileSync(path.join(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json'))).ranks);
 const r=await call('/api/sim/rogue/defaults?spec=assassination','legacy-defaults');
 assert.equal(r.talents.assassinationPointsSpent,41);assert.equal(r.talents.combatPointsSpent,10);assert.equal(r.useExposeArmor,false);
 Object.assign(r,{durationSeconds:20,captureTimeline:true,rogueMaximumHealth:4700,startingEnergyPercent:25,targetArmor:2100,targetDodgePercent:3,targetParryPercent:7});
 r.character.race='human';r.character.equipment={items:[]};r.character.talents={selections:[]};r.character.buffs={selections:[]};
 r.talents={assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0,ranks:preset.ranks};
 r.level60Baseline={race:'human',classKey:'rogue',strength:100,agility:100,stamina:100,intellect:100,spirit:100,observedUnbuffedMeleeAttackPower:700,observedUnbuffedMaximumHealth:3000,clientBuild:'synthetic',sourceReference:'synthetic-ui-http-test-not-live-data',recordedAtUtc:'2026-11-10T00:00:00Z'};
 async function parity(label,input){
  const p=await call('/api/forever/character/preview-rogue',label+'-preview',input);
  const rotation=await call('/api/forever/rotation/preview',label+'-rotation',envelope(input));
  const execution=await call('/api/forever/character/simulate-level60',label+'-run',envelope(input)),sim=execution.sim;
  for(const key of ['maximumEnergy','maximumHealth'])assert.equal(p[key],sim[key],label+' '+key);
  assert.equal(p.attackPower,(p.effectiveStats.values||p.effectiveStats)['attack-power']);
  assert.equal(p.startingEnergy,sim.run.summary.actorSummaries.rogue.startingResources.energy);
  assert.equal(rotation.mode,'fixed-configured');assert.deepEqual(p.rotation.entries.map(({abilityKey,isEnabled,priority,conditions})=>({abilityKey,isEnabled,priority,conditions})),rotation.entries);assert.ok(rotation.entries.length>0);
  assert.equal(execution.talentSourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');assert.equal(execution.talentSourceStatus,'historical-provisional-not-current-client-certified');
  assert.ok(sim.damageDone>0);assert.ok(sim.endingEnergy>=0&&sim.endingEnergy<=sim.maximumEnergy);return {p,sim};
 }
 const base=await parity('observed-health',r);assert.equal(base.p.maximumHealth,3000);assert.equal(base.p.maximumEnergy,110);assert.equal(base.p.startingEnergy,27.5);assert.equal(base.p.attackPower,700);
 const again=await parity('repeat',r);for(const key of ['damageDone','dps','endingEnergy','maximumEnergy','endingHealth','maximumHealth','damageByAbility'])assert.deepEqual(again.sim[key],base.sim[key],key+' deterministic repeat');
 const lower=structuredClone(r);delete lower.talents.ranks['improved-expose-armor'];lower.talents.assassinationPointsSpent=36;
 const validation=await call('/api/forever/talents/rogue/assassination/validate-itemized','legal-36-allocation',lower.talents);assert.equal(validation.valid,true);assert.equal(validation.pointsSpent,36);assert.equal(validation.remaining,15);await parity('legal-36',lower);
 const fallback=structuredClone(r);delete fallback.level60Baseline.observedUnbuffedMaximumHealth;assert.equal((await parity('manual-health-fallback',fallback)).p.maximumHealth,4700);
 for(const poison of ['none','instant','deadly','wound','mind-numbing','crippling']){const x=structuredClone(r);x.mainHandPoison=poison;x.offHandPoison=poison;await parity('coating-'+poison,x);}
 for(const energy of [0,100]){const x=structuredClone(r);x.startingEnergyPercent=energy;assert.equal((await parity('starting-energy-'+energy,x)).p.startingEnergy,110*energy/100);}
 const legacy=structuredClone(r);legacy.useExposeArmor=true;legacy.tuning.improvedExposeArmorFivePointRefund=5;legacy.tuning.mutilateBonusDamagePerHand=17.25;await parity('legacy-tuning',legacy);
 for(const [label,mutate] of [['negative-armor',x=>x.targetArmor=-1],['zero-health',x=>{delete x.level60Baseline.observedUnbuffedMaximumHealth;x.rogueMaximumHealth=0;}],['invalid-energy',x=>x.startingEnergyPercent=101],['invalid-poison',x=>x.mainHandPoison='invalid'],['gear',x=>x.character.equipment.items=[{slotKey:'main-hand',itemId:19102}]]]){
  const x=structuredClone(r);mutate(x);await call('/api/forever/character/preview-rogue',label+'-preview-rejected',x,400);await call('/api/forever/character/simulate-level60',label+'-run-rejected',envelope(x),400);
 }
 fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({status:'passed',checks,sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',scope:'software acceptance, not live-client certification'},null,2));console.log('Rogue UI HTTP gates passed: '+checks);
})().catch(error=>{fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({error:String(error),checks},null,2));console.error(error);process.exitCode=1;});
