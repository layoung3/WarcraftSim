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
 // Compatibility for the two historical Rogue workflows only. These are wire
 // defaults from RogueSimRequest, ForeverExternalModifierSelection and
 // CombatUseItemsOptions, not a second implementation of combat behavior.
 const own=(value,key)=>Object.prototype.hasOwnProperty.call(value,key);
 const record=value=>value!==null&&typeof value==='object'&&!Array.isArray(value);
 const inactiveTuningKey='classWideMutilateBonusDamagePerHand';
 const requestKeys=Object.freeze(['level60Baseline','useReviewedLevel60Baseline','useProvisionalLevel60Baseline',
  'seed','durationSeconds','captureTimeline','useUtilityAbilities','specialization','targetMaximumHealth',
  'rogueMaximumHealth','startingEnergyPercent','startInStealth','additionalTargets','enableBossAttacks',
  'bossSwingSeconds','bossHitDamage','useExposeArmor','targetArmor','useInstantPoison','mainHandPoison',
  'offHandPoison','poisonChargesPerWeapon','bothWeaponsDaggers','isBehindTarget','mainHandWeaponType',
  'offHandWeaponType','targetDodgePercent','targetParryPercent','talents','tuning','character']);
 const inactiveRequestDefaults={
  useClassWideTalents:false,customPriority:null,bossAttacksUseWeapon:false,
  // Saved-build modifiers retain their separate historical "maintained" mode.
  modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'initial-only'},
  combatItems:{restorationPolicy:'conservative-no-overcap',disabledItemKeys:[],allowEmergencyHealingOvercap:false,
   manaRubyCharges:0,manaCitrineCharges:0,manaJadeCharges:0,manaAgateCharges:0,manaRubyBelowManaPercent:65,
   majorManaPotionCharges:0,superiorManaPotionCharges:0,superiorManaPotionBelowManaPercent:55,
   superiorHealingPotionCharges:0,superiorHealingPotionBelowHealthPercent:55,majorManaPotionBelowManaPercent:55,
   minorHealthstoneCharges:0,lesserHealthstoneCharges:0,healthstoneCharges:0,greaterHealthstoneCharges:0,
   majorHealthstoneCharges:0,healthstoneBelowHealthPercent:75,darkRuneCharges:0,demonicRuneCharges:0,
   runeBelowManaPercent:40,minimumHealthToUseRune:1200,mightyRagePotionCharges:0,majorHealingPotionCharges:0,
   majorHealingPotionBelowHealthPercent:55,mightyRageBelowRage:35,engineeringSkill:0,denseDynamiteCharges:0,
   goblinSapperCharges:0,firstExplosiveUseSeconds:0,minimumHealthToUseSapper:1000}
 };
 function exactDefault(value,expected){
  if(Array.isArray(expected))return Array.isArray(value)&&value.length===expected.length&&
   Object.keys(value).length===expected.length&&expected.every((item,index)=>exactDefault(value[index],item));
  if(record(expected))return record(value)&&Object.keys(value).length===Object.keys(expected).length&&
   Object.keys(expected).every(key=>own(value,key)&&exactDefault(value[key],expected[key]));
  return value===expected;
 }
 function projectLegacyRogueTuning(tuning){
  if(!record(tuning))throw Error('Legacy Rogue explicit tuning is required.');
  if(own(tuning,inactiveTuningKey)&&tuning[inactiveTuningKey]!==null)
   throw Error('Class-wide Mutilate tuning requires the class-wide Rogue workflow.');
  // Validate before projecting. No other missing/null distinction is removed.
  return Object.fromEntries(Object.entries(tuning).filter(([key])=>key!==inactiveTuningKey));
 }
 const legacyTuningKeys=[
  'hackAndSlashExtraAttackPercentPerRank','hackAndSlashDaggerCritPercentPerRank','hackAndSlashFistCritPercentPerRank',
  'hackAndSlashMaceArmorIgnorePercentPerRank','weaponExpertiseAvoidanceReductionPerRank','lightningReflexesDodgePercentPerRank',
  'fleetFootedDodgePercentPerRank','baseParryPercent','deflectionParryPercentPerRank',
  'masterOfSubtletyDamagePercentPerRank','masterOfSubtletyDurationSeconds','quickRecoveryEnergyRefundPercentPerRank',
  'energyPerSecond','mainHandSpeedSeconds','offHandSpeedSeconds',
  'mainHandWeaponDamage','offHandWeaponDamage','sinisterStrikeBonusDamage',
  'backstabBonusDamage','hemorrhageBonusDamage','mutilateBonusDamagePerHand',
  'puncturingWoundsBackstabCritPerRank','puncturingWoundsMutilateCritPerRank','puncturingWoundsExtraPointChancePerRank',
  'ambushBonusDamage','garroteTickDamage','eviscerateDamagePerPoint',
  'ruptureTickDamagePerPoint','poisonDamage','lethalityBonusCriticalDamagePerRank',
  'improvedGarroteDamagePerRank','improvedKidneyShotDamagePerRank','feintThreatReduction',
  'baseDodgePercent','evasionBonusDodgePercent','improvedExposeArmorEnergyDiscountPerRank',
  'improvedExposeArmorFivePointRefund','poisonChargePreservationChancePerRank','instantPoisonChancePercent',
  'armorReductionPerExposeComboPoint','armorDamageReductionConstant','deadlyPoisonDamagePerStack',
  'deadlyPoisonDurationSeconds','deadlyPoisonTickIntervalSeconds','utilityPoisonDurationSeconds',
  'woundPoisonHealingReductionPercent','mindNumbingPoisonCastTimeMultiplier','mainHandHitPercent',
  'offHandHitPercent','physicalCritPercent','sliceAndDiceHastePercent',
  'adrenalineRushDurationSeconds'];
 function validateLegacyRogueReportTuning(value){
  const tuning=projectLegacyRogueTuning(value);
  if(legacyTuningKeys.some(key=>!own(tuning,key))||Object.keys(tuning).some(key=>!legacyTuningKeys.includes(key)))
   throw Error('Legacy Rogue explicit tuning fields are missing or unsupported.');
  for(const [key,value] of Object.entries(tuning)){
   if(key==='improvedExposeArmorFivePointRefund'){
    if(value!==null&&(!Number.isInteger(value)||value<0||value>5))throw Error('Legacy Rogue refund tuning is invalid.');
   }else if(typeof value!=='number'||!Number.isFinite(value)||value<0)
    throw Error('Legacy Rogue tuning '+key+' must be a finite JSON number in range.');
  }
  // Existing Combat report bounds, matching ForeverRogueTuning.Validate.
  const positive=['masterOfSubtletyDurationSeconds','mainHandSpeedSeconds','offHandSpeedSeconds','energyPerSecond','adrenalineRushDurationSeconds','deadlyPoisonDurationSeconds','deadlyPoisonTickIntervalSeconds','armorDamageReductionConstant'];
  for(const key of positive)if(tuning[key]<=0)throw Error('Legacy Rogue tuning '+key+' must be positive.');
  const maximum={hackAndSlashExtraAttackPercentPerRank:20,hackAndSlashMaceArmorIgnorePercentPerRank:20,quickRecoveryEnergyRefundPercentPerRank:50,woundPoisonHealingReductionPercent:100,instantPoisonChancePercent:100,poisonChargePreservationChancePerRank:100,puncturingWoundsBackstabCritPerRank:100,puncturingWoundsMutilateCritPerRank:100,puncturingWoundsExtraPointChancePerRank:100,mainHandHitPercent:100,offHandHitPercent:100,physicalCritPercent:100,baseDodgePercent:100,evasionBonusDodgePercent:100,baseParryPercent:100,deflectionParryPercentPerRank:20};
  for(const [key,max] of Object.entries(maximum))if(tuning[key]>max)throw Error('Legacy Rogue tuning '+key+' exceeds the runner bound.');
  if(tuning.mindNumbingPoisonCastTimeMultiplier<1||tuning.mindNumbingPoisonCastTimeMultiplier>3)throw Error('Legacy Rogue tuning mindNumbingPoisonCastTimeMultiplier must be from 1 through 3.');
  return tuning;
 }
 function validateLegacyRogueRequest(request){
  if(!record(request)||requestKeys.some(key=>!own(request,key))||
   Object.keys(request).some(key=>!requestKeys.includes(key)&&!own(inactiveRequestDefaults,key)))
   throw Error('Legacy Rogue required typed request fields are missing or unsupported.');
  for(const [key,value] of Object.entries(inactiveRequestDefaults))
   if(own(request,key)&&!exactDefault(request[key],value))
    throw Error('Legacy Rogue '+key+' must be absent or its complete inactive default.');
  validateLegacyRogueReportTuning(request.tuning);
  return request;
 }
 const legacyRogue=Object.freeze({
  validateRequest:validateLegacyRogueRequest,
  projectTuning:projectLegacyRogueTuning,
  validateReportTuning:validateLegacyRogueReportTuning,
  wireTuning:tuning=>({...projectLegacyRogueTuning(tuning),[inactiveTuningKey]:null})
 });
 return {validateWarrior,validateWarlock,legacyRogue};
});
