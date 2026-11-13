'use strict';
// Production report admission/comparison and replay regressions. The current
// report is an unchanged failed-CI artifact; historical factories retain their
// own recorded provenance. Mutated copies below are contract tests, not new
// gameplay receipts or a promise of replay across simulator source versions.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),crypto=require('node:crypto');
const api=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const identity=require('../WarcraftSim.Api/wwwroot/solo-dps-combat-identity.js');
const {report:combatReport}=require('./solo-dps-combat-comparison.test.js');
const historicalAssassination=require('./fixtures/assassination-reports/rogue-manual-report.json');
const retainedCurrent=require('./fixtures/rogue-legacy-compat/current-combat-report.json');
const clone=value=>JSON.parse(JSON.stringify(value));
const tuningKey='classWideMutilateBonusDamagePerHand';
const optionalKeys=['useClassWideTalents','customPriority','modifiers','combatItems','bossAttacksUseWeapon'];
// This expected wire snapshot comes from the actual server output, not from the
// new compatibility implementation. HTTP acceptance separately checks live DTOs.
const inactive=Object.fromEntries(optionalKeys.map(key=>[key,clone(retainedCurrent.originalSimulationRequest.rogue[key])]));
const failures=[];let cases=0,slotChecks=0;
function test(name,action){cases++;try{action();}catch(error){failures.push({name,message:error.message});}}
function syncRequest(report){
 if(report.rawServerResponse.preparation){
  report.rawServerResponse.preparation.request=clone(report.originalSimulationRequest.rogue);
  report.combatPreparation=clone(report.rawServerResponse.preparation);
 }
 return report;
}
function changeRequest(report,edit){edit(report.originalSimulationRequest.rogue);return syncRequest(report);}
function changeTuning(report,key,value){
 report.build.encounter.rogue.tuning[key]=value;
 return changeRequest(report,typed=>typed.tuning[key]=value);
}
function withDefaults(report,mask=31){
 return changeRequest(clone(report),typed=>{
  for(let i=0;i<optionalKeys.length;i++)if(mask&(1<<i))typed[optionalKeys[i]]=clone(inactive[optionalKeys[i]]);
 });
}
function admittedUnchanged(report){
 const before=JSON.stringify(report),parsed=api.inspectReport(report);
 assert.deepEqual(parsed.integrityWarnings,[]);
 assert.deepEqual(parsed.report,report,'admission must retain the full original report');
 assert.equal(JSON.stringify(report),before,'admission must not mutate submitted data');
 return parsed;
}
function rejectedUnchanged(report){
 const before=JSON.stringify(report);assert.throws(()=>api.inspectReport(report));
 assert.equal(JSON.stringify(report),before,'rejection must leave submitted data unchanged');
}
function equivalents(left,right){
 const a=JSON.stringify(left),b=JSON.stringify(right),result=api.compareReports(left,right);
 assert.equal(result.comparable,true,JSON.stringify(result.warnings));
 assert.deepEqual(result.changes,[],'inactive serialization fields are not gameplay changes');
 assert.equal(result.dpsDelta,0);
 assert.equal(JSON.stringify(left),a);assert.equal(JSON.stringify(right),b);
}
const factories={combat:()=>combatReport(),assassination:()=>clone(historicalAssassination)};
test('retained current failure has exact expected source, five additions and matching owned request',()=>{
 assert.equal(crypto.createHash('sha256').update(fs.readFileSync(require.resolve('./fixtures/rogue-legacy-compat/current-combat-report.json'))).digest('hex'),'a920c1e79b650208e24758a51aa9e60b917e23fa7625fda4e004ceeeb3bb4403');
 assert.equal(retainedCurrent.metadata.sourceCommit,'a2aa5f45ba0fff11b4585f5623f616dcf8837303');
 assert.deepEqual(retainedCurrent.originalSimulationRequest.rogue,retainedCurrent.rawServerResponse.preparation.request);
 assert.deepEqual(inactive.modifiers,{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'initial-only'});
 assert.equal(Object.keys(inactive.combatItems).length,33);
 assert.equal(inactive.combatItems.restorationPolicy,'conservative-no-overcap');
 assert.deepEqual(inactive.combatItems.disabledItemKeys,[]);
 assert.equal(inactive.combatItems.allowEmergencyHealingOvercap,false);
 assert.equal(inactive.combatItems.manaRubyBelowManaPercent,65);
 assert.equal(inactive.combatItems.minimumHealthToUseRune,1200);
 assert.equal(inactive.combatItems.minimumHealthToUseSapper,1000);
 assert.equal(inactive.useClassWideTalents,false);assert.equal(inactive.customPriority,null);assert.equal(inactive.bossAttacksUseWeapon,false);
 admittedUnchanged(retainedCurrent);
});
for(const [spec,factory] of Object.entries(factories)){
 test(spec+' genuine historical report remains admitted',()=>admittedUnchanged(factory()));
 test(spec+' rejects an unknown typed field even when inactive',()=>rejectedUnchanged(changeRequest(factory(),t=>t.unknownInactive=false)));
 for(const key of Object.keys(factory().originalSimulationRequest.rogue))test(spec+' still requires historical typed field '+key,()=>{
  rejectedUnchanged(changeRequest(withDefaults(factory()),t=>delete t[key]));
 });
 test(spec+' rejects coherent unknown tuning field',()=>rejectedUnchanged(changeTuning(withDefaults(factory()),'unknownInactive',0)));
 for(const key of Object.keys(factory().originalSimulationRequest.rogue.tuning))test(spec+' still requires historical tuning field '+key,()=>{
  const bad=withDefaults(factory());delete bad.build.encounter.rogue.tuning[key];changeRequest(bad,t=>delete t.tuning[key]);rejectedUnchanged(bad);
 });
 for(const [key,value] of [['mainHandHitPercent',101],['mainHandSpeedSeconds',0],['mutilateBonusDamagePerHand',-1],['mutilateBonusDamagePerHand','13'],['improvedExposeArmorFivePointRefund',6]])test(spec+' rejects coherent invalid historical tuning '+key+' '+JSON.stringify(value),()=>{
  rejectedUnchanged(changeTuning(withDefaults(factory()),key,value));
 });
 for(let mask=0;mask<32;mask++)test(spec+' exact optional defaults subset '+mask,()=>{
  const old=factory(),current=withDefaults(old,mask);admittedUnchanged(current);equivalents(old,current);
  assert.equal(current.build.modifiers.targetDebuffMode,'maintained','saved modifier mode is an independent contract');
 });
 for(const savedNull of [false,true])for(const requestNull of [false,true])test(spec+' saved/request tuning null representations '+savedNull+'/'+requestNull,()=>{
  const old=factory(),current=withDefaults(old);
  if(savedNull)current.build.encounter.rogue.tuning[tuningKey]=null;
  if(requestNull)changeRequest(current,t=>t.tuning[tuningKey]=null);
  admittedUnchanged(current);equivalents(old,current);
 });
 for(const value of [0,-1,79,'79','',false,true,{},[]])for(const location of ['saved','request','both'])test(spec+' rejects non-null tuning '+location+' '+JSON.stringify(value),()=>{
  const bad=withDefaults(factory());
  if(location!=='request')bad.build.encounter.rogue.tuning[tuningKey]=clone(value);
  if(location!=='saved')changeRequest(bad,t=>t.tuning[tuningKey]=clone(value));
  rejectedUnchanged(bad);
 });
 const invalidTop={useClassWideTalents:[true,null,0,'false',[],{}],customPriority:[[],{},'',0,false,['sinister-strike']],bossAttacksUseWeapon:[true,null,0,'false',[],{}],modifiers:[null,[],false,0,''],combatItems:[null,[],false,0,'']};
 for(const [key,values] of Object.entries(invalidTop))for(const value of values)test(spec+' rejects coherent active/wrong-type '+key+' '+JSON.stringify(value),()=>{
  rejectedUnchanged(changeRequest(withDefaults(factory()),t=>t[key]=clone(value)));
 });
 for(const objectKey of ['modifiers','combatItems']){
  test(spec+' rejects unknown '+objectKey+' key with otherwise inactive values',()=>rejectedUnchanged(changeRequest(withDefaults(factory()),t=>t[objectKey].unknownInactive=null)));
  for(const [key,value] of Object.entries(inactive[objectKey])){
   test(spec+' requires present '+objectKey+'.'+key,()=>rejectedUnchanged(changeRequest(withDefaults(factory()),t=>delete t[objectKey][key])));
   const alternatives=Array.isArray(value)?[null,{},'',false,['unsupported']]:typeof value==='number'?[value+1,String(value),null,false,[],{}]:typeof value==='boolean'?[!value,String(value),null,0,[],{}]:[value==='initial-only'?'maintained':'legacy-half-mean','',null,false,0,[],{}];
   for(const alternative of alternatives)test(spec+' rejects '+objectKey+'.'+key+' '+JSON.stringify(alternative),()=>{
    rejectedUnchanged(changeRequest(withDefaults(factory()),t=>t[objectKey][key]=clone(alternative)));
   });
  }
 }
 test(spec+' exact historical tuning is not replaced by class-wide defaults',()=>{
  const changed=withDefaults(factory());changeTuning(changed,'mutilateBonusDamagePerHand',17.25);
  changeRequest(changed,t=>t.tuning[tuningKey]=null);admittedUnchanged(changed);
  assert.equal(changed.build.encounter.rogue.tuning.mutilateBonusDamagePerHand,17.25);
  assert.equal(changed.originalSimulationRequest.rogue.tuning.mutilateBonusDamagePerHand,17.25);
 });
 test(spec+' source difference preserves its strict or advisory policy',()=>{
  const old=factory(),changed=withDefaults(old);changed.metadata.sourceCommit='another-source-version';
  admittedUnchanged(changed);const result=api.compareReports(old,changed);
  if(spec==='combat'){
   assert.equal(result.comparable,false);assert.equal(result.dpsDelta,null);
   assert(result.warnings.some(value=>/Source\/reference assumptions/.test(value)));
  }else{
   // Existing Assassination reports explicitly treat metadata as advisory.
   // Preserve that policy, but do not let inactive-field normalization hide a
   // source change or imply that the two source versions were replay-verified.
   assert.equal(result.comparable,true);assert.equal(result.dpsDelta,0);
   assert([...result.caveats,...result.changes].some(value=>/source|reference/i.test(value)&&/differ/i.test(value)),'advisory source difference remains visible');
  }
 });
}

const transactionalMutations={
 'active class-wide':r=>changeRequest(r,t=>t.useClassWideTalents=true),
 'empty custom priority':r=>changeRequest(r,t=>t.customPriority=[]),
 'active boss weapon':r=>changeRequest(r,t=>t.bossAttacksUseWeapon=true),
 'changed modifier mode':r=>changeRequest(r,t=>t.modifiers.targetDebuffMode='maintained'),
 'unknown modifier key':r=>changeRequest(r,t=>t.modifiers.unknown=[]),
 'selected buff':r=>changeRequest(r,t=>t.modifiers.buffs=['battle-shout']),
 'changed item policy':r=>changeRequest(r,t=>t.combatItems.restorationPolicy='legacy-half-mean'),
 'changed inactive item threshold':r=>changeRequest(r,t=>t.combatItems.majorManaPotionBelowManaPercent=50),
 'missing inactive item threshold':r=>changeRequest(r,t=>delete t.combatItems.majorManaPotionBelowManaPercent),
 'unknown item key':r=>changeRequest(r,t=>t.combatItems.unknown=0),
 'string zero item charge':r=>changeRequest(r,t=>t.combatItems.majorManaPotionCharges='0'),
 'active healing item':r=>changeRequest(r,t=>t.combatItems.majorHealingPotionCharges=1),
 'non-null class-wide tuning':r=>changeTuning(r,tuningKey,0),
 'unknown typed field':r=>changeRequest(r,t=>t.unknownInactive=false),
 'unknown tuning field':r=>changeTuning(r,'unknownInactive',0),
 'missing required old typed field':r=>changeRequest(r,t=>delete t.bossHitDamage),
 'missing required old tuning field':r=>{delete r.build.encounter.rogue.tuning.energyPerSecond;changeRequest(r,t=>delete t.tuning.energyPerSecond);},
 'out-of-range historical tuning':r=>changeTuning(r,'mainHandHitPercent',101),
 'zero required speed':r=>changeTuning(r,'mainHandSpeedSeconds',0),
 'legacy invalid weapon':r=>{r.build.encounter.rogue.mainHandWeaponType='dagger';changeRequest(r,t=>t.mainHandWeaponType='dagger');},
 'coherent incoming attacks':r=>{r.build.encounter.rogue.enableBossAttacks=true;changeRequest(r,t=>t.enableBossAttacks=true);},
 'summary damage tampering':r=>r.combatSummary.damageDone++,
 'copied preview tampering':r=>r.resolvedCharacter.attackPower++,
 'preparation copy tampering':r=>r.combatPreparation.preview.attackPower++,
 'one-sided inactive default omission':r=>delete r.rawServerResponse.preparation.request.bossAttacksUseWeapon,
 'one-sided nullable tuning omission':r=>{changeTuning(r,tuningKey,null);delete r.combatPreparation.request.tuning[tuningKey];}
};
for(const [name,mutate] of Object.entries(transactionalMutations))test('Combat rejects '+name,()=>{
 const bad=withDefaults(combatReport());mutate(bad);rejectedUnchanged(bad);
});
for(const key of [...optionalKeys,tuningKey])for(const copy of ['submitted','owned','duplicate'])test('Combat rejects one-sided '+copy+' omission '+key,()=>{
 const bad=withDefaults(combatReport());changeTuning(bad,tuningKey,null);
 const typed=copy==='submitted'?bad.originalSimulationRequest.rogue:copy==='owned'?bad.rawServerResponse.preparation.request:bad.combatPreparation.request;
 if(key===tuningKey)delete typed.tuning[key];else delete typed[key];
 rejectedUnchanged(bad);
});
test('real encounter changes remain noncomparable',()=>{
 for(const key of ['bossSwingSeconds','bossHitDamage','targetMaximumHealth']){
  const old=combatReport(),changed=withDefaults(old);
  changeRequest(changed,t=>t[key]++);
  if(key==='targetMaximumHealth')changed.build.encounter.targetHealth=String(changed.originalSimulationRequest.rogue[key]);
  admittedUnchanged(changed);const result=api.compareReports(old,changed);
  assert.equal(result.comparable,false,key);assert.equal(result.dpsDelta,null,key);
  assert(result.changes.some(value=>value.includes('Encounter assumptions')),key);
 }
});
test('real historical tuning and talent changes stay visible',()=>{
 const old=combatReport(),changed=withDefaults(old);changeTuning(changed,'mutilateBonusDamagePerHand',17.25);
 const tuning=api.compareReports(old,changed);assert.equal(tuning.comparable,true);
 assert(tuning.changes.some(value=>value.includes('tuning mutilateBonusDamagePerHand')));
 const talents=api.compareReports(withDefaults(combatReport()),withDefaults(combatReport(30)));
 assert.equal(talents.comparable,true);assert(talents.changes.some(value=>value.includes('adrenaline-rush')));
});
test('genuine old/current source receipts are preserved rather than made equivalent',()=>{
 const old=combatReport(),current=clone(retainedCurrent),before=JSON.stringify(current);
 const result=api.compareReports(old,current);assert.equal(result.comparable,false);
 assert(result.warnings.some(value=>value.includes('Source/reference assumptions')));
 assert.equal(JSON.stringify(current),before);
});
test('replay preserves absent/null/default field distinctions in the full response',()=>{
 const raw=retainedCurrent.rawServerResponse,expected=identity.signature(raw);
 assert.equal(identity.signature(clone(raw)),expected);
 for(const key of optionalKeys){const changed=clone(raw);delete changed.preparation.request[key];assert.notEqual(identity.signature(changed),expected,key);}
 const omitted=clone(raw);delete omitted.preparation.request.tuning[tuningKey];assert.notEqual(identity.signature(omitted),expected);
 const modified=clone(raw);modified.sim.dps+=1e-10;assert.notEqual(identity.signature(modified),expected,'no new numeric tolerance');
 const extra=clone(raw);extra.unexpectedMetadata=null;assert.notEqual(identity.signature(extra),expected,'no unknown-field stripping');
});
test('replay still renames only generated event GUIDs and preserves their joins',()=>{
 const first='11111111-1111-4111-8111-111111111111',second='22222222-2222-4222-8222-222222222222';
 const original={sim:{run:{timeline:[{abilityExecutionId:first},{abilityExecutionId:first}]}}};
 const renamed=clone(original);for(const event of renamed.sim.run.timeline)event.abilityExecutionId=second;
 assert.equal(identity.signature(original),identity.signature(renamed));
 renamed.sim.run.timeline[1].abilityExecutionId=first;assert.notEqual(identity.signature(original),identity.signature(renamed));
 const anchored=clone(original);anchored.literalGuid=first;
 const changed=clone(anchored);for(const event of changed.sim.run.timeline)event.abilityExecutionId=second;
 assert.notEqual(identity.signature(anchored),identity.signature(changed));
});

async function rejectedImportsPreserveSlots(){
 const nodes=new Map(),listeners={};
 const element=()=>({textContent:'',hidden:false,disabled:false,value:'',children:[],listeners:{},addEventListener(key,fn){this.listeners[key]=fn;},replaceChildren(){this.children=[];},append(child){this.children.push(child);},click(){this.listeners.click?.();}});
 const document={getElementById(id){if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);},createElement:element,addEventListener(key,fn){listeners[key]=fn;}};
 const context={document,console};context.window=context;vm.createContext(context);
 // The production browser uses the real shared assumptions/compatibility API.
 vm.runInContext(fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps-assumptions.js','utf8'),context);
 vm.runInContext(fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps-comparison.js','utf8'),context);
 for(const [slot,report] of [['A',withDefaults(combatReport())],['B',withDefaults(combatReport(30))]]){
  listeners['warcraftsim:simulation-complete']({detail:report});document.getElementById('compareUse'+slot).click();
 }
 const snapshot=()=>JSON.stringify(['compareLabelA','compareLabelB','compareRows','compareChanges'].map(id=>document.getElementById(id)),(key,value)=>key==='listeners'?undefined:value);
 assert.match(document.getElementById('compareLabelA').textContent,/rogue.*combat/);
 assert.match(document.getElementById('compareLabelB').textContent,/rogue.*combat/);
 const before=snapshot();
 for(const [name,mutate] of Object.entries(transactionalMutations)){
  const bad=withDefaults(combatReport());mutate(bad);
  for(const slot of ['A','B']){
   document.getElementById('compareImport'+slot).click();
   const file=document.getElementById('compareFile');file.files=[{text:async()=>JSON.stringify(bad)}];await file.listeners.change();
   assert.equal(snapshot(),before,name+' preserves both slots');
   assert.match(document.getElementById('compareStatus').textContent,/not changed/);slotChecks++;
  }
 }
}
rejectedImportsPreserveSlots().catch(error=>failures.push({name:'actual A/B import transaction',message:error.message})).then(()=>{
 if(failures.length){console.error(JSON.stringify({suite:'Legacy Rogue compatibility',cases,slotChecks,failures},null,2));process.exitCode=1;}
 else console.log('Legacy Rogue compatibility passed: '+cases+' production regression cases and '+slotChecks+' actual A/B import rollback checks; retained current/older fixtures, deep inactive defaults, nullable tuning, source policy and exact replay.');
});
