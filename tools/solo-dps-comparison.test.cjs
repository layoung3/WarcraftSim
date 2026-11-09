"use strict";
const assert=require("node:assert/strict");
const {inspectReport,compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
function report(overrides={}){
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
   damageByAbility:{"arcane-blast":8000}},
  rawServerResponse:{sim:{dps:100,damageDone:8000}}};
 overrides(x);return x;
}
const a=report(),b=report(x=>{
 x.build.talents.ranks["arcane-power"]=0;
 x.build.gear.items[0].itemId=20368;
 x.combatSummary.dps=115;x.combatSummary.damageDone=9200;
});
const compared=compareReports(a,b);
assert.equal(compared.comparable,true);
assert.equal(compared.dpsDelta,15);
assert.equal(compared.dpsDeltaPercent,15);
assert.equal(compared.damageDelta,1200);
assert.ok(compared.changes.some(x=>x.includes("Talent arcane-power")));
assert.ok(compared.changes.some(x=>x.includes("Gear main-hand")));
assert.equal(compared.a.ownerDamage,null,"Unknown owner share is not fabricated as zero");
assert.ok(compared.caveats.some(x=>x.includes("reconciliation is unavailable")));
assert.equal(a.combatSummary.dps,100,"Source reports cannot be mutated");
assert.equal(compareReports(a,report(x=>{x.build.encounter.seed="43";})).comparable,false);
assert.ok(compareReports(a,report(x=>{x.build.encounter.seed="43";})).warnings.some(x=>x.includes("Seed")));
assert.ok(compareReports(a,report(x=>{x.build.encounter.duration="90";})).warnings.some(x=>x.includes("Duration")));
assert.ok(compareReports(a,report(x=>{x.build.modifiers.buffs=[];})).warnings.some(x=>x.includes("Buffs")));
assert.ok(compareReports(a,report(x=>{x.originalSimulationRequest.mage.targetLevel=62;})).warnings.some(x=>x.includes("Target level")));
assert.ok(compareReports(a,report(x=>{x.build.encounter.soloScript.enabled=true;})).warnings.some(x=>x.includes("Scripted encounter")));
assert.ok(compareReports(a,report(x=>{x.build.provisional=false;})).warnings.some(x=>x.includes("Baseline confidence")));
const multi=report(x=>{
 x.build.encounter.iterations="3";
 x.iterationStatistics={iterations:3,dps:{mean:110}};
 x.combatSummary.dps=90;
});
const multi2=report(x=>{
 x.build.encounter.iterations="3";x.iterationStatistics={iterations:3,dps:{mean:120}};
 x.combatSummary.dps=70;
});
assert.equal(inspectReport(multi).dps,110,"Multi-run mean must not be representative-run DPS");
assert.equal(compareReports(multi,multi2).dpsDelta,10);
assert.equal(compareReports(multi,multi2).damageDelta,null,
 "Representative damage cannot be presented as a multi-iteration mean");
assert.ok(compareReports(a,multi).warnings.some(x=>x.includes("Iterations")));
assert.equal(compareReports(a,multi).dpsDelta,null);
const warlock=report(x=>{x.build.classKey="warlock";x.build.specialization="demonology";
 x.originalSimulationRequest={classKey:"warlock",specialization:"demonology",
  warlock:{seed:42,durationSeconds:80,targetArmor:3500,targetLevel:63}};
 x.combatSummary={dps:100,damageDone:8000,ownerDamage:6500,petDamage:1500};
});
assert.equal(inspectReport(warlock).reconciliation.status,"reconciled");
assert.ok(compareReports(a,warlock).warnings.some(x=>x.includes("Class")));
const bad=report(x=>{x.combatSummary.dps=null;});
assert.ok(compareReports(a,bad).warnings.some(x=>x.includes("DPS not reported")));
assert.throws(()=>inspectReport({schema:"warcraftsim.solo-dps-build",version:1}),/complete Solo DPS report/);
assert.throws(()=>inspectReport({schema:"warcraftsim.solo-dps-report",version:2}),/complete Solo DPS report/);
console.log("CP175 F4C comparison contracts passed: matched conditions, guarded deltas, distributions, attribution and strict imports.");
