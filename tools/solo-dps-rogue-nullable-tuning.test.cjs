"use strict";
// Execute the actual legacy validator without constructing a second implementation.
const source=require("node:fs").readFileSync("WarcraftSim.Api/wwwroot/solo-dps.js","utf8");
const start=source.indexOf("const rogueNumericKeys="),end=source.indexOf("function readRogueSettings()",start);
if(start<0||end<=start)throw Error("Production Rogue validator boundary is missing.");
const {validateRogueSettings,rogueSettingsFor}=new Function("SoloDpsAssumptions",source.slice(start,end)+";return {validateRogueSettings,rogueSettingsFor};")(require("../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js"));
const clone=x=>JSON.parse(JSON.stringify(x)),key="classWideMutilateBonusDamagePerHand";
let checks=0;
function assert(condition,message){checks++;if(!condition)throw Error("Legacy Rogue nullable tuning: "+message);}
function rejects(action,message){let failed=false;try{action();}catch{failed=true;}assert(failed,message);}
const legacyTuning={mainHandWeaponDamage:150,offHandWeaponDamage:100,mainHandSpeedSeconds:1.8,offHandSpeedSeconds:1.4,mutilateBonusDamagePerHand:17.25,improvedExposeArmorFivePointRefund:null};
for(const specialization of ["assassination","combat"]){
 const settings=()=>({...rogueSettingsFor(specialization),targetArmor:2100,targetDodgePercent:4,targetParryPercent:2,rogueMaximumHealth:4700,startingEnergyPercent:65,poisonChargesPerWeapon:75,startInStealth:false,isBehindTarget:true,useExposeArmor:true,mainHandPoison:"deadly",offHandPoison:"wound",tuning:clone(legacyTuning)});
 for(const schemaHasNull of [false,true])for(const inputHasNull of [false,true]){
  const input=settings(),schema=clone(legacyTuning);if(schemaHasNull)schema[key]=null;if(inputHasNull)input.tuning[key]=null;
  const before=JSON.stringify(input),schemaBefore=JSON.stringify(schema);
  assert(validateRogueSettings(input,schema,specialization)===input,"old/current defaults and exports accepted in both directions");
  assert(JSON.stringify(input)===before,"validation never inserts or drops tuning fields");
  assert(JSON.stringify(schema)===schemaBefore,"authoritative schema remains unchanged");
 }
 for(const invalid of [0,79,-1,"79",true,false,{},[],undefined,NaN,Infinity]){
  const input=settings();input.tuning[key]=invalid;
  rejects(()=>validateRogueSettings(input,{...legacyTuning,[key]:null},specialization),"non-null class-wide override rejected");
  assert(Object.hasOwn(input.tuning,key)&&Object.is(input.tuning[key],invalid),"rejection does not remove or coerce the submitted override");
 }
 for(const mutate of [x=>x.tuning.unknown=1,x=>x.tuning.unknown=null,x=>delete x.tuning.mutilateBonusDamagePerHand,x=>delete x.tuning.improvedExposeArmorFivePointRefund,x=>x.tuning.mainHandSpeedSeconds=0,x=>x.tuning.mutilateBonusDamagePerHand="17.25",x=>x.tuning.improvedExposeArmorFivePointRefund=6,x=>x.mainHandWeaponType=specialization==="combat"?"dagger":"sword"]){
  const input=settings();mutate(input);rejects(()=>validateRogueSettings(input,{...legacyTuning,[key]:null},specialization),"other schema, value and weapon gates remain enforced");
 }
 for(const refund of [null,0,1,2,5]){
  const input=settings();input.tuning.improvedExposeArmorFivePointRefund=refund;input.tuning[key]=null;
  assert(validateRogueSettings(input,{...legacyTuning,[key]:null},specialization)===input,"legacy explicit refund values remain supported");
 }
}
rejects(()=>validateRogueSettings({},legacyTuning,"subtlety"),"legacy adapter does not absorb the separate Subtlety workflow");
console.log("Legacy Rogue inactive tuning compatibility passed: "+checks+" pure JavaScript assertions; old/current schemas, Assassination/Combat, strict unsupported values and unchanged inputs.");
