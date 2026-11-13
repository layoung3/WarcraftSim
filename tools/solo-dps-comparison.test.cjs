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
  {document,window:{SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')}});
 return {get:document.getElementById,select(slot,value){
  listeners["warcraftsim:simulation-complete"]({detail:value});
  document.getElementById("compareUse"+slot).click();
 },openImport(slot){document.getElementById("compareImport"+slot).click();
 },readImport(text){
  document.getElementById("compareFile").files=[{text}];
  return document.getElementById("compareFile").handlers.change();
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

// Complete selected build identity is checked against the authoritative request,
// not against filtered caster ranks or backend-only equipment metadata.
function authoritative(input){
 const r=clone(input),key=r.build.classKey==="hunter"?
  (r.build.specialization==="beast-mastery"?"beastMastery":r.build.specialization):r.build.classKey;
 const t=r.originalSimulationRequest[key];
 if(r.build.classKey==="warrior"){
  t.race=r.build.race;t.talentMode="tree";t.talentRanks=clone(r.build.talents.ranks);
  t.gear=r.build.gear.items.map(item=>({slot:item.slotKey,itemId:item.itemId,
   enchantSpellId:item.enchantSpellId??null,name:"Resolved item",stats:{strength:5}}));
 }else{
  r.originalSimulationRequest.itemizedTalentMode=true;
  r.originalSimulationRequest.itemizedTalentRanks=clone(r.build.talents.ranks);
  if(r.build.classKey==="hunter"){t.race=r.build.race;t.equipment=clone(r.build.gear);}
  else t.character={race:r.build.race,equipment:clone(r.build.gear)};
 }
 return r;
}
const identityReport=authoritative(a);
assert.equal(compareReports(identityReport,clone(identityReport)).comparable,true);
for(const [change,pattern] of [
 [x=>{x.build.race="gnome";},/Race disagrees/],
 [x=>{x.build.gear.items[0].itemId=20368;},/Gear disagrees/],
 [x=>{x.build.gear.items[0].enchantSpellId=23799;},/Gear disagrees/],
 [x=>{x.build.gear.items[0].temporaryEnchantSpellId=25123;},/Gear disagrees/],
 [x=>{x.build.talents.ranks={};},/Talent ranks disagrees/],
 [x=>{x.originalSimulationRequest.itemizedTalentRanks["arcane-power"]=0;},/Talent ranks disagrees/]
]){
 const edited=clone(identityReport);change(edited);
 blocked(identityReport,edited,pattern);
}
const normalized=clone(identityReport);
normalized.build.talents.ranks["unselected"]=0;
normalized.build.gear.items[0].enchantSpellId=0;
normalized.build.gear.items[0].temporaryEnchantSpellId=null;
normalized.originalSimulationRequest.mage.talents={ranks:{}};
normalized.originalSimulationRequest.mage.character.equipment.items[0].name="Backend-only label";
assert.equal(compareReports(identityReport,normalized).comparable,true,
 "Zero ranks, omitted enchants, filtered engine ranks and backend gear labels do not create false drift");
const historicalIdentity=compareReports(a,clone(a));
assert.ok(historicalIdentity.caveats.some(x=>x.includes("Race build/request identity is unavailable")));
assert.ok(historicalIdentity.caveats.some(x=>x.includes("Gear build/request identity is unavailable")));
assert.ok(historicalIdentity.caveats.some(x=>x.includes("Talent ranks build/request identity is unavailable")));
const nativeIdentity=authoritative(warrior);
const changedNative=clone(nativeIdentity);changedNative.build.gear.items[0].itemId=20368;
blocked(nativeIdentity,changedNative,/Gear disagrees/);
const nativeTemp=clone(nativeIdentity);nativeTemp.build.gear.items[0].temporaryEnchantSpellId=25123;
assert.ok(inspectReport(nativeTemp).integrityCaveats.some(x=>x.includes("Temporary enchant identity is unavailable")));

for(const spec of ["beast-mastery","marksmanship","survival"]){
 const r=clone(hunter),key=spec==="beast-mastery"?"beastMastery":spec;
 r.build.specialization=spec;
 r.originalSimulationRequest={classKey:"hunter",specialization:spec,[key]:{
  ...clone(hunter.originalSimulationRequest.beastMastery),
  tuning:{targetArmor:3800,targetDodgePercent:5,targetParryPercent:5,petAvoidancePercent:5,rangedAttackPower:1000},
  petTuning:{targetArmor:3800,petAvoidancePercent:5,petBaseArmor:1200}
 }};
 assert.equal(inspectReport(r).encounter.targetArmor,3500,"Explicit top-level target armor retains precedence");
 delete r.originalSimulationRequest[key].targetArmor;
 assert.equal(inspectReport(r).encounter.targetArmor,3800,"Nested target armor is reported, not called unavailable");
 for(const [container,field] of [["tuning","targetArmor"],["tuning","targetDodgePercent"],
  ["tuning","targetParryPercent"],["tuning","petAvoidancePercent"],
  ["petTuning","petAvoidancePercent"]]){
  const changed=clone(r);changed.originalSimulationRequest[key][container][field]=0;
  blocked(r,changed,/Encounter assumptions|Target armor/);
 }
 const buildTuning=clone(r);
 buildTuning.originalSimulationRequest[key].tuning.rangedAttackPower=1500;
 buildTuning.originalSimulationRequest[key].petTuning.petBaseArmor=2500;
 buildTuning.originalSimulationRequest[key].petTuning.targetArmor=0;
 assert.equal(compareReports(r,buildTuning).comparable,true,"Player/pet stats and unused petTuning.targetArmor must not be classified as encounter changes");
}
for(const [source,key,fields] of [
 [warrior,"warrior",["executePhaseDurationSeconds","encounterPhases",
  "incomingRageExpectedCreatureHealth","incomingRageExpectedArmorReductionPercent"]],
 [warlock,"warlock",["demonDeathAtSeconds","demonSacrificeAtSeconds",
  "demonReplacementAtSeconds","voidwalkerSacrificeAtSeconds"]],
 [hunter,"beastMastery",["scriptedAvoidanceEverySeconds","petUptimePercent",
  "triggerTraps","triggerRangedTraps","trapTriggerDelaySeconds"]]
]){
 for(const field of fields){
  const changed=clone(source);
  changed.originalSimulationRequest[key][field]=field==="encounterPhases"?
   [{key:"phase",startTimeSeconds:20}]:field.startsWith("trigger")?false:5;
  blocked(source,changed,/Encounter assumptions/);
 }
}

// Optional local integration: pass report directories to validate exported
// evidence without changing it. Every mutation is an in-memory copy only.
let realReports=0,realMutations=0;
for(const directory of process.argv.slice(2)){
 for(const filename of fs.readdirSync(directory).filter(name=>name.endsWith("-report.json"))){
  const text=fs.readFileSync(require("node:path").join(directory,filename),"utf8");
  if(!text.trim())continue; // A still-in-progress browser download is not a report.
  const original=JSON.parse(text);
  if(original.schema!=="warcraftsim.solo-dps-report")continue;
  const current=compareReports(original,original);
  assert.equal(current.comparable,true,filename+": "+current.warnings.join("; "));
  realReports++;
  const cls=original.build.classKey,key=cls==="hunter"?
   (original.build.specialization==="beast-mastery"?"beastMastery":original.build.specialization):cls;
  const typed=original.originalSimulationRequest[key];
  const paths=cls==="hunter"?
   [["tuning","targetArmor"],["tuning","targetDodgePercent"],["tuning","targetParryPercent"],
    ["tuning","petAvoidancePercent"],["petTuning","petAvoidancePercent"],
    ["scriptedAvoidanceEverySeconds"],["petUptimePercent"]]:
   cls==="warrior"?[["executePhaseDurationSeconds"],["encounterPhases"],
    ["incomingRageExpectedCreatureHealth"],["incomingRageExpectedArmorReductionPercent"]]:
   cls==="warlock"?[["demonDeathAtSeconds"],["demonSacrificeAtSeconds"],
    ["demonReplacementAtSeconds"],["voidwalkerSacrificeAtSeconds"]]:[];
  for(const path of paths){
   const parent=path.length===1?typed:typed[path[0]],field=path.at(-1);
   if(!parent||!Object.hasOwn(parent,field))continue;
   const changed=clone(original),target=path.length===1?
    changed.originalSimulationRequest[key]:changed.originalSimulationRequest[key][path[0]];
   target[field]=Array.isArray(parent[field])?[{key:"different-phase",startTimeSeconds:10}]:
    parent[field]===null?5:Number(parent[field])+1;
   blocked(original,changed,/Encounter assumptions|Target armor/);realMutations++;
  }
  const raceChanged=clone(original);raceChanged.build.race=original.build.race==="human"?"gnome":"human";
  blocked(original,raceChanged,/Race disagrees/);realMutations++;
  const selected=Object.entries(original.build.talents?.ranks||{}).find(([,rank])=>Number(rank)>0);
  if(selected){
   const changed=clone(original);delete changed.build.talents.ranks[selected[0]];
   blocked(original,changed,/Talent ranks disagrees/);realMutations++;
  }
  if(original.build.gear?.items?.length){
   for(const field of ["itemId","enchantSpellId",...(cls==="warrior"?[]:["temporaryEnchantSpellId"])]){
    const changed=clone(original);
    changed.build.gear.items[0][field]=Number(changed.build.gear.items[0][field]||0)+1;
    blocked(original,changed,/Gear disagrees/);realMutations++;
   }
  }
  if(cls==="hunter"&&typed.tuning){
   const changed=clone(original);
   changed.originalSimulationRequest[key].tuning.rangedAttackPower=
    Number(typed.tuning.rangedAttackPower||0)+10;
   assert.equal(compareReports(original,changed).comparable,true,filename+": player tuning is not an encounter mismatch");
   if(typed.petTuning&&Object.hasOwn(typed.petTuning,"targetArmor")){
    const unused=clone(original);
    unused.originalSimulationRequest[key].petTuning.targetArmor=Number(typed.petTuning.targetArmor)+1;
    assert.equal(compareReports(original,unused).comparable,true,
     filename+": unused pet target armor copy must not block comparisons");
   }
  }
 }
}
if(realReports)console.log("Real-report integrity checks passed:",{reports:realReports,blockedMutations:realMutations});

console.log("CP175 F4C comparison contracts passed: strict imports, request/report integrity, independent reconciliation, matched distributions and per-run labels.");

// Restricted Elemental reports preserve their actual typed request and encounter assumptions.
const elemental=report(x=>{
 x.build.classKey="shaman";x.build.specialization="elemental";x.build.race="orc";
 x.build.gear={items:[]};x.build.talents={ranks:{convection:5}};
 x.build.modifiers={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:"maintained"};
 x.build.encounter.customPriority=["lightning-bolt"];
 x.originalSimulationRequest={classKey:"shaman",specialization:"elemental",itemizedTalentMode:true,itemizedTalentRanks:{convection:5},elemental:{durationSeconds:80,seed:42,targetMaximumHealth:2000000,targetCount:1,incomingBossHitDamage:0,incomingBossSwingSeconds:4,character:{race:"orc",equipment:{items:[]}},talents:{ranks:{convection:5}},externalModifiers:clone(x.build.modifiers),customPriority:["lightning-bolt"]}};
 x.combatSummary.ownerDamage=8000;x.combatSummary.damageByAbility={"lightning-bolt":8000};
});
assert.equal(compareReports(elemental,clone(elemental)).comparable,true);
const elementalIncoming=clone(elemental);elementalIncoming.originalSimulationRequest.elemental.incomingBossHitDamage=50;
blocked(elemental,elementalIncoming,/scenario|encounter/i);
const elementalWrong=clone(elemental);elementalWrong.originalSimulationRequest.shadow={};
assert.throws(()=>inspectReport(elementalWrong),/typed simulation request/);
assert.equal(inspectReport(elemental).petDamage,null,"Unreported companion metric remains unavailable");
console.log("Elemental comparison identity and incoming-boss semantic contracts passed.");

const balance=clone(elemental);
balance.build.classKey="druid";balance.build.specialization="balance";balance.build.race="tauren";
balance.build.talents={ranks:{"improved-wrath":5}};balance.build.encounter.customPriority=["wrath"];
balance.originalSimulationRequest.classKey="druid";balance.originalSimulationRequest.specialization="balance";
balance.originalSimulationRequest.balance=balance.originalSimulationRequest.elemental;delete balance.originalSimulationRequest.elemental;
balance.originalSimulationRequest.itemizedTalentRanks={"improved-wrath":5};
balance.originalSimulationRequest.balance.character.race="tauren";balance.originalSimulationRequest.balance.talents=clone(balance.build.talents);
balance.originalSimulationRequest.balance.customPriority=["wrath"];
delete balance.originalSimulationRequest.balance.targetCount;delete balance.originalSimulationRequest.balance.incomingBossSwingSeconds;
balance.originalSimulationRequest.balance.additionalTargets=0;balance.originalSimulationRequest.balance.bossSwingIntervalSeconds=3;
assert.equal(compareReports(balance,clone(balance)).comparable,true);
const balanceIncoming=clone(balance);balanceIncoming.originalSimulationRequest.balance.bossSwingIntervalSeconds=4;
blocked(balance,balanceIncoming,/scenario|encounter/i);
const balanceWrong=clone(balance);balanceWrong.originalSimulationRequest.elemental={};assert.throws(()=>inspectReport(balanceWrong),/typed simulation request/);
console.log("Balance typed identity and incoming-boss comparison contracts passed.");

const shadow=clone(balance);shadow.build.classKey="priest";shadow.build.specialization="shadow";shadow.build.race="human";
shadow.build.talents={ranks:{"shadow-focus":5}};delete shadow.build.encounter.customPriority;
shadow.originalSimulationRequest.classKey="priest";shadow.originalSimulationRequest.specialization="shadow";
shadow.originalSimulationRequest.shadow=shadow.originalSimulationRequest.balance;delete shadow.originalSimulationRequest.balance;
shadow.originalSimulationRequest.itemizedTalentRanks={"shadow-focus":5};shadow.originalSimulationRequest.shadow.talents=clone(shadow.build.talents);
shadow.originalSimulationRequest.shadow.character.race="human";
for(const key of ["externalModifiers","customPriority","additionalTargets","incomingBossHitDamage","bossSwingIntervalSeconds"])delete shadow.originalSimulationRequest.shadow[key];
shadow.originalSimulationRequest.shadow.targetInitialHealthPercent=100;shadow.originalSimulationRequest.shadow.priestMaximumHealth=3500;
assert.equal(compareReports(shadow,clone(shadow)).comparable,true);
const injuredTarget=clone(shadow);injuredTarget.originalSimulationRequest.shadow.targetInitialHealthPercent=25;
blocked(shadow,injuredTarget,/scenario|encounter/i);
const shadowWrong=clone(shadow);shadowWrong.originalSimulationRequest.balance={};assert.throws(()=>inspectReport(shadowWrong),/typed simulation request/);
console.log("Shadow typed identity and distinct target-initial-health comparison contracts passed.");

// Execute the production import listener and renderer. A failed candidate is
// not a replacement for an accepted slot, including when a read finishes late.
function selectedSnapshot(page){
 const values=id=>page.get(id).children.map(child=>child.children.length?
  child.children.map(cell=>cell.textContent):child.textContent);
 return {a:page.get("compareLabelA").textContent,b:page.get("compareLabelB").textContent,
  hidden:page.get("compareDetails").hidden,rows:values("compareRows"),
  warnings:values("compareWarnings"),changes:values("compareChanges")};
}
function deferred(){let resolve,reject;const promise=new Promise((yes,no)=>{resolve=yes;reject=no;});return {promise,resolve,reject};}
async function imported(page,slot,value){page.openImport(slot);await page.readImport(async()=>JSON.stringify(value));}
(async()=>{
 let transactions=0;
 for(const target of ["A","B"]){
  for(const initial of [[["A",a],["B",b]],[["B",b],["A",a]],[],[["A",a]],[["B",b]],[["A",a],["B",multi]]]){
   for(const read of [async()=>"{",async()=>JSON.stringify(a.build),async()=>{throw new Error("File read failed");}]){
    const page=ui();for(const [slot,value] of initial)page.select(slot,value);
    const before=selectedSnapshot(page);
    page.openImport(target);await page.readImport(read);
    assert.deepEqual(selectedSnapshot(page),before,"Rejected candidate must preserve every accepted label, metric and warning");
    assert.match(page.get("compareStatus").textContent,/Report import rejected:/);
    assert.match(page.get("compareStatus").textContent,new RegExp("Slot "+target+" was not changed"));
    assert.equal(page.get("compareFile").value,"");
    await imported(page,target,target==="A"?b:a);
    const expected=ui();for(const [slot,value] of initial)expected.select(slot,value);
    expected.select(target,target==="A"?b:a);
    assert.deepEqual(selectedSnapshot(page),selectedSnapshot(expected),"Valid import must recover using the new candidate, not the preserved old report");
    assert.doesNotMatch(page.get("compareStatus").textContent,/rejected/);transactions++;
   }
  }
 }
 const page=ui();page.select("A",a);page.select("B",b);
 assert.match(page.get("compareRows").children[0].children[3].textContent,/\+15\.0/);
 await imported(page,"B",staleSeed);
 assert.match(page.get("compareStatus").textContent,/Not directly comparable/);
 assert.equal(page.get("compareRows").children[0].children[3].textContent,"Not comparable","Accepted integrity warnings must never show a numeric delta");
 await imported(page,"B",b);
 for(const rejects of [false,true]){
  const late=deferred();page.openImport("B");const pending=page.readImport(()=>late.promise);
  await imported(page,"B",a);const newer=selectedSnapshot(page),status=page.get("compareStatus").textContent;
  if(rejects)late.reject(new Error("Superseded read failure"));else late.resolve(JSON.stringify(b));
  await pending;assert.deepEqual(selectedSnapshot(page),newer,"Superseded file read must not replace a newer accepted report");
  assert.equal(page.get("compareStatus").textContent,status,"Superseded read failure must not replace newer success status");transactions++;
 }
 const late=deferred();page.openImport("B");const pending=page.readImport(()=>late.promise);
 page.openImport("A");late.reject(new Error("Previous file failed"));await pending;
 await page.readImport(async()=>JSON.stringify(b));
 assert.equal(page.get("compareLabelA").textContent,inspectReport(b).label,"Old read cleanup must not erase a newer file chooser target");
 const expected=ui();expected.select("A",b);expected.select("B",a);
 assert.deepEqual(selectedSnapshot(page),selectedSnapshot(expected));transactions++;
 for(const rejects of [false,true]){
  const late=deferred();page.openImport("B");const pending=page.readImport(()=>late.promise);
  page.select("B",b);const newer=selectedSnapshot(page),status=page.get("compareStatus").textContent;
  if(rejects)late.reject(new Error("Superseded by use-run"));else late.resolve(JSON.stringify(a));
  await pending;assert.deepEqual(selectedSnapshot(page),newer,"Use-run selection must supersede an older file read");
  assert.equal(page.get("compareStatus").textContent,status);transactions++;
 }
 console.log(`Production comparison import transactions passed: ${transactions} rejection/recovery and overlapping-read cases; prior validated deltas remain correctly labeled.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
