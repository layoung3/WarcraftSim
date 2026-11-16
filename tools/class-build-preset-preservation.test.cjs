"use strict";
// This pure JavaScript regression module needs no browser, filesystem or timers.
// Node loads it in CI; it can also execute in a V8 module harness.
const C=require("../WarcraftSim.Api/wwwroot/class-build-contract.js");
const fixtures=[
 require("../docs/acceptance/cp175/cp175-classwide-shaman-elemental-restoration.json"),
 require("../docs/acceptance/cp175/cp175-classwide-rogue-assassination.json")
];
let checks=0;
function assert(condition,message){checks++;if(!condition)throw Error("Talent-only preservation: "+message);}
function equal(a,b,message){assert(JSON.stringify(a)===JSON.stringify(b),message);}
function freeze(value){if(value&&typeof value==="object"){Object.values(value).forEach(freeze);Object.freeze(value);}return value;}
function rejects(action,message){let failed=false;try{action();}catch{failed=true;}assert(failed,message);}
for(const fixture of fixtures){
 for(const optional of ["omitted","null","selected"]){
  const original=C.clone(fixture),item=original.character.equipment.items[0];
  if(optional==="omitted"){delete item.enchantSpellId;delete item.temporaryEnchantSpellId;}
  else {item.enchantSpellId=optional==="selected"?20011:null;item.temporaryEnchantSpellId=null;}
  original.character.rotation={mode:"custom",priority:[original.character.classKey==="shaman"?"lightning-bolt":"shank"]};
  const validated=C.clone(original);
  for(const gear of validated.character.equipment.items){gear.enchantSpellId??=null;gear.temporaryEnchantSpellId??=null;}
  validated.character.itemizedTalents={};validated.presetOrigin="reviewed-empty-rank-witness";
  // A future server default must not overwrite any other editor field either.
  validated.character.encounter.seed+=1;validated.character.rotation={mode:"automatic",priority:[]};
  const before=JSON.stringify(original),serverBefore=JSON.stringify(validated);freeze(original);freeze(validated);
  const applied=C.applyTalentPreset(original,validated),expected=C.clone(original);
  expected.character.itemizedTalents={};expected.presetOrigin=validated.presetOrigin;
  equal(applied,expected,original.character.classKey+" "+optional+" exact non-talent preservation");
  assert(JSON.stringify(original)===before,"original input remains unchanged");
  assert(JSON.stringify(validated)===serverBefore,"validated input remains unchanged");
  equal(applied.character.rotation,original.character.rotation,"custom priority remains unchanged");
  assert(Object.hasOwn(applied.character.equipment.items[0],"temporaryEnchantSpellId")===Object.hasOwn(item,"temporaryEnchantSpellId"),"omitted/null distinction survives");
  applied.character.itemizedTalents.test=1;applied.character.equipment.items[0].name="independent copy";
  assert(JSON.stringify(original)===before&&JSON.stringify(validated)===serverBefore,"output does not alias either input");
  const restored=C.applyTalentPreset(expected,{...C.clone(original),presetOrigin:"reviewed-restore"});
  equal(restored.character.itemizedTalents,original.character.itemizedTalents,"rank removal and restoration are reversible");
  equal(restored.character.equipment,original.character.equipment,"restoration preserves equipment");
 }
 const original=C.clone(fixture),different=C.clone(fixtures.find(x=>x.character.classKey!==original.character.classKey));
 rejects(()=>C.applyTalentPreset(original,different),"cross-class response rejected");
 rejects(()=>C.applyTalentPreset(original,{...original,presetOrigin:null}),"missing provenance rejected");
 const malformed=C.clone(original);malformed.character.itemizedTalents=null;
 rejects(()=>C.applyTalentPreset(original,malformed),"malformed rank map rejected");
}

for(const item of [C.createEquipmentItem("wrist",19830),C.createEquipmentItem("chest",null,"class-build-manual-budget","Manual stat budget")]){
 const canonical=C.clone(item);canonical.itemId??=null;canonical.enchantSpellId??=null;canonical.temporaryEnchantSpellId??=null;
 equal(item,canonical,"new editor equipment already has canonical nullable fields");
 assert(Object.hasOwn(item,"itemId")&&Object.hasOwn(item,"enchantSpellId")&&Object.hasOwn(item,"temporaryEnchantSpellId"),"catalog and manual equipment declare nullable wire fields");
 const build=C.clone(fixtures[0]);build.character.equipment.items=[item];
 equal(C.requireRenderableBuild(build),build,"canonical item remains renderable");
 const after=C.applyTalentPreset(build,{...C.clone(build),presetOrigin:"reviewed-canonical"});
 equal(after.character.equipment,build.character.equipment,"canonical item survives talent-only apply");
}
const one=C.createEquipmentItem("wrist",19830),two=C.createEquipmentItem("wrist",19830);
one.stats.values.stamina=9;one.modifiers.push({key:"test"});
equal(two.stats.values,{},"new equipment stats are isolated");equal(two.modifiers,[],"new equipment modifier arrays are isolated");


for(const temporary of [false,true]){
 const item=C.createEquipmentItem("wrist",19830),before=C.clone(item),key=temporary?"temporaryEnchantSpellId":"enchantSpellId";
 C.setEquipmentEnchant(item,temporary,"20011");assert(item[key]===20011,"enchant selection is numeric");
 const selected=C.clone(item);rejects(()=>C.setEquipmentEnchant(item,temporary,"invalid"),"invalid enchant rejected");equal(item,selected,"invalid enchant keeps previous selection");
 C.setEquipmentEnchant(item,temporary,"");equal(item,before,"enchant removal restores canonical null without deleting its field");
 C.setEquipmentEnchant(item,temporary,null);equal(item,before,"repeated enchant removal stays reversible");
}

console.log("Talent-only preset preservation passed: "+checks+" pure JavaScript assertions; omitted/null/selected enchants, both classes, isolated ranks, custom priority and rollback.");
