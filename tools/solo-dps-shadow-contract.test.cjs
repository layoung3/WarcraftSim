"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm");
const source=fs.readFileSync("WarcraftSim.Api/wwwroot/solo-dps.js","utf8");
const gate=source.slice(source.indexOf("function validateRestrictedCasterImport(data)"),source.indexOf("const beforeElementalImport="));
const ctx={};vm.createContext(ctx);vm.runInContext(gate,ctx);
const build=()=>({classKey:"priest",gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[]},encounter:{duration:30,seed:1,iterations:1,targetHealth:200000,timeline:true,shadowTargetInitialHealth:100,shadowPriestHealth:3500,soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]}}});
ctx.validateRestrictedCasterImport(build());let count=1;
for(const change of [x=>x.racialActive=true,x=>x.gear.items.push({itemId:1}),x=>x.encounter.iterations=2,x=>x.encounter.targetArmor=3500,x=>x.encounter.soloScript.enabled=true,x=>x.modifiers.buffs.push("arcane-intellect"),x=>x.modifiers.consumables.push("flask"),x=>x.modifiers.targetDebuffs.push("curse"),x=>x.encounter.customPriority=[],x=>x.encounter.rotation="adaptive",x=>x.encounter.combatItems={},x=>x.encounter.priorityRows=[],x=>x.encounter.shadowTargetInitialHealth=0,x=>x.encounter.shadowTargetInitialHealth=101,x=>x.encounter.shadowPriestHealth=-1,x=>x.encounter.shadowPriestHealth=Infinity,x=>x.encounter.rotationConditions={},x=>x.preparedCombatConsumables=[{key:"majorManaPotionCharges",charges:1}]]){const b=build();change(b);assert.throws(()=>ctx.validateRestrictedCasterImport(b));count++;}
const preset=JSON.parse(fs.readFileSync("WarcraftSim.Api/wwwroot/itemized-presets/priest-shadow.json"));
const runtime=fs.readFileSync("WarcraftSim.Data/Forever/Priest/ForeverPriestShadowTalentBuild.cs","utf8");
const rankBlock=runtime.slice(runtime.indexOf("public Dictionary<string, int> Ranks"),runtime.indexOf("private static readonly Dictionary"));
const ranks=Object.fromEntries([...rankBlock.matchAll(/\["([^"]+)"\]\s*=\s*(\d+)/g)].map(([,key,value])=>[key,Number(value)]));
assert.deepEqual(preset.ranks,ranks);assert.equal(Object.values(ranks).reduce((a,b)=>a+b),36);
assert.match(preset.provenance.statement,/15 unspent/);assert.equal(preset.ranks.penance,undefined);
assert.ok(source.includes("if(c!=='priest')r.externalModifiers=mods()"));
assert.ok(source.includes("r.targetInitialHealthPercent=Number($('shadowTargetInitialHealth').value)"));
assert.ok(source.includes("r.priestMaximumHealth=Number($('shadowPriestHealth').value)"));
console.log(`Shadow exact partial36 and strict unavailable-control contracts passed: ${count} fixtures.`);
(async()=>{
 const transaction=source.slice(source.indexOf("applyConfig=async function(data){",source.indexOf("const beforeElementalImport=")),source.indexOf("const beforeElementalSave="));
 for(const priorClass of ["priest","druid","shaman","mage"])for(const dirty of [true,false]){
  const original={classKey:priorClass,race:"human",provisional:true,talents:{ranks:{}},encounter:{shadowTargetInitialHealth:65,shadowPriestHealth:4000}},state={active:structuredClone(original)};
  const sandbox={buildDirty:dirty,request:{},loadedClass:priorClass,clone:structuredClone,saveConfig:()=>state.active,validateRestrictedCasterImport:()=>{},beforeElementalImport:async data=>{sandbox.buildDirty=false;state.active=structuredClone(data);if(data.reject)throw Error("Invalid Shadow rank");}};
  vm.createContext(sandbox);vm.runInContext(transaction,sandbox);
  await assert.rejects(()=>sandbox.applyConfig({classKey:"priest",reject:true}),/Invalid Shadow rank/);
  assert.deepEqual(state.active,original);assert.equal(sandbox.buildDirty,dirty);
 }
 console.log("Shadow invalid server-ranked imports preserve same/cross-class health and dirty state.");
})().catch(e=>{console.error(e);process.exitCode=1;});

// Execute actual body generation and all installed body wrappers for every admitted route.
const fragment=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const bodyCode=[fragment("function body(){","function resetSelections()"),
 fragment("function readWarriorAssumptions(){","function restoreWarriorAssumptions()"),
 fragment("const legacyBody=body;","const legacyLoadPreset="),
 fragment("const originalSoloBody=body;","const originalSoloSave="),
 fragment("const previousConditionalBody=body;","const previousConditionalDraw="),
 fragment("const baseAddBody=body;","const priorOrGroupDraw=")].join("\n");
const specs={mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery'],warrior:['arms','fury','protection'],shaman:['elemental'],druid:['balance'],priest:['shadow']};
let generated=0;
for(const [cls,specsForClass] of Object.entries(specs))for(const spec of specsForClass){
 const values={classKey:cls,spec,race:'orc',duration:'30',seed:'42',targetHealth:'200000',rotation:'adaptive',hunterAspect:'hawk',hunterDelay:'0',warriorStance:'battle',warriorRage:'0',warriorArmor:'3500',warriorWeaponMode:'two-hand',warriorWeaponMin:'160',warriorWeaponMax:'250',warriorWeaponSpeed:'3.5',warriorBlockChance:'5',warriorBlockValue:'60',shadowTargetInitialHealth:'65',shadowPriestHealth:'4000'};
 const elements=new Proxy({},{get:(o,key)=>o[key]??(o[key]={value:values[key]??'',checked:['provisional','timeline'].includes(key)})});
 if(cls==='warrior'&&spec==='protection'){for(const key of ['warriorTank','warriorIncoming','warriorManualShield'])elements[key].checked=true;elements.warriorWeaponMode.value='one-hand';}
 const typedKey=(c,s)=>c==='priest'?'shadow':c==='druid'?'balance':c==='shaman'?'elemental':c==='hunter'?({'beast-mastery':'beastMastery',marksmanship:'marksmanship',survival:'survival'}[s]):c;
 const request={talents:{ranks:{}},combatItems:{majorManaPotionCharges:0},petEngagementDelaySeconds:0};
 if(cls==='priest')delete request.combatItems;
 const before=JSON.stringify(request),effects={buffs:[],consumables:[],targetDebuffs:[]};
 const sandbox={$:id=>elements[id],request,loadedClass:cls,loadedSpec:spec,clone:structuredClone,typedKey,itemizedMode:true,talentCatalog:[],cfg:{equipment:{items:[]}},priorityRows:[],soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},validateRemovedPriorityTargets:()=>{},validMode:()=>true,mods:()=>structuredClone(effects),saveConfig:()=>({}),validateRestrictedCasterImport:()=>{},buildCharacter:()=>({classKey:cls,specializationKey:spec,level:60,race:'orc',equipment:{items:[]}}),SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')};
 vm.createContext(sandbox);vm.runInContext(bodyCode,sandbox);const built=sandbox.body(),typed=built[typedKey(cls,spec)];
 assert.equal(built.classKey,cls);assert.equal(built.specialization,spec);assert.equal(typed.durationSeconds,30);assert.equal(typed.seed,42);assert.equal(JSON.stringify(request),before,'Request generation cannot mutate source');
 if(cls==='priest'){
  for(const key of ['externalModifiers','combatItems','rotationPolicy','customPriority','soloEncounter','priorityConditions','majorManaPotionCharges'])assert.equal(typed[key],undefined,key);
  assert.equal(typed.targetInitialHealthPercent,65);assert.equal(typed.priestMaximumHealth,4000);
 }else assert.deepEqual(typed.externalModifiers,effects);
 if(['priest','druid','shaman'].includes(cls)){assert.equal(typed.soloEncounter,undefined);assert.equal(typed.priorityConditions,undefined);}
 generated++;
}
console.log(`Actual request body generation passed across all ${generated} admitted class/spec routes.`);

assert.ok(source.includes("$('rotationSubtitle').textContent=shadow?'Shadow uses a fixed toggle-owned priority"));
assert.ok(source.includes("Custom ordered overrides are unavailable.':'Automatic priorities follow your current talents."));

const html=fs.readFileSync("WarcraftSim.Api/wwwroot/solo-dps.html","utf8");
assert.ok(html.includes('id="shadowHealthPrecedence"'));
assert.ok(html.includes("Supplied observed or historical-proxy naked Health takes precedence"));
assert.ok(source.includes("Supplied observed or historical-proxy naked Health takes precedence over the manual Priest health fallback."));
