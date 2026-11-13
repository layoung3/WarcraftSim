"use strict";
(function(root){
 const clone=value=>JSON.parse(JSON.stringify(value));
 const supportedClasses=Object.freeze(["shaman","rogue","paladin"]);
 const object=value=>value!==null&&typeof value==="object"&&!Array.isArray(value);
 const paladinSettingTypes=Object.freeze(Object.fromEntries([
  ...["targetInitialHealthPercent","targetCount","maximumHealth","initialHealthPercent","initialManaPercent","manaRegenPerSecond","sealSwitchIntervalSeconds","weaponMinDamage","weaponMaxDamage","weaponSpeedSeconds","manualShieldBlockValue","baseMeleeHitPercent","baseRangedHitPercent","baseSpellHitPercent","criticalChancePercent","incomingBlockChancePercent","incomingCriticalChancePercent","incomingBossHitDamage","incomingBossSwingSeconds","incomingAttackPower","friendlyTargetMaximumHealth","friendlyTargetInitialHealthPercent","friendlyTargetIncomingDamage","friendlyTargetIncomingIntervalSeconds","healBelowHealthPercent","windfuryTotemProcChancePercent"].map(x=>[x,"number"]),
  ...["targetCreatureType","seal","aura","manualWeaponKind","incomingDamageSchool","incomingAttackType","healTarget","oathTarget"].map(x=>[x,"string"]),
  ...["targetControlImmune","useRighteousFury","useAutoAttacks","useManualWeaponInputs","manualShield","useManualCombatAssumptions","friendlyTargetEnabled","useWindfuryTotem"].map(x=>[x,"boolean"]),
  ["secondarySeal","nullable-string"],["utilityScenario","nullable-object"],["sealOfJusticeProcChancePercent","nullable-number"]]));
 function requireSupportedClass(key){if(!supportedClasses.includes(key))throw Error("Choose a supported Shaman, Rogue or Paladin class.");return key;}
 function effectiveBuild(build){const copy=clone(build);delete copy.presetOrigin;delete copy.character.specialization;return copy;}
 function canonicalSimulation(value){
  // Only generated event correlation IDs are renamed, preserving all joins,
  // literal IDs, dictionary keys, timing, resources, damage and other fields.
  const guid=/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i,zero="00000000-0000-0000-0000-000000000000";
  const names=new Map(),literals=new Set();let next=0;
  const generated=p=>p.length===4&&p[0]==="run"&&p[1]==="timeline"&&Number.isInteger(p[2])&&["abilityExecutionId","autoAttackInstanceId","auraInstanceId","absorbInstanceId","forcedTargetInstanceId"].includes(p[3]);
  function collect(x,p=[]){if(Array.isArray(x)){x.forEach((v,i)=>collect(v,[...p,i]));return;}if(x&&typeof x==="object"){for(const[k,v]of Object.entries(x)){if(guid.test(k))literals.add(k);collect(v,[...p,k]);}return;}if(typeof x==="string"&&guid.test(x)&&(!generated(p)||x===zero))literals.add(x);}
  function rename(x){if(!names.has(x)){let name;do{name="00000000-0000-4000-8000-"+(next++).toString(16).padStart(12,"0");}while(literals.has(name));names.set(x,name);}return names.get(x);}
  function visit(x,p=[]){if(Array.isArray(x))return x.map((v,i)=>visit(v,[...p,i]));if(x&&typeof x==="object")return Object.fromEntries(Object.keys(x).sort().map(k=>[k,visit(x[k],[...p,k])]));if(typeof x==="number"&&!Number.isFinite(x))throw Error("Nonfinite simulation value.");if(typeof x==="string"&&guid.test(x)&&x!==zero&&generated(p)&&!literals.has(x))return rename(x);return x;}
  collect(value);return JSON.stringify(visit(value));
 }
 function requireBuild(value){if(!object(value)||value.schema!=="warcraftsim.class-build"||value.version!==1||!object(value.character))throw Error("Choose a version-1 class build.");requireSupportedClass(value.character.classKey);return value;}
 function requireRenderableBuild(value,catalog){
  requireBuild(value);const c=value.character,strings=x=>Array.isArray(x)&&x.every(v=>typeof v==="string");
  if(!object(c.itemizedTalents)||!object(c.equipment)||!Array.isArray(c.equipment.items)||c.equipment.items.some(x=>!object(x)||!object(x.stats)||!object(x.stats.values)||!Array.isArray(x.modifiers))||!object(c.modifiers)||![c.modifiers.buffs,c.modifiers.consumables,c.modifiers.targetDebuffs].every(strings)||!object(c.combatItems)||!object(c.encounter)||!object(c.rotation)||!strings(c.rotation.priority)||!object(c.classSettings))throw Error("The complete build needs valid object and collection fields before it can replace the editor.");
  if(Object.entries(c.itemizedTalents).some(([key,rank])=>!key||!Number.isSafeInteger(rank)||rank<0))throw Error("Talent ranks must be named, nonnegative whole numbers.");
  if(c.classKey==="paladin")for(const[key,value]of Object.entries(c.classSettings)){const type=paladinSettingTypes[key];if(!type||!(type==="nullable-string"?value===null||typeof value==="string":type==="nullable-object"?value===null||object(value):type==="nullable-number"?value===null||typeof value==="number"&&Number.isFinite(value):typeof value===type&&(type!=="number"||Number.isFinite(value))))throw Error("Unsupported or malformed Paladin class setting: "+key+".");}
  if(catalog){if(catalog.classKey!==c.classKey||!Array.isArray(catalog.talents))throw Error("The talent catalog must match the imported class.");const nodes=new Map(catalog.talents.map(x=>[x.key,x]));if(Object.entries(c.itemizedTalents).some(([key,rank])=>!nodes.has(key)||rank>nodes.get(key).maxRank))throw Error("Talent ranks must match the current class catalog and rank caps.");}
  return value;
 }
 function requireReport(value){
  if(!object(value)||value.schema!=="warcraftsim.class-build-report"||value.version!==1)throw Error("Choose a complete version-1 class-build report.");
  requireRenderableBuild(value.build);
  if(typeof value.sourceCommit!=="string"||!value.sourceCommit||typeof value.workflowCheckoutCommit!=="string"||!value.workflowCheckoutCommit||!object(value.validation)||!object(value.preparation)||!Array.isArray(value.coverage)||!object(value.originalSimulationRequest)||value.originalSimulationRequest.character?.classKey!==value.build.character.classKey||!object(value.simulation))throw Error("The report needs matching class, preparation, request, response and source records.");
  const s=value.simulation;
  if(![s.dps,s.damageDone].every(x=>Number.isFinite(x)&&x>=0)||!object(s.damageByAbility)||Object.values(s.damageByAbility).some(x=>!Number.isFinite(x)||x<0)||!object(s.run)||!Array.isArray(s.run.timeline)||s.run.timeline.some(x=>!object(x)||!Number.isInteger(x.type)||x.type<0||x.type>45||!Number.isFinite(x.timeSeconds)||x.timeSeconds<0)||value.build.character.encounter.captureTimeline&&s.run.timeline.length===0)throw Error("The report has malformed combat totals or timeline events.");
  if(value.build.character.classKey==="paladin"){
   if(s.classKey!=="paladin"||![s.healingDone,s.overhealingDone,s.absorptionDone,s.damageTaken,s.blockedDamage].every(x=>Number.isFinite(x)&&x>=0))throw Error("A Paladin report needs the Paladin combat and healing response.");
   if(![s.maximumMana,s.maximumHealth,s.endingMaximumMana,s.endingMaximumHealth].every(x=>Number.isFinite(x)&&x>0)||![s.endingMana,s.endingHealth].every(x=>Number.isFinite(x)&&x>=0)||s.endingMana>s.endingMaximumMana||s.endingHealth>s.endingMaximumHealth)throw Error("Paladin ending resources require explicit positive ending caps and bounded ending values.");
   if(s.maximumMana!==value.preparation.maximumMana||s.maximumHealth!==value.preparation.maximumHealth)throw Error("Paladin initial resource caps must match their prepared values; ending caps are recorded separately.");
  }
  if(s.classKey!==undefined&&s.classKey!==value.build.character.classKey||value.preparation.classKey!==undefined&&value.preparation.classKey!==value.build.character.classKey)throw Error("Report class markers disagree.");
  if(value.preparation.request&&canonicalRequest(value.preparation.request)!==canonicalRequest(value.originalSimulationRequest))throw Error("Report request disagrees with its recorded preparation.");
  if(value.validation.preparation&&canonicalPreparation(value.validation.preparation)!==canonicalPreparation(value.preparation))throw Error("Report preparation records disagree.");
  // Rendering is only a structural gate. The server reconstructs preparation;
  // only a current-engine replay can compare the saved combat results.
  canonicalSimulation(s);return value;
 }
 function canonicalRequest(value){const copy=clone(value);if(object(copy.character))delete copy.character.id;return canonicalSimulation(copy);}
 function canonicalPreparation(value){const copy=clone(value);if(object(copy.request?.character))delete copy.request.character.id;return canonicalSimulation(copy);}
 function compareReplay(saved,current){
  requireReport(saved);requireReport(current);
  const requestMatched=canonicalRequest(saved.originalSimulationRequest)===canonicalRequest(current.originalSimulationRequest),simulationMatched=canonicalSimulation(saved.simulation)===canonicalSimulation(current.simulation);
  return {matched:requestMatched&&simulationMatched,requestMatched,simulationMatched,sourceMatches:saved.sourceCommit===current.sourceCommit,checkoutMatches:saved.workflowCheckoutCommit===current.workflowCheckoutCommit};
 }
 function requireComparisonRequest(value){
  if(!object(value)||Object.keys(value).some(x=>!["baseline","candidate","iterations","validationIterations"].includes(x)))throw Error("Choose a supported class-build comparison request.");
  requireRenderableBuild(value.baseline);requireRenderableBuild(value.candidate);
  if(value.baseline.character.classKey!==value.candidate.character.classKey||value.baseline.character.level!==value.candidate.character.level)throw Error("Compare the same class and level.");
  if(![value.iterations,value.validationIterations].every(x=>Number.isInteger(x)&&x>=2&&x<=40))throw Error("Use 2–40 paired comparison seeds and 2–40 separate validation seeds.");
  const seed=value.baseline.character.encounter.seed;if(!Number.isInteger(seed)||seed<0||seed>2146483604)throw Error("Comparison baseline seed must leave room for both nonnegative seed cohorts.");
  return value;
 }
 function createEquipmentItem(slotKey,itemId=null,itemKey="",name=""){
  return {slotKey,itemKey,itemId,enchantSpellId:null,temporaryEnchantSpellId:null,name,stats:{values:{}},modifiers:[]};
 }
 function setEquipmentEnchant(item,temporary,spellId){
  const value=spellId===""||spellId==null?null:Number(spellId);
  if(value!==null&&(!Number.isSafeInteger(value)||value<=0))throw Error("An enchant needs a positive spell ID.");
  item[temporary?"temporaryEnchantSpellId":"enchantSpellId"]=value;
 }
 function setCombatItemEnabled(inventory,key,enabled){
  if(!["major-mana-potion","major-healing-potion"].includes(key)||typeof enabled!=="boolean")throw Error("Choose a supported inventory enablement.");
  const disabled=inventory.disabledItemKeys??[];if(!Array.isArray(disabled)||disabled.some(x=>typeof x!=="string"))throw Error("Disabled inventory must be a list of item keys.");
  inventory.disabledItemKeys=disabled.filter(x=>x!==key);if(!enabled)inventory.disabledItemKeys.push(key);
 }
 function applyTalentPreset(build,validated){
  requireBuild(build);if(!object(build.character.itemizedTalents))throw Error("A talent preset needs an editable rank map.");
  // The current editor may contain a bad rank while the user repairs a build.
  // A validated preset can replace those ranks without replacing other inputs.
  requireRenderableBuild({...build,character:{...build.character,itemizedTalents:{}}});requireRenderableBuild(validated);
  if(build.character.classKey!==validated.character.classKey)throw Error("A talent preset cannot change character class.");
  if(typeof validated.presetOrigin!=="string"||!validated.presetOrigin)throw Error("A reviewed talent preset needs its provenance key.");
  // The server validates ranks, but DTO serialization may materialize optional
  // null fields. Keep the editor's exact non-talent representation and inputs.
  const copy=clone(build);copy.character.itemizedTalents=clone(validated.character.itemizedTalents);copy.presetOrigin=validated.presetOrigin;return copy;
 }
 const eventNames={0:"Simulation started",1:"Simulation ended",2:"Rotation decision",3:"Cast started",4:"Cast completed",5:"Effect impact",6:"Damage",7:"Healing",8:"Resource change",9:"Aura applied",10:"Aura removed",11:"Aura expired",12:"Periodic tick",13:"Actor died",14:"Phase started",15:"Phase ended",16:"Cast cancelled",17:"Scripted damage",18:"Damage pattern",19:"Damage sequence hit",20:"Threat changed",21:"Forced target applied",22:"Forced target removed",23:"Forced target expiration",24:"Absorb applied",25:"Absorb consumed",26:"Absorb removed",27:"Absorb expiration",28:"Channel started",29:"Channel tick",30:"Channel completed",31:"Channel cancelled",32:"Auto attack started",33:"Auto attack swing",34:"Auto attack stopped",35:"Health changed",36:"Reactive opportunity",37:"Boss ability start",38:"Boss ability impact",39:"Boss ability tick",40:"Boss cast started",41:"Boss cast completed",42:"Interrupt requested",43:"Boss cast interrupted",44:"Equipment activated",45:"Target state changed"};
 const eventCategory=t=>t===6?"damage":t===7?"healing":t===8?"resource":[9,10,11].includes(t)?"aura":[2,3,4,16,28,30,44].includes(t)?"action":"other";
 const api={clone,supportedClasses,requireSupportedClass,effectiveBuild,canonicalSimulation,canonicalRequest,compareReplay,requireBuild,requireRenderableBuild,requireReport,requireComparisonRequest,createEquipmentItem,setEquipmentEnchant,setCombatItemEnabled,applyTalentPreset,eventNames,eventCategory};
 if(typeof module!=="undefined"&&module.exports)module.exports=api;if(root)root.ClassBuildContract=api;
})(typeof window!=="undefined"?window:null);
