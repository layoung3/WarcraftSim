"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm");
const source=fs.readFileSync("WarcraftSim.Api/wwwroot/solo-dps.js","utf8");
const gate=source.slice(source.indexOf("function validateRestrictedCasterImport(data)"),source.indexOf("const beforeElementalImport="));
const ctx={};vm.createContext(ctx);vm.runInContext(gate,ctx);
const build=()=>({classKey:"shaman",gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[]},encounter:{duration:30,seed:1,iterations:1,targetHealth:200000,timeline:true,rotation:"adaptive",customPriority:[],priorityRows:[],rotationConditions:{},combatItems:{majorManaPotionCharges:0},soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]}}});
ctx.validateRestrictedCasterImport(build());
let count=1;
for(const change of [x=>x.racialActive=true,x=>x.gear.items.push({itemId:1}),x=>x.encounter.iterations=2,x=>x.encounter.targetArmor=3500,x=>x.encounter.targetLevel=62,x=>x.encounter.soloScript.enabled=true,x=>x.encounter.soloScript.addWindows.push({}),x=>x.encounter.priorityRows.push({conditions:[{}]}),x=>x.encounter.priorityRows.push({targetActorKey:"target"}),x=>x.encounter.priorityRows.push({orGroups:[[]]}),x=>x.modifiers.targetDebuffs.push("curse-of-elements"),x=>x.encounter.majorManaPotionCharges=1,x=>x.encounter.rotationConditions.foo=1,x=>x.encounter.classSettings={foo:true}]){const b=build();change(b);assert.throws(()=>ctx.validateRestrictedCasterImport(b));count++;}
const preset=JSON.parse(fs.readFileSync("WarcraftSim.Api/wwwroot/itemized-presets/shaman-elemental.json"));
const old=JSON.parse(fs.readFileSync("WarcraftSim.Api/wwwroot/default-talents/shaman-elemental.json"));
assert.deepEqual(preset.ranks,old.overrides.talents.ranks);assert.equal(Object.values(preset.ranks).reduce((a,b)=>a+b),51);
assert.ok(source.includes("post('/api/forever/character/preview-'+"));assert.ok(source.includes("if(['shaman','druid','priest'].includes(cls))return submitted"));
console.log(`Elemental import and preset contracts passed: ${count} gate fixtures, exact existing 51 ranks.`);

for(const field of ["historicalValidationSourceStatus","historicalValidationSourceCommit"])
 assert.ok(source.includes(`${field}:t.${field}||null`),field+" preserves exact source facts or unknown");
assert.ok(source.includes("historicalValidationSourceUrl:t.historicalValidationSourceStatus?t.sourceUrl||null:null"));
assert.ok(source.includes("(isRestrictedCaster()?restrictedCasterName()+': one deterministic run; batch statistics unavailable.':"));
console.log("Elemental historical validation provenance and result disclosure contracts passed.");

assert.ok(source.includes("const admitted=talentCatalog.filter(t=>t.historicalValidationSourceStatus)"));
assert.ok(source.includes("Historical provisional "));
assert.ok(source.includes("admitted.filter(t=>t.tree===tree).length"));
assert.ok(source.includes("t.historicalValidationSourceCommit).filter(Boolean)"));
console.log("Elemental coverage derives three tree counts and provenance from the actual restricted catalog.");

assert.ok(source.includes("number.textContent=isRestrictedCaster()?formatResultNumber(value,key.includes('percent')?'percent':'duration'):String(value)"));
const formatter=source.slice(source.indexOf("function formatResultNumber("),source.indexOf("function readableResultNumbers("));
vm.runInContext(formatter,ctx);
assert.equal(ctx.formatResultNumber(21.528378378378378,"percent"),"21.5");
assert.equal(ctx.formatResultNumber(1000.123456,"duration"),"1,000.12");
console.log("Elemental display-only stat formatting retains raw response precision.");

(async()=>{
 const transactional=source.slice(source.indexOf("applyConfig=async function(data){",source.indexOf("const beforeElementalImport=")),source.indexOf("const beforeElementalSave="));
 for(const priorClass of ["shaman","mage"])for(const dirty of [true,false]){
 const original={classKey:priorClass,race:"orc",provisional:true,talents:{ranks:{convection:5}},encounter:{customPriority:[],priorityRows:[]}},state={active:JSON.parse(JSON.stringify(original))};
 const sandbox={buildDirty:dirty,request:{},loadedClass:"shaman",clone:v=>JSON.parse(JSON.stringify(v)),saveConfig:()=>state.active,validateRestrictedCasterImport:()=>{},beforeElementalImport:async data=>{sandbox.buildDirty=false;state.active=JSON.parse(JSON.stringify(data));if(data.reject)throw Error("Unsupported action");}};
 vm.createContext(sandbox);vm.runInContext(transactional,sandbox);
 await assert.rejects(()=>sandbox.applyConfig({classKey:"shaman",reject:true,encounter:{customPriority:["LIGHTNING-BOLT-RANK7"]}}),/Unsupported action/);
 assert.deepEqual(state.active,original);
 assert.equal(sandbox.buildDirty,dirty,"Rejected import retains prior unsaved-change guard");
 const accepted={classKey:"shaman",encounter:{customPriority:["lightning-bolt"]}};
 await sandbox.applyConfig(accepted);assert.deepEqual(state.active,accepted);assert.equal(sandbox.buildDirty,false);
 sandbox.beforeElementalImport=async()=>{throw Error("Server unavailable");};
 await assert.rejects(()=>sandbox.applyConfig({classKey:"shaman",reject:true}),/prior build restoration failed: Server unavailable/);
 }
 console.log("Elemental asynchronous backend rejection restores same-class and cross-class prior builds transactionally.");
})().catch(error=>{console.error(error);process.exitCode=1;});
