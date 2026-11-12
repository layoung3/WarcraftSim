"use strict";
(function(root){
 const clone=value=>JSON.parse(JSON.stringify(value));
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
 function requireBuild(value){if(!value||value.schema!=="warcraftsim.class-build"||value.version!==1||!value.character||!["shaman","rogue"].includes(value.character.classKey))throw Error("Choose a version-1 Shaman or Rogue class build.");return value;}
 function requireRenderableBuild(value){
  requireBuild(value);const c=value.character,object=x=>x!==null&&typeof x==="object"&&!Array.isArray(x),strings=x=>Array.isArray(x)&&x.every(v=>typeof v==="string");
  if(!object(c.itemizedTalents)||!object(c.equipment)||!Array.isArray(c.equipment.items)||c.equipment.items.some(x=>!object(x)||!object(x.stats)||!object(x.stats.values)||!Array.isArray(x.modifiers))||!object(c.modifiers)||![c.modifiers.buffs,c.modifiers.consumables,c.modifiers.targetDebuffs].every(strings)||!object(c.combatItems)||!object(c.encounter)||!object(c.rotation)||!strings(c.rotation.priority)||!object(c.classSettings))throw Error("The complete build needs valid object and collection fields before it can replace the editor.");
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
 function applyTalentPreset(build,validated){
  requireRenderableBuild(build);requireRenderableBuild(validated);
  if(build.character.classKey!==validated.character.classKey)throw Error("A talent preset cannot change character class.");
  if(typeof validated.presetOrigin!=="string"||!validated.presetOrigin)throw Error("A reviewed talent preset needs its provenance key.");
  // The server validates ranks, but DTO serialization may materialize optional
  // null fields. Keep the editor's exact non-talent representation and inputs.
  const copy=clone(build);copy.character.itemizedTalents=clone(validated.character.itemizedTalents);copy.presetOrigin=validated.presetOrigin;return copy;
 }
 const eventNames={0:"Simulation started",1:"Simulation ended",2:"Rotation decision",3:"Cast started",4:"Cast completed",5:"Effect impact",6:"Damage",7:"Healing",8:"Resource change",9:"Aura applied",10:"Aura removed",11:"Aura expired",12:"Periodic tick",13:"Actor died",14:"Phase started",15:"Phase ended",16:"Cast cancelled",17:"Scripted damage",18:"Damage pattern",19:"Damage sequence hit",20:"Threat changed",21:"Forced target applied",22:"Forced target removed",23:"Forced target expiration",24:"Absorb applied",25:"Absorb consumed",26:"Absorb removed",27:"Absorb expiration",28:"Channel started",29:"Channel tick",30:"Channel completed",31:"Channel cancelled",32:"Auto attack started",33:"Auto attack swing",34:"Auto attack stopped",35:"Health changed",36:"Reactive opportunity",37:"Boss ability start",38:"Boss ability impact",39:"Boss ability tick",40:"Boss cast started",41:"Boss cast completed",42:"Interrupt requested",43:"Boss cast interrupted",44:"Equipment activated",45:"Target state changed"};
 const eventCategory=t=>t===6?"damage":t===7?"healing":t===8?"resource":[9,10,11].includes(t)?"aura":[2,3,4,16,28,30,44].includes(t)?"action":"other";
 const api={clone,effectiveBuild,canonicalSimulation,requireBuild,requireRenderableBuild,createEquipmentItem,setEquipmentEnchant,applyTalentPreset,eventNames,eventCategory};
 if(typeof module!=="undefined"&&module.exports)module.exports=api;if(root)root.ClassBuildContract=api;
})(typeof window!=="undefined"?window:null);
