'use strict';
const assert=require('node:assert/strict'),vm=require('node:vm'),fs=require('node:fs');
const {inspectReport,compareReports}=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const f=require('./solo-dps-feral-comparison-fixture.cjs'),{source,part}=require('./solo-dps-feral-fixture.cjs');
let cases=0;
function blocked(a,b,pattern){
 const result=compareReports(a,b);assert.equal(result.comparable,false);assert.equal(result.dpsDelta,null);assert.equal(result.damageDelta,null);assert.equal(result.dpsDeltaPercent,null);
 if(pattern)assert.ok(result.warnings.some(x=>pattern.test(x)),result.warnings.join('\n'));cases++;return result;
}
function failClosed(a,b){
 let result;
 try{result=compareReports(a,b);}catch(error){assert.match(error.message,/complete Solo DPS report/);cases++;return;}
 assert.equal(result.comparable,false,'Conflicting/missing report must not pass: '+JSON.stringify(result.warnings));assert.equal(result.dpsDelta,null);cases++;
}
function variant(change,changeRaw=()=>{}){const saved=f.build();change(saved);f.totals(saved);const raw=f.rawResponse(saved);changeRaw(raw);return f.report(saved,raw);}
const a=f.report(),snapshot=JSON.stringify(a),inspection=inspectReport(a);
assert.equal(compareReports(a,a).comparable,true);assert.equal(inspection.integrityWarnings.length,0);
assert.equal(inspection.summary.spellbookAttackPower,902.4);assert.equal(inspection.summary.maximumHealth,2173);assert.equal(inspection.summary.maximumMana,2594);
assert.equal(inspection.summary.startingMana,2594);assert.equal(inspection.summary.manaAfterInitialShift,2344);assert.equal(inspection.summary.startingEnergy,0);
assert.equal(inspection.summary.endingHealth,null,'No fabricated Health from legacy tank projections');
assert.equal(inspection.reconciliation.status,'reconciled');assert.equal(inspection.abilityReconciliation.status,'reconciled');
assert.equal(JSON.stringify(a),snapshot,'Inspection and comparison cannot mutate the source report');

// Build choices remain comparable. Synthetic damage is deliberately constant;
// these assertions test classification, not a fabricated AP-to-DPS oracle.
for(const [key,value] of [['preTalentCatAttackPower',900],['weaponDps',0],['weaponDps',77.125],['meleeHitPercent',0],['meleeCritPercent',31.25]]){
 const b=variant(saved=>saved.manualInputs[key]=value),result=compareReports(a,b);assert.equal(result.comparable,true,result.warnings.join('\n'));assert.ok(result.changes.some(x=>x.includes(key)));cases++;
}
for(const key of ['berserk','furor']){
 const b=variant(saved=>saved.talents.ranks[key]=0),result=compareReports(a,b);assert.equal(result.comparable,true,result.warnings.join('\n'));assert.ok(result.changes.some(x=>x.includes('Talent '+key)));cases++;
}
const cooldownA=variant(saved=>saved.scenario.useFeralCooldowns=true),cooldownB=variant(saved=>{saved.scenario.useFeralCooldowns=true;saved.talents.ranks.berserk=0;});
assert.equal(compareReports(cooldownA,cooldownB).comparable,true,'A learned talent changes priority without changing the encounter');
assert.ok(compareReports(cooldownA,cooldownB).changes.some(x=>/configured learned priority/.test(x)));cases++;
// A legal empty allocation, explicit zero map keys, and fractional/zero manual
// values are data, never truthiness-based fallbacks to legacy authored totals.
for(const value of [0,.125,800.125]){
 const b=variant(saved=>{saved.manualInputs.preTalentCatAttackPower=value;saved.talents.ranks={};});
 assert.equal(b.combatSummary.spellbookAttackPower,value);assert.equal(compareReports(b,b).comparable,true);cases++;
}
const zero=variant(saved=>saved.talents.ranks={furor:0}),absent=variant(saved=>saved.talents.ranks={});
assert.equal(compareReports(zero,absent).comparable,true);assert.ok(compareReports(zero,absent).changes.some(x=>/zero-key presence/.test(x)));cases++;
const zeroResources=variant(saved=>{saved.manualInputs.shapeshiftMana=2594;},raw=>{raw.sim.endingMana=0;raw.sim.endingEnergy=0;raw.sim.run.summary.actorSummaries.druid.endingResources={mana:0,energy:0,rage:0};});
assert.equal(compareReports(zeroResources,zeroResources).comparable,true);assert.equal(zeroResources.combatSummary.manaAfterInitialShift,0);assert.equal(zeroResources.combatSummary.endingMana,0);cases++;
const fractional=variant(saved=>{saved.manualInputs.shapeshiftMana=250.125;saved.manualInputs.preTalentCatAttackPower=.125;});
assert.equal(compareReports(fractional,fractional).comparable,true);assert.equal(fractional.combatSummary.manaAfterInitialShift,2343.875);cases++;

// Retain the generic incompatibility category, but name the exact Feral
// scenario dimension and both plain-text values in user-visible diagnostics.
const scenarioDetails=[
 [b=>b.scenario.targetBehind=false,'Position behind target (targetBehind): true → false'],
 [b=>b.scenario.startInProwl=true,'Start in Prowl (startInProwl): false → true'],
 [b=>b.scenario.preferredCatOpener='pounce','Cat opener (preferredCatOpener): ravage → pounce'],
 [b=>b.scenario.useFaerieFire=true,'Faerie Fire (useFaerieFire): false → true'],
 [b=>b.scenario.useFeralCooldowns=true,'Feral cooldowns (useFeralCooldowns): false → true'],
 [b=>b.manualInputs.shapeshiftMana=0,'Initial shift Mana cost (shapeshiftMana): 250 → 0'],
 [b=>b.manualInputs.shiftingPowerBaseMana=1200.125,'Shifting Power base Mana (shiftingPowerBaseMana): 1244 → 1200.125'],
 [b=>b.manualInputs.shiftingPowerCostReductionPercent=5.25,'Shifting Power cost reduction percent (shiftingPowerCostReductionPercent): 0 → 5.25']
];
for(const [change,detail] of scenarioDetails){
 const candidate=variant(change),result=blocked(a,candidate,/Encounter assumptions/),message='Encounter assumptions: '+detail;
 assert.ok(result.warnings.includes(message));assert.ok(result.changes.includes(message));
 const reverse=compareReports(candidate,a),[label,values]=detail.split(': '),[before,after]=values.split(' → ');
 assert.ok(reverse.warnings.includes('Encounter assumptions: '+label+': '+after+' → '+before),'Report A/B direction is exact');
 assert.equal(reverse.comparable,false);assert.equal(reverse.dpsDelta,null);assert.equal(reverse.damageDelta,null);
}
const positionCandidate=variant(b=>b.scenario.targetBehind=false),positionDetail='Encounter assumptions: Position behind target (targetBehind): true → false';
assert.deepEqual(compareReports(a,positionCandidate),{a:inspectReport(a),b:inspectReport(positionCandidate),comparable:false,
 warnings:['Encounter assumptions differs',positionDetail],caveats:[],changes:['Encounter assumptions differ',positionDetail],
 dpsDelta:null,dpsDeltaPercent:null,damageDelta:null},'Complete analysis retains classification, inspected source receipts and all numeric values; only diagnostic arrays gain detail');
const combinedScenario=variant(b=>{b.scenario.targetBehind=false;b.scenario.startInProwl=true;b.scenario.preferredCatOpener='pounce';});
const combinedResult=blocked(a,combinedScenario,/Encounter assumptions/);
assert.equal(combinedResult.warnings.filter(x=>x.startsWith('Encounter assumptions: ')).length,3,'Every changed dimension is shown without duplicating the generic category');
assert.equal(JSON.stringify(a),snapshot,'Diagnostic detail never rewrites the source report');

for(const [change,pattern] of [
 [b=>b.scenario.targetArmor=2500,/Target armor/],[b=>b.scenario.targetBehind=false,/Encounter assumptions/],
 [b=>b.scenario.startInProwl=true,/Encounter assumptions/],[b=>b.scenario.preferredCatOpener='pounce',/Encounter assumptions/],
 [b=>b.scenario.useFaerieFire=true,/Encounter assumptions/],[b=>b.scenario.useFeralCooldowns=true,/Encounter assumptions/],
 [b=>b.scenario.targetMaximumHealth=500000,/Target health/],[b=>b.scenario.seed=100,/Seed/],
 [b=>b.scenario.durationSeconds=11,/Duration/],[b=>b.manualInputs.shapeshiftMana=251,/Encounter assumptions/],
 [b=>b.manualInputs.shiftingPowerBaseMana=1200,/Encounter assumptions/],[b=>b.manualInputs.shiftingPowerCostReductionPercent=5,/Encounter assumptions/],
 [b=>b.race='troll',/Source\/reference/],[b=>b.race='tauren',/Source\/reference/]
])blocked(a,variant(change),pattern);
blocked(a,variant(()=>{},raw=>raw.inputTiming.additionalInputLatencyMilliseconds=80),/Actual input timing/);
blocked(a,variant(()=>{},raw=>{raw.preparation.baseline.sourceReference+='#different';raw.baseline.sourceReference=raw.preparation.baseline.sourceReference;}),/Source\/reference/);
blocked(a,variant(()=>{},raw=>{raw.preparation.baseline.confidenceClassification='lower-confidence-test';raw.baseline.confidenceClassification='lower-confidence-test';}),/Source\/reference/);
blocked(a,variant(()=>{},raw=>{raw.preparation.directRequest.tuning.omenProcPercent=5;raw.preparation.modelingNotes.push('Changed provisional Omen parameter');}),/Source\/reference/);
const sourceChanged=variant(()=>{},raw=>{raw.talentSourceUrl+='?different';});blocked(a,sourceChanged,/Source\/reference|source URL/);
const customNotes=f.plain(a);customNotes.talentAllocation.selected[0].knownLimitations+=' Newly disclosed source gap.';blocked(a,customNotes,/source knownLimitations/);
const version=f.plain(a);version.metadata.sourceCommit='different-simulator';blocked(a,version,/Source\/reference/);

// Every required identity/resource/provenance copy must fail closed if absent,
// even when comparing the incomplete report to itself.
for(const remove of [
 x=>delete x.originalSimulationRequest.feral,x=>delete x.originalSimulationRequest.itemizedTalentRanks,
 x=>delete x.build.inputMode,x=>delete x.build.manualInputs,x=>delete x.build.scenario,x=>delete x.build.talents.totalPoints,
 x=>delete x.feralPreparation,x=>delete x.resolvedCharacter,x=>delete x.baselineProvenance,x=>delete x.metadata,
 x=>delete x.talentAllocation,x=>delete x.talentAllocation.selected[0].knownLimitations,
 x=>delete x.talentAllocation.selected[0].historicalValidationSourceCommit,x=>delete x.talentAllocation.selected[0].simulationStatus,
 x=>delete x.rawServerResponse.talentSourceStatus,x=>delete x.rawServerResponse.inputTiming,
 x=>delete x.rawServerResponse.preparation.baseline.confidenceClassification,x=>delete x.rawServerResponse.preparation.baseline.observedUnbuffedMaximumMana,
 x=>delete x.rawServerResponse.preparation.directRequest.tuning.omenProcPercent,x=>delete x.rawServerResponse.preparation.preview.abilities,
 x=>delete x.rawServerResponse.preparation.modelingNotes,x=>delete x.rawServerResponse.preparation.preview.modelingNotes,
 x=>delete x.rawServerResponse.sim.run.summary.actorSummaries.druid,x=>delete x.rawServerResponse.sim.run.summary.actorSummaries.druid.startingResources.mana,
 x=>delete x.rawServerResponse.sim.run.customMetrics,x=>delete x.rawServerResponse.sim.run.summary.abilities,
 ...['authoredAttackPower','heartOfTheWildAttackPowerIncrement','predatoryStrikesAttackPower','spellbookAttackPower','maximumHealth','maximumMana','maximumEnergy','maximumRage','startingMana','startingEnergy','startingRage','endingMana','endingEnergy','endingRage','initialShiftManaCost','manaAfterInitialShift','actorResources','configuredRotation','targetSemantics','inputTiming','damageByAbility','abilityMetrics'].map(key=>x=>delete x.combatSummary[key])
]){const missing=f.plain(a);remove(missing);failClosed(missing,missing);}

// Altered copies cannot be laundered by importing the same altered file twice.
for(const mutate of [
 x=>x.build.manualInputs.preTalentCatAttackPower=801,x=>x.build.scenario.seed=144002,x=>x.build.race='tauren',
 x=>x.originalSimulationRequest.itemizedTalentRanks.furor=0,x=>x.originalSimulationRequest.feral.talents.totalPoints=35,
 x=>x.originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower=801,x=>x.rawServerResponse.allocatedTalentPoints=51,
 x=>x.talentAllocation.totalSelectedRanks=51,x=>x.talentAllocation.treePoints.feralPointsSpent=30,
 x=>x.talentAllocation.selected[0].historicalValidationSourceCommit='different',x=>x.talentAllocation.selected[0].simulationStatus='fully-implemented',
 x=>x.talentAllocation.selected[0].dataConfidence='verified-current-client',x=>x.talentAllocation.selected[0].sourceCommit='different',
 x=>x.rawServerResponse.preparation.request.targetArmor=10,x=>x.rawServerResponse.preparation.directRequest.maximumMana=4000,
 x=>x.rawServerResponse.preparation.directRequest.tuning.attackPower=180,x=>x.feralPreparation.preview.spellbookAttackPower=999,
 x=>x.resolvedCharacter.preview.maximumHealth=5500,x=>x.baselineProvenance.confidenceClassification='observed',
 x=>x.combatSummary.startingMana=2344,x=>x.combatSummary.manaAfterInitialShift=2594,x=>x.combatSummary.maximumHealth=5500,
 x=>x.combatSummary.spellbookAttackPower=800,x=>x.combatSummary.actorKey='rogue',x=>x.combatSummary.endingHealth=2173,
 x=>x.combatSummary.targetSemantics.mitigation='unmitigated',x=>x.combatSummary.targetSemantics.targetArmor=1234,
 x=>x.combatSummary.inputTiming.additionalInputLatencyMilliseconds=80,x=>x.inputTiming.additionalInputLatencyMilliseconds=80,
 x=>x.combatSummary.actorResources.ending.energy=16.5,x=>x.combatSummary.endingMana+=.000000001,
 x=>x.combatSummary.configuredRotation.entries.reverse(),x=>x.warnings=x.warnings.filter(note=>note!==x.rawServerResponse.preparation.modelingNotes[0]),
 x=>x.rawServerResponse.sim.run.summary.seed++,x=>x.rawServerResponse.sim.run.summary.durationSeconds++,
 x=>x.rawServerResponse.sim.run.summary.actorSummaries.druid.endingResources.energy++,
 x=>x.combatSummary.damageByAbility.claw--,x=>x.combatSummary.abilityMetrics.claw.damageDone--,
 x=>x.combatSummary.ownerDamage--,x=>x.combatSummary.dps++,x=>x.combatSummary.timelineEventCount++,
 x=>x.rawServerResponse.sim.run.summary.actorSummaries.druid.damageDoneByAbility.claw--,
 x=>x.rawServerResponse.sim.run.customMetrics['feral-heart-of-the-wild-maximum-mana']++,
 x=>x.rawServerResponse.sim.run.customMetrics['feral-omen-procs']++,x=>x.combatSummary.petDamage=1,
 x=>x.rawServerResponse.sim.run.summary.abilities.claw.abilityKey='other'
]){const tampered=f.plain(a);mutate(tampered);try{failClosed(tampered,tampered);}catch(error){console.error("Mutation:",mutate.toString());throw error;}}
const lostZero=f.plain(zero);delete lostZero.originalSimulationRequest.itemizedTalentRanks.furor;failClosed(lostZero,lostZero);
const invalidTotals=f.plain(a);invalidTotals.build.talents.totalPoints=36.5;assert.throws(()=>inspectReport(invalidTotals),/integers/);cases++;
for(const bad of ['800',null,false,{},-1]){const x=f.plain(a);x.originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower=bad;assert.throws(()=>inspectReport(x));cases++;}
const huge=variant(saved=>saved.manualInputs.preTalentCatAttackPower=1e20),hugeTamper=f.plain(huge);hugeTamper.combatSummary.spellbookAttackPower+=1e7;failClosed(hugeTamper,hugeTamper);

// Internally consistent but falsified totals, AP or resource receipts still
// need arithmetic reconciliation with independently recorded components.
const badAbility=f.plain(a);for(const value of [badAbility.combatSummary.damageByAbility,badAbility.rawServerResponse.sim.damageByAbility,badAbility.rawServerResponse.sim.run.summary.damageDoneByAbility,badAbility.rawServerResponse.sim.run.summary.actorSummaries.druid.damageDoneByAbility])value.claw=900;
for(const value of [badAbility.combatSummary.abilityMetrics,badAbility.rawServerResponse.sim.run.summary.abilities,badAbility.rawServerResponse.sim.run.summary.actorSummaries.druid.abilities])value.claw.damageDone=900;
blocked(badAbility,badAbility,/Ability damage does not reconcile/);
const impossible=f.plain(a);impossible.rawServerResponse.sim.endingEnergy=101;impossible.rawServerResponse.sim.run.summary.actorSummaries.druid.endingResources.energy=101;impossible.combatSummary.endingEnergy=101;impossible.combatSummary.actorResources.ending.energy=101;blocked(impossible,impossible,/exceeds/);

// Missing displayed counters must fail even when all copies were stripped
// together; integer counts cannot silently become em-dashes in the ability UI.
for(const key of ['castCompletedCount','damageOccurrenceCount','criticalDamageCount','channelTickOccurrenceCount']){
 for(const bad of [undefined,null,'1',.5,-1]){
  const x=f.plain(a);
  for(const map of [x.combatSummary.abilityMetrics,x.rawServerResponse.sim.run.summary.abilities,x.rawServerResponse.sim.run.summary.actorSummaries.druid.abilities]){
   if(bad===undefined)delete map.claw[key];else map.claw[key]=bad;
  }
  failClosed(x,x);
 }
}
const annotation=f.plain(a);annotation.warnings.push('Manually recorded extra scenario assumption');blocked(a,annotation,/Source\/reference/);

// Exercise the production complete-envelope replay, independently from compare
// classification. Only generated IDs change; all source/timing/ordered data stay.
const replayContext={};vm.createContext(replayContext);vm.runInContext(part('function feralReplaySignature(','function feralCombatSummary('),replayContext);
const signature=replayContext.feralReplaySignature,replay=f.plain(a.rawServerResponse);
replay.sim.run.timeline=[{abilityExecutionId:'22222222-2222-4222-8222-222222222222',amount:1,atSeconds:1},{abilityExecutionId:'22222222-2222-4222-8222-222222222222',amount:2,atSeconds:2}];
const renumbered=JSON.parse(JSON.stringify(replay).replaceAll('11111111-1111-4111-8111-111111111111','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa').replaceAll('22222222-2222-4222-8222-222222222222','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'));
assert.equal(signature(replay),signature(renumbered));cases++;
for(const change of [
 x=>x.inputTiming.additionalInputLatencyMilliseconds++,x=>x.preparation.preview.maximumMana++,
 x=>x.preparation.baseline.confidenceClassification='different',x=>x.talentSourceCommit='different',
 x=>x.preparation.directRequest.tuning.omenProcPercent++,x=>x.sim.omenProcs++,x=>x.sim.run.processedEventCount++,
 x=>x.sim.run.summary.actorSummaries.druid.startingResources.mana++,x=>x.sim.run.timeline.reverse(),
 x=>x.sim.run.timeline[1].abilityExecutionId='33333333-3333-4333-8333-333333333333',
 x=>x.preparation.preview.configuredRotation.entries.reverse(),x=>x.preparation.modelingNotes.pop()
]){const changed=f.plain(replay);change(changed);assert.notEqual(signature(replay),signature(changed));cases++;}

// Preserve existing generic reports: old schema-v1 Balance reports need no new
// Feral-only preparation or source fields. The main regression suite exercises
// every other older supported class, damage distribution and rendered slots.
const balance={schema:'warcraftsim.solo-dps-report',version:1,build:{schema:'warcraftsim.solo-dps-build',version:1,classKey:'druid',specialization:'balance',race:'night-elf',provisional:true,talents:{ranks:{}},gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},encounter:{duration:10,seed:1,iterations:1,targetHealth:1000000,soloScript:{enabled:false}}},originalSimulationRequest:{classKey:'druid',specialization:'balance',itemizedTalentMode:true,itemizedTalentRanks:{},balance:{character:{race:'night-elf',equipment:{items:[]}},durationSeconds:10,seed:1,targetMaximumHealth:1000000,soloEncounter:{enabled:false},externalModifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},useProvisionalLevel60Baseline:true}},combatSummary:{dps:100,damageDone:1000,ownerDamage:1000,petDamage:0,damageByAbility:{wrath:1000}},rawServerResponse:{classKey:'druid',specialization:'balance',sim:{dps:100,damageDone:1000,damageByAbility:{wrath:1000}}}};
assert.equal(compareReports(balance,balance).comparable,true);assert.equal(compareReports(a,balance).comparable,false);cases+=2;
const mixed=f.plain(balance);mixed.originalSimulationRequest.feral={};assert.throws(()=>inspectReport(mixed));cases++;
console.log(`Feral actual-production comparison contracts passed: ${cases} build/scenario/source/receipt/reconciliation cases; synthetic fixtures are not gameplay evidence.`);

// Execute the production slot/import listeners, not a detached success flag.
(async()=>{
 const elements=new Map(),listeners={};
 const node=()=>({textContent:'',children:[],hidden:false,disabled:false,value:'',handlers:{},append(...items){this.children.push(...items);},replaceChildren(...items){this.children=items;},addEventListener(name,handler){this.handlers[name]=handler;},click(){return this.handlers.click?.();}});
 const get=id=>{if(!elements.has(id))elements.set(id,node());return elements.get(id);};
 const document={getElementById:get,createElement:node,addEventListener(name,handler){listeners[name]=handler;}},window={SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')};
 vm.runInNewContext(fs.readFileSync(require.resolve('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js'),'utf8'),{document,window});
 const compare=window.WarcraftSimComparison.compareReports;let acceptedPair=null;
 window.WarcraftSimComparison.compareReports=(left,right)=>{acceptedPair=JSON.stringify([left,right]);return compare(left,right);};
 const values=id=>get(id).children.map(child=>child.children.length?child.children.map(cell=>cell.textContent):child.textContent);
 const snapshot=()=>({a:get('compareLabelA').textContent,b:get('compareLabelB').textContent,hidden:get('compareDetails').hidden,
  rows:values('compareRows'),warnings:values('compareWarnings'),changes:values('compareChanges'),acceptedPair});
 async function imported(slot,value){get('compareImport'+slot).click();get('compareFile').files=[{text:async()=>JSON.stringify(value)}];await get('compareFile').handlers.change();}
 const b=variant(saved=>saved.manualInputs.preTalentCatAttackPower=900);
 const broken=f.plain(a);delete broken.feralPreparation;
 const missingResources=f.plain(a);delete missingResources.combatSummary.actorResources;
 await imported('A',a);const empty=snapshot();await imported('B',broken);
 assert.deepEqual(snapshot(),empty,'Rejected Feral candidate cannot fill an initially empty slot');
 assert.match(get('compareStatus').textContent,/Report import rejected:/);assert.equal(get('compareLabelB').textContent,'No report selected');
 await imported('B',b);assert.equal(get('compareDetails').hidden,false);assert.match(get('compareStatus').textContent,/settings match/);
 for(const target of ['A','B'])for(const invalid of [broken,missingResources]){
  assert.throws(()=>inspectReport(invalid),/complete Solo DPS report/,'Missing required receipts remain hard validation failures');
  await imported('A',a);await imported('B',b);const before=snapshot();
  await imported(target,invalid);
  assert.deepEqual(snapshot(),before,'Hard rejection preserves the accepted Feral pair, labels, values and caveats');
  assert.match(get('compareStatus').textContent,/Report import rejected:/);
  assert.match(get('compareStatus').textContent,new RegExp('Slot '+target+' was not changed'));
  await imported(target==='A'?'B':'A',target==='A'?b:a);
  assert.deepEqual(snapshot(),before,'Re-rendering the other slot proves no rejected candidate was committed');
  await imported(target,target==='A'?b:a);
  assert.equal(acceptedPair,JSON.stringify(target==='A'?[b,b]:[a,a]),'Valid same-slot recovery uses the new report');
  assert.equal(get('compareDetails').hidden,false);assert.match(get('compareStatus').textContent,/settings match/);
 }
 // Missing copies reject atomically; contradictory but well-shaped copies are
 // accepted only as explicitly noncomparable, never as a numeric DPS ranking.
 for(const mutate of [x=>x.inputTiming.additionalInputLatencyMilliseconds++,x=>x.inputTiming.additionalInputLatencyMilliseconds=80,x=>x.baselineProvenance.sourceKind='unverified-source',x=>x.talentAllocation.selected[0].sourceCommit='different',x=>x.combatSummary.spellbookAttackPower++]){
  const invalid=f.plain(a);mutate(invalid);assert.ok(inspectReport(invalid).integrityWarnings.length);
  await imported('A',a);await imported('B',invalid);
  assert.match(get('compareStatus').textContent,/Not directly comparable/);
  assert.equal(get('compareRows').children[0].children[3].textContent,'Not comparable');
  assert.equal(get('compareRows').children[1].children[3].textContent,'Not compared');
  await imported('B',b);assert.match(get('compareStatus').textContent,/settings match/);
 }
 // Exercise the actual rendered warning/change badges and hidden deltas.
 for(const [change,detail] of scenarioDetails){
  await imported('A',a);await imported('B',variant(change));
  assert.ok(values('compareWarnings').includes('Mismatch: Encounter assumptions: '+detail));
  assert.ok(values('compareChanges').includes('Encounter assumptions: '+detail));
  assert.equal(get('compareRows').children[0].children[3].textContent,'Not comparable');
  assert.equal(get('compareRows').children[1].children[3].textContent,'Not compared');
 }
 listeners['warcraftsim:simulation-complete']({detail:broken});assert.equal(get('compareUseA').disabled,true);assert.equal(get('compareDetails').hidden,true);assert.equal(get('compareRows').children.length,0);
 console.log('Feral production comparison imports preserve validated slots on hard rejection, suppress conflicting-receipt deltas and recover on valid reports.');
})().catch(error=>{console.error(error);process.exitCode=1;});
