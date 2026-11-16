"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path");
const C=require("../WarcraftSim.Api/wwwroot/class-build-contract.js");
const base=(process.env.CP174_BASE_URL||"http://127.0.0.1:5142")+"/api/forever/shared/class-builds";
const out=path.resolve(process.env.CP174_REPORT_DIR||"/tmp/cp174-full-reports/class-build-http");fs.mkdirSync(out,{recursive:true});
let checks=0;const files=[];
async function call(route,body,expected=200,raw=false){const r=await fetch(base+route,{method:body===undefined?"GET":"POST",headers:body===undefined?{}:{"Content-Type":"application/json"},body:body===undefined?undefined:raw?body:JSON.stringify(body)});const value=await r.json();assert.equal(r.status,expected,route+": "+JSON.stringify(value).slice(0,1000));checks++;return value;}
function save(name,value){fs.writeFileSync(path.join(out,name+".json"),JSON.stringify(value,null,2));files.push(name+".json");}
(async()=>{
 const coverage=await call("/coverage");assert.equal(coverage.classes.length,9);assert.ok(coverage.classes.every(x=>x.trees.length===3&&!x.allTreeCompletion));save("coverage",coverage);
 const witnesses=[];let shamanCatalog,shamanEquipment;
 for(const cls of ["shaman","rogue"]){
  const catalog=await call("/"+cls+"/catalog");assert.equal(catalog.talents.length,cls==="shaman"?50:54);assert.equal(new Set(catalog.talents.map(x=>x.tree)).size,3);save(cls+"-catalog",catalog);
  const gear=await call("/"+cls+"/equipment");save(cls+"-equipment",gear);
  if(cls==="shaman"){shamanCatalog=catalog;shamanEquipment=gear;}
  const oldPending=gear.items.find(x=>x.item.id===19682);assert.equal(oldPending.eligibility.restrictionStatus,"pending");
  for(const preset of catalog.presets){
   let build=C.clone(catalog.defaultBuild);build.acceptPartialCoverage=true;build.character.encounter.durationSeconds=30;build.character.encounter.targetMaximumHealth=2000000;build.character.encounter.seed=18452;build.character.encounter.captureTimeline=true;
   build.character.equipment.items=cls==="shaman"?[19829,19830].map((id,i)=>({slotKey:i?"wrist":"waist",itemId:id})): [{slotKey:"wrist",itemId:18452}];
   // Use catalog-owned slot facts; no inferred slot or value is permitted.
   for(const item of build.character.equipment.items){const record=gear.items.find(x=>x.item.id===item.itemId);assert.ok(record?.eligibility.canExecute,"Explicit current item eligibility "+item.itemId);item.slotKey=record.item.allowedSlots[0];Object.assign(item,{itemKey:"",name:"",enchantSpellId:null,temporaryEnchantSpellId:null,stats:{values:{}},modifiers:[]});}
   build.character.modifiers.buffs=["battle-shout"];build.character.modifiers.targetDebuffs=["faerie-fire-rank4"];
   build.character.classSettings=cls==="shaman"?{incomingBossHitDamage:100,incomingBossSwingSeconds:3,classWideTargetCount:2}:{enableBossAttacks:true,bossHitDamage:70,bossSwingSeconds:3,additionalTargets:1};
   const before=C.clone(build),applied=await call("/apply-talent-preset",{build,presetKey:preset.key});build=applied.build;
   const preserved=C.clone(before);preserved.character.itemizedTalents=build.character.itemizedTalents;preserved.presetOrigin=build.presetOrigin;assert.deepEqual(build,preserved,"Preset is talents-only");assert.equal(applied.validation.canExecute,true,JSON.stringify(applied.validation));
   const first=await call("/simulate",build);assert.ok(first.simulation.dps>0);assert.ok(first.simulation.run.timeline.length>0);save(preset.key+"-build",build);save(preset.key+"-report",first);witnesses.push({name:preset.key,build,report:first});
   if(cls==="shaman"){assert.ok(first.simulation.run.timeline.some(x=>x.type===8&&x.abilityKey==="water-shield"&&x.amount>0),"Actual Restoration Water Shield Mana in "+preset.key);if(build.character.itemizedTalents["mana-tide-totem"])assert.ok(first.simulation.run.timeline.some(x=>x.type===8&&x.abilityKey==="mana-tide-totem"&&x.amount>0),"Actual learned Mana Tide pulses in "+preset.key);checks++;}
   const repeat=await call("/simulate",build);assert.equal(C.canonicalSimulation(first.simulation),C.canonicalSimulation(repeat.simulation),"Whole-combat repeat "+preset.key);checks++;
   assert.equal((await call("/validate-report",first)).valid,true);
   const changed=C.clone(first);changed.simulation.dps++;await call("/validate-report",changed,400);
   const origin=C.clone(build);origin.presetOrigin="different-origin";origin.character.specialization="different-origin";const same=await call("/simulate",origin);assert.equal(C.canonicalSimulation(first.simulation),C.canonicalSimulation(same.simulation));checks++;
   const baseline=await call("/validate",build);const without=C.clone(build);without.character.equipment.items=[];without.character.modifiers.buffs=[];const naked=await call("/validate",without);assert.ok(baseline.preparation.maximumHealth>naked.preparation.maximumHealth,"Actual catalog Stamina reaches Health");const restored=await call("/validate",build);assert.deepEqual(restored.preparation,baseline.preparation,"Repeated preparation does not duplicate contributions");
  }
 }
 const witness=witnesses.find(x=>x.name==="rogue-subtlety"),candidate=C.clone(witness.build);candidate.character.itemizedTalents.precision=1;
 // This row2 partial-rank edit keeps the five earlier Combat points and has no deeper dependent in this starter.
 const compared=await call("/compare",{baseline:witness.build,candidate,iterations:4,validationIterations:3});save("rogue-paired-comparison",compared);
 assert.deepEqual(compared.training.samples.map(x=>x.seed),[18452,18453,18454,18455]);assert.ok(compared.validation.samples.every(x=>x.seed>=1018455));
 for(const cohort of [compared.training,compared.validation])for(const sample of cohort.samples)assert.ok(Math.abs(sample.deltaDps-(sample.candidateDps-sample.baselineDps))<1e-9);
 const mismatch=C.clone(candidate);mismatch.character.encounter.targetArmor++;await call("/compare",{baseline:witness.build,candidate:mismatch,iterations:2,validationIterations:2},400);
 await call("/compare",{baseline:witness.build,candidate,iterations:41,validationIterations:2},400);
 const invalid=C.clone(witness.build);invalid.character.itemizedTalents["unknown-key"]=0;const rejected=await call("/validate",invalid);assert.equal(rejected.canSave,false);assert.equal(rejected.canExecute,false);
 const raw=JSON.stringify(witness.build);await call("/validate",raw.replace('{','{"schema":"duplicate",'),400,true);
 const partial=C.clone(witness.build);partial.character.itemizedTalents={"master-of-deception":1};partial.acceptPartialCoverage=false;const partialResult=await call("/validate",partial);assert.equal(partialResult.isLegal,true);assert.equal(partialResult.canExecute,false);assert.ok(partialResult.coverageWarnings.length);
 const starter=C.clone(shamanCatalog.defaultBuild);starter.acceptPartialCoverage=true;starter.character.encounter.durationSeconds=45;starter.character.encounter.targetMaximumHealth=2000000;starter.character.encounter.seed=3001;starter.character.classSettings={targetDistanceYards:30};starter.character.rotation={mode:"custom",priority:["lightning-bolt"]};
 const geared=C.clone(starter);geared.character.equipment.items=[19829,19830].map(id=>({slotKey:shamanEquipment.items.find(x=>x.item.id===id).item.allowedSlots[0],itemId:id,itemKey:"",name:"",enchantSpellId:id===19830?1220624:null,temporaryEnchantSpellId:null,stats:{values:{}},modifiers:[]}));
 const gearComparison=await call("/compare",{baseline:starter,candidate:geared,iterations:8,validationIterations:8});save("elemental-restoration-gear-comparison",gearComparison);assert.ok(gearComparison.training.delta.mean>0&&gearComparison.validation.delta.mean>0,"The sourced gear candidate must improve the mean in both finite cohorts");checks++;
 save("acceptance-receipt",{checks,witnesses:witnesses.map(x=>x.name),files,sourceCommit:process.env.CP174_SOURCE_COMMIT||process.env.GITHUB_SHA||"not-stamped-local-build",browserEvidence:false});
 console.log("Class-wide HTTP acceptance passed: "+checks+" responses/checks; "+witnesses.length+" complete build/report witnesses; paired and separate validation cohorts.");
})().catch(error=>{console.error(error);process.exitCode=1;});
