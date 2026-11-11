'use strict';
// Execute the production request/export/import/report code. This harness only
// supplies DOM values and API boundaries; it never reimplements combat rules.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
const plain=x=>JSON.parse(JSON.stringify(x));
const part=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const identities=part('const specMap=','const slots=');
const helpers=part('const enhancementNumericKeys=','function resetSelections()');
const bodies=[part('function body(){','const enhancementNumericKeys='),
 part('function readWarriorAssumptions(){','function restoreWarriorAssumptions()'),
 part('const legacyBody=body;','const legacyLoadPreset='),
 part('const originalSoloBody=body;','const originalSoloSave='),
 part('const previousConditionalBody=body;','const previousConditionalDraw='),
 part('const baseAddBody=body;','const priorOrGroupDraw=')].join('\n');
const exportCode=[part('function saveConfig(){','async function applyConfig(data){'),
 part('const legacySave=saveConfig;','const legacyApplyConfig='),
 part('const originalSoloSave=saveConfig;','const originalSoloApply='),
 part('const beforeElementalSave=saveConfig;','const beforeElementalPreset=')].join('\n');
const gate=part('function validateRestrictedCasterImport(data){','const beforeElementalImport=');
const transaction=part('applyConfig=async function(data){\n validateRestrictedCasterImport(data);','const beforeElementalSave=');
const preset=JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/shaman-enhancement.json'));
assert.equal(preset.totalRankedPoints,36);assert.equal(Object.keys(preset.ranks).length,12);
assert.equal(Object.values(preset.ranks).reduce((a,b)=>a+b,0),36);
assert.match(preset.provenance.statement,/15 unspent/);assert.match(preset.provenance.statement,/not current-client certified/);
assert.equal(preset.provenance.sourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');
assert.equal(preset.provenance.sourceBlob,'ca93c9b67fc585c5aed52eabb3a90d6048fce25a');
const settings={targetArmor:2100,maximumHealth:4900,weaponMinDamage:180,weaponMaxDamage:240,weaponSpeedSeconds:2.6,
 offHandWeaponMinDamage:100,offHandWeaponMaxDamage:145,offHandWeaponSpeedSeconds:1.8,incomingBossHitDamage:0,incomingBossSwingSeconds:4,
 dualWield:true,useStormstrike:true,useRageOfTheFarseer:true,useEarthShock:true,useMaelstromLightningBolt:true,useLightningBoltFiller:false,
 useWindfuryTotem:false,deployWindfuryTotem:false,useLightningShield:false,mainHandImbue:'rockbiter',offHandImbue:'flametongue'};
function environment(cls='shaman',spec='enhancement'){
 const values={classKey:cls,spec,race:'orc',duration:'30',seed:'42',iterations:'1',targetHealth:'200000',rotation:'adaptive',hunterAspect:'hawk',hunterDelay:'0',warriorStance:'battle',warriorRage:'0',warriorArmor:'3500',warriorWeaponMode:'two-hand',warriorWeaponMin:'160',warriorWeaponMax:'250',warriorWeaponSpeed:'3.5',warriorBlockChance:'5',warriorBlockValue:'60',shadowTargetInitialHealth:'65',shadowPriestHealth:'4000'};
 const elements=new Proxy({},{get:(o,key)=>o[key]??(o[key]={value:values[key]??'',checked:['provisional','timeline'].includes(key)})});
 for(const [key,value] of Object.entries(settings))elements['enhancement'+key[0].toUpperCase()+key.slice(1)][typeof value==='boolean'?'checked':'value']=value;
 if(cls==='warrior'&&spec==='protection'){for(const key of ['warriorTank','warriorIncoming','warriorManualShield'])elements[key].checked=true;elements.warriorWeaponMode.value='one-hand';}
 const enhancement=cls==='shaman'&&spec==='enhancement';
 const request={talents:enhancement?{elementalPointsSpent:0,enhancementPointsSpent:36,restorationPointsSpent:0,ranks:plain(preset.ranks)}:{ranks:{}},combatItems:{majorManaPotionCharges:0,majorHealingPotionCharges:0},petEngagementDelaySeconds:0};
 if(enhancement){delete request.petEngagementDelaySeconds;Object.assign(request,settings);}
 if(cls==='priest'||cls==='rogue')delete request.combatItems;
 if(cls==='rogue'){delete request.petEngagementDelaySeconds;Object.assign(request,{talents:{assassinationPointsSpent:0,combatPointsSpent:0,subtletyPointsSpent:0,ranks:{}},targetArmor:0,targetDodgePercent:0,targetParryPercent:0,rogueMaximumHealth:4000,startingEnergyPercent:100,poisonChargesPerWeapon:100,startInStealth:true,isBehindTarget:true,useExposeArmor:false,mainHandPoison:'instant',offHandPoison:'instant',tuning:{mainHandWeaponDamage:125,offHandWeaponDamage:80,mainHandSpeedSeconds:2.6,offHandSpeedSeconds:1.8}});}
 const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}};
 const sandbox={$:id=>elements[id],request,loadedClass:cls,loadedSpec:spec,clone:plain,itemizedMode:true,talentCatalog:Object.keys(preset.ranks).map(key=>({key,modeled:true})),cfg,priorityRows:[],soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},
 validateRemovedPriorityTargets:()=>{},validMode:()=>true,mods:()=>plain(cfg.effects),hunterSurvivalSelection:()=>({}),
 buildCharacter:()=>({classKey:cls,specializationKey:spec,level:60,race:'orc',equipment:{items:[]}}),SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')};
 vm.createContext(sandbox);vm.runInContext([identities,helpers,gate,bodies,exportCode].join('\n'),sandbox);
 if(cls==='rogue')vm.runInContext("Object.assign(request,rogueFixedSettings);restoreRogueSettings(Object.fromEntries([...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueFixedSettings),'tuning'].map(key=>[key,request[key]])));",sandbox);
 return {sandbox,elements};
}
let checked=0;const index=environment();
const specs=vm.runInContext('specMap',index.sandbox);
for(const [cls,options] of Object.entries(specs))for(const spec of options.filter(value=>value!=='feral')){
 const {sandbox:c}=environment(cls,spec),before=JSON.stringify(c.request),built=plain(c.body());
 const key=vm.runInContext(`typedKey(${JSON.stringify(cls)},${JSON.stringify(spec)})`,c),typed=built[key];
 assert.equal(built.classKey,cls);assert.equal(built.specialization,spec);assert.equal(typed.seed,42);assert.equal(typed.durationSeconds,30);assert.equal(JSON.stringify(c.request),before,'request generation must not mutate defaults');
 if(key==='enhancement'){
  for(const field of ['externalModifiers','rotationPolicy','customPriority','priorityConditions','soloEncounter','majorManaPotionCharges','targetCount','useRacialActive'])assert.equal(Object.hasOwn(typed,field),false,'No phantom Enhancement '+field);
  for(const [field,value] of Object.entries(settings))assert.equal(typed[field],value,field+' survives actual wrapper chain');
  assert.deepEqual(built.itemizedTalentRanks,preset.ranks);assert.equal(typed.character.specializationKey,'enhancement');
  const exported=plain(c.saveConfig());assert.deepEqual(exported.encounter.enhancement,settings);assert.equal(exported.specialization,'enhancement');
  for(const field of ['rotation','customPriority','priorityRows','rotationConditions','classSettings','majorManaPotionCharges','useMajorManaPotion'])assert.equal(Object.hasOwn(exported.encounter,field),false,field+' unavailable in export');
  c.validateRestrictedCasterImport(exported);
 }else if(key==='rogue'){assert.equal(typed.externalModifiers,undefined);assert.equal(typed.mainHandWeaponType,'dagger');}
 else if(key==='shadow')assert.equal(typed.externalModifiers,undefined);
 else assert.deepEqual(typed.externalModifiers,c.cfg.effects,cls+' existing modifier request unchanged');
 checked++;
}
assert.equal(checked,17);
const {sandbox:c}=environment(),valid=plain(c.saveConfig());let rejected=0;
for(const mutate of [x=>x.encounter.enhancement=null,x=>x.encounter.enhancement.extra=1,x=>x.encounter.enhancement.targetArmor=-1,
 x=>x.encounter.enhancement.maximumHealth=0,x=>x.encounter.enhancement.weaponMinDamage=241,x=>x.encounter.enhancement.offHandImbue='windfury',
 x=>x.encounter.enhancement.dualWield=false,x=>x.encounter.enhancement.useStormstrike='true',x=>x.encounter.enhancement.weaponSpeedSeconds=Infinity,
 x=>x.gear.items.push({enchantSpellId:1}),x=>x.gear.extra=true,x=>x.modifiers.buffs.push('arcane-intellect'),x=>x.modifiers.unknown=[],
 x=>x.racialActive=true,x=>x.encounter.iterations=2,x=>x.encounter.soloScript.enabled=true,x=>x.encounter.soloScript.addWindows.push({}),
 x=>x.encounter.rotation='adaptive',x=>x.encounter.customPriority=[],x=>x.encounter.priorityRows=[],x=>x.encounter.rotationConditions={},
 x=>x.encounter.combatItems=null,x=>x.preparedCombatConsumables=[{key:'majorManaPotionCharges',charges:1}],x=>x.level60Baseline={}]){
 const bad=plain(valid);mutate(bad);assert.throws(()=>c.validateRestrictedCasterImport(bad));rejected++;
}
// Legal unspent allocations pass frontend gating; tier/rank validation remains server-owned.
const lower=plain(valid);delete lower.talents.ranks['rage-of-the-farseer'];lower.talents.enhancementPointsSpent=35;c.validateRestrictedCasterImport(lower);
assert.equal(51-Object.values(lower.talents.ranks).reduce((a,b)=>a+b),16);
const summaryExpression='({'+[...source.matchAll(/combatSummary:\{([\s\S]*?)\n  \},\n  warnings:/g)][0][1]+'})';
function report(){
 const b=plain(c.body()),data={dps:100,damageDone:3000,maximumMana:2150,maximumHealth:3000,endingMana:1000,endingHealth:3000,damageByAbility:{stormstrike:3000},combatItemUses:{},run:{summary:{abilities:{}}}};
 return {schema:'warcraftsim.solo-dps-report',version:1,build:plain(c.saveConfig()),originalSimulationRequest:b,combatSummary:plain(vm.runInNewContext(summaryExpression,{b,data,clone:plain})),rawServerResponse:{classKey:'shaman',specialization:'enhancement',sim:data}};
}
const {inspectReport,compareReports}=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const a=report();assert.deepEqual(a.combatSummary.targetSemantics,{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:2100,targetCount:1,initialHealthPercent:100});assert.equal(a.combatSummary.maximumHealth,3000);assert.equal(a.originalSimulationRequest.enhancement.maximumHealth,4900);
assert.equal(compareReports(a,a).comparable,true);
const weapon=plain(a);weapon.build.encounter.enhancement.weaponMaxDamage=260;weapon.originalSimulationRequest.enhancement.weaponMaxDamage=260;
const difference=compareReports(a,weapon);assert.equal(difference.comparable,true);assert.ok(difference.changes.some(x=>/weaponMaxDamage/.test(x)));
const armor=plain(a);armor.build.encounter.enhancement.targetArmor=0;armor.originalSimulationRequest.enhancement.targetArmor=0;armor.combatSummary.targetSemantics.targetArmor=0;
assert.equal(compareReports(a,armor).comparable,false);assert.ok(compareReports(a,armor).warnings.some(x=>/armor/i.test(x)));
const altered=plain(a);altered.build.encounter.enhancement.mainHandImbue='windfury';assert.ok(inspectReport(altered).integrityWarnings.some(x=>/mainHandImbue/.test(x)));
const race=plain(a);race.build.race='troll';assert.ok(inspectReport(race).integrityWarnings.some(x=>/Race/.test(x)));
for(const child of ['elemental','shadow','balance']){const invalid=plain(a);invalid.originalSimulationRequest[child]={};assert.throws(()=>inspectReport(invalid));}
(async()=>{
 for(const priorClass of ['shaman','mage','druid','priest'])for(const dirty of [true,false]){
  const original={...plain(valid),classKey:priorClass},state={active:plain(original)};
  const sandbox={isFeral:()=>false,isRogue:()=>false,buildDirty:dirty,request:{},loadedClass:priorClass,clone:plain,saveConfig:()=>state.active,validateRestrictedCasterImport:()=>{},beforeElementalImport:async data=>{sandbox.buildDirty=false;state.active=plain(data);if(data.reject)throw Error('Unknown canonical Enhancement rank');}};
  vm.createContext(sandbox);vm.runInContext(transaction,sandbox);
  await assert.rejects(()=>sandbox.applyConfig({...plain(valid),reject:true}),/Unknown canonical/);
  assert.deepEqual(state.active,original);assert.equal(sandbox.buildDirty,dirty);
 }
 console.log(`Enhancement executable contracts passed: all ${checked} request routes, ${rejected} strict import rejections, actual exports/target summaries, legal35 ranks, same/cross-class dirty rollback and weapon-vs-target comparison.`);
})().catch(error=>{console.error(error);process.exitCode=1;});

// Actual nested-import validation uses the schema of the real defaults response.
const stock={majorManaPotionCharges:1,majorHealingPotionCharges:0,disabledItemKeys:[],restorationPolicy:'conservative-no-overcap',allowEmergencyHealingOvercap:false};
c.validateEnhancementCombatItems(stock,stock);
for(const change of [x=>x.enabled=false,x=>x.majorManaPotionCharges='1',x=>x.majorManaPotionCharges=.5,x=>x.disabledItemKeys=null,x=>x.disabledItemKeys=[1],x=>x.allowEmergencyHealingOvercap='false']){const bad=plain(stock);change(bad);assert.throws(()=>c.validateEnhancementCombatItems(bad,stock));}
(async()=>{
 let release,calls=0;const hold=new Promise(resolve=>release=resolve),controls=[{disabled:false},{disabled:true}];
 const locked={setBuildPreparing:()=>{},buildPreparationPending:false,requirePreparedBuild:()=>{},document:{querySelectorAll:()=>controls},simulate:async()=>{calls++;await hold;return 'done';}};
 vm.createContext(locked);vm.runInContext(part('let simulationInFlight=false;','function safe(task)'),locked);
 const first=locked.simulate();assert.deepEqual(controls.map(x=>x.disabled),[true,true]);await locked.simulate();assert.equal(calls,1,'Repeated click cannot start another validation or simulation');
 release();await first;assert.deepEqual(controls.map(x=>x.disabled),[false,true]);
 const renderControls=[{disabled:false},{disabled:true}];
 const rendering={setBuildPreparing:()=>{},buildPreparationPending:false,requirePreparedBuild:()=>{},document:{querySelectorAll:()=>renderControls},simulate:async()=>{renderControls[1].disabled=false;}};
 vm.createContext(rendering);vm.runInContext(part('let simulationInFlight=false;','function safe(task)'),rendering);await rendering.simulate();assert.deepEqual(renderControls.map(x=>x.disabled),[false,false],'First successful report retains export/replay availability');
 // Execute the real async pipeline through its request identity checks. Both
 // preflight and in-flight edits fail before report assignment/publication.
 for(const phase of ['preflight','in-flight']){
  let release,entered;const pending=new Promise(r=>release=r),started=new Promise(r=>entered=r);const current=report(),prior={sentinel:'prior report'};
  const state={body:()=>plain(current.originalSimulationRequest),saveConfig:()=>plain(current.build),request:{talents:plain(current.build.talents)},talentCatalog:[],itemizedMode:true,generation:1,clone:plain,
   $:id=>({value:id==='iterations'?'1':'',disabled:false}),isRestrictedCaster:()=>true,restrictedCasterName:()=> 'Enhancement',
   validateTalentSelection:async()=>{if(phase==='preflight'){entered();await pending;}},validateGear:async()=>({}),preview:async()=>({}),api:async()=>({}),note:()=>{},results:prior,
   post:async()=>{entered();await pending;return {sim:{}};}};
  vm.createContext(state);vm.runInContext(part('async function simulate(){','function replaySignature('),state);
  const run=state.simulate();await started;current.build.encounter.enhancement.targetArmor=0;current.originalSimulationRequest.enhancement.targetArmor=0;release();
  await assert.rejects(()=>run,/Build changed while simulation was pending/);assert.equal(state.results,prior,'Prior valid report retained; no mismatched publication');
 }
 console.log('Enhancement nested inventory types, delayed validation/execution edits and repeated-click locks passed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
(async()=>{
 const {sandbox:c,elements}=environment();let entered,release;
 const barrier=new Promise(resolve=>entered=resolve),pending=new Promise(resolve=>release=resolve);
 Object.assign(c,{generation:1,presetLoadId:0,buildDirty:false,baselineAvailable:false,priorityRows:[],classCapabilities:[],note:()=>{},
  augmentItemizedCatalog:async()=>{},recalculateTalentTotals:()=>{},validateTalentSelection:async()=>{},renderTalents:()=>{},renderMods:()=>{},updateIdentity:()=>{},drawPriorityRows:()=>{},renderPriorityConditions:()=>{},queueStatRefresh:()=>{},human:x=>x,
  api:async route=>route.includes('defaults')?plain({seed:42,durationSeconds:30,targetMaximumHealth:200000,captureTimeline:true,talents:{ranks:{}},combatItems:{majorManaPotionCharges:0,majorHealingPotionCharges:0},...settings}):route.includes('default-itemized')?plain(preset):route.includes('/capabilities/')?{executesThroughCurrentUnifiedEndpoint:true,hasRotationPreview:true,encounterControls:[],knownRestrictions:[]}: {talents:Object.keys(preset.ranks).map(key=>({key,tree:'enhancement',modeled:true}))},
  checkBaseline:async()=>{entered();await pending;}});
 for(const id of ['scriptedBossEnabled'])elements[id].closest=()=>({hidden:false});
 c.cfg.effects.buffs=['arcane-intellect'];
 vm.runInContext(part('async function loadPreset(token=generation){','async function augmentItemizedCatalog()')+'\n'+part('const beforeElementalPreset=loadPreset;',"$('shadowPreviewRotation').addEventListener"),c);
 const loading=c.loadPreset();await barrier;
 assert.equal(elements.enhancementWeaponMaxDamage.value,settings.weaponMaxDamage);
 elements.enhancementWeaponMaxDamage.value=321;c.buildDirty=true;release();await loading;
 assert.equal(elements.enhancementWeaponMaxDamage.value,321,'Late baseline/source wrappers must not overwrite an edit made after initial load');
 assert.deepEqual(plain(c.cfg.effects),{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},'Confirmed Enhancement preset transition clears unsupported effects');
 console.log('Enhancement actual delayed preset wrappers preserve newer hand settings and clear unsupported transition effects.');
})().catch(error=>{console.error(error);process.exitCode=1;});
