'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const modulePath=process.env.COMBAT_COMPARISON_MODULE||'../WarcraftSim.Api/wwwroot/solo-dps-comparison.js';
const api=require(modulePath),fixture=require('./solo-dps-combat-comparison-fixture.json'),clone=x=>JSON.parse(JSON.stringify(x));
function report(points=31){
 const raw=clone(fixture[points]),t=raw.preparation.request,p=raw.preparation.preview,s=raw.sim,rs=s.run.summary;
 const settingKeys=['targetArmor','targetDodgePercent','targetParryPercent','rogueMaximumHealth','startingEnergyPercent','poisonChargesPerWeapon','startInStealth','isBehindTarget','useExposeArmor','mainHandPoison','offHandPoison','useInstantPoison','bothWeaponsDaggers','mainHandWeaponType','offHandWeaponType','useUtilityAbilities','additionalTargets','enableBossAttacks','tuning'];
 const selected=fixture.catalog.filter(x=>t.talents.ranks[x.key]>0).map(x=>({key:x.key,name:x.name,tree:x.tree,rank:t.talents.ranks[x.key],maxRank:x.maxRank,row:x.row,column:x.column,modeled:true,simulationStatus:x.simulationStatus,dataConfidence:x.dataConfidence,knownLimitations:x.knownLimitations,provenance:x.visualSourceStatus,sourceCommit:x.visualSourceCommit,sourceUrl:x.visualSourceUrl,historicalValidationSourceStatus:x.historicalValidationSourceStatus,historicalValidationSourceCommit:x.historicalValidationSourceCommit,historicalValidationSourceUrl:x.sourceUrl,prerequisite:x.enginePrerequisiteKey}));
 return {schema:'warcraftsim.solo-dps-report',version:1,metadata:{ruleset:'wow-forever',simulatorVersion:'test-fixture',sourceCommit:'1cd4afde349d8093bbd8e08b8d4235ca08599529',runtimeTargetBuild:'1.60.1.70707',historicalTalentReferenceCommit:raw.talentSourceCommit,notes:[]},
 build:{schema:'warcraftsim.solo-dps-build',version:1,itemizedMode:true,classKey:'rogue',specialization:'combat',race:'human',provisional:true,gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},preparedCombatConsumables:[],racialActive:false,talents:clone(t.talents),encounter:{duration:String(t.durationSeconds),seed:String(t.seed),targetHealth:String(t.targetMaximumHealth),iterations:'1',timeline:t.captureTimeline,soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},rogue:Object.fromEntries(settingKeys.map(k=>[k,clone(t[k])]))}},
 originalSimulationRequest:{classKey:'rogue',specialization:'combat',itemizedTalentMode:true,itemizedTalentRanks:clone(t.talents.ranks),rogue:clone(t)},iterationStatistics:null,
 talentAllocation:{mode:'strict-rank-derived',unspentPoints:51-points,totalSelectedRanks:points,treePoints:{assassinationPointsSpent:0,combatPointsSpent:points,subtletyPointsSpent:0},selected},
 resolvedCharacter:clone(p),baselineProvenance:clone(raw.baseline),combatPreparation:clone(raw.preparation),inputTiming:clone(raw.inputTiming),capabilityNotes:['Bounded fixed-sword historical Combat, provisional.'],
 combatSummary:{dps:s.dps,damageDone:s.damageDone,ownerDamage:s.damageDone,petDamage:0,hawkDamage:null,damageByAbility:clone(s.damageByAbility),abilityMetrics:clone(rs.abilities),combatItemUses:null,consumableDecisions:null,timelineEventCount:s.run.timeline.length,actorKey:'rogue',maximumMana:null,endingMana:null,maximumHealth:s.maximumHealth,endingHealth:s.endingHealth,maximumEnergy:s.maximumEnergy,startingEnergy:rs.actorSummaries.rogue.startingResources.energy,endingEnergy:s.endingEnergy,targetSemantics:{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:t.targetArmor,targetDodgePercent:t.targetDodgePercent,targetParryPercent:t.targetParryPercent,targetCount:1,initialHealthPercent:100},inputTiming:clone(raw.inputTiming),configuredRotation:clone(p.rotation),attackPower:p.attackPower},
 warnings:[...raw.talentModelingNotes,...p.modelingNotes,...s.modelingNotes,...selected.map(x=>x.knownLimitations),raw.inputTiming.note,'Bounded fixed-sword historical Combat, provisional.'],rawServerResponse:raw};
}
if(require.main!==module){module.exports={report};return;}
let count=0;function test(name,fn){fn();count++;console.log('PASS '+name);}
const good=report();
test('accepted 31/20 and 30/21 reports compare without DPS ordering claim',()=>{assert.equal(api.inspectReport(good).integrityWarnings.length,0);assert.equal(api.compareReports(good,report(30)).comparable,true);});
const mutations={
 'owned preparation missing':r=>delete r.rawServerResponse.preparation,
 'preparation copy missing':r=>delete r.combatPreparation,
 'preview missing':r=>delete r.rawServerResponse.preparation.preview,
 'resolved preview missing':r=>delete r.resolvedCharacter,
 'timing missing':r=>delete r.inputTiming,
 'source missing':r=>delete r.rawServerResponse.talentSourceCommit,
 'baseline missing':r=>delete r.baselineProvenance,
 'typed request missing':r=>delete r.originalSimulationRequest.rogue,
 'build absent zero':r=>r.build.talents.ranks.precision=0,
 'owned ranks disagreement':r=>r.combatPreparation.request.talents.ranks.precision=2,
 'unknown zero key':r=>r.originalSimulationRequest.itemizedTalentRanks.unknown=0,
 'Precision4':r=>r.originalSimulationRequest.itemizedTalentRanks.precision=4,
 'Precision5':r=>r.originalSimulationRequest.itemizedTalentRanks.precision=5,
 'daggers':r=>r.build.encounter.rogue.bothWeaponsDaggers=true,
 'adds':r=>r.originalSimulationRequest.rogue.additionalTargets=1,
 'incoming':r=>r.originalSimulationRequest.rogue.enableBossAttacks=true,
 'utility':r=>r.originalSimulationRequest.rogue.useUtilityAbilities=true,
 'tuning':r=>r.build.encounter.rogue.tuning.mutilateBonusDamagePerHand=17.25,
 'allocation total':r=>r.talentAllocation.totalSelectedRanks=51,
 'selected cap':r=>r.talentAllocation.selected[0].maxRank=5,
 'selected confidence omission':r=>delete r.talentAllocation.selected[0].dataConfidence,
 'selected status':r=>r.talentAllocation.selected[0].simulationStatus='complete',
 'selected source':r=>r.talentAllocation.selected[0].historicalValidationSourceCommit='other',
 'warnings missing':r=>r.warnings=[],
 'capability missing':r=>delete r.capabilityNotes,
 'AP':r=>r.combatSummary.attackPower++,
 'Health':r=>r.combatSummary.maximumHealth++,
 'Energy':r=>r.combatSummary.startingEnergy++,
 'priority':r=>r.combatSummary.configuredRotation.entries.reverse(),
 'target':r=>r.combatSummary.targetSemantics.targetCount=2,
 'DPS':r=>r.combatSummary.dps++,
 'damage':r=>r.combatSummary.damageDone++,
 'ability damage':r=>r.combatSummary.damageByAbility[Object.keys(r.combatSummary.damageByAbility)[0]]++,
 'ability metrics':r=>r.combatSummary.abilityMetrics[Object.keys(r.combatSummary.abilityMetrics)[0]].damageDone++,
 'run duration':r=>r.rawServerResponse.sim.run.summary.durationSeconds++,
 'seed':r=>r.rawServerResponse.sim.run.summary.seed++,
 'timeline count':r=>r.combatSummary.timelineEventCount++,
 'actor Energy':r=>r.rawServerResponse.sim.run.summary.actorSummaries.rogue.endingResources.energy++,
 'iterations':r=>r.build.encounter.iterations=2,
 'modifiers':r=>r.build.modifiers.buffs.push('extra'),
 'gear':r=>r.build.gear.items.push({slotKey:'head',itemId:1}),
 'envelope extra':r=>r.originalSimulationRequest.inputLatencyMs=0,
 'numeric type':r=>r.combatSummary.dps=String(r.combatSummary.dps),
 'typed unknown':r=>r.originalSimulationRequest.rogue.customPriority=[],
 'tuning missing field':r=>{delete r.originalSimulationRequest.rogue.tuning.energyPerSecond;},
 'selected column':r=>r.talentAllocation.selected[0].column=2,
 'selected prerequisite':r=>r.talentAllocation.selected[0].prerequisite='invented',
 'source visual missing':r=>delete r.talentAllocation.selected[0].sourceCommit
};
// Coherent-copy faults must be rejected too, not merely mismatched receipts.
function tuningChange(r,key,value){for(const t of [r.build.encounter.rogue,r.originalSimulationRequest.rogue,r.rawServerResponse.preparation.request])t.tuning[key]=value;sync(r);}
const positiveTuning=['masterOfSubtletyDurationSeconds','mainHandSpeedSeconds','offHandSpeedSeconds','energyPerSecond','adrenalineRushDurationSeconds','deadlyPoisonDurationSeconds','deadlyPoisonTickIntervalSeconds','armorDamageReductionConstant'];
const boundedTuning={hackAndSlashExtraAttackPercentPerRank:20,hackAndSlashMaceArmorIgnorePercentPerRank:20,quickRecoveryEnergyRefundPercentPerRank:50,woundPoisonHealingReductionPercent:100,instantPoisonChancePercent:100,poisonChargePreservationChancePerRank:100,puncturingWoundsBackstabCritPerRank:100,puncturingWoundsMutilateCritPerRank:100,puncturingWoundsExtraPointChancePerRank:100,mainHandHitPercent:100,offHandHitPercent:100,physicalCritPercent:100,baseDodgePercent:100,evasionBonusDodgePercent:100,baseParryPercent:100,deflectionParryPercentPerRank:20};
for(const key of positiveTuning)mutations['coherent zero '+key]=r=>tuningChange(r,key,0);
for(const [key,max] of Object.entries(boundedTuning))mutations['coherent excessive '+key]=r=>tuningChange(r,key,max+1);
for(const value of [0,0.99,3.01])mutations['coherent cast multiplier '+value]=r=>tuningChange(r,'mindNumbingPoisonCastTimeMultiplier',value);
function characterChange(r,edit){edit(r.originalSimulationRequest.rogue.character);r.rawServerResponse.preparation.request.character=clone(r.originalSimulationRequest.rogue.character);sync(r);}
for(const [name,edit] of Object.entries({
 'missing id':c=>delete c.id,'invalid id':c=>c.id='authored-id','zero id':c=>c.id='00000000-0000-0000-0000-000000000000',
 'extra field':c=>c.unknown=0,'missing name':c=>delete c.name,'empty name':c=>c.name='',
 'authored stats':c=>c.baseStats.values.agility=250,'extra stats field':c=>c.baseStats.extra=0,
 'missing stats':c=>delete c.baseStats,'missing talents':c=>delete c.talents,'missing buffs':c=>delete c.buffs,'missing equipment':c=>delete c.equipment,
 'generic talent':c=>c.talents.selections.push({key:'precision',rank:1}),'generic buff':c=>c.buffs.selections.push({key:'buff'}),'equipment item':c=>c.equipment.items.push({slotKey:'head'})
}))mutations['coherent character '+name]=r=>characterChange(r,edit);
for(const [key,values] of Object.entries({bossSwingSeconds:[0,'x',null],bossHitDamage:[-1,null,'0'],poisonChargesPerWeapon:[2147483648]}))for(const value of values)mutations['coherent typed '+key+' '+JSON.stringify(value)]=r=>{for(const t of [r.originalSimulationRequest.rogue,r.rawServerResponse.preparation.request])t[key]=value;if(Object.hasOwn(r.build.encounter.rogue,key))r.build.encounter.rogue[key]=value;sync(r);};
for(const [name,mutate] of Object.entries(mutations))test('reject '+name,()=>{const bad=clone(good);mutate(bad);assert.throws(()=>api.inspectReport(bad));});
function sync(r){r.combatPreparation=clone(r.rawServerResponse.preparation);r.resolvedCharacter=clone(r.rawServerResponse.preparation.preview);}
for(const [name,mutate] of Object.entries({
 timing:r=>{r.rawServerResponse.inputTiming.additionalInputLatencyMilliseconds=80;r.inputTiming=clone(r.rawServerResponse.inputTiming);r.combatSummary.inputTiming=clone(r.inputTiming);},
 'metadata source':r=>r.metadata.sourceCommit='another-stamped-build',
 'baseline confidence':r=>{r.rawServerResponse.baseline.confidenceClassification='another historical confidence';r.rawServerResponse.preparation.preview.baseline=clone(r.rawServerResponse.baseline);r.baselineProvenance=clone(r.rawServerResponse.baseline);sync(r);},
 'capability policy':r=>{r.capabilityNotes.push('Another bounded source caveat');r.warnings.push('Another bounded source caveat');},
 target:r=>{r.originalSimulationRequest.rogue.targetMaximumHealth=500000;r.build.encounter.targetHealth='500000';r.rawServerResponse.preparation.request=clone(r.originalSimulationRequest.rogue);sync(r);}
}))test('coherent imported '+name+' occupies slots but disables comparable delta',()=>{const b=clone(good);mutate(b);api.inspectReport(b);const c=api.compareReports(good,b);assert.equal(c.comparable,false);assert.equal(c.dpsDelta,null);});
test('explicit zero presence remains identity and visible selection change',()=>{const a=report(30),b=clone(a);for(const map of [b.build.talents.ranks,b.originalSimulationRequest.itemizedTalentRanks,b.originalSimulationRequest.rogue.talents.ranks,b.rawServerResponse.preparation.request.talents.ranks])map['adrenaline-rush']=0;sync(b);const c=api.compareReports(a,b);assert.equal(c.comparable,true);assert(c.changes.some(x=>x.includes('zero-key presence')));});
test('coherent explicit tuning choices remain comparable',()=>{const b=clone(good);for(const t of [b.build.encounter.rogue,b.originalSimulationRequest.rogue,b.rawServerResponse.preparation.request])t.tuning.mutilateBonusDamagePerHand=17.25;sync(b);const c=api.compareReports(good,b);assert.equal(c.comparable,true);assert(c.changes.some(x=>x.includes('Combat tuning mutilate')));});
test('genuine older Assassination comparison remains accepted with cross-spec caveat',()=>{const b=require('./fixtures/assassination-reports/rogue-manual-report.json');api.inspectReport(b);const c=api.compareReports(good,b);assert.equal(c.comparable,false);assert(c.caveats.some(x=>x.includes('Older Assassination')));});
test('relabeled Combat artifact is not a historical Assassination report',()=>{const b=clone(good);for(const t of [b.build,b.originalSimulationRequest,b.originalSimulationRequest.rogue,b.rawServerResponse])t.specialization='assassination';delete b.inputTiming;delete b.combatPreparation;assert.throws(()=>api.inspectReport(b),/Assassination historical report/);});
for(const [key,max] of Object.entries(boundedTuning))test('runner boundary '+key+' accepts '+max,()=>{const b=clone(good);tuningChange(b,key,max);api.inspectReport(b);});
for(const key of positiveTuning)test('runner positive '+key+' accepts 0.001',()=>{const b=clone(good);tuningChange(b,key,0.001);api.inspectReport(b);});
for(const value of [1,3])test('runner cast multiplier boundary '+value,()=>{const b=clone(good);tuningChange(b,'mindNumbingPoisonCastTimeMultiplier',value);api.inspectReport(b);});
test('runner permits zero utility poison duration and bonus damage',()=>{const b=clone(good);for(const key of ['utilityPoisonDurationSeconds','mutilateBonusDamagePerHand','mainHandWeaponDamage','offHandWeaponDamage'])tuningChange(b,key,0);api.inspectReport(b);});
// Exercise actual import handler and slot assignment, not a synthetic inspector mock.

async function slots(){
 const nodes=new Map(),listeners={},element=()=>({textContent:'',hidden:false,disabled:false,value:'',children:[],listeners:{},addEventListener(k,fn){this.listeners[k]=fn;},replaceChildren(){this.children=[];},append(x){this.children.push(x);},click(){this.listeners.click?.();}});
 const document={getElementById(id){if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);},createElement:element,addEventListener(k,fn){listeners[k]=fn;}};
 const ctx={window:{SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')},document,console};vm.createContext(ctx);vm.runInContext(fs.readFileSync(require.resolve(modulePath),'utf8'),ctx);
 const use=(r,s)=>{listeners['warcraftsim:simulation-complete']({detail:r});document.getElementById('compareUse'+s).click();};use(good,'A');use(report(30),'B');
 const snapshot=()=>JSON.stringify(['compareLabelA','compareLabelB','compareRows','compareChanges'].map(id=>document.getElementById(id)),(k,v)=>k==='listeners'?undefined:v);
 const before=snapshot();for(const mutate of Object.values(mutations)){const bad=clone(good);mutate(bad);for(const slot of ['A','B']){document.getElementById('compareImport'+slot).click();const f=document.getElementById('compareFile');f.files=[{text:async()=>JSON.stringify(bad)}];await f.listeners.change();assert.equal(snapshot(),before);assert.match(document.getElementById('compareStatus').textContent,/not changed/);}}
 console.log('PASS '+Object.keys(mutations).length*2+' transactional A/B rejection checks');
}
slots().then(()=>console.log('Combat comparison: '+count+' checks plus transactional imports passed.')).catch(e=>{console.error(e);process.exitCode=1;});
