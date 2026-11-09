"use strict";
// Frontend input guards only. Combat and catalog resolution remain server-owned.
(function(root,factory){
 const api=factory();
 if(typeof module==='object'&&module.exports)module.exports=api;
 else root.SoloDpsAssumptions=api;
})(typeof globalThis==='object'?globalThis:this,()=>{
 function validateWarrior(request,equipment){
  const hands=(equipment?.items||[]).filter(x=>['main-hand','off-hand'].includes(x.slotKey));
  if(request.shieldEquipped&&hands.length)
   throw Error('Manual shield assumptions require empty main-hand and off-hand equipment slots. Unequip those items or turn off the manual shield; catalog equipment remains authoritative.');
  if(request.shieldEquipped&&(request.isTwoHanded||request.dualWield))
   throw Error('A manual shield requires the one-handed, no off-hand weapon configuration in Encounter.');
  if(request.tankMode&&!request.enableBossAutoAttacks)
   throw Error('Tank mode requires Boss auto-attacks in Encounter.');
  if(request.preset==='protection'&&(!request.tankMode||!request.enableBossAutoAttacks))
   throw Error('Protection setup required: open Encounter and enable tank mode and Boss auto-attacks, or explicitly choose the labelled manual Protection assumptions.');
  if(request.preset==='protection'&&!request.shieldEquipped&&!hands.some(x=>x.slotKey==='main-hand'))
   throw Error('Protection requires a shield. Open Encounter to explicitly opt into the labelled manual shield and one-handed weapon assumptions.');
  for(const key of ['mainHandMinimumDamage','mainHandMaximumDamage','mainHandSpeedSeconds','playerBlockPercent','playerBlockValue'])
   if(!Number.isFinite(request[key]))throw Error('Manual weapon and block assumptions must be finite numbers.');
  if(request.mainHandMinimumDamage<0||request.mainHandMaximumDamage<request.mainHandMinimumDamage||request.mainHandSpeedSeconds<=0||request.playerBlockPercent<0||request.playerBlockPercent>100||request.playerBlockValue<0)
   throw Error('Check manual weapon damage, swing time, block chance (0-100%) and nonnegative block value.');
  return request;
 }
 function validateWarlock(request){
  if(request.enablePet&&['infernal','doomguard'].includes(request.petType)){
   if(request.allowTemporaryDemon!==true)
    throw Error('Infernal and Doomguard require the explicit temporary-demon simulation assumption in Encounter > Warlock health & demon.');
   if(request.petType==='doomguard'&&request.allowDoomguardRitual!==true)
    throw Error('Doomguard also requires the explicit ritual-prerequisites simulation assumption in Encounter > Warlock health & demon.');
  }
  return request;
 }
 return {validateWarrior,validateWarlock};
});
