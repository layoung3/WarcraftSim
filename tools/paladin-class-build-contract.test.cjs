"use strict";
// Production contract tests: no simulated DOM and no fabricated browser evidence.
const assert=require("node:assert/strict");
const C=require("../WarcraftSim.Api/wwwroot/class-build-contract.js");
let checks=0;
const check=(fn)=>{fn();checks++;};
const build={schema:"warcraftsim.class-build",version:1,presetOrigin:null,acceptPartialCoverage:false,character:{
 classKey:"paladin",specialization:"retribution",race:"human",level:60,baselineMode:"strict-reviewed",baseline:null,
 equipment:{items:[C.createEquipmentItem("chest",null,"manual-stat-budget","Explicit manual budget")]},itemizedTalents:{},
 modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:"initial-only"},combatItems:{majorManaPotionCharges:2,majorHealingPotionCharges:1,disabledItemKeys:["major-healing-potion"]},
 useRacialActive:false,encounter:{durationSeconds:30,seed:175113,targetMaximumHealth:200000,targetArmor:3500,captureTimeline:true,survivalPolicy:"existing-runner-behavior"},
 rotation:{mode:"custom",priority:["holy-light"]},classSettings:{seal:"righteousness",secondarySeal:null,aura:"devotion",friendlyTargetEnabled:true,healTarget:"friendly",friendlyTargetMaximumHealth:5000,friendlyTargetInitialHealthPercent:50,useManualWeaponInputs:false}}};
build.character.equipment.items[0].stats.values.intellect=15;
const catalog={classKey:"paladin",talents:[{key:"rank-contract-witness",maxRank:5}]};
for(const cls of ["shaman","rogue","paladin"])check(()=>assert.equal(C.requireSupportedClass(cls),cls));
for(const cls of ["Paladin","priest","",null,{},"toString","__proto__"])check(()=>assert.throws(()=>C.requireSupportedClass(cls)));
check(()=>assert.equal(C.requireRenderableBuild(build,catalog),build));
for(const rank of [0,1,3,5])check(()=>{const b=C.clone(build);b.character.itemizedTalents={"rank-contract-witness":rank};assert.equal(C.requireRenderableBuild(b,catalog),b);});
const malformed=[
 x=>x.version=2,x=>x.schema="warcraftsim.solo-dps-build",x=>x.character.classKey="priest",x=>x.character.itemizedTalents=null,
 x=>x.character.itemizedTalents={"rank-contract-witness":-1},x=>x.character.itemizedTalents={"rank-contract-witness":1.5},x=>x.character.itemizedTalents={"rank-contract-witness":"2"},
 x=>x.character.itemizedTalents={"rank-contract-witness":6},x=>x.character.itemizedTalents={"wrong-class-talent":1},
 x=>x.character.classSettings.mainHandPoison="instant",x=>x.character.classSettings.mainHandImbue="windfury",x=>x.character.classSettings.friendlyTargetEnabled="true",
 x=>x.character.classSettings.seal=0,x=>x.character.classSettings.incomingBossHitDamage="100",x=>x.character.classSettings.secondarySeal={},x=>x.character.classSettings.utilityScenario=[],
 x=>x.character.classSettings=null,x=>x.character.equipment.items.push(null),x=>x.character.rotation.priority=null,x=>x.character.modifiers.buffs=null
];
for(const mutate of malformed)check(()=>{const invalid=C.clone(build),before=C.clone(build);mutate(invalid);assert.throws(()=>C.requireRenderableBuild(invalid,catalog));assert.deepEqual(build,before);});
for(const type of ["omitted","null","selected"])check(()=>{
 const original=C.clone(build),gear=original.character.equipment.items[0];
 if(type==="omitted"){delete gear.enchantSpellId;delete gear.temporaryEnchantSpellId;}else{gear.enchantSpellId=type==="selected"?20011:null;gear.temporaryEnchantSpellId=null;}
 const validated=C.clone(original);validated.presetOrigin="reviewed-paladin-contract-witness";validated.character.itemizedTalents={"rank-contract-witness":3};
 validated.character.race="undead";validated.character.baselineMode="historical-provisional";validated.character.equipment.items=[];validated.character.classSettings={};validated.character.rotation={mode:"automatic",priority:[]};validated.character.combatItems={};validated.character.encounter.seed++;
 const before=C.clone(original),serverBefore=C.clone(validated),result=C.applyTalentPreset(original,validated),expected=C.clone(original);expected.presetOrigin=validated.presetOrigin;expected.character.itemizedTalents=validated.character.itemizedTalents;
 assert.deepEqual(result,expected);assert.deepEqual(original,before);assert.deepEqual(validated,serverBefore);assert.deepEqual(result.character.rotation,{mode:"custom",priority:["holy-light"]});
 result.character.equipment.items[0].stats.values.intellect=99;assert.deepEqual(original,before);
});
check(()=>{const invalid=C.clone(build);invalid.character.itemizedTalents={"rank-contract-witness":-1};const applied=C.applyTalentPreset(invalid,{...C.clone(build),presetOrigin:"reviewed-valid"});assert.deepEqual(applied.character.itemizedTalents,{});assert.equal(invalid.character.itemizedTalents["rank-contract-witness"],-1);});
check(()=>{const other=C.clone(build);other.character.classKey="rogue";assert.throws(()=>C.applyTalentPreset(build,{...other,presetOrigin:"rogue-example"}));});

const first="12345678-1234-1234-1234-123456789abc",second="87654321-4321-4321-4321-cba987654321";
function makeReport(){
 const request={character:{id:first,classKey:"paladin",race:"human",level:60},talents:{ranks:{}},seed:175113,customPriority:["holy-light"],friendlyTargetEnabled:true};
 const preparation={classKey:"paladin",request:C.clone(request),maximumMana:4000,maximumHealth:3500,effectiveStats:{values:{intellect:100}}};
 return {schema:"warcraftsim.class-build-report",version:1,build:C.clone(build),sourceCommit:"source-one",workflowCheckoutCommit:"checkout-one",validation:{isLegal:true,canSave:true,canExecute:true,preparation:C.clone(preparation)},preparation,originalSimulationRequest:request,coverage:[],simulation:{classKey:"paladin",dps:2,damageDone:60,healingDone:100,overhealingDone:30,absorptionDone:0,damageTaken:50,blockedDamage:20,maximumMana:4000,maximumHealth:3500,endingMaximumMana:4000,endingMaximumHealth:3500,endingMana:3000,endingHealth:3450,damageByAbility:{judgement:60},run:{summary:{},customMetrics:{},timeline:[{type:6,timeSeconds:1,abilityKey:"judgement",amount:60,abilityExecutionId:first},{type:8,timeSeconds:1,resourceKey:"mana",amount:-100,resourceTotalAfter:3000,abilityExecutionId:first}]}},notes:[]};
}
check(()=>assert.equal(C.requireReport(makeReport()).simulation.classKey,"paladin"));
check(()=>{const report=makeReport();Object.assign(report.simulation,{endingMaximumMana:4150,endingMaximumHealth:3700,endingMana:4050,endingHealth:3600});assert.equal(C.requireReport(report),report,"Live ending caps can grow without rewriting the initial prepared caps.");});
for(const field of ["endingMaximumMana","endingMaximumHealth"]){
 check(()=>{const report=makeReport();delete report.simulation[field];assert.throws(()=>C.requireReport(report),/ending caps/);});
 for(const value of [null,0,-1,"4000",true,Infinity,NaN])check(()=>{const report=makeReport();report.simulation[field]=value;assert.throws(()=>C.requireReport(report),/ending caps/);});
}
for(const mutate of [x=>x.simulation.endingMana=x.simulation.endingMaximumMana+1,x=>x.simulation.endingHealth=x.simulation.endingMaximumHealth+1,x=>x.simulation.endingMaximumMana=x.simulation.endingMana-1,x=>x.simulation.endingMaximumHealth=x.simulation.endingHealth-1,x=>x.simulation.maximumMana++,x=>x.simulation.maximumHealth++])check(()=>{const report=makeReport();mutate(report);assert.throws(()=>C.requireReport(report),/caps|prepared/);});
for(const cls of ["shaman","rogue"])check(()=>{const report=makeReport();report.build=C.clone(require("../docs/acceptance/cp175/"+(cls==="shaman"?"cp175-classwide-shaman-elemental-restoration.json":"cp175-classwide-rogue-assassination.json")));report.originalSimulationRequest.character.classKey=cls;report.preparation.classKey=cls;report.preparation.request=C.clone(report.originalSimulationRequest);report.validation.preparation=C.clone(report.preparation);delete report.simulation.classKey;delete report.simulation.endingMaximumMana;delete report.simulation.endingMaximumHealth;assert.equal(C.requireReport(report),report,"Paladin-only ending caps do not change the retained "+cls+" structural response reader.");});
check(()=>{const saved=makeReport(),current=C.clone(saved);current.originalSimulationRequest.character.id=second;current.preparation.request.character.id=second;current.validation.preparation.request.character.id=second;current.simulation.run.timeline.forEach(x=>x.abilityExecutionId=second);assert.deepEqual(C.compareReplay(saved,current),{matched:true,requestMatched:true,simulationMatched:true,sourceMatches:true,checkoutMatches:true});});
check(()=>{const saved=makeReport(),current=C.clone(saved);current.sourceCommit="different-source";current.workflowCheckoutCommit="different-checkout";const result=C.compareReplay(saved,current);assert.equal(result.matched,true);assert.equal(result.sourceMatches,false);assert.equal(result.checkoutMatches,false);});
for(const mutate of [
 x=>x.simulation.endingMana++,x=>x.simulation.endingMaximumMana++,x=>x.simulation.endingMaximumHealth++,x=>x.simulation.run.timeline[0].amount++,x=>x.simulation.run.timeline[0].timeSeconds++,
 x=>x.simulation.run.timeline[1].abilityExecutionId=second,x=>x.simulation.run.customMetrics.extra="new recorded outcome"
])check(()=>{const saved=makeReport(),current=C.clone(saved);mutate(current);assert.equal(C.compareReplay(saved,current).matched,false);});
check(()=>{const saved=makeReport(),current=C.clone(saved);for(const r of [current.originalSimulationRequest,current.preparation.request,current.validation.preparation.request])r.customPriority=["flash-of-light"];const result=C.compareReplay(saved,current);assert.equal(result.requestMatched,false);assert.equal(result.simulationMatched,true);assert.equal(result.matched,false);});
check(()=>{const a={character:{id:first,ownerKey:first}},b={character:{id:second,ownerKey:second}};assert.notEqual(C.canonicalRequest(a),C.canonicalRequest(b),"Only the exact generated profile ID is excluded; all other IDs remain literal inputs.");});
for(const mutate of [
 x=>x.schema="warcraftsim.solo-dps-report",x=>x.version=2,x=>x.build.character.classKey="warrior",x=>x.originalSimulationRequest.character.classKey="rogue",x=>x.simulation.classKey="rogue",
 x=>delete x.simulation.healingDone,x=>x.simulation.absorptionDone=-1,x=>x.simulation.run.timeline=[null],x=>x.simulation.run.timeline=[],x=>x.simulation.run.timeline[0].type=46,
 x=>x.preparation.request.seed++,x=>x.validation.preparation.maximumMana++,x=>x.preparation.classKey="shaman",x=>x.simulation.dps=Infinity
])check(()=>{const saved=makeReport(),invalid=C.clone(saved);mutate(invalid);assert.throws(()=>C.requireReport(invalid));assert.deepEqual(saved,makeReport());});

check(()=>{const inventory=C.clone(build.character.combatItems),before=C.clone(inventory);C.setCombatItemEnabled(inventory,"major-mana-potion",false);assert.deepEqual(inventory.disabledItemKeys,["major-healing-potion","major-mana-potion"]);assert.equal(inventory.majorManaPotionCharges,2);C.setCombatItemEnabled(inventory,"major-mana-potion",false);assert.equal(inventory.disabledItemKeys.length,2);C.setCombatItemEnabled(inventory,"major-mana-potion",true);assert.deepEqual(inventory,before);const selected=C.clone(inventory);assert.throws(()=>C.setCombatItemEnabled(inventory,"wrong-item",true));assert.deepEqual(inventory,selected);});
const compare={baseline:C.clone(build),candidate:C.clone(build),iterations:4,validationIterations:3};
check(()=>assert.equal(C.requireComparisonRequest(compare),compare));
for(const mutate of [x=>x.iterations=1,x=>x.validationIterations=41,x=>x.iterations=2.5,x=>x.extra="unknown",x=>x.baseline.character.encounter.seed=-1,x=>x.baseline.character.encounter.seed=2146483605,x=>x.candidate.character.classKey="rogue",x=>x.candidate.character.level=59,x=>x.candidate.schema="unknown"])
 check(()=>{const invalid=C.clone(compare),before=C.clone(compare);mutate(invalid);assert.throws(()=>C.requireComparisonRequest(invalid));assert.deepEqual(compare,before);});
console.log("Paladin production class-build contract passed: "+checks+" checks; class/settings guards, exact talent-only preservation, complete ending-cap/resource validation, full request/response replay, structural import rejection, inventory enablement and comparison validation.");
