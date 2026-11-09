"use strict";
const assert=require("node:assert/strict");
const fs=require("node:fs");
const vm=require("node:vm");
const {inspectReport,compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const clone=value=>JSON.parse(JSON.stringify(value));
function report(overrides=()=>{}){
 const x={schema:"warcraftsim.solo-dps-report",version:1,
  build:{schema:"warcraftsim.solo-dps-build",version:1,classKey:"mage",
   specialization:"arcane",race:"human",provisional:true,
   talents:{ranks:{"arcane-power":1}},gear:{items:[{slotKey:"main-hand",itemId:19102}]},
   modifiers:{buffs:["arcane-intellect"],consumables:[],targetDebuffs:[],targetDebuffMode:"maintained"},
   encounter:{duration:"80",seed:"42",iterations:"1",targetHealth:"2000000",
    soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[]},
    customPriority:["arcane-blast"]}},
  originalSimulationRequest:{classKey:"mage",specialization:"arcane",
   mage:{durationSeconds:80,seed:42,targetLevel:63,targetArmor:3500,
    targetMaximumHealth:2000000}},
  combatSummary:{dps:100,damageDone:8000,ownerDamage:null,petDamage:null,
   damageByAbility:{"arcane-blast":8000}}};
 x.originalSimulationRequest.mage.soloEncounter=clone(x.build.encounter.soloScript);
 x.originalSimulationRequest.mage.externalModifiers=clone(x.build.modifiers);
 overrides(x);
 // A real export repeats representative metrics in its raw response.
 x.rawServerResponse={sim:{dps:x.combatSummary.dps,damageDone:x.combatSummary.damageDone,
  damageByAbility:clone(x.combatSummary.damageByAbility||{})}};
 return x;
}
function distribution(mean=110,representative=100,samples=[90,100,140]){
 return report(x=>{
  x.build.encounter.iterations="3";
  x.iterationStatistics={iterations:3,baseSeed:42,seedStrategy:"baseSeed + index",
   dps:{mean},representativeSeed:43,samples:samples.map((dps,i)=>({seed:42+i,dps}))};
  x.combatSummary.dps=representative;x.combatSummary.damageDone=representative*80;
  x.combatSummary.damageByAbility={"arcane-blast":representative*80};
 });
}
function blocked(a,b,pattern){
 const result=compareReports(a,b);
 assert.equal(result.comparable,false);
 assert.equal(result.dpsDelta,null);
 assert.equal(result.dpsDeltaPercent,null);
 assert.equal(result.damageDelta,null);
 if(pattern)assert.ok(result.warnings.some(x=>pattern.test(x)),result.warnings.join("\n"));
 return result;
}
const a=report(),b=report(x=>{
 x.build.talents.ranks["arcane-power"]=0;
 x.build.gear.items[0].itemId=20368;
 x.combatSummary.dps=115;x.combatSummary.damageDone=9200;
 x.combatSummary.damageByAbility={"arcane-blast":9200};
});
const snapshot=JSON.stringify(a),compared=compareReports(a,b);
assert.equal(compared.comparable,true);
assert.equal(compared.dpsDelta,15);
assert.equal(compared.dpsDeltaPercent,15);
assert.equal(compared.damageDelta,1200);
assert.ok(compared.changes.some(x=>x.includes("Talent arcane-power")));
assert.ok(compared.changes.some(x=>x.includes("Gear main-hand")));
assert.equal(compared.a.ownerDamage,null,"Unknown owner share is not fabricated as zero");
assert.ok(compared.caveats.some(x=>x.includes("reconciliation is unavailable")));
assert.equal(JSON.stringify(a),snapshot,"Source reports cannot be mutated");

for(const [change,pattern] of [
 [x=>{x.build.encounter.seed="43";},/Seed/],
 [x=>{x.build.encounter.duration="90";},/Duration/],
 [x=>{x.build.modifiers.buffs=[];},/Buffs/],
 [x=>{x.originalSimulationRequest.mage.targetLevel=62;},/Target level/],
 [x=>{x.build.encounter.soloScript.enabled=true;},/Scripted encounter/],
 [x=>{x.build.provisional=false;},/Baseline confidence/]
])blocked(a,report(change),pattern);

// Duplicate build/request metadata must not hide changed execution conditions.
for(const [change,pattern] of [
 [x=>{x.originalSimulationRequest.mage.seed=99;},/Seed disagrees/],
 [x=>{x.originalSimulationRequest.mage.durationSeconds=95;},/Duration disagrees/],
 [x=>{x.originalSimulationRequest.mage.soloEncounter.enabled=true;},/Scripted encounter disagrees/],
 [x=>{x.originalSimulationRequest.mage.externalModifiers.buffs=[];},/buffs disagrees/],
 [x=>{x.originalSimulationRequest.mage.useProvisionalLevel60Baseline=false;},/Baseline confidence disagrees/],
 [x=>{x.originalSimulationRequest.mage.incomingDamageAtSeconds=10;},/Encounter assumptions/],
 [x=>{x.originalSimulationRequest.mage.targetDodgePercent=10;},/Encounter assumptions/],
 [x=>{x.originalSimulationRequest.mage.targetInitialHealthPercent=20;},/Encounter assumptions/],
 [x=>{x.originalSimulationRequest.mage.assumeExternalHealingForLifeTap=true;},/Encounter assumptions/],
 [x=>{x.originalSimulationRequest.mage.scriptedParryEverySeconds=5;},/Encounter assumptions/]
])blocked(a,report(change),pattern);
for(const remove of [
 x=>{delete x.build.encounter.seed;delete x.originalSimulationRequest.mage.seed;},
 x=>{delete x.build.encounter.iterations;},
 x=>{delete x.build.provisional;},
 x=>{delete x.build.modifiers;delete x.originalSimulationRequest.mage.externalModifiers;},
 x=>{delete x.build.encounter.soloScript;delete x.originalSimulationRequest.mage.soloEncounter;}
]){
 const missing=report(remove);
 blocked(missing,clone(missing),/not recorded/);
}
const unknown=report(x=>{x.combatSummary.dps=null;x.combatSummary.damageDone=null;});
unknown.rawServerResponse.sim={dps:100,damageDone:8000};
assert.equal(inspectReport(unknown).dps,null,"An explicit unknown must not be replaced by another field");
assert.equal(inspectReport(unknown).damage,null);
blocked(a,unknown,/DPS not reported/);
const unknownAbilities=report(x=>{
 x.combatSummary.damageByAbility={unknown:null,boolean:false,blank:" ",array:[],valid:"12"};
});
assert.deepEqual(inspectReport(unknownAbilities).sources,[{name:"valid",damage:12}]);
assert.equal(inspectReport(unknownAbilities).abilityReconciliation,null);

const multi=distribution(),multi2=distribution(120,110,[70,110,180]);
assert.equal(inspectReport(multi).dps,110,"Mean must not be representative-run DPS");
assert.equal(inspectReport(multi).damage,8000);
assert.equal(compareReports(multi,multi2).dpsDelta,10);
assert.equal(compareReports(multi,multi2).damageDelta,null,"Representative damage is not a multi-run mean");
blocked(a,multi,/Iterations/);
for(const change of [
 x=>{x.iterationStatistics.iterations=2;},
 x=>{x.iterationStatistics.samples[2].seed=43;},
 x=>{x.iterationStatistics.samples[0].dps=900;},
 x=>{x.iterationStatistics.representativeSeed=999;},
 x=>{delete x.iterationStatistics.samples;},
 x=>{x.iterationStatistics.baseSeed=99;}
]){
 const invalid=clone(multi);change(invalid);
 blocked(multi,invalid,/disagrees|sample|Distribution|Representative|Base seed/i);
}
const otherSeeds=clone(multi);
otherSeeds.iterationStatistics.samples[2].seed=99;
blocked(multi,otherSeeds,/Sample seeds/);
const otherStrategy=clone(multi);otherStrategy.iterationStatistics.seedStrategy="other";
blocked(multi,otherStrategy,/Seed strategy/);

const warlock=report(x=>{
 x.build.classKey="warlock";x.build.specialization="demonology";
 x.originalSimulationRequest={classKey:"warlock",specialization:"demonology",
  warlock:clone(x.originalSimulationRequest.mage)};
 x.combatSummary={dps:100,damageDone:8000,ownerDamage:6500,petDamage:1500,
  damageByAbility:{"shadow-bolt":6500,"demon:firebolt":1500}};
});
assert.equal(inspectReport(warlock).reconciliation.status,"reconciled");
blocked(a,warlock,/Class/);
const mismatch=clone(warlock);mismatch.combatSummary.ownerDamage=6000;
blocked(warlock,mismatch,/Owner and companion/);
const rawMismatch=clone(a);rawMismatch.rawServerResponse.sim.dps=99;
blocked(a,rawMismatch,/raw server response/);
const abilityMismatch=clone(a);abilityMismatch.combatSummary.damageByAbility["arcane-blast"]=7900;
blocked(a,abilityMismatch,/Ability damage/);
const shiftedAbility=clone(a);
shiftedAbility.combatSummary.damageByAbility={"invented-ability":8000};
blocked(a,shiftedAbility,/Ability damage disagrees/);
const staleSeed=report(x=>{x.originalSimulationRequest.mage.seed=99;});
blocked(staleSeed,clone(staleSeed),/Seed disagrees/);
const durationMismatch=report(x=>{
 x.combatSummary.damageDone=9000;x.combatSummary.damageByAbility={"arcane-blast":9000};
});
blocked(a,durationMismatch,/DPS and duration/);
const rounded=clone(warlock);rounded.combatSummary.ownerDamage+=0.01;
assert.equal(inspectReport(rounded).reconciliation.status,"reconciled","Small serialization rounding is tolerated");

// Hawk is neither owner nor permanent pet; include it once and label it separately.
const hunter=report(x=>{
 x.build.classKey="hunter";x.build.specialization="beast-mastery";
 x.originalSimulationRequest={classKey:"hunter",specialization:"beast-mastery",
  beastMastery:clone(x.originalSimulationRequest.mage)};
 x.combatSummary={dps:100,damageDone:8000,ownerDamage:6000,petDamage:1500,hawkDamage:500,
  damageByAbility:{"hunter:auto":6000,"pet:melee":1500,"hawk:attack":500}};
});
hunter.rawServerResponse.sim.hawkDamage=500;
assert.equal(inspectReport(hunter).reconciliation.status,"reconciled");
assert.equal(compareReports(hunter,clone(hunter)).comparable,true);
const oldHunter=clone(hunter);delete oldHunter.combatSummary.hawkDamage;
assert.equal(inspectReport(oldHunter).hawkDamage,500,"Historical reports retain raw Hawk attribution");
assert.equal(inspectReport(oldHunter).reconciliation.status,"reconciled");

// Native Warrior report envelope has no classKey/specialization on its request.
const warrior=report(x=>{
 x.build.classKey="warrior";x.build.specialization="arms";
 x.originalSimulationRequest={warrior:{...clone(x.originalSimulationRequest.mage),preset:"arms"},iterations:1};
 x.combatSummary={dps:100,damageDone:8000,ownerDamage:8000,petDamage:0,
  damageByAbility:{"mortal-strike":8000}};
});
warrior.rawServerResponse={classKey:"warrior",specialization:"arms",sim:{meanDps:100,
 batch:{representativeRun:{summary:{actorSummaries:{warrior:{
  damageDone:8000,damageDoneByAbility:{"mortal-strike":8000}}}}}}}};
assert.equal(compareReports(warrior,clone(warrior)).comparable,true);
const nativeFallback=clone(warrior);
delete nativeFallback.combatSummary.dps;delete nativeFallback.combatSummary.damageDone;
assert.equal(inspectReport(nativeFallback).dps,100);
assert.equal(inspectReport(nativeFallback).damage,8000);

for(const invalid of [
 {schema:"warcraftsim.solo-dps-build",version:1},
 {schema:"warcraftsim.solo-dps-report",version:2},
 ...[
 x=>{x.build.schema="other";},x=>{x.build.version=2;},
 x=>{x.build.classKey="constructor";},x=>{x.build.classKey="unknown";},x=>{x.build.specialization="destruction";},
 x=>{x.combatSummary=[];},x=>{x.originalSimulationRequest={};},
 x=>{x.originalSimulationRequest.classKey="warlock";},
 x=>{x.originalSimulationRequest.warrior={};},
 x=>{x.build.gear.items={};},x=>{x.build.gear.items=[null];},
 x=>{x.build.modifiers.buffs="arcane-intellect";},
 x=>{x.build.talents.ranks=[];},x=>{x.combatSummary.damageByAbility=[];},
 x=>{x.build.encounter.soloScript="invalid";},
 x=>{x.iterationStatistics={schema:"other",version:1};},
 x=>{x.iterationStatistics=[];},x=>{x.build.encounter.iterations="1.5";},
 x=>{x.build.encounter.iterations=0;},x=>{x.build.encounter.iterations=101;},
 ...[false,true,[],{}," ",-1,Infinity,NaN].map(value=>x=>{x.combatSummary.dps=value;})
 ].map(change=>report(change))
])assert.throws(()=>inspectReport(invalid),/complete Solo DPS report/);
for(const change of [
 x=>{delete x.rawServerResponse;},x=>{x.rawServerResponse.sim=[];},
 x=>{x.iterationStatistics.samples=[{seed:42,dps:false}];}
]){
 const invalid=clone(multi);change(invalid);
 assert.throws(()=>inspectReport(invalid),/complete Solo DPS report/);
}

// Lightweight DOM integration executes the real renderer in both slot orders.
function ui(){
 const nodes=new Map(),listeners={};
 function node(){
  return {textContent:"",children:[],disabled:false,hidden:false,value:"",handlers:{},
   append(...values){this.children.push(...values);},replaceChildren(...values){this.children=values;},
   addEventListener(event,fn){this.handlers[event]=fn;},click(){return this.handlers.click?.();}};
 }
 const document={getElementById(id){if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);},
  createElement:node,addEventListener(event,fn){listeners[event]=fn;}};
 vm.runInNewContext(fs.readFileSync(require.resolve("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js"),"utf8"),
  {document,window:{}});
 return {get:document.getElementById,select(slot,value){
  listeners["warcraftsim:simulation-complete"]({detail:value});
  document.getElementById("compareUse"+slot).click();
 }};
}
for(const [left,right] of [[a,multi],[multi,a],[multi,multi2]]){
 const page=ui();page.select("A",left);page.select("B",right);
 const row=page.get("compareRows").children[0].children;
 assert.ok(row[1].textContent.includes(inspectReport(left).dpsLabel));
 assert.ok(row[2].textContent.includes(inspectReport(right).dpsLabel));
 assert.equal(page.get("compareRows").children[1].children[3].textContent,"Not compared");
}
const page=ui();page.select("A",hunter);page.select("B",hunter);
assert.ok(page.get("compareRows").children.some(row=>row.children[0].textContent.includes("Hawk")));
console.log("CP175 F4C comparison contracts passed: strict imports, request/report integrity, independent reconciliation, matched distributions and per-run labels.");
