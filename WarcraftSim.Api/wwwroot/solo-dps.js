"use strict";
(() => {
const $=id=>document.getElementById(id), api=async(url,options)=>{
 const response=await fetch(url,options),payload=await response.json();
 if(!response.ok)throw Error(payload.error||payload.Error||JSON.stringify(payload));return payload;
};
const post=(url,body)=>api(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
const clone=value=>JSON.parse(JSON.stringify(value));
const pretty=value=>JSON.stringify(value,null,2);
const option=(value,label)=>new Option(label,value);
const specMap={rogue:['assassination','combat'],priest:['shadow'],druid:['balance','feral'],shaman:['elemental','enhancement'],mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery'],warrior:['arms','fury','protection']};
const typedKey=(c,s)=>c==='priest'?'shadow':c==='druid'?s:c==='shaman'?s:c==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[s]):c;
const isRestrictedCaster=()=>['shaman','druid','priest','rogue'].includes($('classKey').value);
const isFeral=()=>$('classKey').value==='druid'&&$('spec').value==='feral';
const isRogue=()=>$('classKey').value==='rogue';
const isCombatRogue=()=>isRogue()&&$('spec').value==='combat';
const isEnhancement=()=>$('classKey').value==='shaman'&&$('spec').value==='enhancement';
const restrictedCasterName=()=>isRogue()?(isCombatRogue()?'Combat':'Assassination'):$('classKey').value==='priest'?'Shadow':$('classKey').value==='druid'?(isFeral()?'Feral':'Balance'):isEnhancement()?'Enhancement':'Elemental';
const slots=['head','neck','shoulder','back','chest','wrist','hands','waist','legs','feet','finger1','finger2','trinket1','trinket2','main-hand','off-hand','ranged'];
const families=['bat','bear','bird-of-prey','boar','carrion-bird','cat','core-hound','crab','crocolisk','fox','gorilla','hyena','raptor','scorpid','spider','tallstrider','turtle','wind-serpent','wolf'];
let classCapabilities=[],races=[],catalog=[],catalogAll=[],showCatalogAll=false,enchants=[],modifiers=[],talentCatalog=[],request=null,results=null,generation=0,activeSlot=null,pickerPage=1,pickerSelected=null,pickerTimer=null,pickerDetailsLoad=0,priorityRows=[],availableIconKeys=new Set(),presetLoadId=0,loadedClass=null,loadedSpec=null,buildDirty=false,pendingPreset=null,statsRefreshId=0,statsRefreshTimer=null,baselineCheckId=0,baselineAvailable=false,itemizedMode=true,pickerTab='items',pickerCompatible=null,pickerEnchantSelection={permanent:0,temporary:0};
const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}};
let soloScript={enabled:false,startingHealthPercent:100,bossHealthMilestones:[]};
const note=msg=>{$('status').textContent=msg;};
const human=s=>String(s||'').replaceAll('-',' ').replace(/\b\w/g,c=>c.toUpperCase());
function navigate(view){for(const tab of document.querySelectorAll('.tab')){const active=tab.dataset.view===view;tab.classList.toggle('active',active);tab.setAttribute('aria-selected',String(active));tab.tabIndex=active?0:-1;}for(const section of document.querySelectorAll('.view')){const active=section.id==='view-'+view;section.hidden=!active;section.classList.toggle('active',active);} }
function updateIdentity(){const r=selectedRace();$('characterTitle').textContent=human($('spec').value)+' '+human($('classKey').value);$('characterSubtitle').textContent=($('race').selectedOptions?.[0]?.textContent||r?.name||'Choose race')+' · Level 60';}
const qualityClass=q=>'quality-'+String(q||'').toLowerCase();
const showError=e=>note('Action unavailable: '+(e?.message||e));

function slotsFor(item){return item.allowedSlots||[];}
function selectedRace(){return races.find(x=>x.key===$('race').value);}
function buildCharacter(){
 const character={name:'Solo DPS Character',rulesetKey:'wow-forever',classKey:$('classKey').value,specializationKey:$('spec').value,level:60,race:$('race').value,equipment:clone(cfg.equipment)};
 if(isCombatRogue()){
  // Preserve the fetched identity explicitly: model binding must not generate a
  // different Character.Id for preview, execution and complete-response replay.
  if(typeof request?.character?.id!=='string')throw Error('Combat default character identity is unavailable.');
  return {id:request.character.id,...character,baseStats:{values:{}},talents:{selections:[]},buffs:{selections:[]}};
 }
 return character;
}
function mods(){return clone(cfg.effects);}
function validMode(){if(!$('race').value)throw Error('Select an eligible race.');return $('provisional').checked;}
function body(){
 if(!request||loadedClass!==$('classKey').value||loadedSpec!==$('spec').value)throw Error('Selected class and specialization are still loading. Run again after defaults finish.');
 if(isCombatRogue())for(const id of ['duration','seed','iterations','targetHealth'])readCombatNumber(id);
 if(isFeral()){const feral=feralRequest(saveFeralConfig());return {classKey:'druid',specialization:'feral',itemizedTalentMode:true,itemizedTalentRanks:clone(feral.talents.ranks),feral};}
 const c=$('classKey').value,s=$('spec').value,r=clone(request),enhancement=c==='shaman'&&s==='enhancement';
 if(['shaman','druid','priest','rogue'].includes(c)){validateRestrictedCasterImport(saveConfig());if(c!=='priest'&&c!=='rogue'&&!enhancement)r.majorManaPotionCharges=0;}
 if(!['hunter','shaman','druid','priest'].includes(c))r.specialization=s;
 r.useReviewedLevel60Baseline=!validMode();r.useProvisionalLevel60Baseline=validMode();
 if(!['shaman','druid','priest','rogue'].includes(c))r.useRacialActive=$('racialActive').checked;if(c!=='priest'&&c!=='rogue'&&!enhancement)r.externalModifiers=mods();
 r.durationSeconds=Number($('duration').value);r.seed=Number($('seed').value);r.targetMaximumHealth=Number($('targetHealth').value);r.captureTimeline=$('timeline').checked;
 if(!Number.isInteger(r.seed)||r.durationSeconds<=0||r.durationSeconds>1200||r.targetMaximumHealth<=0)throw Error('Check encounter duration, seed and target health.');
 if(c==='hunter'){r.race=$('race').value;r.equipment=clone(cfg.equipment);r.petFamily=$('petFamily').value;if(s==='beast-mastery')r.petEnabled=$('companionEnabled').checked;else r.hasPet=$('companionEnabled').checked;}
 else {r.character=buildCharacter();if(c!=='priest'&&c!=='rogue'&&!enhancement)r.rotationPolicy=$('rotation').value;else if(c==='priest'){r.targetInitialHealthPercent=Number($('shadowTargetInitialHealth').value);r.priestMaximumHealth=Number($('shadowPriestHealth').value);}if(c==='warlock'){r.petType=$('demonType').value;r.enablePet=$('companionEnabled').checked;r.talentAwareAutoRotation=r.rotationPolicy==='adaptive';}}
 if(c==='rogue'){
  Object.assign(r,readRogueSettings());
  // Adapt only the outgoing copy; historical saved tuning stays byte-equivalent.
  r.tuning=SoloDpsAssumptions.legacyRogue.wireTuning(r.tuning);
 }
 if(enhancement){Object.assign(r,readEnhancementSettings());r.useWindfuryWeapon=r.mainHandImbue==='windfury';}
 const property=typedKey(c,s);
 if(itemizedMode)r.talents.ranks=Object.fromEntries(Object.entries(r.talents.ranks||{}).filter(([key])=>
   talentCatalog.some(t=>t.key===key&&t.modeled!==false)));
 return {classKey:c,specialization:s,itemizedTalentMode:itemizedMode,
  ...(itemizedMode?{itemizedTalentRanks:clone(request.talents.ranks||{})}:{}),[property]:r};
}
// Strict historical/manual fixed-Cat contract. This is not the legacy defaults
// DTO: the baseline owns primaries and Health/Mana; AP is a required assumption.
const feralInputMode='historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap';
const feralManualKeys=['preTalentCatAttackPower','weaponDps','meleeHitPercent','meleeCritPercent','shapeshiftMana','shiftingPowerBaseMana','shiftingPowerCostReductionPercent'];
const feralBooleanKeys=['startInProwl','targetBehind','useFaerieFire','useFeralCooldowns'];
const feralScenarioKeys=['role','seed','durationSeconds','captureTimeline','targetLevel','targetInitialHealthPercent','targetMaximumHealth','targetArmor',...feralBooleanKeys,'preferredCatOpener'];
const feralTalentTotals=['balancePointsSpent','feralPointsSpent','restorationPointsSpent','totalPoints','unspentPoints'];
let feralPreviewReceipt=null,feralPreviewIdentity=null,feralCapabilityNotes=[];
const feralControl=key=>'feral'+key[0].toUpperCase()+key.slice(1);
function feralDefaults(){return {
 character:{classKey:'druid',specializationKey:'feral',rulesetKey:'wow-forever',level:60,race:''},
 role:'cat',useProvisionalLevel60Baseline:false,useReviewedLevel60Baseline:false,
 manualInputs:{preTalentCatAttackPower:null,weaponDps:75,meleeHitPercent:88,meleeCritPercent:20,shapeshiftMana:250,shiftingPowerBaseMana:1244,shiftingPowerCostReductionPercent:0},
 talents:{ranks:{},balancePointsSpent:0,feralPointsSpent:0,restorationPointsSpent:0,totalPoints:0,unspentPoints:51},
 seed:144001,durationSeconds:120,captureTimeline:false,targetLevel:63,targetInitialHealthPercent:100,targetMaximumHealth:1000000,targetArmor:0,startInProwl:false,preferredCatOpener:'ravage',targetBehind:true,useFaerieFire:false,useFeralCooldowns:false
};}
function exactFeralObject(value,keys,label){
 if(!value||typeof value!=='object'||Array.isArray(value)||Object.keys(value).some(key=>!keys.includes(key))||keys.some(key=>!Object.prototype.hasOwnProperty.call(value,key)))throw Error('Feral '+label+' requires exactly: '+keys.join(', ')+'.');
}
function validateFeralImport(data){
 exactFeralObject(data,['schema','version','inputMode','itemizedMode','classKey','specialization','race','provisional','manualInputs','scenario','talents'],'build fields');
 if(data.schema!=='warcraftsim.solo-dps-build'||data.version!==1||data.inputMode!==feralInputMode||data.itemizedMode!==true||data.classKey!=='druid'||data.specialization!=='feral'||!['night-elf','tauren','troll'].includes(data.race)||data.provisional!==true)throw Error('Feral requires explicit historical-provisional fixed-Cat identity; reviewed/observed modes and legacy migrations are unavailable.');
 exactFeralObject(data.manualInputs,feralManualKeys,'manual inputs');
 for(const key of feralManualKeys)if(typeof data.manualInputs[key]!=='number'||!Number.isFinite(data.manualInputs[key])||data.manualInputs[key]<0)throw Error('Feral '+key+' must be an explicit finite nonnegative number. Blank AP is not zero.');
 for(const key of ['meleeHitPercent','meleeCritPercent','shiftingPowerCostReductionPercent'])if(data.manualInputs[key]>100)throw Error('Feral '+key+' must be at most 100.');
 const s=data.scenario;exactFeralObject(s,feralScenarioKeys,'scenario');
 for(const key of ['seed','durationSeconds','targetLevel','targetInitialHealthPercent','targetMaximumHealth','targetArmor'])if(typeof s[key]!=='number'||!Number.isFinite(s[key]))throw Error('Feral '+key+' must be a finite JSON number.');
 for(const key of ['captureTimeline',...feralBooleanKeys])if(typeof s[key]!=='boolean')throw Error('Feral '+key+' must be a boolean.');
 if(s.role!=='cat'||s.targetLevel!==63||s.targetInitialHealthPercent!==100||s.targetArmor<0||s.targetMaximumHealth<=0||s.durationSeconds<=0||s.durationSeconds>1200||!Number.isInteger(s.seed)||s.seed<-2147483648||s.seed>2147483647||!['ravage','pounce'].includes(s.preferredCatOpener)||s.startInProwl&&s.preferredCatOpener==='ravage'&&!s.targetBehind)throw Error('Invalid fixed-Cat Feral scenario, position or opener.');
 exactFeralObject(data.talents,['ranks',...feralTalentTotals],'ranked talents and declared totals');
 const ranks=data.talents.ranks;
 if(!ranks||typeof ranks!=='object'||Array.isArray(ranks)||Object.values(ranks).some(rank=>!Number.isInteger(rank)||rank<0)||feralTalentTotals.some(key=>!Number.isInteger(data.talents[key])||data.talents[key]<0))throw Error('Feral ranks and all declared totals must be nonnegative JSON integers.');
 if('furor' in ranks&&![0,5].includes(ranks.furor))throw Error('Furor explicitly supports only rank 0 or 5 (source maximum 5).');
 const total=Object.values(ranks).reduce((a,b)=>a+b,0);
 if(total>51||data.talents.totalPoints!==total||data.talents.unspentPoints!==51-total||data.talents.balancePointsSpent+data.talents.feralPointsSpent+data.talents.restorationPointsSpent!==total)throw Error('Feral declared totals disagree with the exact rank map.');
 return data;
}
function readFeralNumber(id){const raw=$(id).value;if(typeof raw==='string'&&!raw.trim())throw Error('Enter '+(id==='feralPreTalentCatAttackPower'?'manual pre-talent total Cat AP (explicit zero is allowed)':id)+'.');const value=Number(raw);if(!Number.isFinite(value))throw Error('Invalid Feral number: '+id);return value;}
function saveFeralConfig(){
 const scenario={role:'cat',targetLevel:63,targetInitialHealthPercent:100,seed:readFeralNumber('seed'),durationSeconds:readFeralNumber('duration'),targetMaximumHealth:readFeralNumber('targetHealth'),captureTimeline:$('timeline').checked,targetArmor:readFeralNumber('feralTargetArmor'),preferredCatOpener:$('feralPreferredCatOpener').value};
 for(const key of feralBooleanKeys)scenario[key]=$(feralControl(key)).checked;
 if(Number($('iterations').value)!==1||cfg.equipment.items.length||cfg.effects.buffs.length||cfg.effects.consumables.length||cfg.effects.targetDebuffs.length||$('racialActive').checked||priorityRows.length||soloScript.enabled||soloScript.bossHealthMilestones?.length||soloScript.addWindows?.length)throw Error('Feral equipment, modifiers, racials, priorities, scripts and batches are unavailable.');
 return validateFeralImport({schema:'warcraftsim.solo-dps-build',version:1,inputMode:feralInputMode,itemizedMode,classKey:'druid',specialization:'feral',race:$('race').value,provisional:$('provisional').checked,manualInputs:Object.fromEntries(feralManualKeys.map(key=>[key,readFeralNumber(feralControl(key))])),scenario,talents:clone(request.talents)});
}
function feralRequest(saved){
 validateFeralImport(saved);
 return {character:{classKey:'druid',specializationKey:'feral',rulesetKey:'wow-forever',level:60,race:saved.race},useProvisionalLevel60Baseline:true,useReviewedLevel60Baseline:false,level60Baseline:null,manualInputs:clone(saved.manualInputs),talents:clone(saved.talents),...clone(saved.scenario)};
}
function restoreFeralControls(value){
 for(const key of feralManualKeys)$(feralControl(key)).value=value.manualInputs[key]??'';
 for(const key of feralBooleanKeys)$(feralControl(key)).checked=value[key];
 $('feralTargetArmor').value=value.targetArmor;$('feralPreferredCatOpener').value=value.preferredCatOpener;
}
function invalidateFeralPreview(){
 feralPreviewReceipt=null;feralPreviewIdentity=null;
 if(!isFeral())return;
 $('stats').replaceChildren();$('contributions').textContent='';$('statScope').textContent='Enter manual pre-talent Cat AP and explicitly opt into the historical baseline. No current preparation is available.';$('statScope').hidden=false;
 $('feralBaseline').textContent='Historical primary/resource baseline is shown only after valid preparation.';
 $('feralPriorityOrder').textContent='';$('feralRotationEntries').textContent='Waiting for a valid, current preparation. Configured order is not immediate eligibility.';
 $('statStatus').textContent='Preparation required';
 for(const id of ['run','save','exportBuild'])$(id).disabled=true;
}
function feralPreviewMatches(){try{return !!feralPreviewReceipt&&feralPreviewIdentity===JSON.stringify(feralRequest(saveFeralConfig()));}catch{return false;}}
function renderFeralPreparation(response){
 const p=response.preview,b=response.baseline;
 if(response.inputMode!==feralInputMode||!p||!b)throw Error('Incomplete historical Feral preparation receipt.');
 $('stats').replaceChildren();
 for(const [label,value] of [['Manual pre-talent Cat AP',p.authoredAttackPower],['HOTW AP increment',p.heartOfTheWildAttackPowerIncrement],['Predatory Strikes AP',p.predatoryStrikesAttackPower],['Prepared spellbook AP',p.spellbookAttackPower],['Maximum Health (racials omitted)',p.maximumHealth],['Maximum Mana',p.maximumMana],['Initial shift Mana cost',p.initialShiftManaCost],['Post-shift Mana',p.manaAfterInitialShift],['Maximum Energy',p.maximumEnergy],['Starting Energy',p.startingEnergy]]){
  const row=document.createElement('div'),name=document.createElement('span'),number=document.createElement('strong');name.textContent=label;number.textContent=String(value);row.append(name,number);$('stats').append(row);
 }
 $('feralBaseline').textContent=['Historical pre-talent baseline (not an effective character sheet):',...['strength','agility','stamina','intellect','spirit'].map(key=>human(key)+': '+b[key]),'Pre-talent Health: '+b.observedUnbuffedMaximumHealth,'Pre-talent Mana: '+b.observedUnbuffedMaximumMana,'Source: '+b.sourceKind+' · '+b.verificationStatus,'Racial passives omitted, including Tauren Health. Troll is extrapolated.'].join('\n');
 $('statScope').textContent='Fixed Cat, after the mandatory initial shift and before combat. Manual AP already includes ordinary attribute and assumed Cat contributions; only HOTW and Predatory increments are added. Shifting Power can act at t=0 after this boundary.';$('statScope').hidden=false;
 $('contributions').textContent=pretty(response);
 $('feralRotationEntries').textContent=pretty(p.configuredRotation);$('feralPriorityOrder').textContent=(p.configuredRotation.entries||[]).map(entry=>String(entry.priority)+'. '+human(entry.abilityKey)+(entry.isEnabled?'':' (disabled)')).join(' > ');
 $('baselineStatus').textContent='Historical provisional · '+human(b.race)+' · racial passives omitted';$('statStatus').textContent='Up to date';
}
async function previewFeral(refreshToken){
 if(statsRefreshTimer!==null){clearTimeout(statsRefreshTimer);statsRefreshTimer=null;}
 const token=refreshToken??++statsRefreshId,epoch=generation,submitted=feralRequest(saveFeralConfig()),identity=JSON.stringify(submitted);
 let response;try{response=await post('/api/forever/character/preview-feral',submitted);}
 catch(error){let current;try{current=JSON.stringify(feralRequest(saveFeralConfig()));}catch{}if(token!==statsRefreshId||epoch!==generation||!isFeral()||current!==identity)return;throw error;}
 if(token!==statsRefreshId||epoch!==generation||!isFeral())return response;
 let current;try{current=JSON.stringify(feralRequest(saveFeralConfig()));}catch{return response;}
 if(identity!==current)return response;
 if(feralReplaySignature(response.request)!==feralReplaySignature(submitted))throw Error('Feral preparation does not match the current submitted request.');
 renderFeralPreparation(response);feralPreviewReceipt=clone(response);feralPreviewIdentity=identity;setBuildPreparing(buildPreparationPending);
 return response;
}
// Compare complete envelopes, retaining every metric, timing and ordered event.
// Only generated GUID strings are canonically renamed, preserving relationships.
function feralReplaySignature(value){
 const identities=new Map(),guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
 const generated=path=>{
  const p=path[0]==='preparation'?path.slice(1):path;
  if(p.length===3&&p[0]==='preview'&&p[1]==='configuredRotation'&&p[2]==='id')return true;
  const event=path[0]==='sim'?path.slice(1):path;
  return event.length===4&&event[0]==='run'&&event[1]==='timeline'&&typeof event[2]==='number'&&['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'].includes(event[3]);
 };
 const visit=(x,path=[])=>Array.isArray(x)?x.map((item,index)=>visit(item,[...path,index])):x&&typeof x==='object'?Object.fromEntries(Object.keys(x).sort().map(key=>[key,visit(x[key],[...path,key])])):typeof x==='string'&&guid.test(x)&&generated(path)?(identities.has(x)?identities.get(x):(identities.set(x,'00000000-0000-4000-8000-'+String(identities.size).padStart(12,'0')),identities.get(x))):x;
 return JSON.stringify(visit(value));
}

function feralCombatSummary(response){
 const p=response.preparation.preview,sim=response.sim,actor=sim.run.summary.actorSummaries.druid;
 return {actorKey:'druid',petDamage:0,combatItemUses:null,consumableDecisions:null,
  authoredAttackPower:p.authoredAttackPower,heartOfTheWildAttackPowerIncrement:p.heartOfTheWildAttackPowerIncrement,predatoryStrikesAttackPower:p.predatoryStrikesAttackPower,spellbookAttackPower:p.spellbookAttackPower,
  maximumHealth:p.maximumHealth,endingHealth:null,maximumMana:p.maximumMana,startingMana:actor.startingResources.mana,initialShiftManaCost:p.initialShiftManaCost,manaAfterInitialShift:p.manaAfterInitialShift,endingMana:sim.endingMana,
  maximumEnergy:p.maximumEnergy,startingEnergy:p.startingEnergy,endingEnergy:sim.endingEnergy,maximumRage:p.maximumRage,startingRage:p.startingRage,endingRage:sim.endingRage,
  actorResources:{starting:clone(actor.startingResources),ending:clone(actor.endingResources)},configuredRotation:clone(p.configuredRotation),inputTiming:clone(response.inputTiming)};
}
function renderFeralResultMetrics(summary){
 $('metrics').replaceChildren();
 for(const [label,key] of [['DPS','dps'],['Total damage','damageDone'],['Manual pre-talent Cat AP','authoredAttackPower'],['HOTW AP increment','heartOfTheWildAttackPowerIncrement'],['Predatory Strikes AP','predatoryStrikesAttackPower'],['Prepared spellbook AP','spellbookAttackPower'],['Maximum Health (racials omitted)','maximumHealth'],['Maximum Mana','maximumMana'],['Actor starting Mana (before shift)','startingMana'],['Initial shift Mana cost','initialShiftManaCost'],['Post-shift Mana','manaAfterInitialShift'],['Ending Mana','endingMana'],['Maximum Energy','maximumEnergy'],['Starting Energy','startingEnergy'],['Ending Energy','endingEnergy']]){
  const row=document.createElement('div'),name=document.createElement('span'),value=document.createElement('strong');name.textContent=label;value.textContent=String(summary[key]);row.append(name,value);$('metrics').append(row);
 }
 const timing=document.createElement('div');timing.textContent='Actual additional input latency: '+summary.inputTiming.additionalInputLatencyMilliseconds+' ms. Physical damage uses armor; bleeds are unmitigated.';$('metrics').append(timing);
}

const rogueNumericKeys=['targetArmor','targetDodgePercent','targetParryPercent','rogueMaximumHealth','startingEnergyPercent','poisonChargesPerWeapon'];
const rogueBooleanKeys=['startInStealth','isBehindTarget','useExposeArmor'];
const roguePoisonKeys=['mainHandPoison','offHandPoison'];
const rogueWeaponTuningKeys=['mainHandWeaponDamage','offHandWeaponDamage','mainHandSpeedSeconds','offHandSpeedSeconds'];
const rogueFixedSettings={useInstantPoison:true,bothWeaponsDaggers:true,mainHandWeaponType:'dagger',offHandWeaponType:'dagger',useUtilityAbilities:false,additionalTargets:0,enableBossAttacks:false};
const combatRogueFixedSettings={...rogueFixedSettings,bothWeaponsDaggers:false,mainHandWeaponType:'sword',offHandWeaponType:'sword'};
const rogueSettingsFor=spec=>spec==='combat'?combatRogueFixedSettings:rogueFixedSettings;
function validateRogueSettings(settings,tuningSchema,specialization='assassination'){
 const label=specialization==='combat'?'Combat':'Assassination';
 if(!['assassination','combat'].includes(specialization))throw Error('Unsupported canonical Rogue specialization.');
 if(!settings||typeof settings!=='object'||Array.isArray(settings))throw Error(label+' manual settings are required.');
 const allowed=[...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueSettingsFor(specialization)),'tuning'];
 for(const key of Object.keys(settings))if(!allowed.includes(key))throw Error('Unsupported '+label+' setting: '+key);
 for(const key of rogueNumericKeys)if(typeof settings[key]!=='number'||!Number.isFinite(settings[key])||settings[key]<0)throw Error(label+' '+key+' must be a finite nonnegative number.');
 for(const key of rogueBooleanKeys)if(typeof settings[key]!=='boolean')throw Error(label+' '+key+' must be a boolean.');
 for(const [key,value] of Object.entries(rogueSettingsFor(specialization)))if(settings[key]!==value)throw Error('Restricted '+label+' requires '+key+' = '+value+'.');
 if(settings.rogueMaximumHealth<=0||['startingEnergyPercent','targetDodgePercent','targetParryPercent'].some(key=>settings[key]>100)||!Number.isInteger(settings.poisonChargesPerWeapon))throw Error('Invalid '+label+' health, Energy, avoidance or poison charges.');
 for(const key of roguePoisonKeys)if(!['none','instant','deadly','wound','mind-numbing','crippling'].includes(settings[key]))throw Error('Unsupported '+label+' poison coating.');
 const tuning=SoloDpsAssumptions.legacyRogue.projectTuning(settings.tuning);
 if(!tuning||typeof tuning!=='object'||Array.isArray(tuning))throw Error(label+' explicit tuning is required.');
 for(const [key,value] of Object.entries(tuning)){
  if(key==='improvedExposeArmorFivePointRefund'){if(value!==null&&(!Number.isInteger(value)||value<0||value>5))throw Error('Invalid '+label+' legacy refund tuning.');}
  else if(typeof value!=='number'||!Number.isFinite(value)||value<0)throw Error('Invalid '+label+' tuning: '+key);
 }
 for(const key of rogueWeaponTuningKeys)if(typeof tuning[key]!=='number'||key.endsWith('SpeedSeconds')&&tuning[key]<=0)throw Error('Invalid '+label+' weapon tuning: '+key);
 // The current server defaults own the tuning schema; imports may retain explicit
 // legacy values, but cannot invent fields or silently acquire missing defaults.
 // This opt-in class-wide field has no consumer in either legacy workflow.
 // Its absent/null wire forms are equivalent; non-null overrides are rejected above.
 const legacyKeys=object=>Object.keys(SoloDpsAssumptions.legacyRogue.projectTuning(object));
 if(tuningSchema&&(legacyKeys(tuning).some(key=>!Object.prototype.hasOwnProperty.call(tuningSchema,key))||legacyKeys(tuningSchema).some(key=>!Object.prototype.hasOwnProperty.call(tuning,key))))throw Error(label+' tuning fields must exactly match the current runner schema.');
 return settings;
}
function readRogueSettings(){
 const settings={...rogueSettingsFor($('spec').value),tuning:clone(request.tuning)};
 for(const key of [...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys]){
  const input=$('rogue'+key[0].toUpperCase()+key.slice(1));
  settings[key]=rogueBooleanKeys.includes(key)?input.checked:rogueNumericKeys.includes(key)?(isCombatRogue()?readCombatNumber(input.id):Number(input.value)):input.value;
 }
 for(const key of rogueWeaponTuningKeys){const id='rogue'+key[0].toUpperCase()+key.slice(1);settings.tuning[key]=isCombatRogue()?readCombatNumber(id):Number($(id).value);}
 return validateRogueSettings(settings,request.tuning,$('spec').value);
}
function restoreRogueSettings(settings,specialization=$('spec').value){
 validateRogueSettings(settings,request.tuning,specialization);
 for(const key of [...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...rogueWeaponTuningKeys]){
  const input=$('rogue'+key[0].toUpperCase()+key.slice(1)),value=rogueWeaponTuningKeys.includes(key)?settings.tuning[key]:settings[key];
  if(rogueBooleanKeys.includes(key))input.checked=value;else input.value=value;
 }
}
// Combat owns a flat strict preview receipt, bound to the submitted typed request.
// It is not Feral's {request, preview} envelope and cannot attest an echoed request.
let combatPreviewReceipt=null,combatPreviewIdentity=null,combatCapabilityNotes=[];
function readCombatNumber(id){
 const raw=$(id).value;if(typeof raw==='string'&&!raw.trim())throw Error('Enter '+id+'; blank is not an explicit zero.');
 const value=Number(raw);if(!Number.isFinite(value))throw Error('Invalid Combat number: '+id);return value;
}
function combatPreviewMatches(){try{return !!combatPreviewReceipt&&combatPreviewIdentity===JSON.stringify(body().rogue);}catch{return false;}}
function invalidateCombatPreview(){
 combatPreviewReceipt=null;combatPreviewIdentity=null;
 if(!isCombatRogue())return;
 $('stats').replaceChildren();$('contributions').textContent='';$('statScope').textContent='Combat preparation required for the current exact inputs. Select an available historical baseline.';$('statScope').hidden=false;
 $('rogueRotationEntries').textContent='Waiting for current valid Combat preparation. Configured priority is not immediate eligibility.';$('statStatus').textContent='Preparation required';
 for(const id of ['run','save','exportBuild'])$(id).disabled=true;
}
function renderCombatPreparation(response){
 if(!response?.effectiveStats||!response.baseline||!Array.isArray(response.rotation?.entries)||response.maximumEnergy!==100||![response.attackPower,response.maximumHealth,response.startingEnergy].every(Number.isFinite))throw Error('Incomplete strict Combat preparation receipt.');
 const values=response.effectiveStats.values||response.effectiveStats;$('stats').replaceChildren();
 for(const [key,value] of Object.entries(values)){
  if(value==null||typeof value==='object')continue;
  const row=document.createElement('div'),label=document.createElement('span'),number=document.createElement('strong');
  label.textContent=key==='energy'?'Maximum Energy':key==='health'?'Maximum player Health':human(key);
  number.textContent=typeof formatResultNumber==='function'?formatResultNumber(value,key.includes('percent')?'percent':'duration'):String(value);row.append(label,number);$('stats').append(row);
 }
 const row=document.createElement('div'),label=document.createElement('span'),number=document.createElement('strong');label.textContent='Starting Energy';number.textContent=typeof formatResultNumber==='function'?formatResultNumber(response.startingEnergy,'duration'):String(response.startingEnergy);row.append(label,number);$('stats').append(row);
 $('statScope').textContent=response.statPreviewScope||'Exact fixed-sword Combat precombat inputs; historical provisional, not current-client certified.';$('statScope').hidden=false;
 $('contributions').textContent=pretty(response);$('rogueRotationEntries').textContent=pretty(response.rotation.entries);
 $('baselineStatus').textContent='Baseline: '+response.sourceStatus;$('statStatus').textContent='Up to date';
}
async function previewCombatRogue(refreshToken){
 if(statsRefreshTimer!==null){clearTimeout(statsRefreshTimer);statsRefreshTimer=null;}
 const token=refreshToken??++statsRefreshId,epoch=generation,submitted=body().rogue,identity=JSON.stringify(submitted);
 const current=()=>{try{return token===statsRefreshId&&epoch===generation&&isCombatRogue()&&identity===JSON.stringify(body().rogue);}catch{return false;}};
 let response;try{response=await post('/api/forever/character/preview-rogue',submitted);}catch(error){if(!current())return;invalidateCombatPreview();setBuildPreparing(buildPreparationPending);throw error;}
 if(!current())return response;
 try{renderCombatPreparation(response);}catch(error){invalidateCombatPreview();setBuildPreparing(buildPreparationPending);throw error;}
 combatPreviewReceipt=clone(response);combatPreviewIdentity=identity;setBuildPreparing(buildPreparationPending);return response;
}
function capabilityNotesForDisplay(capability){
 const notes=capability.knownRestrictions||[];
 if($('classKey').value!=='rogue'||$('spec').value!=='assassination')return notes;
 // Preserve the historical class capability response; availability is scoped to
 // the selected preset here, while the separate Combat option remains visible.
 const legacy='Assassination only: partial historical provisional 38/0/0 allocation with 13 unspent; twelve source-backed talent keys, not optimized, a complete build or current-client certification. Combat and Subtlety are unavailable.';
 return notes.map(note=>note===legacy?'Selected Assassination preset: partial historical provisional 38/0/0 allocation with 13 unspent; twelve source-backed talent keys, not optimized, a complete build or current-client certification. Combat is available as a separate partial historical-provisional preset; Subtlety remains unavailable.':note);
}
function setRogueScope(){
 const combat=isCombatRogue();$('rogueSettingsTitle').textContent=(combat?'Combat':'Assassination')+' manual assumptions';$('roguePriorityTitle').textContent='Fixed '+(combat?'Combat':'Assassination')+' priority';
 $('rogueAssassinationScope').hidden=combat;$('rogueCombatScope').hidden=!combat;$('rogueCombatTalentScope').hidden=!combat;
 if(combat){$('rogueCombatTalentLimitations').replaceChildren();for(const t of talentCatalog){const li=document.createElement('li');li.textContent=human(t.key)+': '+(t.simulationStatus||'partial')+'. '+(Array.isArray(t.knownLimitations)?t.knownLimitations.join(' '):t.knownLimitations||'');$('rogueCombatTalentLimitations').append(li);}}
}
const enhancementNumericKeys=['targetArmor','maximumHealth','weaponMinDamage','weaponMaxDamage','weaponSpeedSeconds','offHandWeaponMinDamage','offHandWeaponMaxDamage','offHandWeaponSpeedSeconds','incomingBossHitDamage','incomingBossSwingSeconds'];
const enhancementBooleanKeys=['dualWield','useStormstrike','useRageOfTheFarseer','useEarthShock','useMaelstromLightningBolt','useLightningBoltFiller','useWindfuryTotem','deployWindfuryTotem','useLightningShield'];
const enhancementSettingKeys=[...enhancementNumericKeys,...enhancementBooleanKeys,'mainHandImbue','offHandImbue'];
function validateEnhancementSettings(settings){
 if(!settings||typeof settings!=='object'||Array.isArray(settings))throw Error('Enhancement manual settings are required.');
 for(const key of Object.keys(settings))if(!enhancementSettingKeys.includes(key))throw Error('Unsupported Enhancement setting: '+key);
 for(const key of enhancementNumericKeys)if(typeof settings[key]!=='number'||!Number.isFinite(settings[key])||settings[key]<0)throw Error('Enhancement '+key+' must be a finite nonnegative number.');
 for(const key of enhancementBooleanKeys)if(typeof settings[key]!=='boolean')throw Error('Enhancement '+key+' must be a boolean.');
 for(const key of ['maximumHealth','weaponSpeedSeconds','offHandWeaponSpeedSeconds','incomingBossSwingSeconds'])if(settings[key]<=0)throw Error('Enhancement '+key+' must be positive.');
 if(settings.weaponMaxDamage<settings.weaponMinDamage||settings.offHandWeaponMaxDamage<settings.offHandWeaponMinDamage)throw Error('Enhancement maximum weapon damage cannot be below minimum damage.');
 if(!['none','windfury','flametongue','rockbiter','frostbrand'].includes(settings.mainHandImbue)||!['none','flametongue','rockbiter','frostbrand'].includes(settings.offHandImbue)||(!settings.dualWield&&settings.offHandImbue!=='none'))throw Error('Enhancement imbues require a valid hand; off-hand requires dual wield and cannot use Windfury.');
 return settings;
}
function validateEnhancementCombatItems(saved,defaults){
 if(!saved||typeof saved!=='object'||Array.isArray(saved))throw Error('Enhancement combat inventory must be an object.');
 for(const [key,value] of Object.entries(saved)){
  if(!Object.prototype.hasOwnProperty.call(defaults,key))throw Error('Unsupported Enhancement combat inventory field: '+key);
  const expected=defaults[key];
  if(Array.isArray(expected)){
   if(!Array.isArray(value)||value.some(entry=>typeof entry!=='string'))throw Error('Enhancement '+key+' must be a list of item keys.');
  }else if(typeof value!==typeof expected||typeof value==='number'&&!Number.isFinite(value))throw Error('Invalid Enhancement combat inventory type: '+key);
  if((key.endsWith('Charges')||key==='engineeringSkill')&&!Number.isInteger(value))throw Error('Enhancement '+key+' must be an integer.');
 }
 return saved;
}
function readEnhancementSettings(){
 return validateEnhancementSettings(Object.fromEntries(enhancementSettingKeys.map(key=>{
  const element=$('enhancement'+key[0].toUpperCase()+key.slice(1));
  return [key,enhancementBooleanKeys.includes(key)?element.checked:enhancementNumericKeys.includes(key)?Number(element.value):element.value];
 })));
}
function restoreEnhancementSettings(settings){
 for(const key of enhancementSettingKeys){
  const element=$('enhancement'+key[0].toUpperCase()+key.slice(1));
  if(enhancementBooleanKeys.includes(key))element.checked=settings[key];else element.value=settings[key];
 }
}
function resetSelections(){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};renderGear();renderMods();}
async function initClass(){
 const token=++generation,c=$('classKey').value;++statsRefreshId;++baselineCheckId;setBuildPreparing(true);baselineAvailable=false;request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+c+' configuration…');$('spec').replaceChildren(...specMap[c].map(s=>option(s,s.replaceAll('-',' '))));
 const eligible=races.filter(r=>r.classes.includes(c));$('race').replaceChildren(...eligible.map(r=>option(r.key,(r.key==='high-order-skyborne'?'Skyborne (Alliance)':r.key==='windshaper-skyborne'?'Skyborne (Horde)':r.name))));
 resetSelections();const catalogWork=loadCatalog();pendingClassCatalog={classKey:c,work:catalogWork};
 try{await catalogWork;if(token!==generation)return;await loadPreset(token);if(token===generation)updateIdentity();}
 catch(error){if(token===generation){setBuildPreparing(false);throw error;}}
 finally{if(pendingClassCatalog?.work===catalogWork)pendingClassCatalog=null;}
}
async function loadPreset(token=generation){
 if(token!==generation)return;
 const c=$('classKey').value,s=$('spec').value,loadId=++presetLoadId;
 request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+human(s)+' source-backed starting build…');
 try{
  const route=c==='priest'?'/api/sim/shadow-priest/defaults':c==='druid'?'/api/sim/balance-druid/defaults':c==='shaman'?'/api/sim/'+s+'-shaman/defaults':c==='hunter'?'/api/sim/'+s+'-hunter/defaults':'/api/sim/'+c+'/defaults?spec='+encodeURIComponent(s);
  const initial=c==='druid'&&s==='feral'?feralDefaults():await api(route);
  const preset=await api('/api/forever/talents/'+c+'/'+s+'/default-itemized');
  if(token!==generation||loadId!==presetLoadId)return;
  request=initial;itemizedMode=true;if(isFeral()){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};restoreFeralControls(initial);$('provisional').checked=false;invalidateFeralPreview();}
  const talentData=await api('/api/forever/talents/'+c+'/'+s);
  if(token!==generation||loadId!==presetLoadId)return;talentCatalog=talentData.talents;
  await augmentItemizedCatalog();
  if(token!==generation||loadId!==presetLoadId)return;
  if(!request.talents||!preset?.ranks||preset.totalRankedPoints!==(c==='rogue'?(s==='combat'?31:38):c==='priest'||c==='shaman'&&s==='enhancement'?36:c==='druid'?(s==='feral'?36:35):51)||
    Object.keys(preset.ranks).some(key=>!talentCatalog.some(t=>t.key===key)))
   throw Error('The source-backed ranked starting allocation does not match the known talent catalog.');
  request.talents.ranks=clone(preset.ranks);
  if(c==='mage'){
   request.useArcanePower=Number(request.talents.ranks['arcane-power']||0)>0;
   request.usePresenceOfMind=Number(request.talents.ranks['presence-of-mind']||0)>0;
   // New builder never inherits an invisible prepared Mana Potion.
   request.useMajorManaPotion=false;request.majorManaPotionCharges=0;request.combatItems.majorManaPotionCharges=0;
  }
  if(['shaman','druid'].includes(c)&&s!=='feral'){if(s!=='enhancement')request.majorManaPotionCharges=0;request.combatItems.majorManaPotionCharges=0;}
  if(c==='shaman'&&s==='enhancement'){for(const key of Object.keys(request.combatItems))if(key.endsWith('Charges'))request.combatItems[key]=0;request.mainHandImbue=request.mainHandImbue??(request.useWindfuryWeapon?'windfury':'none');restoreEnhancementSettings(request);cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};}
  if(c==='rogue'){Object.assign(request,rogueSettingsFor(s));restoreRogueSettings(Object.fromEntries([...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueFixedSettings),'tuning'].map(key=>[key,request[key]])));cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};}
  recalculateTalentTotals();
  await validateTalentSelection();
  if(token!==generation||loadId!==presetLoadId)return;
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;$('targetHealth').value=request.targetMaximumHealth;
  $('timeline').checked=!!request.captureTimeline;
  const capability=c==='rogue'||c==='druid'&&s==='feral'||c==='shaman'&&s==='enhancement'?await api('/api/forever/shared/capabilities/'+c+'?specialization='+s):classCapabilities.find(x=>x.classKey===c);
  if(token!==generation||loadId!==presetLoadId)return;
  if(!capability?.executesThroughCurrentUnifiedEndpoint)
   throw Error('This class does not have a certified unified level-60 character adapter.');
  if(isFeral())feralCapabilityNotes=clone(capability.knownRestrictions||[]);
  if(isCombatRogue())combatCapabilityNotes=clone(capability.knownRestrictions||[]);
  const encounterControls=new Set(capability.encounterControls||[]);
  $('petFamilyRow').hidden=!encounterControls.has('pet-family');
  $('demonRow').hidden=!encounterControls.has('pet-type');
  $('companionRow').hidden=!(encounterControls.has('pet-family')||encounterControls.has('pet-type'));
  $('rotationRow').hidden=!capability.hasRotationPreview;
  $('classCapabilityNotes').textContent=capabilityNotesForDisplay(capability).join(' · ') ||
    'The selected class uses server-backed encounter and rotation controls.';
  $('companionEnabled').checked=c==='warlock'?!!request.enablePet:c==='hunter'?(s==='beast-mastery'?!!request.petEnabled:!!request.hasPet):false;
  $('petFamily').value=request.petFamily||'cat';$('demonType').value=request.petType||'succubus';$('rotation').value=request.rotationPolicy||'adaptive';
  $('priorityEditor').hidden=!(capability.hasRotationPreview||c==='hunter');$('priorityText').value='';priorityRows=[];drawPriorityRows();renderPriorityConditions();$('priorityStatus').textContent=c==='hunter'?'Hunter priority preview now validates using the selected combat runner and learned actions.':'Automatic priority follows the selected talents and class runner.';
  renderTalents();renderMods();updateIdentity();
  // Internal identity permits preflight requests; only the outermost wrapper
  // may publish user-facing readiness after all serialized controls settle.
  loadedClass=c;loadedSpec=s;buildDirty=false;
  queueStatRefresh();
  await checkBaseline();
 }catch(error){if(token===generation&&loadId===presetLoadId){request=null;note('Cannot load authoritative ranked preset: '+error.message);}}
}
async function augmentItemizedCatalog(){
 const token=generation,c=$('classKey').value,s=$('spec').value;
 // Strict Enhancement admits sixteen modeled keys: twelve Enhancement and four Elemental.
 if(c==='rogue'||c==='druid'&&s==='feral'||c==='shaman'&&s==='enhancement'){talentCatalog=talentCatalog.filter(t=>t.modeled!==false);return;}
 const extra=await api('/api/forever/talents/'+c+'/'+s+'/itemized-catalog');
 if(token!==generation||c!==$('classKey').value||s!==$('spec').value)return;
 talentCatalog=[...talentCatalog.filter(t=>t.modeled!==false).map(t=>({...t,modeled:true})),...extra.unmodeled];
}
const talentTrees={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',
 affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',
 marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent',elemental:'elementalPointsSpent',enhancement:'enhancementPointsSpent',restoration:'restorationPointsSpent',balance:'balancePointsSpent',feral:'feralPointsSpent',discipline:'disciplinePointsSpent',holy:'holyPointsSpent',shadow:'shadowPointsSpent',assassination:'assassinationPointsSpent',combat:'combatPointsSpent',subtlety:'subtletyPointsSpent'};
function recalculateTalentTotals(){
 if(!request?.talents)return;
 const talents=request.talents,ranks=talents.ranks||{};
 for(const [tree,key] of Object.entries(talentTrees))
  if(key in talents)talents[key]=talentCatalog.filter(t=>t.tree===tree)
    .reduce((sum,t)=>sum+Number(ranks[t.key]||0),0);
 if(isFeral()){talents.totalPoints=Object.values(ranks).reduce((a,b)=>a+b,0);talents.unspentPoints=51-talents.totalPoints;}
}
async function validateTalentSelection(){
 const endpoint='/api/forever/talents/'+$('classKey').value+'/'+$('spec').value;
 const selected=clone(request.talents),selectedCatalog=clone(talentCatalog),strict=itemizedMode;
 if(strict)await post(endpoint+'/validate-itemized',selected);
 const typed=clone(selected);
 if(strict)typed.ranks=Object.fromEntries(Object.entries(typed.ranks||{}).filter(([key])=>
   selectedCatalog.some(t=>t.key===key&&t.modeled!==false)));
 await post(endpoint+'/validate',typed);
}
function renderTalents(){
 queueStatRefresh();
 const root=$('talents');for(const grid of root.querySelectorAll('.talent-tree-grid'))grid._talentObserver?.disconnect();root.replaceChildren();if(!request)return;
 const talents=request.talents||{},ranks=talents.ranks||{};
 const mapping={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent',elemental:'elementalPointsSpent',enhancement:'enhancementPointsSpent',restoration:'restorationPointsSpent',balance:'balancePointsSpent',feral:'feralPointsSpent',discipline:'disciplinePointsSpent',holy:'holyPointsSpent',shadow:'shadowPointsSpent',assassination:'assassinationPointsSpent',combat:'combatPointsSpent',subtlety:'subtletyPointsSpent'};
 const treeNames=isRogue()?['assassination','combat','subtlety']:$('classKey').value==='priest'?['discipline','holy','shadow']:$('classKey').value==='druid'?['balance','feral','restoration']:$('classKey').value==='shaman'?['elemental','enhancement','restoration']:$('classKey').value==='mage'?['arcane','fire','frost']:$('classKey').value==='warlock'?['affliction','demonology','destruction']:$('classKey').value==='warrior'?['arms','fury','protection']:['beast-mastery','marksmanship','survival'];
 let talentEditPending=false;
 async function setRank(t,delta){
  if(talentEditPending||renderTalents.pending?.request===request&&renderTalents.pending.ranks===request.talents.ranks){note('Wait for the current talent edit to validate.');return;}
  const ranks=talents.ranks||{},key=t.key,prior=ranks[key]||0,next=Math.min(t.maxRank,Math.max(0,prior+delta));
  if(next===prior)return;
  const priorPresent=Object.prototype.hasOwnProperty.call(ranks,key);
  const oldTotals=Object.fromEntries(Object.entries(talents).filter(([k])=>k.endsWith('PointsSpent')||isFeral()&&feralTalentTotals.includes(k)));
  const editGeneration=generation,editRequest=request,editTalents=talents,editRanks=ranks;if(isFeral())invalidateFeralPreview();if(isCombatRogue())invalidateCombatPreview();
  const cooldownFlag=key==='arcane-power'?'useArcanePower':key==='presence-of-mind'?'usePresenceOfMind':null;
  const oldFlag=cooldownFlag?request[cooldownFlag]:null;
  if(next||isFeral())ranks[key]=next;else delete ranks[key];
  if(cooldownFlag&&(prior===0||next===0))request[cooldownFlag]=next>0;
  recalculateTalentTotals();talentEditPending=true;const editOwner={request,ranks};renderTalents.pending=editOwner;
  try{await validateTalentSelection();if(editGeneration!==generation||editRequest!==request||editTalents!==request.talents||editRanks!==request.talents.ranks)return;
   // On talent removal, scrub its old custom action. Otherwise a saved
   // rotation can keep offering a learned-only spell after it is unlearned.
   if(next===0 && (
     $('classKey').value==='mage' &&
      ['arcane-power','presence-of-mind','arcane-blast','arcane-wrath',
       'combustion','pyroblast','blast-wave','ice-lance','ice-barrier',
       'cold-snap','ice-block'].includes(key) ||
     $('classKey').value==='warlock' &&
      ['amplify-curse','wrack','siphon-life','shadowburn',
       'bane-of-havoc','incinerate','conflagrate'].includes(key) ||
     $('classKey').value==='hunter' &&
      ['scatter-shot','sniper-shot','counterattack','strider-kick',
       'deterrence','summon-hawk','intimidation','bestial-wrath'].includes(key))){
     request.customPriority=(request.customPriority||[]).filter(x=>x!==key);
     priorityRows=priorityRows.filter(x=>x.key!==key);
     drawPriorityRows();syncPriorityText();
   }
   buildDirty=true;renderTalents();renderPriorityConditions();
   const updated=[...document.querySelectorAll('.talent-node')].find(x=>x.dataset.talentKey===key);updated?.focus({preventScroll:true});}
  catch(error){if(editGeneration!==generation||editRequest!==request||editTalents!==request.talents||editRanks!==request.talents.ranks)return;if(priorPresent)ranks[key]=prior;else delete ranks[key];
   for(const [k,v] of Object.entries(oldTotals))talents[k]=v;
   if(cooldownFlag)request[cooldownFlag]=oldFlag;
   note('Talent allocation rejected: '+error.message);if(isFeral()||isCombatRogue())queueStatRefresh();}
  finally{talentEditPending=false;if(renderTalents.pending===editOwner)renderTalents.pending=null;if(isFeral())$('feralFurorRank').value=String(request?.talents?.ranks?.furor||0);}
 }
 function eligibility(t){
   const own=ranks[t.key]||0;
   const field=mapping[t.tree],required=t.requiredEarlierPoints||0;
   const earlier=itemizedMode&&t.row
        ?talentCatalog.filter(x=>x.tree===t.tree&&x.row&&x.row<t.row)
          .reduce((sum,x)=>sum+Number(ranks[x.key]||0),0)
        :field in talents
       ?Math.max(0,Number(talents[field]||0)-own)
       :talentCatalog.filter(x=>x.tree===t.tree&&(x.requiredEarlierPoints||0)<required)
           .reduce((sum,x)=>sum+Number(ranks[x.key]||0),0);
   const parent=t.enginePrerequisiteKey;
   const needed=t.enginePrerequisiteRank||1;
   const missingParent=!!parent&&(ranks[parent]||0)<needed;
   const missingTier=required>earlier;
   return {locked:missingParent||missingTier,
     reason:missingParent?'Requires '+needed+'/'+needed+' '+human(parent):
       missingTier?'Requires '+required+' earlier '+human(t.tree)+' points ('+earlier+' allocated)':''};
 }
 function nodeFor(t){
   const node=document.createElement('button');node.type='button';node.className='talent-node';node.dataset.talentKey=t.key;
   const rank=ranks[t.key]||0,elig=eligibility(t);
   node.classList.toggle('learned',rank>0);node.classList.toggle('locked',rank===0&&elig.locked);
    node.classList.toggle('unmodeled',t.modeled===false);
   node.setAttribute('aria-disabled',String(rank===0&&elig.locked));
   node.setAttribute('aria-label',human(t.key)+', rank '+rank+' of '+t.maxRank+'. Enter adds, right-click or Delete removes.');
   node.title=(t.name||human(t.key))+' · '+rank+'/'+t.maxRank+'\n'+
     (t.historicalRankDescriptions?.[Math.max(0,rank-1)]||t.historicalRankDescriptions?.[0]||'No verified tooltip description')+
     '\n'+(t.modeled===false?'Historical-only: consumes actual points but grants NO combat effect.':'Historical reference only; gameplay validation remains server-owned.');
   const face=document.createElement('span');face.className='talent-face';
   const fallback=document.createElement('span');fallback.className='talent-fallback';fallback.textContent='✧';
   face.append(fallback);
   if(t.iconKey && availableIconKeys.has(t.iconKey) && /^[a-z0-9_]+$/.test(t.iconKey)){
     const img=document.createElement('img');img.className='talent-art';img.alt='';
     img.loading='lazy';img.src='/forever-icons/'+t.iconKey+'.jpg';
     img.addEventListener('load',()=>fallback.hidden=true);
     img.addEventListener('error',()=>{img.remove();fallback.hidden=false;});
     face.append(img);
   }
   const badge=document.createElement('strong');badge.className='talent-rank';badge.textContent=rank+'/'+t.maxRank;face.append(badge);
   const label=document.createElement('span');label.className='talent-name';label.textContent=t.name||human(t.key);
   const source=document.createElement('small');source.className='talent-source';
   source.textContent=t.visualSourceStatus==='unmatched-engine-talent'?'Engine-modeled · layout unknown':
     t.visualSourceStatus==='engine-70707-cap-historical-visual'?'70707 rank cap · historical art only':
     t.visualSourceStatus==='engine-hotfix-position-historical-tooltip'?'Hotfix position · old tooltip reference':
     'Historical visual reference';
   if((['warrior','mage','warlock','hunter'].includes($('classKey').value)) && t.sourceBuild){
    source.textContent='Forever '+t.sourceBuild+' · source-backed provisional';
   }
   if(isFeral()){source.textContent=t.simulationStatus||'Historical provisional';node.title+='\n'+(Array.isArray(t.knownLimitations)?t.knownLimitations.join(' '):t.knownLimitations||'');}
   node.append(face,label,source);
   if(['warrior','mage','warlock','hunter'].includes($('classKey').value)){
    attachSourceIndicators(node,t);
    // Talent tiles are buttons already; avoid nested buttons. The same
    // confidence/mechanics explanations are reachable through keyboard
    // focus and announced with the parent tile's accessible name.
    const notices=sourceStatusInfo(t).map(x=>x.text);
    if(notices.length){
     node.title+='\n'+notices.join('\n');
     node.setAttribute('aria-label',node.getAttribute('aria-label')+
      '. '+notices.join(' '));
    }
   }
   if(t.enginePrerequisiteKey){const prereq=document.createElement('span');prereq.className='talent-prereq';prereq.textContent='Requires '+(t.enginePrerequisiteRank||1)+' rank(s) of '+human(t.enginePrerequisiteKey);node.append(prereq);node.title+='\\n'+prereq.textContent;}
   if(isFeral()&&t.key==='furor'){node.setAttribute('aria-label','Furor rank '+rank+' of 5. Inactive in fixed Cat. Use the explicit 0 or 5 selector above.');node.addEventListener('click',()=>{$('feralFurorRank').focus();});return node;}
   node.addEventListener('click',()=>{if(rank===0&&elig.locked){note('Talent locked: '+elig.reason);return;}setRank(t,1);});
   node.addEventListener('contextmenu',e=>{e.preventDefault();setRank(t,-1);});
   node.addEventListener('keydown',e=>{if(e.key==='Delete'||e.key==='Backspace'){e.preventDefault();setRank(t,-1);}});
   return node;
 }
 for(const tree of treeNames){
   const section=document.createElement('section');section.className='talent-tree';
   const h=document.createElement('h2');h.textContent=human(tree);section.append(h);
   const field=mapping[tree];if(field in talents){
    const actual=talentCatalog.filter(t=>t.tree===tree).reduce((sum,t)=>sum+Number(ranks[t.key]||0),0);
    const legacy=Math.max(0,Number(talents[field]||0)-actual);
    const total=document.createElement('div');total.className='tree-points';total.dataset.tree=tree;
    const label=document.createElement('span');label.textContent='Selected talent ranks';
    const output=document.createElement('output');output.textContent=String(actual);
    output.setAttribute('aria-label',human(tree)+' actual selected ranks');
    total.append(label,output);section.append(total);
    if(legacy>0){
     const warning=document.createElement('p');warning.className='inline-note talent-legacy-warning';
     warning.textContent=legacy+' inherited preset points are not itemized into talents; engine tiers still use the legacy totals.';
     section.append(warning);
    }
   }
   const choices=talentCatalog.filter(t=>t.tree===tree);
   const positions=new Set(),located=[],unplaced=[];
   for(const t of choices){
     const coord=t.row&&t.column?t.row+':'+t.column:null;
     if(coord&&!positions.has(coord)){positions.add(coord);located.push(t);}
     else unplaced.push(t);
   }
   if(located.length){
     const grid=document.createElement('div');grid.className='talent-tree-grid';
     const byKey=new Map(located.map(t=>[t.key,t]));
     // Noninteractive SVG overlay uses exact grid coordinates; only actual
     // engine prerequisites become arrows. Historical-only links are omitted.
     const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');
     svg.setAttribute('class','talent-connections');
     svg.setAttribute('aria-hidden','true');
     const connections=[];
     for(const t of located){
       const parent=t.enginePrerequisiteKey && byKey.get(t.enginePrerequisiteKey);
       if(!parent)continue;
       const satisfied=(ranks[parent.key]||0)>=(t.enginePrerequisiteRank||1);
       const line=document.createElementNS('http://www.w3.org/2000/svg','path');
       line.setAttribute('class',satisfied?'dependency-line satisfied':'dependency-line');
       const arrow=document.createElementNS('http://www.w3.org/2000/svg','path');
       arrow.setAttribute('class',satisfied?'dependency-arrow satisfied':'dependency-arrow');
       svg.append(line,arrow);connections.push({parent:parent.key,child:t.key,line,arrow});
     }
     grid.append(svg);
     for(const t of located){const node=nodeFor(t);node.style.gridRow=String(t.row);node.style.gridColumn=String(t.column);grid.append(node);}
     section.append(grid);
     // Align connectors with measured node centers, not assumed fixed row
     // heights: wrapped labels and responsive grids change actual geometry.
     const redraw=()=>{
       const bounds=grid.getBoundingClientRect(),width=Math.max(1,grid.clientWidth),height=Math.max(1,grid.clientHeight);
       svg.setAttribute('viewBox','0 0 '+width+' '+height);
       for(const edge of connections){
         const from=grid.querySelector('[data-talent-key="'+edge.parent+'"]');
         const to=grid.querySelector('[data-talent-key="'+edge.child+'"]');
         if(!from||!to)continue;
         const a=from.getBoundingClientRect(),b=to.getBoundingClientRect();
         const x1=a.left+a.width/2-bounds.left,y1=a.top+a.height/2-bounds.top;
         const x2=b.left+b.width/2-bounds.left,y2=b.top+b.height/2-bounds.top;
         const middle=(y1+y2)/2;
         edge.line.setAttribute('d','M '+x1+' '+y1+' L '+x1+' '+middle+' L '+x2+' '+middle+' L '+x2+' '+y2);
         edge.arrow.setAttribute('d','M '+(x2-5)+' '+(y2-8)+' L '+x2+' '+y2+' L '+(x2+5)+' '+(y2-8));
       }
     };
     if(typeof ResizeObserver!=='undefined'){
       const observer=new ResizeObserver(redraw);observer.observe(grid);
       for(const node of grid.querySelectorAll('.talent-node'))observer.observe(node);
       grid._talentObserver=observer;
     }
     requestAnimationFrame(redraw);
   }
   if(unplaced.length){
     const other=document.createElement('div');other.className='talent-unplaced';
     const hint=document.createElement('p');hint.className='inline-note';hint.textContent='Engine talents without a reconciled non-conflicting tree position';other.append(hint);
     for(const t of unplaced)other.append(nodeFor(t));section.append(other);
   }
   if(!choices.length){const hint=document.createElement('p');hint.className='empty-hint';hint.textContent='No combat-connected talents are exposed here by the selected runner. Other class-tree options cannot yet be safely allocated.';section.append(hint);}
   root.append(section);
 }
 if(isCombatRogue())setRogueScope();
 if(isFeral()){$('feralTalentLimitations').replaceChildren();for(const t of talentCatalog.filter(t=>t.simulationStatus)){const li=document.createElement('li');li.textContent=human(t.key)+': '+t.simulationStatus+'. '+(Array.isArray(t.knownLimitations)?t.knownLimitations.join(' '):t.knownLimitations||'');$('feralTalentLimitations').append(li);}$('feralFurorRank').value=String(ranks.furor||0);$('feralFurorRank').onchange=()=>{const t=talentCatalog.find(x=>x.key==='furor');setRank(t,Number($('feralFurorRank').value)-Number(ranks.furor||0));};}
 showTalentSummary();
}
function showTalentSummary(){
 const ranked=Object.values(request?.talents?.ranks||{}).reduce((sum,n)=>sum+Number(n||0),0);
 $('talentSummary').textContent=ranked+' rank-derived points · '+Math.max(0,51-ranked)+' remaining / 51. Source-backed historical-only ranks count without awarding combat effects.';
}
async function checkBaseline(){
 const token=++baselineCheckId;++statsRefreshId;baselineAvailable=false;
 if(isFeral())invalidateFeralPreview();
 if(isCombatRogue())invalidateCombatPreview();
 if(isFeral()&&!$('provisional').checked){$('baselineStatus').textContent='Feral reviewed/observed modes unavailable';$('statStatus').textContent='Select Historical baseline and enter manual Cat AP';return;}
 if(!$('race').value)return;
 try{
  if(!$('provisional').checked){
   // The public base-stats endpoint can return a race/class stat record even
   // when it is NOT an approved level-60 baseline for character generation.
   // Match the generator's actual review eligibility before sending preview POSTs.
   const coverage=await api('/api/forever/character/level60-baseline-coverage');
   if(token!==baselineCheckId)return;
   const matching=(coverage.combinations||[]).find(x=>
     x.race===$('race').value&&x.classKey===$('classKey').value);
   if(matching?.status!=='reviewed-forever-in-game'){
    baselineAvailable=false;
    $('baselineStatus').textContent='No reviewed Forever baseline for this race/class';
    $('statStatus').textContent='Select Historical baseline to preview provisional stats';
    return;
   }
  }
  const qs=new URLSearchParams({race:$('race').value,classKey:$('classKey').value,
   level:'60',provisional:String($('provisional').checked)});
  const result=await api('/api/forever/character/base-stats?'+qs);
  if(token!==baselineCheckId)return;
  baselineAvailable=true;
  $('baselineStatus').textContent=$('provisional').checked?
   'Historical provisional · unverified Forever':(result.sourceStatus||'Reviewed baseline');
  queueStatRefresh();
 }catch(e){
  if(token!==baselineCheckId)return;
  baselineAvailable=false;
  $('baselineStatus').textContent='Baseline unavailable: '+e.message;
  $('statStatus').textContent='Choose an available baseline';
 }
}
async function loadCatalog(){
 const loadId=++catalogLoadId,c=$('classKey').value,race=$('race').value;
 const current=()=>loadId===catalogLoadId&&c===$('classKey').value&&race===$('race').value;
 if(isRestrictedCaster()){
  const data=await api('/api/forever/character/modifiers');if(!current())return;
  catalog=[];catalogAll=[];enchants=[];modifiers=data.modifiers;
  $('catalogCoverage').textContent=restrictedCasterName()+' catalog equipment and enchants are pending eligibility evidence; no gear is equipped.';renderGear();renderMods();return;
 }
 let page=1;catalog=[];
 while(page<=100){const filters=new URLSearchParams({page:String(page),pageSize:'200',usableOnly:'true',classKey:c,race,level:'60'});
 const data=await api('/api/forever/equipment/items?'+filters);if(!current())return;catalog.push(...data.items);if(catalog.length>=data.total)break;page++;}
 page=1;catalogAll=[];
 while(page<=100){const data=await api('/api/forever/equipment/items?page='+page+'&pageSize=200');
 if(!current())return;catalogAll.push(...data.items);if(catalogAll.length>=data.total)break;page++;}
 const coverage=await api('/api/forever/equipment/coverage');if(!current())return;
 $('catalogCoverage').textContent='Curated local data: '+coverage.totalItems+' items; '+
  coverage.executableEnchants+' executable enchants; '+
  coverage.unsupportedEquipmentEffects+' unmodeled effects.';
 page=1;enchants=[];
 while(page<=100){const data=await api('/api/forever/equipment/enchants?page='+page+'&pageSize=200');if(!current())return;enchants.push(...data.enchants);if(enchants.length>=data.total)break;page++;}
 const m=await api('/api/forever/character/modifiers');if(!current())return;modifiers=m.modifiers;renderGear();renderMods();
}
function showItemIcon(container,item){
 const id=item?.iconFileDataId;
 const exactKey=item?.iconKey;
 const localName=typeof exactKey==='string'&&/^[a-z0-9_]+$/.test(exactKey)&&availableIconKeys.has(exactKey)
   ?exactKey
   :Number.isInteger(id)&&id>0&&availableIconKeys.has('filedata-'+id)?'filedata-'+id:null;
 if(!localName)return;
 const img=document.createElement('img');img.className='item-art';img.alt='';
 img.loading='lazy';img.src='/forever-icons/'+localName+'.jpg';
 img.addEventListener('error',()=>img.remove());
 container.append(img);
}
const slotSymbols={'head':'◉','neck':'◇','shoulder':'◆','back':'▧','chest':'▤','wrist':'⌁','hands':'✥','waist':'▰','legs':'▥','feet':'▱','finger1':'◌','finger2':'◌','trinket1':'✦','trinket2':'✦','main-hand':'⚔','off-hand':'⛨','ranged':'➶'};
function renderGear(){
 queueStatRefresh();
 const root=$('gearRows');root.replaceChildren();
 for(const slot of slots){
  const equipped=cfg.equipment.items.find(x=>x.slotKey===slot),item=catalog.find(x=>x.id===equipped?.itemId);
  const button=document.createElement('button');button.type='button';button.className='gear-slot';button.dataset.slot=slot;button.setAttribute('aria-label',human(slot)+': '+(item?.name||'Empty')+'. Open item picker');
  const symbol=document.createElement('span');symbol.className='slot-icon';symbol.textContent=slotSymbols[slot]||'◇';showItemIcon(symbol,item);
  const copy=document.createElement('span');copy.className='slot-copy';
  const label=document.createElement('small');label.textContent=human(slot);
  const name=document.createElement('strong');name.textContent=item?.name||'Empty slot';name.className=item?qualityClass(item.quality):'empty-item';
  copy.append(label,name);
  if(equipped?.enchantSpellId||equipped?.temporaryEnchantSpellId){const line=document.createElement('em');line.textContent=[equipped.enchantSpellId?'Enchanted':'',equipped.temporaryEnchantSpellId?'Temporary effect':''].filter(Boolean).join(' · ');copy.append(line);}
  button.append(symbol,copy);
  if(item)attachSourceIndicators(button,item);
  if(equipped?.enchantSpellId){
   const enchantSource=enchants.find(x=>x.spellId===equipped.enchantSpellId);
   if(enchantSource)attachSourceIndicators(button,enchantSource);
  }
  button.addEventListener('click',()=>openPicker(slot,'items'));
   const wrap=document.createElement('div');wrap.className='gear-slot-wrap';wrap.append(button);
   if(equipped){
    const shortcut=document.createElement('button');shortcut.type='button';shortcut.className='gear-enchant-shortcut';
    const active=!!(equipped.enchantSpellId||equipped.temporaryEnchantSpellId);shortcut.classList.toggle('active',active);
    shortcut.textContent=active?'✦':'◇';shortcut.dataset.enchantSlot=slot;
    shortcut.setAttribute('aria-label',(active?'Edit':'Add')+' enchants for '+human(slot));
    shortcut.addEventListener('click',event=>{event.stopPropagation();openPicker(slot,'enchants');});
    wrap.append(shortcut);
   }
   root.append(wrap);
 }
}
function setPickerTab(tab){
 pickerTab=tab;const items=tab==='items';
 $('pickerTabItems').setAttribute('aria-selected',String(items));$('pickerTabEnchants').setAttribute('aria-selected',String(!items));
 $('pickerTabItems').tabIndex=items?0:-1;$('pickerTabEnchants').tabIndex=items?-1:0;
 $('pickerItemsPanel').hidden=!items;$('pickerEnchantsPanel').hidden=items;
 if(!items)renderPickerEnchants().catch(showError);
}
function openPicker(slot,tab='items'){
 if(isRestrictedCaster()){note(restrictedCasterName()+' equipment is pending eligibility evidence. No item is permanently prohibited.');return;}
 activeSlot=slot;pickerPage=1;pickerSelected=null;pickerCompatible=null;pickerDetailsLoad++;
 $('pickerDetails').classList.remove('is-loading');$('pickerDetails').removeAttribute('aria-busy');
 $('pickerDetails').textContent='Select an item to inspect its details.';$('pickerTitle').textContent=human(slot);$('itemSearch').value='';
 const equipped=cfg.equipment.items.find(x=>x.slotKey===slot);
 if(equipped)pickerSelected=catalog.find(x=>x.id===equipped.itemId)||null;
 renderPicker();$('itemDialog').showModal();setPickerTab(tab);
 if(pickerSelected)renderPickerDetails().catch(showError);
 if(tab==='items')$('itemSearch').focus();else $('pickerTabEnchants').focus();
}
function pickerCandidates(){const q=$('itemSearch').value.trim().toLowerCase();return (showCatalogAll?catalogAll:catalog).filter(i=>slotsFor(i).includes(activeSlot)&&(!q||i.name.toLowerCase().includes(q)||String(i.id).includes(q)));}
function renderPicker(){
 const matches=pickerCandidates(),pageSize=12,pages=Math.max(1,Math.ceil(matches.length/pageSize));pickerPage=Math.max(1,Math.min(pages,pickerPage));
 const root=$('pickerItems');root.replaceChildren();for(const item of matches.slice((pickerPage-1)*pageSize,pickerPage*pageSize)){
  const b=document.createElement('button');b.type='button';b.className='picker-item'+(pickerSelected?.id===item.id?' selected':'');b.dataset.itemId=String(item.id);
  const name=document.createElement('strong');name.className=qualityClass(item.quality);name.textContent=item.name;
  const sub=document.createElement('small');sub.textContent='Lv '+(item.itemLevel||item.requiredLevel||60)+' · '+
    (item.quality||'Unclassified')+(catalog.some(x=>x.id===item.id)?'':' · Inspect only: unavailable');const icon=document.createElement('span');icon.className='slot-icon';icon.textContent='◇';showItemIcon(icon,item);b.append(icon,name,sub);attachSourceIndicators(b,item);
  b.addEventListener('click',()=>{pickerCompatible=null;pickerSelected=item;for(const row of root.querySelectorAll('.picker-item'))row.classList.toggle('selected',row.dataset.itemId===String(item.id));renderPickerDetails().catch(showError);});root.append(b);
 }
 if(!matches.length){const p=document.createElement('p');p.className='empty-hint';p.textContent='No eligible catalog items match this search.';root.append(p);}
 const controls=$('pickerPagination');controls.replaceChildren();const prev=document.createElement('button');prev.className='subtle';prev.textContent='Previous';prev.disabled=pickerPage===1;prev.addEventListener('click',()=>{pickerPage--;renderPicker();});
 const text=document.createElement('span');text.textContent='Page '+pickerPage+' of '+pages+' · '+matches.length+' items';
 const next=document.createElement('button');next.className='subtle';next.textContent='Next';next.disabled=pickerPage>=pages;next.addEventListener('click',()=>{pickerPage++;renderPicker();});controls.append(prev,text,next);
}
// Compatibility is server-owned, and stale requests never overwrite newer selections.
async function resolvePickerEnchants(item,slot){
 const loadId=++pickerDetailsLoad,root=$('pickerDetails');
 root.classList.add('is-loading');root.setAttribute('aria-busy','true');
 try{
  const result=await api('/api/forever/equipment/items/'+item.id+'/compatible-enchants?slot='+encodeURIComponent(slot));
  if(loadId!==pickerDetailsLoad||item!==pickerSelected||slot!==activeSlot||!$('itemDialog').open)return null;
  pickerCompatible=result;
  const equipped=cfg.equipment.items.find(x=>x.slotKey===slot),same=equipped?.itemId===item.id;
  pickerEnchantSelection={permanent:same?equipped.enchantSpellId||0:0,temporary:same?equipped.temporaryEnchantSpellId||0:0};
  return result;
 }finally{if(loadId===pickerDetailsLoad){root.classList.remove('is-loading');root.removeAttribute('aria-busy');}}
}
async function commitPicker(item){
 const slot=activeSlot,previous=clone(cfg.equipment);cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==slot);
 const entry={slotKey:slot,itemId:item.id};
 if(pickerEnchantSelection.permanent)entry.enchantSpellId=pickerEnchantSelection.permanent;
 if(pickerEnchantSelection.temporary)entry.temporaryEnchantSpellId=pickerEnchantSelection.temporary;
 cfg.equipment.items.push(entry);
 try{await validateGear();buildDirty=true;renderGear();$('itemDialog').close();note('Equipment validated by server.');}
 catch(error){cfg.equipment=previous;showError(error);}
}
async function renderPickerDetails(){
 const item=pickerSelected,slot=activeSlot,root=$('pickerDetails');if(!item)return;
 if(!pickerCompatible||pickerCompatible.itemId!==item.id||pickerCompatible.slot!==slot)await resolvePickerEnchants(item,slot);
 if(!pickerCompatible||item!==pickerSelected||slot!==activeSlot||!$('itemDialog').open)return;
 root.replaceChildren();
 const header=document.createElement('div');header.className='selected-item-heading';
 const portrait=document.createElement('span');portrait.className='slot-icon';portrait.textContent='◇';showItemIcon(portrait,item);
 const title=document.createElement('h3');title.textContent=item.name;title.className=qualityClass(item.quality);header.append(portrait,title);attachSourceIndicators(header,item,true);root.append(header);
 const p=document.createElement('p');p.className='inline-note';p.textContent='Level '+item.requiredLevel+' · '+(item.verificationStatus||item.sourceStatus||'Source pending');root.append(p);
 const stats=document.createElement('p');stats.textContent=Object.entries(item.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No static stats listed';root.append(stats);
 const weapon=item.weapon;
 if(weapon){
  const detail=document.createElement('p');detail.className='inline-note';
  const damage=Number.isFinite(Number(weapon.minimumDamage))&&Number.isFinite(Number(weapon.maximumDamage))
    ?weapon.minimumDamage+'–'+weapon.maximumDamage+' damage':'Damage unavailable';
  detail.textContent='Weapon: '+[weapon.type,weapon.hand,damage,
    weapon.speedSeconds!=null?weapon.speedSeconds+'s speed':null].filter(Boolean).join(' · ');
  root.append(detail);
 }
 const effects=Array.isArray(item.effects)?item.effects:[];
 if(effects.length){
  const heading=document.createElement('p');heading.className='inline-note';heading.textContent='Equipment effects:';root.append(heading);
  const list=document.createElement('ul');list.className='item-effect-status';
  for(const effect of effects){
   const status=String(effect.executionStatus||'').toLowerCase();
   const label=status==='modeled'?'Executable':status==='static'?'Static effect only':
    status==='not-modeled'?'Known but not implemented':'Insufficient source data';
   const line=document.createElement('li');
   line.textContent=[effect.kind||'Effect',effect.spellId?'Spell '+effect.spellId:null,label,effect.notes||null]
    .filter(Boolean).join(' · ');
   list.append(line);
  }
  root.append(list);
 }
 const actions=document.createElement('div');actions.className='picker-actions';
 const equip=document.createElement('button');equip.type='button';equip.className='simulate';
  const usable=catalog.some(x=>x.id===item.id);
  equip.textContent=usable?'Equip item':'Unavailable for this class/race/source';
  equip.disabled=!usable;
  equip.title=usable?'Server validates all restrictions before equip':'Discoverable record only; cannot bypass class/race/source rules';
  equip.addEventListener('click',()=>commitPicker(item));
 const enchant=document.createElement('button');enchant.type='button';enchant.className='subtle';enchant.textContent='Enchants';enchant.addEventListener('click',()=>setPickerTab('enchants'));
 actions.append(equip,enchant);
 const equipped=cfg.equipment.items.find(x=>x.slotKey===slot);
 if(equipped){
  const remove=document.createElement('button');remove.type='button';remove.className='subtle';
  remove.textContent='Unequip '+human(slot);remove.id='unequipSelectedSlot';
  remove.addEventListener('click',async()=>{
   const previous=clone(cfg.equipment);
   cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==slot);
   try{await validateGear();buildDirty=true;renderGear();renderMods();
    $('itemDialog').close();note('Unequipped '+human(slot)+'; stats refreshed.');}
   catch(error){cfg.equipment=previous;renderGear();showError(error);}
  });
  actions.append(remove);
 }
 root.append(actions);
 if(pickerTab==='enchants')await renderPickerEnchants();
}
async function renderPickerEnchants(){
 const root=$('pickerEnchantDetails'),item=pickerSelected,slot=activeSlot;
 root.replaceChildren();
 if(!item){root.textContent='Select an item from Items first.';return;}
 if(!pickerCompatible||pickerCompatible.itemId!==item.id||pickerCompatible.slot!==slot){
  root.textContent='Loading compatible enchants…';if(!await resolvePickerEnchants(item,slot))return;
 }
 root.replaceChildren();
 const heading=document.createElement('p');heading.className='inline-note';heading.textContent='Compatible enchants for '+item.name;root.append(heading);
 const search=document.createElement('input');search.id='enchantSearch';search.type='search';search.placeholder='Search enchants by name or spell ID';search.setAttribute('aria-label','Search compatible enchants');root.append(search);
 const layout=document.createElement('div');layout.className='enchant-browser';
 const fields=document.createElement('div');fields.className='enchant-groups';
 const inspection=document.createElement('section');inspection.className='enchant-inspection';
 inspection.setAttribute('aria-label','Enchantment details');inspection.textContent='Select an enchant to inspect its effect.';
 function inspect(enchant,temporary){
  inspection.replaceChildren();
  const title=document.createElement('h3');title.textContent=enchant.name;inspection.append(title);attachSourceIndicators(inspection,enchant,true);
  const rows=[
   temporary?'Temporary enchant':'Permanent enchant',
   Object.entries(enchant.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No static stats listed',
   enchant.weaponDamageBonus?'Weapon damage +'+enchant.weaponDamageBonus:'',
   enchant.durationSeconds?'Duration: '+enchant.durationSeconds+' seconds':'',
   'Source: '+(enchant.verificationStatus||enchant.sourceStatus||'Unverified'),
   enchant.modeled===false?'Unmodeled: no executable simulation effect':'Engine-modeled where applicable'
  ];
  for(const value of rows.filter(Boolean)){const p=document.createElement('p');p.textContent=value;inspection.append(p);}
 }
 const removePermanent=document.createElement('button');removePermanent.type='button';removePermanent.className='subtle';removePermanent.id='removePermanentEnchant';removePermanent.textContent='Remove permanent enchant';
 removePermanent.addEventListener('click',()=>{pickerEnchantSelection.permanent=0;drawChoices();inspection.textContent='Permanent enchant removed from selection. Apply to save.';});
 root.append(removePermanent);
 const currentTemp=pickerCompatible.enchants.find(e=>e.spellId===pickerEnchantSelection.temporary&&e.isTemporary);
 const temporaryNote=document.createElement('p');temporaryNote.className='inline-note';temporaryNote.textContent=currentTemp?
   'Temporary: '+currentTemp.name+' (managed under Buffs & Consumables)':
   'Temporary weapon enhancements are managed under Buffs & Consumables.';
 // Read-only status only; this dialog owns permanent enchant management.
 root.append(temporaryNote);
 function drawChoices(){
  fields.replaceChildren();
  const group=document.createElement('fieldset');group.className='enchant-group';
  const legend=document.createElement('legend');legend.textContent='Permanent enchants';group.append(legend);
  const options=document.createElement('div');options.className='enchant-options';
  const matches=pickerCompatible.enchants.filter(e=>!e.isTemporary&&
   (e.name.toLowerCase().includes(search.value.trim().toLowerCase())||String(e.spellId).includes(search.value.trim())));
  for(const e of matches){
   const button=document.createElement('button');button.type='button';button.className='enchant-tile';button.dataset.enchantSpellId=String(e.spellId);
   const selected=pickerEnchantSelection.permanent===e.spellId;button.classList.toggle('selected',selected);button.setAttribute('aria-pressed',String(selected));
   const details=[e.name,Object.entries(e.stats||{}).map(([k,v])=>human(k)+' '+v).join(' · '),e.verificationStatus||e.sourceStatus||''].filter(Boolean).join(' · ');
   button.title=details;button.dataset.tooltip=details;button.setAttribute('aria-label',e.name+' permanent enchant');
   const icon=document.createElement('span');icon.className='enchant-icon';icon.textContent='✦';
   const copy=document.createElement('span');copy.className='enchant-copy';
   const name=document.createElement('strong');name.textContent=e.name;
   const summary=document.createElement('small');summary.textContent=Object.entries(e.stats||{}).map(([k,v])=>human(k)+' '+v).join(' · ')||'Effect details';
   copy.append(name,summary);
   const marker=document.createElement('span');marker.className='enchant-selected-mark';marker.textContent=selected?'✓':'';marker.setAttribute('aria-hidden','true');
   button.append(icon,copy,marker);attachSourceIndicators(button,e);
   button.addEventListener('click',()=>{pickerEnchantSelection.permanent=e.spellId;drawChoices();inspect(e,false);});
   options.append(button);
  }
  if(!matches.length){const note=document.createElement('p');note.className='inline-note';note.textContent='No matching permanent enchant';options.append(note);}
  group.append(options);fields.append(group);
 }
 search.addEventListener('input',drawChoices);
 drawChoices();layout.append(fields,inspection);root.append(layout);
 const apply=document.createElement('button');apply.type='button';apply.className='simulate';apply.textContent='Apply enchants';apply.addEventListener('click',()=>commitPicker(item));root.append(apply);
}
// Shared source confidence and mechanical execution disclosure, reusable by talents.
function sourceStatusInfo(entry){
 const source=String(entry?.verificationStatus||entry?.visualSourceStatus||entry?.sourceStatus||'').toLowerCase();
 const exact=String(entry?.dataConfidence||'').toLowerCase();
 const confidence=['verified','provisional','unknown'].includes(exact)?exact:
  /reviewed-forever-in-game|client-db2-reviewed/.test(source)?'verified':
  entry?.sourceBuild&&entry?.sourceUrl?'provisional':'unknown';
 let mechanics=String(entry?.implementationStatus||'').toLowerCase();
 if(!['fully-implemented','partially-implemented','not-implemented'].includes(mechanics)){
  mechanics=entry?.modeled===false?'not-implemented':'fully-implemented';
  if(Array.isArray(entry?.effects)&&entry.effects.some(x=>x.executionStatus!=='modeled'))
   mechanics='partially-implemented';
  if(entry?.setKey&&!['devilsaur-armor','dalrends-arms'].includes(entry.setKey))
   mechanics='partially-implemented';
 }
 const origin=[entry?.sourceBuild?'Build: '+entry.sourceBuild:null,
  entry?.snapshotDate?'Snapshot: '+entry.snapshotDate:null,
  entry?.sourceReviewedAt?'Reviewed: '+entry.sourceReviewedAt:null,
  entry?.sourceUrl?'Source: '+entry.sourceUrl:null].filter(Boolean).join('\n');
 const notes=[];
 if(confidence==='provisional')notes.push({kind:'confidence',mark:'i',text:
  'This value comes from published or datamined WoW Forever information and has not been independently verified in the current build. It may change.\n'+origin});
 if(confidence==='unknown')notes.push({kind:'confidence',mark:'?',text:
  'Source confidence is unknown for one or more values. Missing numeric properties are not invented.\n'+origin});
 if(mechanics!=='fully-implemented')notes.push({kind:'mechanics',mark:'!',text:
  mechanics==='not-implemented'?
   'This effect is not executed by the simulator; no unsupported bonus is granted.\n'+origin:
   'Some effects are not implemented. Supported individual statistics are included, but unavailable procs or set bonuses do not execute.\n'+origin});
 return notes;
}
function attachSourceIndicators(parent,entry,interactive=false){
 const messages=sourceStatusInfo(entry);
 if(!messages.length)return;
 const group=document.createElement('span');group.className='source-indicators';
 for(const note of messages){
  const token=document.createElement(interactive?'button':'span');
  if(interactive){token.type='button';token.setAttribute('aria-expanded','false');}
  token.className='source-indicator '+note.kind;token.textContent=note.mark;
  token.title=note.text;token.setAttribute('aria-label',note.kind==='confidence'?
   'Data confidence details: '+note.text:'Implementation status details: '+note.text);
  if(interactive){
   const popup=document.createElement('span');popup.className='source-indicator-popup';
   popup.setAttribute('role','tooltip');popup.textContent=note.text;
   token.addEventListener('click',event=>{event.stopPropagation();
    const show=token.getAttribute('aria-expanded')!=='true';
    token.setAttribute('aria-expanded',String(show));
   });
   token.addEventListener('keydown',event=>{if(event.key==='Escape')token.setAttribute('aria-expanded','false');});
   token.append(popup);
  }
  group.append(token);
 }
 parent.append(group);
}
// Delegate once so modifier/enchant rerenders do not retain old tile listeners.
// Preserve the real CSS pseudo-content and native hover/focus visibility.
function installResponsiveTooltips(root=document,viewport=window){
 const selector='.modifier-tile[data-tooltip]:hover,.modifier-tile[data-tooltip]:focus-visible,.enchant-tile[data-tooltip]:hover,.enchant-tile[data-tooltip]:focus-visible';
 let pending=false;
 function update(){
  const width=root.documentElement.clientWidth||viewport.innerWidth;
  for(const tile of root.querySelectorAll(selector)){
   if(!tile.isConnected)continue;
   const available=Math.max(1,width-16)+'px';
   if(tile.style.getPropertyValue('--tooltip-available-width')!==available)tile.style.setProperty('--tooltip-available-width',available);
   const pseudo=viewport.getComputedStyle(tile,'::after');
   if(pseudo.display==='none')continue;
   const number=key=>Number.parseFloat(pseudo[key]);
   const outer=number('width')+(pseudo.boxSizing==='border-box'?0:number('paddingLeft')+number('paddingRight')+number('borderLeftWidth')+number('borderRightWidth'));
   const origin=tile.getBoundingClientRect().left+Number.parseFloat(viewport.getComputedStyle(tile).borderLeftWidth);
   if(!Number.isFinite(outer)||outer<=0||!Number.isFinite(origin))continue;
   const left=Math.max(8,Math.min(origin,width-8-outer));
   const value=(left-origin)+'px';
   if(tile.style.getPropertyValue('--tooltip-left')!==value)tile.style.setProperty('--tooltip-left',value);
  }
 }
 // Entry and replacement must be positioned before another native input or
 // layout read; only scroll/resize work waits for a coalesced frame. Direct
 // updates never clear the pending flag owned by that queued callback.
 function schedule(){if(!pending){pending=true;viewport.requestAnimationFrame(()=>{pending=false;update();});}}
 for(const event of ['mouseover','mouseout','focusin','focusout','click'])root.addEventListener(event,update);
 root.addEventListener('scroll',schedule,{capture:true,passive:true});
 viewport.addEventListener('resize',schedule,{passive:true});
}
installResponsiveTooltips();
function modifierTooltip(m){
 const describe=stats=>Object.entries(stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ');
 const stats=m.sharedStatGroup?['Independent: '+(describe(m.independentStats)||'None'),
  'Shared elixir critical chance (counted once across selected elixirs): '+describe(m.sharedStats)].join('\n'):
  describe(m.stats)||'No stat effect listed';
 return [m.name,stats,m.durationSeconds?'Duration: '+m.durationSeconds+' seconds':'Duration: engine-defined',
  m.exclusiveGroup?'Stacking: '+m.exclusiveGroup:'',m.verificationStatus||'Source not verified',
  m.modeled===false?'Not executed by simulator':'Modeled effect; server validates selection'].filter(Boolean).join('\n');
}
function modifierTile({name,selected,onToggle,tooltip,disabled=false,iconKey=null,sourceEntry=null}){
 const button=document.createElement('button');button.type='button';button.className='modifier-tile';
 button.setAttribute('aria-label',name);button.setAttribute('aria-pressed',String(selected));
 button.classList.toggle('selected',selected);button.classList.toggle('unmodeled',disabled);
 button.title=tooltip;button.dataset.tooltip=tooltip;button.disabled=disabled;
 const symbol=document.createElement('span');symbol.className='effect-icon';symbol.textContent='✧';symbol.setAttribute('aria-hidden','true');
 if(iconKey&&/^[a-z0-9_]+$/.test(iconKey)&&availableIconKeys.has(iconKey)){
  const img=document.createElement('img');img.className='effect-art';img.alt='';img.src='/forever-icons/'+iconKey+'.jpg';
  img.addEventListener('error',()=>img.remove());symbol.append(img);
 }
 const check=document.createElement('span');check.className='modifier-selected-mark';check.textContent='✓';check.setAttribute('aria-hidden','true');
 button.append(symbol,check);if(sourceEntry)attachSourceIndicators(button,sourceEntry);
 button.addEventListener('click',()=>{onToggle(!button.classList.contains('selected'));});
 return button;
}
function renderMods(){
 queueStatRefresh();
 const root=$('modifiers');root.replaceChildren();
 if($('classKey').value==='priest'||isRogue()||isFeral()){const note=document.createElement('p');note.textContent=restrictedCasterName()+' external modifiers, combat items and racial actives are unavailable. No inventory or modifier selections are sent to this runner.';root.append(note);return;}
 const sections=[
  {title:'Flasks & elixirs',category:'consumable',match:m=>m.exclusiveGroup!=='well-fed-food',field:'consumables'},
  {title:'Food',category:'consumable',match:m=>m.exclusiveGroup==='well-fed-food',field:'consumables'},
  {title:'External buffs',category:'external-buff',field:'buffs'},
  {title:'Target debuffs',category:'target-debuff',field:'targetDebuffs'}
 ];
 for(const group of sections){
  const section=document.createElement('section');section.className='modifier-group modifier-tiles';
  const heading=document.createElement('h2');heading.textContent=group.title;section.append(heading);
  const tiles=document.createElement('div');tiles.className='modifier-tile-grid';
  for(const m of modifiers.filter(x=>x.category===group.category&&(!group.match||group.match(x)))){
   const tile=modifierTile({name:m.name,selected:cfg.effects[group.field].includes(m.key),tooltip:modifierTooltip(m),
    disabled:isEnhancement()||m.modeled===false||(isRestrictedCaster()&&(m.category==='target-debuff'||!Object.keys(m.stats||{}).length)),iconKey:m.iconKey||null,sourceEntry:m,onToggle:checked=>{
     const previous=cfg.effects[group.field];
     if(checked){
      const taken=modifiers.find(x=>x.exclusiveGroup&&x.exclusiveGroup===m.exclusiveGroup&&Object.values(cfg.effects).flat().includes(x.key)&&x.key!==m.key);
      if(taken){note('Cannot stack '+m.name+' with '+taken.name+': '+m.exclusiveGroup);return;}
      if(!previous.includes(m.key))cfg.effects[group.field]=[...previous,m.key];
     }else cfg.effects[group.field]=previous.filter(k=>k!==m.key);
     buildDirty=true;renderMods();queueStatRefresh();
    }});
   tile.dataset.modifierKey=m.key;tiles.append(tile);
  }
  section.append(tiles);root.append(section);
 }
 // Temporary weapon consumables use CP172's EXISTING temporary enchant field.
 // They are never duplicated as external static modifiers or permanent enchants.
 const temporaryItems=enchants.filter(e=>e.isTemporary&&e.modeled);
 if(temporaryItems.length){
  const section=document.createElement('section');section.className='modifier-group modifier-tiles';
  const heading=document.createElement('h2');heading.textContent='Temporary weapon consumables';section.append(heading);
  const tiles=document.createElement('div');tiles.className='modifier-tile-grid';
  for(const slot of ['main-hand','off-hand']){
   const equipped=cfg.equipment.items.find(x=>x.slotKey===slot);
   const item=catalog.find(x=>x.id===equipped?.itemId);
   if(!equipped||!item)continue;
   for(const enchant of temporaryItems){
    if(!enchant.allowedSlots?.includes(slot)||enchant.requiresWeapon&&!item.weapon||
      enchant.requiresTwoHandedWeapon&&item.weapon?.hand!=='two-hand')continue;
    const selected=equipped.temporaryEnchantSpellId===enchant.spellId;
    const tile=modifierTile({name:enchant.name+' on '+human(slot),selected,sourceEntry:enchant,
     tooltip:[enchant.name,'Weapon: '+human(slot),
       Object.entries(enchant.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · '),
       'Duration: '+enchant.durationSeconds+' seconds',
       enchant.verificationStatus||enchant.sourceStatus||'Source unverified',
       'Applied once via CP172 equipment resolution'].filter(Boolean).join('\n'),
     onToggle:async checked=>{
      const prior=clone(cfg.equipment);
      const entry=cfg.equipment.items.find(x=>x.slotKey===slot);
      if(checked)entry.temporaryEnchantSpellId=enchant.spellId;
      else delete entry.temporaryEnchantSpellId;
      try{await validateGear();buildDirty=true;renderGear();renderMods();}
      catch(error){cfg.equipment=prior;renderGear();renderMods();note('Weapon enhancement rejected: '+error.message);}
     }});
    tile.dataset.temporaryEnchant=String(enchant.spellId);
    tile.dataset.weaponSlot=slot;tiles.append(tile);
   }
  }
  if(!tiles.childElementCount){const p=document.createElement('p');p.className='inline-note';
   p.textContent='Equip a compatible weapon to use a supported temporary enhancement.';section.append(p);}
  else section.append(tiles);
  root.append(section);
 }
 const upkeep=document.createElement('label');upkeep.className='modifier-uptime-label';
 upkeep.textContent='External target debuff application';
 const upkeepSelect=document.createElement('select');upkeepSelect.id='targetDebuffMode';
 upkeepSelect.setAttribute('aria-label','External target debuff application');
 for(const [value,label] of [['maintained','Maintained by an external source'],['initial-only','Applied once; expires naturally']]){
  const choice=document.createElement('option');choice.value=value;choice.textContent=label;upkeepSelect.append(choice);
 }
 upkeepSelect.value=cfg.effects.targetDebuffMode||'maintained';upkeepSelect.disabled=isRestrictedCaster();
 upkeepSelect.addEventListener('change',()=>{cfg.effects.targetDebuffMode=upkeepSelect.value;buildDirty=true;queueStatRefresh();});
 upkeep.append(upkeepSelect);root.append(upkeep);
 if(request?.combatItems){
  const section=document.createElement('section');section.className='modifier-group modifier-tiles';
  const heading=document.createElement('h2');heading.textContent='Supported combat consumables';section.append(heading);
  const tiles=document.createElement('div');tiles.className='modifier-tile-grid';
  const options=[['manaRubyCharges','Mana Ruby'],['manaCitrineCharges','Mana Citrine'],['manaJadeCharges','Mana Jade'],['manaAgateCharges','Mana Agate'],['majorManaPotionCharges','Major Mana Potion'],['superiorManaPotionCharges','Superior Mana Potion'],['superiorHealingPotionCharges','Superior Healing Potion'],['minorHealthstoneCharges','Minor Healthstone'],['lesserHealthstoneCharges','Lesser Healthstone'],['healthstoneCharges','Healthstone'],['greaterHealthstoneCharges','Greater Healthstone'],['majorHealthstoneCharges','Major Healthstone'],['darkRuneCharges','Dark Rune'],['demonicRuneCharges','Demonic Rune'],['majorHealingPotionCharges','Major Healing Potion'],['mightyRagePotionCharges','Mighty Rage Potion'],['denseDynamiteCharges','Dense Dynamite'],['goblinSapperCharges','Goblin Sapper Charge']];
  const abilityKeys={manaRubyCharges:'mana-ruby',manaCitrineCharges:'mana-citrine',
   manaJadeCharges:'mana-jade',manaAgateCharges:'mana-agate',
   majorManaPotionCharges:'major-mana-potion',superiorManaPotionCharges:'superior-mana-potion',superiorHealingPotionCharges:'superior-healing-potion',minorHealthstoneCharges:'minor-healthstone',
   lesserHealthstoneCharges:'lesser-healthstone',healthstoneCharges:'healthstone',
   greaterHealthstoneCharges:'greater-healthstone',majorHealthstoneCharges:'major-healthstone',
   darkRuneCharges:'dark-rune',demonicRuneCharges:'demonic-rune',
   majorHealingPotionCharges:'major-healing-potion',mightyRagePotionCharges:'mighty-rage-potion',
   denseDynamiteCharges:'dense-dynamite',goblinSapperCharges:'goblin-sapper-charge'};
  const potionSources={superiorManaPotionCharges:'https://www.wowhead.com/forever/item=13443/superior-mana-potion',
   superiorHealingPotionCharges:'https://www.wowhead.com/forever/item=3928/superior-healing-potion'};
  for(const [key,name] of options){
   if(['manaRubyCharges','manaCitrineCharges','manaJadeCharges','manaAgateCharges'].includes(key)&&$('classKey').value!=='mage')continue;
   if(key==='mightyRagePotionCharges'&&$('classKey').value!=='warrior')continue;
   const itemKey=abilityKeys[key],quantityNow=Number(request.combatItems[key]||0);
   const disabled=(request.combatItems.disabledItemKeys||[]).includes(itemKey);
   const enabled=quantityNow>0&&!disabled;
   const wrapper=document.createElement('div');wrapper.className='combat-item-entry';
   const sourceEntry=potionSources[key]?{sourceUrl:potionSources[key],sourceBuild:'1.60.1',
    verificationStatus:'published-forever-potion-tooltip-provisional',dataConfidence:'provisional',
    implementationStatus:'fully-implemented'}:null;
   const tile=modifierTile({name,selected:enabled,sourceEntry,
    tooltip:name+'\\n'+(disabled?'Disabled despite available inventory.':
     quantityNow===0?'No prepared inventory.':'Enabled with '+quantityNow+' available uses.')+
     '\\nActual use requires engine-owned personal/shared cooldown and legal resource thresholds.',
    onToggle:checked=>{
     const disabledKeys=new Set(request.combatItems.disabledItemKeys||[]);
     if(checked){
      if(Number(request.combatItems[key]||0)===0)request.combatItems[key]=1;
      disabledKeys.delete(itemKey);
     }else disabledKeys.add(itemKey);
     request.combatItems.disabledItemKeys=[...disabledKeys].sort();
     buildDirty=true;renderMods();
    }});
   tile.dataset.combatItem=key;wrapper.append(tile);
   const quantity=document.createElement('input');quantity.type='number';quantity.min='0';
   quantity.max=(key.toLowerCase().includes('healthstone')||['manaRubyCharges','manaCitrineCharges','manaJadeCharges','manaAgateCharges'].includes(key))?'1':'20';
   quantity.step='1';quantity.value=String(quantityNow);
   quantity.setAttribute('aria-label',name+' available charges');
   quantity.title=name+' inventory (0 means unavailable; a stocked item can also be disabled)';
   quantity.addEventListener('change',()=>{
    const value=Number(quantity.value);
    if(!Number.isInteger(value)||value<0||value>Number(quantity.max)){
     quantity.value=String(request.combatItems[key]||0);note('Consumable quantity must be 0–'+quantity.max+'.');return;
    }
    request.combatItems[key]=value;buildDirty=true;renderMods();
   });
   const status=document.createElement('small');status.className='combat-item-status';
   status.textContent=disabled?'Disabled'+(quantityNow>0?' · '+quantityNow+' stocked':' · no stock'):
    quantityNow===0?'No inventory':'Enabled · '+quantityNow+' available';
   wrapper.append(status);
   wrapper.append(quantity);tiles.append(wrapper);
  }
  section.append(tiles);
  const skill=document.createElement('label');skill.textContent='Engineering skill (explosives require 205/250)';
  skill.className='tree-points';const field=document.createElement('input');field.type='number';field.min=0;field.max=300;
  field.value=request.combatItems.engineeringSkill||0;
  field.addEventListener('change',()=>{const v=Number(field.value);if(Number.isInteger(v)&&v>=0&&v<=300){request.combatItems.engineeringSkill=v;buildDirty=true;}});
  skill.append(field);section.append(skill);root.append(section);
 }
 const racial=document.createElement('section');racial.className='modifier-group modifier-tiles';
 const title=document.createElement('h2');title.textContent='Relevant racial actives';racial.append(title);
 const tiles=document.createElement('div');tiles.className='modifier-tile-grid';
 const checkbox=$('racialActive');
 tiles.append(modifierTile({name:'Use supported racial active ability',selected:checkbox.checked,disabled:isRestrictedCaster(),
 tooltip:'Use supported racial active ability\nActual racial availability and execution are determined by the selected race and simulator.',
 onToggle:checked=>{checkbox.checked=checked;checkbox.dispatchEvent(new Event('change',{bubbles:true}));buildDirty=true;renderMods();}}));
 racial.append(tiles);root.append(racial);
}
async function validateGear(){
 if(isRestrictedCaster()){if(cfg.equipment.items.length)throw Error(restrictedCasterName()+' catalog equipment remains pending class eligibility evidence.');return {normalizedLoadout:{items:[]},unmodeledEquippedEffects:[],sourceStatus:'equipment-pending-evidence'};}
 try{const r=await post('/api/forever/equipment/resolve',{classKey:$('classKey').value,level:60,race:$('race').value,equipment:cfg.equipment});
 $('gearStatus').textContent='Server validated '+r.normalizedLoadout.items.length+' items; '+(r.unmodeledEquippedEffects?.length||0)+' unmodeled effects.';return r;
 }catch(e){$('gearStatus').textContent=e.message;throw e;}
}
// The stat snapshot is computed only by the backend. Token checks prevent a
// slower previous request from overwriting newer race, gear or modifier data.
function queueStatRefresh(){
 if(isFeral()){++statsRefreshId;invalidateFeralPreview();}
 if(isCombatRogue()){++statsRefreshId;invalidateCombatPreview();setBuildPreparing(buildPreparationPending);}
 if(!request||!baselineAvailable||loadedClass!==$('classKey').value||loadedSpec!==$('spec').value||
    !$('race').value)return;
 if(statsRefreshTimer!==null)clearTimeout(statsRefreshTimer);
 const token=++statsRefreshId;
 $('statStatus').textContent='Updating…';
 statsRefreshTimer=setTimeout(async()=>{
  statsRefreshTimer=null;
  try{await preview(token);}
  catch(error){if(token===statsRefreshId){
   $('statStatus').textContent='Unavailable: '+(error.message||error);
   $('statStatus').dataset.lastError=String(error.message||error);
   $('statStatus').dataset.failureCount=String(1+Number($('statStatus').dataset.failureCount||0));
  }}
 },180);
}
async function preview(refreshToken){
 if(isFeral())return previewFeral(refreshToken);
 if(isCombatRogue())return previewCombatRogue(refreshToken);
 if(statsRefreshTimer!==null){clearTimeout(statsRefreshTimer);statsRefreshTimer=null;}
 const token=refreshToken??++statsRefreshId,previewGeneration=generation,previewClass=$('classKey').value,previewSpec=$('spec').value,previewRace=$('race').value,previewProvisional=$('provisional').checked;
 const character=buildCharacter();
 if(request?.talents?.ranks){
  character.talents={selections:Object.entries(request.talents.ranks)
   .filter(([,rank])=>rank>0).map(([talentKey,rank])=>({talentKey,rank}))};
 }
 const response=isRestrictedCaster()?await post('/api/forever/character/preview-'+typedKey($('classKey').value,$('spec').value),body()[typedKey($('classKey').value,$('spec').value)]):await post('/api/forever/character/generate-level60',{
  character,modifiers:mods(),useReviewedBaseline:!validMode(),useProvisionalBaseline:validMode()
 });
 if(token!==statsRefreshId||previewGeneration!==generation||previewClass!==$('classKey').value||previewSpec!==$('spec').value||previewRace!==$('race').value||previewProvisional!==$('provisional').checked)return response;
 const stats=response.effectiveStats||{};
 const values=stats.values||stats;
 $('stats').replaceChildren();
 for(const [key,value] of Object.entries(values)){
  if(value==null||typeof value==='object')continue;
  const el=document.createElement('div');const name=document.createElement('span');name.textContent=isRogue()&&key==='energy'?'Maximum Energy':isRogue()&&key==='health'?'Maximum player Health':human(key);
  const number=document.createElement('strong');number.textContent=isRestrictedCaster()?formatResultNumber(value,key.includes('percent')?'percent':'duration'):String(value);el.append(name,number);
  $('stats').append(el);
 }
 if(isRogue()){
  const row=document.createElement('div'),label=document.createElement('span'),value=document.createElement('strong');
  label.textContent='Starting Energy';value.textContent=formatResultNumber(response.startingEnergy,'duration');row.append(label,value);$('stats').append(row);
 }
 $('statScope').textContent=response.statPreviewScope||'';
 $('statScope').hidden=!response.statPreviewScope;
 $('contributions').textContent=pretty(response.statPreviewScope?
  {sources:response.contributions||[],previewAdjustments:response.statPreviewAdjustments||{},modelingNotes:response.modelingNotes||[]}:
  response.contributions||[]);
 $('baselineStatus').textContent='Baseline: '+response.sourceStatus;
 $('statStatus').textContent='Up to date';
 return response;
}
async function simulate(){
 const b=body(),build=clone(saveConfig()),runGeneration=generation,runTalents=clone(request.talents||{}),runCatalog=clone(talentCatalog),runItemizedMode=itemizedMode;
 const ensureUnchanged=()=>{if(runGeneration!==generation||JSON.stringify(body())!==JSON.stringify(b)||JSON.stringify(saveConfig())!==JSON.stringify(build))throw Error('Build changed while simulation was pending; no mismatched report was published. Run again with the selected build.');};
 const iterations=Number($('iterations').value);
 if(isRestrictedCaster()&&iterations!==1)throw Error(restrictedCasterName()+' supports one deterministic run only.');
 if(!Number.isInteger(iterations)||iterations<1||iterations>100)
  throw Error('Choose between 1 and 100 real seeded iterations.');
 await validateTalentSelection();
 const gearValidation=await validateGear();const resolved=await preview();
 const metadata=await api('/api/forever/character/report-metadata');
 ensureUnchanged();
 note('Simulating…');$('run').disabled=true;
 try{
 const batch=iterations>1?await post('/api/forever/shared/simulate-iterations',{
  configuration:b,iterations
 }):null;
 const response=batch?batch.representative:await post('/api/forever/character/simulate-level60',b);
 ensureUnchanged();
 const data=response.sim||{};
 results={
  schema:'warcraftsim.solo-dps-report',version:1,metadata,build,
  originalSimulationRequest:clone(b),
  iterationStatistics:batch?clone(batch):null,
  talentAllocation:{
   mode:runItemizedMode?'strict-rank-derived':'legacy-typed-provisional',
   unspentPoints:Math.max(0,51-Object.values(runTalents?.ranks||{}).reduce((sum,value)=>sum+Number(value||0),0)),
   totalSelectedRanks:Object.values(runTalents?.ranks||{}).reduce((sum,value)=>sum+Number(value||0),0),
   selected:runCatalog.filter(t=>Number(runTalents?.ranks?.[t.key]||0)>0)
    .map(t=>({key:t.key,name:t.name,tree:t.tree,rank:runTalents.ranks[t.key],
      maxRank:t.maxRank,row:t.row,column:t.column,modeled:t.modeled!==false,
      ...(b.specialization==='feral'||b.classKey==='rogue'&&b.specialization==='combat'?{simulationStatus:t.simulationStatus||null,dataConfidence:t.dataConfidence||null,knownLimitations:clone(t.knownLimitations||null)}:{}),
      provenance:t.visualSourceStatus||t.sourceStatus||t.iconStatus||'unresolved',
      sourceCommit:t.visualSourceCommit||null,sourceUrl:t.visualSourceUrl||null,
      historicalValidationSourceStatus:t.historicalValidationSourceStatus||null,
      historicalValidationSourceCommit:t.historicalValidationSourceCommit||null,
      historicalValidationSourceUrl:t.historicalValidationSourceStatus?t.sourceUrl||null:null,
      prerequisite:t.enginePrerequisiteKey||null})),
   treePoints:Object.fromEntries(Object.entries(runTalents||{}).filter(([k])=>k.endsWith('PointsSpent')))
  },
  validatedLoadout:clone(gearValidation),

  resolvedCharacter:clone(b.specialization==='feral'?response.preparation:b.classKey==='rogue'&&b.specialization==='combat'?response.preparation.preview:resolved),
  baselineProvenance:clone(response.baseline||resolved.baseline||null),
  ...(b.specialization==='feral'?{feralPreparation:clone(response.preparation),inputTiming:clone(response.inputTiming),capabilityNotes:clone(feralCapabilityNotes)}:{}),
  ...(b.classKey==='rogue'&&b.specialization==='combat'?{combatPreparation:clone(response.preparation),inputTiming:clone(response.inputTiming),capabilityNotes:clone(combatCapabilityNotes)}:{}),
  combatSummary:{
    dps:data.dps??null,damageDone:data.damageDone??null,
    ownerDamage:data.hunterDamage??data.warlockDamage??(['mage','shaman','druid','priest','rogue'].includes(b.classKey)?data.damageDone:null),petDamage:data.petDamage??(['mage','rogue'].includes(b.classKey)?0:null),hawkDamage:data.hawkDamage??null,
    damageByAbility:clone(data.damageByAbility||{}),abilityMetrics:clone(data.run?.summary?.abilities||{}),
    combatItemUses:['priest','rogue'].includes(b.classKey)?null:clone(data.combatItemUses||{}),consumableDecisions:['priest','rogue'].includes(b.classKey)?null:clone(data.combatItemDecisionReasons||[]),
    timelineEventCount:data.run?.timeline?.length??null,
    actorKey:b.classKey,maximumMana:data.maximumMana??null,endingMana:data.endingMana??null,
    maximumHealth:data.maximumHealth??null,endingHealth:data.endingHealth??null,
    maximumEnergy:data.maximumEnergy??null,startingEnergy:data.run?.summary?.actorSummaries?.rogue?.startingResources?.energy??null,endingEnergy:data.endingEnergy??null,
    targetSemantics:b.specialization==='feral'?{level:63,mitigation:'physical-armor-bleeds-unmitigated',targetArmor:b.feral.targetArmor,targetCount:1,initialHealthPercent:100}:b.classKey==='rogue'?{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:b.rogue.targetArmor,targetDodgePercent:b.rogue.targetDodgePercent,targetParryPercent:b.rogue.targetParryPercent,targetCount:1,initialHealthPercent:100}:b.classKey==='shaman'&&b.specialization==='enhancement'?{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:b.enhancement.targetArmor,targetCount:1,initialHealthPercent:100}:['shaman','druid','priest'].includes(b.classKey)?{level:63,mitigation:'unmitigated',targetCount:b.classKey==='shaman'?b.elemental.targetCount:b.classKey==='druid'?1+b.balance.additionalTargets:1,initialHealthPercent:b.classKey==='priest'?b.shadow.targetInitialHealthPercent:100}:null,
    ...(b.classKey==='rogue'&&b.specialization==='combat'?{attackPower:response.preparation.preview.attackPower,configuredRotation:clone(response.preparation.preview.rotation),inputTiming:clone(response.inputTiming)}:{}),
    ...(b.specialization==='feral'?feralCombatSummary(response):{})
  },
  warnings:[...clone(metadata.notes||[]),...clone(response.talentModelingNotes||[]),...clone(data.modelingNotes||[]),...clone(response.preparation?.modelingNotes||[]),...clone(response.preparation?.preview?.modelingNotes||[]),...(response.specialization==='feral'?clone(feralCapabilityNotes):[]),...(response.classKey==='rogue'&&response.specialization==='combat'?clone(combatCapabilityNotes):[]),...(response.inputTiming?.note?[response.inputTiming.note]:[]),
    ...runCatalog.filter(t=>Number(runTalents?.ranks?.[t.key]||0)>0).flatMap(t=>t.knownLimitations||[]),
    ...runCatalog.filter(t=>t.modeled===false&&Number(runTalents?.ranks?.[t.key]||0)>0)
      .map(t=>'Unmodeled talent '+t.key+' spends points but has no executable combat effect ('+
        (t.visualSourceStatus||'historical provisional')+').'),
    ...(!runItemizedMode&&Object.keys(runTalents||{}).some(key=>key.endsWith('PointsSpent'))?
      ['Legacy aggregate talent totals may be unitemized; inspect selected ranks before interpreting results.']:[])],
  rawServerResponse:clone(response)
 };
 const sim=response.sim||{},summary=sim.run?.summary||{};
 const distribution=$('distributionSummary');distribution.replaceChildren();
 distribution.hidden=!batch;
 $('distributionNotice').textContent=batch?
  'DPS statistics from '+batch.iterations+' independent seeded simulations. '+batch.standardDeviationMethod+'. '+batch.percentileMethod+'.':
  (isRestrictedCaster()?restrictedCasterName()+': one deterministic run; batch statistics unavailable.':'Single deterministic run. Select 2–100 iterations to compute a meaningful DPS distribution.');
 if(batch){
  const d=batch.dps;
  const rows=[
   ['Mean DPS',d.mean,'Arithmetic mean of sampled DPS values'],
   ['Standard deviation',d.standardDeviation,'Population standard deviation, sqrt(Σ(x−μ)²/N)'],
   ['DPS range (mean ± SD)',String(d.mean)+' ± '+String(d.standardDeviation),'Summary variability, not a confidence interval'],
   ['Median DPS',d.median,'50th percentile by linear interpolation'],
   ['95th percentile DPS',d.percentile95,'95th percentile of sampled DPS'],
   ['99th percentile DPS',d.percentile99,'99th percentile of sampled DPS'],
   ['Minimum DPS',d.minimum,'Lowest actual sampled DPS'],
   ['Maximum DPS',d.maximum,'Highest actual sampled DPS'],
   ['Iteration count',batch.iterations,'Each seed generated a fresh independent engine simulation'],
   ['Duration (seconds)',b[typedKey(b.classKey,b.specialization)].durationSeconds,'Duration used by each iteration']
  ];
  for(const [label,value,help] of rows){
   const div=document.createElement('div');div.title=help;
   const name=document.createElement('span');name.textContent=label;
   const number=document.createElement('strong');number.textContent=String(value);
   div.append(name,number);distribution.append(div);
  }
 }
 $('results').hidden=false;$('resultEmpty').hidden=true;$('exportResults').disabled=false;$('replayResult').disabled=false;$('metrics').replaceChildren();
 for(const [name,value] of Object.entries({DPS:sim.dps,Damage:sim.damageDone,Duration:b[typedKey(b.classKey,b.specialization)].durationSeconds,PetDamage:sim.petDamage,EndingMana:sim.endingMana,MaximumEnergy:sim.maximumEnergy,StartingEnergy:sim.run?.summary?.actorSummaries?.rogue?.startingResources?.energy,EndingEnergy:sim.endingEnergy,MaximumHealth:sim.maximumHealth,EndingHealth:sim.endingHealth})){
  if(value===undefined)continue;const el=document.createElement('div');el.textContent=name+': ';const strong=document.createElement('strong');strong.textContent=String(value);el.append(strong);$('metrics').append(el);
 }
 $('abilities').replaceChildren();
 const extras={OwnerDamage:sim.hunterDamage??sim.warlockDamage, PetDamage:sim.petDamage, DemonMeleeDamage:sim.demonMeleeDamage, DemonSpellDamage:sim.demonSpellDamage, ProcActivations:sim.missileBarrageProcs, EndingMana:sim.endingMana, MaximumMana:sim.maximumMana, Consumables:sim.combatItemUses,ConsumableDecisions:sim.combatItemDecisionReasons};
 for(const [name,value] of Object.entries(extras)){if(value==null)continue;const el=document.createElement('div');const label=document.createElement('span');label.textContent=name;const strong=document.createElement('strong');strong.textContent=typeof value==='object'?pretty(value):String(value);el.append(label,strong);$('metrics').append(el);}
 if(b.specialization==='feral')renderFeralResultMetrics(results.combatSummary);
 const damages=sim.damageByAbility||summary.damageDoneByAbility||{};
 for(const [name,damage] of Object.entries(damages).sort((a,b)=>b[1]-a[1])){const tr=document.createElement('tr');const a=summary.abilities?.[name]||{};for(const v of [name,a.castCompletedCount??'—',a.damageOccurrenceCount??'—',a.criticalDamageCount??'—',damage]){const td=document.createElement('td');td.textContent=String(v);tr.append(td);} $('abilities').append(tr);}
 $('raw').textContent=pretty(response);
 const timeline=sim.run?.timeline||[];
 $('eventLog').textContent=timeline.length?pretty(timeline.slice(0,200)):'No captured events. Enable timeline capture in Encounter.';
 navigate('results');
 note('Simulation completed using server results.');
 }finally{$('run').disabled=false;}
}
function replaySignature(result){
 if(result?.classKey==='rogue'&&result?.specialization==='combat')return SoloDpsCombatIdentity.signature(result);
 if(result?.specialization==='feral')return feralReplaySignature(result);
 const sim=result?.sim||{},r=sim.run||{};
 // Reuse the established Rogue refund acceptance projection: only fresh
 // engine identities are omitted, never gameplay timing, targets or amounts.
 const identityFields=new Set(['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId']);
 const timeline=(r.timeline||[]).map(event=>Object.fromEntries(Object.entries(event).filter(([key])=>!identityFields.has(key))));
 return JSON.stringify({
  damageDone:sim.damageDone??null,dps:sim.dps??null,
  damageByAbility:sim.damageByAbility??{},
  petDamage:sim.petDamage??null,
  abilitySummary:r.summary?.abilities??{},
  consumableUses:sim.combatItemUses??{},
  consumableDecisions:sim.combatItemDecisionReasons??[],
  maximumEnergy:sim.maximumEnergy??null,endingEnergy:sim.endingEnergy??null,maximumHealth:sim.maximumHealth??null,endingHealth:sim.endingHealth??null,
  actorSummaries:r.summary?.actorSummaries??{},timeline
 });
}
async function replayLastResult(){
 if(!results?.originalSimulationRequest)throw Error('Run a simulation before replaying.');
 if(results.iterationStatistics?.iterations>1){
  const repeated=await post('/api/forever/shared/simulate-iterations',{
   configuration:results.originalSimulationRequest,iterations:results.iterationStatistics.iterations
  });
  if(JSON.stringify(repeated.samples)!==JSON.stringify(results.iterationStatistics.samples)||
     repeated.representativeSeed!==results.iterationStatistics.representativeSeed)
   throw Error('Seeded DPS distribution differs from saved samples. Compare engine version, seed strategy and modifiers.');
  note('Repeated seeded distribution matched every sample and representative seed.');
  return;
 }
 const repeated=await post('/api/forever/character/simulate-level60',results.originalSimulationRequest);
 if(replaySignature(repeated)!==replaySignature(results.rawServerResponse))
  throw Error('Replay differs from saved seed/configuration. Preserve both reports for investigation.');
 note(results.originalSimulationRequest.classKey==='rogue'&&results.originalSimulationRequest.specialization==='combat'?'Deterministic Combat replay matched every parsed response field, preparation, timing and ordered event with generated identity relationships preserved.':results.originalSimulationRequest.specialization==='feral'?'Deterministic Feral replay matched the complete preparation, timing, metrics and ordered events (generated IDs normalized).':'Deterministic replay matched damage, DPS, ability summary, pets and consumables.');
}
function saveConfig(){return {schema:'warcraftsim.solo-dps-build',version:1,itemizedMode,classKey:$('classKey').value,specialization:$('spec').value,race:$('race').value,provisional:$('provisional').checked,gear:cfg.equipment,modifiers:cfg.effects,
 preparedCombatConsumables:[
  ...Object.entries(request?.combatItems||{}).filter(([key,value])=>key.endsWith('Charges')&&Number(value)>0)
    .map(([key,charges])=>({key,charges:Number(charges)}))
 ],racialActive:$('racialActive').checked,encounter:{duration:$('duration').value,seed:$('seed').value,iterations:$('iterations').value,targetHealth:$('targetHealth').value,timeline:$('timeline').checked,petFamily:$('petFamily').value,demonType:$('demonType').value,companionEnabled:$('companionEnabled').checked,rotation:$('rotation').value,
 useArcanePower:!!request?.useArcanePower,usePresenceOfMind:!!request?.usePresenceOfMind,
 useMajorManaPotion:false,
 majorManaPotionCharges:0,
 customPriority:clone(request?.customPriority||[]),priorityRows:clone(priorityRows),hunterMelee:!!request?.useMeleeAuto,
 hunterParryEvery:request?.scriptedParryEverySeconds??null,
 hunterSurvival:$('classKey').value==='hunter'?hunterSurvivalSelection():undefined,
 rotationConditions:request?Object.fromEntries(['majorManaPotionBelowManaPercent','evocationBelowManaPercent','lifeTapBelowManaPercent','lifeTapAboveHealthPercent'].filter(k=>k in request).map(k=>[k,request[k]])):{},combatItems:clone(request?.combatItems||{})},talents:request?.talents};}
async function applyConfig(data){
 if(data?.schema==='warcraftsim.solo-dps-report')throw Error('A completed simulation report cannot be imported as a build; extract its embedded build first.');
 if(data?.schema!=='warcraftsim.solo-dps-build'||data.version!==1)throw Error('Unsupported build schema/version.');
 if(!specMap[data.classKey]?.includes(data.specialization))throw Error('Unsupported class/spec.');
 // Clear the last build's scripted targets BEFORE any class/preset load or
 // asynchronous priority/stat validation. The later import wrapper restores
 // the same incoming script after successful validation.
 soloScript=clone(data.encounter?.soloScript||{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]});
 $('classKey').value=data.classKey;await initClass();$('spec').value=data.specialization;await loadPreset();
 if(![...$('race').options].some(x=>x.value===data.race))throw Error('Race is not eligible for this class.');
 $('race').value=data.race;$('provisional').checked=!!data.provisional;
 if(data.classKey==='shaman'&&data.specialization==='enhancement'){Object.assign(request,clone(data.encounter.enhancement));restoreEnhancementSettings(request);}
 if(data.classKey==='rogue'){validateRogueSettings(data.encounter.rogue,request.tuning,data.specialization);restoreRogueSettings(data.encounter.rogue,data.specialization);Object.assign(request,clone(data.encounter.rogue));}
 if(data.classKey==='priest'){$('shadowTargetInitialHealth').value=data.encounter?.shadowTargetInitialHealth??100;$('shadowPriestHealth').value=data.encounter?.shadowPriestHealth??3500;}
 cfg.equipment=clone(data.gear||{items:[]});
 cfg.effects=clone(data.modifiers||{buffs:[],consumables:[],targetDebuffs:[]});
 cfg.effects.targetDebuffMode ||= 'maintained';
 if(['shaman','druid','priest'].includes(data.classKey))for(const key of [...cfg.effects.buffs,...cfg.effects.consumables]){const m=modifiers.find(x=>x.key===key);if(!m||m.modeled===false||!Object.keys(m.stats||{}).length)throw Error('Unsupported prepared caster static modifier: '+key);}
 if(data.itemizedMode!==true)throw Error('Legacy aggregate-point builds are not accepted in the level-60 builder. Load an itemized preset and export the ranked build.');
 if(request&&data.talents){
  request.talents=clone(data.talents);
  itemizedMode=true;
  recalculateTalentTotals();
  for(const [key,value] of Object.entries(data.talents))
   if(key.endsWith('PointsSpent')&&value!==request.talents[key])
    throw Error('Imported '+key+' differs from rank-derived points.');
  if(Object.keys(request.talents.ranks||{}).some(key=>!talentCatalog.some(t=>t.key===key)))
    throw Error('Imported build contains a talent outside the current class catalog.');
 }
 if(request&&data.encounter?.combatItems&&request.combatItems){
  if(data.classKey==='shaman'&&data.specialization==='enhancement')validateEnhancementCombatItems(data.encounter.combatItems,request.combatItems);
  request.combatItems=clone(data.encounter.combatItems);
 }
 if(request&&data.classKey==="hunter"){
  restoreHunterSurvivalSelection(data.encounter?.hunterSurvival);
  const melee=data.encounter?.hunterMelee??data.encounter?.hunterSurvival?.useMeleeAuto;
  const parry=data.encounter?.hunterParryEvery===undefined?
   data.encounter?.hunterSurvival?.scriptedParryEverySeconds:data.encounter.hunterParryEvery;
  if(melee!==undefined&&typeof melee!=="boolean")
   throw Error("Hunter melee engagement must be a boolean.");
  if(parry!==undefined&&parry!==null &&
     (!Number.isFinite(Number(parry))||Number(parry)<=0||Number(parry)>120))
   throw Error("Hunter scripted parry interval is invalid.");
  if(melee!==undefined && "useMeleeAuto" in request)request.useMeleeAuto=melee;
  if("scriptedParryEverySeconds" in request)
   request.scriptedParryEverySeconds=parry==null?null:Number(parry);
 }if(request&&data.classKey==='mage'){
  // Old exports contain no prepared Major Mana Potion selection; default OFF.
  const charges=Number(data.encounter?.majorManaPotionCharges??0);
  if(!Number.isInteger(charges)||charges<0||charges>20)throw Error('Invalid prepared Mana Potion quantity.');
  request.majorManaPotionCharges=0;request.useMajorManaPotion=false;
  if(charges>0&&data.encounter?.useMajorManaPotion===true)
   request.combatItems.majorManaPotionCharges=charges;
  for(const [flag,talent] of [['useArcanePower','arcane-power'],['usePresenceOfMind','presence-of-mind']]){
   const learned=Number(request.talents?.ranks?.[talent]||0)>0;
   request[flag]=learned&&(data.encounter?.[flag]===undefined?true:data.encounter[flag]===true);
  }
 }if(request&&data.encounter?.rotationConditions){const permitted=data.classKey==='mage'?['majorManaPotionBelowManaPercent','evocationBelowManaPercent']:data.classKey==='warlock'?['lifeTapBelowManaPercent','lifeTapAboveHealthPercent']:[];for(const [key,value] of Object.entries(data.encounter.rotationConditions)){if(!permitted.includes(key)||!(key in request)||typeof value!=='number'||!Number.isFinite(value)||value<0||value>100)throw Error('Unsupported or invalid rotation condition: '+key);request[key]=value;}renderPriorityConditions();}if(request&&Array.isArray(data.encounter?.customPriority)){request.customPriority=clone(data.encounter.customPriority);loadPriorityRows(request.customPriority);if(Array.isArray(data.encounter.priorityRows)&&data.encounter.priorityRows.every(x=>typeof x.key==='string'&&typeof x.enabled==='boolean')){priorityRows=clone(data.encounter.priorityRows);drawPriorityRows();}syncPriorityText();}
 $('racialActive').checked=!!data.racialActive;
 // Restore explicit demon assumptions before custom-priority preflight calls body().
 if(data.classKey==='warlock'){
  const settings=data.encounter?.classSettings||{};
  for(const key of ['allowTemporaryDemon','allowDoomguardRitual'])
   if(settings[key]!==undefined&&typeof settings[key]!=='boolean')throw Error('Imported demon assumptions must be explicit booleans.');
  $('warlockTemporary').checked=settings.allowTemporaryDemon===true;
  $('warlockRitual').checked=settings.allowDoomguardRitual===true;
 }
 for(const [key,id] of Object.entries({duration:'duration',seed:'seed',iterations:'iterations',targetHealth:'targetHealth',timeline:'timeline',petFamily:'petFamily',demonType:'demonType',companionEnabled:'companionEnabled',rotation:'rotation'})){if(data.encounter?.[key]!==undefined){if(id==='timeline'||id==='companionEnabled')$(id).checked=!!data.encounter[key];else $(id).value=data.encounter[key];}}
 await validateTalentSelection();
 // A valid imported talent allocation is not enough if the saved custom
 // priority still refers to an unlearned spell. Validate against the
 // existing server-owned rotation builder before accepting the import.
 if(['mage','warlock','hunter','shaman','druid','priest'].includes(data.classKey) && request?.customPriority?.length){
   const submitted=body();
   const importedKey=typedKey(data.classKey,data.specialization);
   // The wrapper which imports the new encounter script runs AFTER this
   // preflight. Do not send the previous build's scripted add targets as
   // part of this build's rotation validation.
   if(!['shaman','druid','priest'].includes(data.classKey))submitted[importedKey].soloEncounter=clone(
     data.encounter?.soloScript||{enabled:false,startingHealthPercent:100,bossHealthMilestones:[]});
   // Preview here is ONLY a learned-action eligibility gate.
   // A scripted add such as target-1 does not exist in the preview actor
   // roster until the actual encounter is constructed. Keep all imported
   // AND/OR predicates and explicit target keys in the saved request, and
   // let the real simulation validate/execute those target conditions.
   // Never allow the prior build's conditional rules to leak into preview.
   if(!['shaman','druid','priest'].includes(data.classKey))submitted[importedKey].priorityConditions=[];
   await post('/api/forever/rotation/preview',submitted);
 }
 if(data.classKey==='rogue')await post('/api/forever/character/preview-rogue',body().rogue);
 if(data.classKey==='shaman'&&data.specialization==='enhancement')await post('/api/forever/character/preview-enhancement',body().enhancement);
 await validateGear();
 renderGear();renderMods();renderTalents();renderPriorityConditions();updateIdentity();buildDirty=false;await checkBaseline();queueStatRefresh();
}
const hunterSurvivalFlags=[
 ['applyHuntersMark',"Precast Hunter's Mark on the primary target"],
 ['useMeleeAuto','Engage in melee (required for Mongoose Bite and Wing Clip)'],
 ['useMongooseBite','Automatic Mongoose Bite when a real opportunity exists'],
 ['useWingClip','Automatic Wing Clip'],['useFireTraps','Automatic Fire trap'],
 ['useFrostTraps','Automatic Frost trap'],['triggerTraps','Allow configured trap triggers'],
 ['triggerRangedTraps','Target explicitly enters trap proximity while Hunter is ranged'],
 ['bossImmuneToRoots','Primary target is immune to Entrapment roots']
];
const hunterSurvivalNumbers=[
 ['trapTriggerDelaySeconds','Trap trigger delay (seconds)',0,1200,.05],
 ['trapAttackPowerCoefficient','Trap AP coefficient (provisional)',0,10,.01],
 ['scriptedParryEverySeconds','Scripted incoming parry interval (blank disables)',0.01,120,.25]
];
const hunterSurvivalKinds=[['fireTrapKind','Fire trap',['explosive','immolation']],
 ['frostTrapKind','Frost trap',['frost','freezing']]];
function hunterSurvivalSelection(){
 return request?Object.fromEntries([...hunterSurvivalFlags,...hunterSurvivalNumbers,...hunterSurvivalKinds]
  .filter(([key])=>key in request).map(([key])=>[key,request[key]])):{};
}
function restoreHunterSurvivalSelection(saved){
 if(saved===undefined)return;
 if(!saved||typeof saved!=='object'||Array.isArray(saved))throw Error('Invalid Hunter Survival action settings.');
 const known=new Set([...hunterSurvivalFlags,...hunterSurvivalNumbers,...hunterSurvivalKinds].map(([key])=>key));
 for(const key of Object.keys(saved))if(!known.has(key)||!(key in request))throw Error('Unavailable Hunter setting: '+key);
 for(const [key] of hunterSurvivalFlags)if(key in saved&&typeof saved[key]!=='boolean')throw Error('Invalid Hunter toggle: '+key);
 for(const [key,,min,max] of hunterSurvivalNumbers)if(key in saved){
  const value=saved[key];if(key==='scriptedParryEverySeconds'&&value===null)continue;
  if(typeof value!=='number'||!Number.isFinite(value)||value<min||value>max)throw Error('Invalid Hunter numeric setting: '+key);
 }
 for(const [key,,values] of hunterSurvivalKinds)if(key in saved&&!values.includes(saved[key]))throw Error('Invalid Hunter trap kind: '+key);
 Object.assign(request,clone(saved));
}
function renderHunterSurvivalControls(root){
 const hint=document.createElement('p');hint.className='source-note';
 hint.textContent='Mongoose Bite needs melee engagement and a marked-target Expose Prey proc or actual avoidance opportunity. Trap placement alone does not imply a trigger. Automatic-action toggles do not remove explicitly selected custom actions. Spatial movement and enemy control AI remain unmodeled.';
 root.append(hint);
 const changed=()=>{buildDirty=true;$('priorityStatus').textContent='Hunter action settings changed. Preview or Apply validates the engine priority.';};
 for(const [key,name] of hunterSurvivalFlags){
  if(!(key in request))continue;
  const label=document.createElement('label');label.className='check-row';
  const field=document.createElement('input');field.type='checkbox';field.checked=request[key]===true;
  field.id='hunter-'+key;field.setAttribute('aria-label',name);
  field.addEventListener('change',()=>{request[key]=field.checked;changed();});
  label.append(field,document.createTextNode(' '+name));root.append(label);
 }
 for(const [key,name,min,max,step] of hunterSurvivalNumbers){
  if(!(key in request))continue;
  const label=document.createElement('label');label.textContent=name;
  const field=document.createElement('input');field.type='number';field.min=min;field.max=max;field.step=step;
  field.value=request[key]??'';field.id='hunter-'+key;field.setAttribute('aria-label',name);
  field.addEventListener('change',()=>{
   const value=key==='scriptedParryEverySeconds'&&field.value===''?null:Number(field.value);
   if(value!==null&&(!Number.isFinite(value)||value<min||value>max)){field.value=request[key]??'';note('Invalid '+name+'.');return;}
   request[key]=value;changed();
  });label.append(field);root.append(label);
 }
 for(const [key,name,values] of hunterSurvivalKinds){
  if(!(key in request))continue;
  const label=document.createElement('label');label.textContent=name;
  const field=document.createElement('select');field.id='hunter-'+key;field.setAttribute('aria-label',name);
  field.replaceChildren(...values.map(value=>option(value,human(value))));field.value=request[key];
  field.addEventListener('change',()=>{request[key]=field.value;changed();});label.append(field);root.append(label);
 }
}
function renderPriorityConditions(){
 const root=$('priorityConditions');root.replaceChildren();if(!request)return;
 const cls=$('classKey').value;
 if(cls==='hunter')renderHunterSurvivalControls(root);
 const fields=cls==='mage'?[
  ['majorManaPotionBelowManaPercent','Mana potion below mana (%)'],
  ['evocationBelowManaPercent','Evocation below mana (%)']
 ]:cls==='warlock'?[
  ['lifeTapBelowManaPercent','Life Tap below mana (%)'],
  ['lifeTapAboveHealthPercent','Life Tap above health (%)']
 ]:[];
 if(cls==='mage'){
  for(const [key,name,talent] of [['useArcanePower','Use learned Arcane Power','arcane-power'],
   ['usePresenceOfMind','Use learned Presence of Mind','presence-of-mind']]){
   if(Number(request.talents?.ranks?.[talent]||0)===0)continue;
   const label=document.createElement('label');label.className='check-row';
   const control=document.createElement('input');control.type='checkbox';control.checked=!!request[key];
   control.id=key;control.setAttribute('aria-label',name);
   control.addEventListener('change',async()=>{const prior=request[key];request[key]=control.checked;
    try{await previewPriority();buildDirty=true;}
    catch(error){request[key]=prior;control.checked=prior;note('Cooldown priority rejected: '+error.message);}
   });
   label.append(control,document.createTextNode(' '+name));root.append(label);
  }
 }
 for(const [key,label] of fields){
   if(!(key in request))continue;
   const wrapper=document.createElement('label');wrapper.textContent=label;
   const field=document.createElement('input');field.type='number';field.min='0';field.max='100';field.step='1';field.value=request[key];
   field.setAttribute('aria-label',label);
   field.addEventListener('change',async()=>{
     const value=Number(field.value),previous=request[key];
     if(!Number.isFinite(value)||value<0||value>100){field.value=previous;note('Condition threshold must be between 0 and 100.');return;}
     request[key]=value;
     try{await previewPriority();$('priorityStatus').textContent=human(key)+' updated in engine-owned rotation conditions.';}
     catch(e){request[key]=previous;field.value=previous;note('Condition rejected: '+e.message);}
   });
   wrapper.append(field);root.append(wrapper);
 }
}
function drawPriorityRows(){
 const root=$('priorityList');root.replaceChildren();
 priorityRows.forEach((item,index)=>{
  const row=document.createElement('div');row.className='talent-row priority-entry';
  const label=document.createElement('label');label.className='check-row';
  const checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.checked=item.enabled;
  checkbox.setAttribute('aria-label','Enable '+human(item.key));
  checkbox.addEventListener('change',()=>{item.enabled=checkbox.checked;syncPriorityText();});
  const name=document.createElement('span');name.textContent=human(item.key);label.append(checkbox,name);
  const controls=document.createElement('div');controls.className='rank-controls';
  for(const [direction,text] of [[-1,'↑'],[1,'↓']]){
   const button=document.createElement('button');button.type='button';button.textContent=text;
   button.disabled=(direction===-1&&index===0)||(direction===1&&index===priorityRows.length-1);
   button.setAttribute('aria-label','Move '+human(item.key)+(direction===-1?' up':' down'));
   button.addEventListener('click',()=>{const other=index+direction;[priorityRows[index],priorityRows[other]]=[priorityRows[other],priorityRows[index]];syncPriorityText();drawPriorityRows();});
   controls.append(button);
  }
  row.append(label,controls);root.append(row);
 });
}
function syncPriorityText(){$('priorityText').value=priorityRows.filter(x=>x.enabled).map(x=>x.key).join('\n');}
function loadPriorityRows(keys){priorityRows=keys.map(key=>({key,enabled:true}));drawPriorityRows();}
async function previewPriority(){
 if(!request)throw Error('Select class and specialization first.');
 const previewRequest=body();
 const result=await post('/api/forever/rotation/preview',previewRequest);
 return result.entries.filter(x=>!['major-mana-potion','evocation','life-tap'].includes(x.abilityKey)).map(x=>x.abilityKey);
}
async function inspectPriority(){
 const prior=request.customPriority||[];request.customPriority=[];
 try{loadPriorityRows(await previewPriority());syncPriorityText();$('priorityStatus').textContent='Showing the current engine-generated priority. Select Apply to customize it.';}
 finally{request.customPriority=prior;}
}
async function applyPriority(){
 const previous=clone(request.customPriority||[]);
 const keys=$('priorityText').value.split(/[\n,]+/).map(x=>x.trim()).filter(Boolean);if(priorityRows.length && !keys.length)throw Error('All priority entries are disabled. Use automatic rotation explicitly, or enable at least one ability.');const currentEnabled=priorityRows.filter(x=>x.enabled).map(x=>x.key);if(JSON.stringify(currentEnabled)!==JSON.stringify(keys))loadPriorityRows(keys);
 if(new Set(keys).size!==keys.length)throw Error('Duplicate ability in priority order.');
 if(!keys.length)throw Error('Empty priority: use the Reset button to select automatic rotation.');
 request.customPriority=keys;
 try{await previewPriority();buildDirty=true;$('priorityStatus').textContent='Custom priority validated by the engine; it will be used for the next simulation.';}
 catch(e){request.customPriority=previous;throw e;}
}
function download(name,data){const url=URL.createObjectURL(new Blob([pretty(data)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();URL.revokeObjectURL(url);}

function readWarriorAssumptions(){
 return {
  tankMode:$('warriorTank').checked,shieldEquipped:$('warriorManualShield').checked,
  isTwoHanded:$('warriorWeaponMode').value==='two-hand',dualWield:$('warriorWeaponMode').value==='dual-wield',
  mainHandMinimumDamage:Number($('warriorWeaponMin').value),mainHandMaximumDamage:Number($('warriorWeaponMax').value),
  mainHandSpeedSeconds:Number($('warriorWeaponSpeed').value),
  playerBlockPercent:Number($('warriorBlockChance').value),playerBlockValue:Number($('warriorBlockValue').value),
  tankStatMode:$('warriorManualShield').checked?'manual':(request?.tankStatMode||'manual')
 };
}
function restoreWarriorAssumptions(){
 $('warriorTank').checked=request.tankMode===true;$('warriorManualShield').checked=request.shieldEquipped===true;
 $('warriorWeaponMode').value=request.dualWield?'dual-wield':request.isTwoHanded?'two-hand':'one-hand';
 $('warriorWeaponMin').value=request.mainHandMinimumDamage??160;$('warriorWeaponMax').value=request.mainHandMaximumDamage??250;
 $('warriorWeaponSpeed').value=request.mainHandSpeedSeconds??3.5;
 $('warriorBlockChance').value=request.playerBlockPercent??5;$('warriorBlockValue').value=request.playerBlockValue??60;
}
const legacyBody=body;
body=function(){
 if($('classKey').value!=='warrior'){
  const result=legacyBody(),c=$('classKey').value,s=$('spec').value;
  const child=result[typedKey(c,s)];
  if(c==='mage')child.useMageArmor=$('mageArmor').checked;
  if(c==='warlock'){
   child.useLifeTap=$('warlockLifeTap').checked;
   child.assumeExternalHealingForLifeTap=false;
   child.prePullSacrificedDemon=$('warlockSacrifice').value||null;
   child.allowTemporaryDemon=$('warlockTemporary').checked;
   child.allowDoomguardRitual=$('warlockRitual').checked;
   SoloDpsAssumptions.validateWarlock(child);
  }
  if(c==='hunter'){
   const aspect=$('hunterAspect').value;
   if(aspect==='monkey'&&s!=='beast-mastery')throw Error('Aspect of the Monkey is supported by Beast Mastery only.');
   child.useAspectOfTheHawk=aspect==='hawk';
   child.useAspectOfTheBeast=aspect==='beast';
   if(s==='beast-mastery')child.useAspectOfTheMonkey=aspect==='monkey';
   child.usePetSpecial=$('hunterSpecial').checked;
   if('petEngagementDelaySeconds' in child)
    child.petEngagementDelaySeconds=Number($('hunterDelay').value);
   else if(Number($('hunterDelay').value)!==0)
    throw Error('Selected Hunter runner has no engagement-delay setting.');
  }
  return result;
 }
 if(!request||loadedClass!=='warrior'||loadedSpec!==$('spec').value)throw Error('Warrior build is still loading.');
 if(!$('provisional').checked)throw Error('Warrior requires an explicitly selected historical baseline.');
 const r=clone(request);delete r.talents;
 r.preset=$('spec').value;r.level=60;r.race=$('race').value;r.statSourceMode='race-base';
 r.talentMode='tree';r.talentRanks=clone(request.talents?.ranks||{});
 r.externalModifiers=mods();
 r.captureRepresentativeTimeline=$('timeline').checked;
 r.durationSeconds=Number($('duration').value);r.seed=Number($('seed').value);
 r.startingStance=$('warriorStance').value;r.startingRage=Number($('warriorRage').value);
 r.enableStanceDancing=$('warriorDance').checked;
 r.enableBossAutoAttacks=$('warriorIncoming').checked;
 r.targetArmor=Number($('warriorArmor').value);
 Object.assign(r,readWarriorAssumptions());
 SoloDpsAssumptions.validateWarrior(r,cfg.equipment);
 r.rotationMode=request.rotationMode||'automatic';
 r.customRotation=clone(request.customRotation||[]);
 // The Warrior runner already supports source-race-gated Blood Fury and
 // Berserking as scheduled major cooldowns; preparation remains explicit.
 r.majorCooldowns=(r.majorCooldowns||[]).filter(x=>!['blood-fury','berserking','berserker-rage'].includes(x.abilityKey));
 if($('warriorBerserkerRage').checked)r.majorCooldowns.push({abilityKey:'berserker-rage',mode:'on-cooldown'});
 if($('racialActive').checked){
  const racial=r.race==='orc'?'blood-fury':r.race==='troll'?'berserking':null;
  if(!racial)throw Error('Only Orc Blood Fury and Troll Berserking are currently executable Warrior racials.');
  r.majorCooldowns.push({abilityKey:racial,mode:'on-cooldown'});
 }
 if(!Number.isInteger(r.seed)||r.durationSeconds<=0||r.durationSeconds>1200||
  !Number.isFinite(r.startingRage)||r.startingRage<0||r.startingRage>100||
  r.targetArmor<0)throw Error('Warrior duration, seed, Rage or armor is invalid.');
 return {classKey:'warrior',specialization:r.preset,warrior:r,equipment:clone(cfg.equipment)};
};
const legacyLoadPreset=loadPreset;
loadPreset=async function(token=generation){
 if($('classKey').value!=='warrior')return legacyLoadPreset(token);
 if(token!==generation)return;
 const s=$('spec').value,loadId=++presetLoadId;
 loadedClass=null;loadedSpec=null;request=null;$('run').disabled=true;
 try{
  const initial=await api('/api/forever/shared/warrior/defaults?spec='+encodeURIComponent(s));
  if(token!==generation||loadId!==presetLoadId)return;request=initial;
  request.talents={ranks:clone(request.talentRanks||{})};request.customRotation=[];
  itemizedMode=true;
  const talentData=await api('/api/forever/shared/warrior/talents');
  if(token!==generation||loadId!==presetLoadId)return;talentCatalog=talentData.talents;
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;
  $('timeline').checked=true;$('provisional').checked=true;
  $('petFamilyRow').hidden=true;$('demonRow').hidden=true;$('companionRow').hidden=true;
  $('targetHealthRow').hidden=true;$('rotationRow').hidden=true;
  $('warriorSettings').hidden=false;$('mageSettings').hidden=true;
  $('warlockSettings').hidden=true;$('hunterSettings').hidden=true;
  $('warriorStance').value=request.startingStance;
  $('warriorRage').value=request.startingRage;
  $('warriorDance').checked=!!request.enableStanceDancing;
  $('warriorIncoming').checked=!!request.enableBossAutoAttacks;
  restoreWarriorAssumptions();
  $('warriorBerserkerRage').checked=(request.majorCooldowns||[]).some(x=>x.abilityKey==='berserker-rage');
  syncWarriorRacialControl();
  $('warriorArmor').value=request.targetArmor;
  $('racialActive').disabled=!['orc','troll'].includes($('race').value);
  $('racialActive').checked=false;
  $('classCapabilityNotes').textContent='Warrior uses a server-prepared race, equipment and talent build, executed in the existing seeded engine. Race baselines are historical-provisional.';
  $('priorityEditor').hidden=false;priorityRows=[];drawPriorityRows();
  $('priorityText').value='';$('priorityStatus').textContent='Automatic Warrior policy; inspect to create an executable custom order.';
  renderTalents();renderMods();updateIdentity();
  loadedClass='warrior';loadedSpec=s;buildDirty=false;
  await checkBaseline();queueStatRefresh();
 }catch(e){if(token===generation&&loadId===presetLoadId){note('Warrior builder unavailable: '+e.message);request=null;}}
};
const legacyValidateTalents=validateTalentSelection;
validateTalentSelection=async function(){
 if($('classKey').value!=='warrior')return legacyValidateTalents();
 const r=clone(request);r.talentRanks=clone(r.talents?.ranks||{});delete r.talents;
 return post('/api/forever/shared/warrior/validate',r);
};
const legacyBaseline=checkBaseline;
checkBaseline=async function(){
 if($('classKey').value!=='warrior')return legacyBaseline();
 baselineAvailable=$('provisional').checked;
 $('baselineStatus').textContent=baselineAvailable?'Historical Warrior race baseline — provisional':'Warrior requires Historical baseline';
 $('statStatus').textContent=baselineAvailable?'Loading Warrior backend stats':'Select Historical baseline';
 if(baselineAvailable)queueStatRefresh();
};
const legacyPreview=preview;
preview=async function(token){
 if($('classKey').value!=='warrior')return legacyPreview(token);
 const result=await post('/api/forever/shared/warrior/prepare',body());
 if(token!==undefined&&token!==statsRefreshId)return result;
 $('stats').replaceChildren();
 for(const [key,value] of Object.entries(result.effectiveStats||{})){
  const row=document.createElement('div'),label=document.createElement('span'),v=document.createElement('strong');
  label.textContent=human(key);v.textContent=String(value);row.append(label,v);$('stats').append(row);
 }
 $('contributions').textContent=pretty({profileStats:result.profileStats,contributions:result.contributions||[]});
 $('statScope').textContent=result.statPreviewScope||'';$('statScope').hidden=!result.statPreviewScope;
 $('statStatus').textContent='Backend Warrior stats resolved';return result;
};
const legacyPreviewPriority=previewPriority;
let warriorPriorityWarnings=[];
previewPriority=async function(){
 if($('classKey').value==='warrior'){
  const response=await post('/api/forever/shared/warrior/rotation-preview',body().warrior);
  warriorPriorityWarnings=(response.warnings||[]).filter(value=>typeof value==='string'&&value.trim());
  if(warriorPriorityWarnings.length)$('priorityStatus').textContent=warriorPriorityWarnings.join('; ');
  return response.entries.filter(x=>x.enabled).map(x=>x.abilityKey).filter(Boolean);
 }
 // Hunter uses the same runner-backed preview as the base implementation.
 // Discovery lists are not proof that an action is learned or executable.
 return legacyPreviewPriority();
};
const legacyInspectPriority=inspectPriority;
inspectPriority=async function(){
 if($('classKey').value!=='warrior')return legacyInspectPriority();
 const mode=request.rotationMode;request.rotationMode='automatic';
 try{loadPriorityRows(await previewPriority());syncPriorityText();
  $('priorityStatus').textContent='Engine-generated Warrior priority. Apply custom order to activate.';}
 finally{request.rotationMode=mode;}
};
const legacyApplyPriority=applyPriority;
applyPriority=async function(){
 if($('classKey').value!=='warrior')return legacyApplyPriority();
 const current=clone(request.customRotation||[]),mode=request.rotationMode;
 const keys=priorityRows.filter(x=>x.enabled).map(x=>x.key);
 if(!keys.length||new Set(keys).size!==keys.length)throw Error('Select unique enabled actions for the custom rotation.');
 request.rotationMode='custom';
 request.customRotation=priorityRows.map(x=>({
  action:x.key,enabled:x.enabled,
  ...(x.minimumRage===undefined?{}:{minimumRage:Number(x.minimumRage)}),
  ...(x.targetHealthAtOrBelowPercent===undefined?{}:
   {targetHealthAtOrBelowPercent:Number(x.targetHealthAtOrBelowPercent)})
 }));
 try{await previewPriority();buildDirty=true;
  $('priorityStatus').textContent=['Custom Warrior rotation accepted by the server.',
   ...warriorPriorityWarnings].join(' ');}
 catch(e){request.customRotation=current;request.rotationMode=mode;
  $('priorityStatus').textContent='Custom Warrior rotation rejected: '+e.message;throw e;}
};
const legacyDrawPriority=drawPriorityRows;
drawPriorityRows=function(){
 legacyDrawPriority();
 const rows=[...$('priorityList').children];
 rows.forEach((row,i)=>{
  row.draggable=true;row.addEventListener('dragstart',e=>{e.dataTransfer?.setData('text/plain',String(i));});
  row.addEventListener('dragover',e=>e.preventDefault());
  row.addEventListener('drop',e=>{e.preventDefault();
   const source=Number(e.dataTransfer?.getData('text/plain'));
   if(Number.isInteger(source)&&source>=0&&source<priorityRows.length){
    const [value]=priorityRows.splice(source,1);priorityRows.splice(i,0,value);
    syncPriorityText();drawPriorityRows();buildDirty=true;
   }
  });
  if($('classKey').value==='warrior'){
   const label=document.createElement('label');label.textContent='Min Rage';
   const field=document.createElement('input');field.type='number';field.min='0';field.max='100';
   field.value=priorityRows[i].minimumRage??'';
   field.setAttribute('aria-label','Minimum Rage for '+priorityRows[i].key);
   field.addEventListener('change',()=>{const v=field.value===''?undefined:Number(field.value);
    if(v!==undefined&&(v<0||v>100)){note('Rage condition must be 0–100.');return;}
    priorityRows[i].minimumRage=v;buildDirty=true;});
   label.append(field);row.append(label);
  }
 });
};
const legacySave=saveConfig;
saveConfig=function(){
 const built=legacySave();
 if($('classKey').value==='warrior')built.encounter.warrior={
  ...readWarriorAssumptions(),
  startingStance:$('warriorStance').value,startingRage:Number($('warriorRage').value),
  targetArmor:Number($('warriorArmor').value),
  enableStanceDancing:$('warriorDance').checked,
  enableBossAutoAttacks:$('warriorIncoming').checked,
  majorCooldowns:[...(request.majorCooldowns||[]).filter(x=>x.abilityKey!=='berserker-rage'),
    ...($('warriorBerserkerRage').checked?[{abilityKey:'berserker-rage',mode:'on-cooldown'}]:[])],
  rotationMode:request.rotationMode,customRotation:clone(request.customRotation||[])
 };
 else if(!isRestrictedCaster())built.encounter.classSettings={
  mageArmor:$('mageArmor').checked,
  warlockLifeTap:$('warlockLifeTap').checked,
  warlockSacrifice:$('warlockSacrifice').value,
  allowTemporaryDemon:$('warlockTemporary').checked,
  allowDoomguardRitual:$('warlockRitual').checked,
  hunterAspect:$('hunterAspect').value,
  hunterSpecial:$('hunterSpecial').checked,
  hunterDelay:Number($('hunterDelay').value)
 };
 return built;
};
const legacyApplyConfig=applyConfig;
applyConfig=async function(data){
 await legacyApplyConfig(data);
 if(data.classKey==='warrior'){
  const w=data.encounter?.warrior||{};
  if(w.shieldEquipped===true&&w.tankStatMode!==undefined&&w.tankStatMode!=='manual')
   throw Error('This manual-shield setup requires manual block stats. A derived-stat shield import cannot be silently converted; use a supported manual setup or catalog equipment.');
  for(const key of ['mainHandMinimumDamage','mainHandMaximumDamage','mainHandSpeedSeconds','playerBlockPercent','playerBlockValue'])
   if(w[key]!==undefined&&(typeof w[key]!=='number'||!Number.isFinite(w[key])))
    throw Error('Imported manual weapon and block assumptions must be finite numbers.');
  for(const [key,value] of Object.entries(w))if(key in request)request[key]=clone(value);
  for(const key of ['tankMode','shieldEquipped','isTwoHanded','dualWield'])
   if(w[key]!==undefined&&typeof w[key]!=='boolean')throw Error('Imported Warrior '+key+' must be a boolean.');
  restoreWarriorAssumptions();
  $('warriorStance').value=request.startingStance||'automatic';
  $('warriorRage').value=request.startingRage||0;
  $('warriorArmor').value=request.targetArmor??3500;
  $('warriorDance').checked=!!request.enableStanceDancing;
  $('warriorIncoming').checked=!!request.enableBossAutoAttacks;
  $('warriorBerserkerRage').checked=(request.majorCooldowns||[]).some(x=>x.abilityKey==='berserker-rage');
  syncWarriorRacialControl();
  priorityRows=(request.customRotation||[]).map(x=>({
   key:x.action,enabled:x.enabled,
   minimumRage:x.minimumRage,targetHealthAtOrBelowPercent:x.targetHealthAtOrBelowPercent
  }));drawPriorityRows();syncPriorityText();
 }else{
  const c=data.encounter?.classSettings||{};
  $('mageArmor').checked=!!c.mageArmor;
  $('warlockLifeTap').checked=c.warlockLifeTap!==false;
  $('warlockSacrifice').value=c.warlockSacrifice||'';
  for(const key of ['allowTemporaryDemon','allowDoomguardRitual'])
   if(c[key]!==undefined&&typeof c[key]!=='boolean')throw Error('Imported demon assumptions must be explicit booleans.');
  $('warlockTemporary').checked=c.allowTemporaryDemon===true;
  $('warlockRitual').checked=c.allowDoomguardRitual===true;
  $('hunterAspect').value=c.hunterAspect||'none';
  $('hunterSpecial').checked=!!c.hunterSpecial;
  $('hunterDelay').value=c.hunterDelay||0;
 }
 queueStatRefresh();
};


function syncWarriorRacialControl(){
 if($('classKey').value!=='warrior')return;
 const supported=['orc','troll'].includes($('race').value);
 $('racialActive').disabled=!supported;
 if(!supported)$('racialActive').checked=false;
}
$('race').addEventListener('change',syncWarriorRacialControl);
const legacySimulate=simulate;
simulate=async function(){
 if($('classKey').value!=='warrior')return legacySimulate();
 const configuration=body(),iterations=Number($('iterations').value);
 if(!Number.isInteger(iterations)||iterations<1||iterations>100)
  throw Error('Iterations must be 1–100.');
 await validateTalentSelection();
 const gear=await validateGear(),stats=await preview();
 const prepared=await post('/api/forever/shared/warrior/prepare',configuration);
 const metadata=await api('/api/forever/character/report-metadata');
 const saved=saveConfig();$('run').disabled=true;note('Simulating Warrior through the seeded backend…');
 try{
  const distribution=await post('/api/forever/shared/simulate-iterations',{
   warrior:prepared.warrior,iterations
  });
  const sim=distribution.representative.sim;
  const run=sim.batch?.representativeRun||{},summary=run.summary||{};
  const warrior=summary.actorSummaries?.warrior||{};
  results={
   schema:'warcraftsim.solo-dps-report',version:1,metadata,build:clone(saved),
   originalSimulationRequest:{warrior:clone(prepared.warrior),iterations},
   iterationStatistics:clone(distribution),
   validatedLoadout:clone(gear),
   resolvedCharacter:clone(stats),
   talentAllocation:{mode:'strict-rank-derived',selected:clone(request.talents?.ranks||{})},
   baselineProvenance:{mode:'historical-provisional',race:prepared.warrior.race},
   combatSummary:{dps:sim.meanDps,damageDone:warrior.damageDone,ownerDamage:warrior.damageDone,petDamage:0,
    damageByAbility:warrior.damageDoneByAbility||{},abilities:warrior.abilities||{},
    combatItemUses:clone(sim.combatItemUses||{})},
   rawServerResponse:clone(distribution.representative),
   warnings:[...clone(sim.modelingNotes||[]),...(configuration.warrior.shieldEquipped?
    ['Manual shield and one-handed weapon assumptions were explicitly selected. They are not sourced catalog equipment; manual block stats were used.']:[])]
  };
  const d=distribution.dps;
  $('distributionSummary').replaceChildren();$('distributionSummary').hidden=iterations===1;
  $('distributionNotice').textContent=iterations===1?'One deterministic Warrior run; not a statistical distribution.':
   iterations+' independent seeded Warrior runs. '+distribution.percentileMethod;
  if(iterations>1){
   for(const [key,value] of Object.entries({
    'Mean DPS':d.mean,'SD (population)':d.standardDeviation,'Median':d.median,
    'P95':d.percentile95,'P99':d.percentile99,'Min':d.minimum,'Max':d.maximum,
    'Iterations':iterations})){
    const e=document.createElement('div'),label=document.createElement('span'),
       num=document.createElement('strong');label.textContent=key;num.textContent=String(value);
    e.append(label,num);$('distributionSummary').append(e);
   }
  }
  $('metrics').replaceChildren();$('abilities').replaceChildren();
  for(const [name,value] of Object.entries({
   'DPS':sim.meanDps,'Total damage':warrior.damageDone,
   'Duration':prepared.warrior.durationSeconds,'Effective boss armor':sim.effectiveBossArmor,
   'Starting Rage':prepared.warrior.startingRage,
   'Warrior max health':run.customMetrics?.['warrior-health-maximum'],
   'Combat consumables used':Object.values(sim.combatItemUses||{}).reduce((sum,v)=>sum+Number(v),0)
  })){
   const el=document.createElement('div'),key=document.createElement('span'),
     val=document.createElement('strong');key.textContent=name;val.textContent=String(value??'—');
   el.append(key,val);$('metrics').append(el);
  }
  for(const [key,damage] of Object.entries(warrior.damageDoneByAbility||{}).sort((a,b)=>b[1]-a[1])){
   const row=document.createElement('tr'),ability=warrior.abilities?.[key]||{};
   for(const cell of [key,ability.castCompletedCount??'—',
    ability.damageOccurrenceCount??'—',ability.criticalDamageCount??'—',damage]){
    const td=document.createElement('td');td.textContent=String(cell);row.append(td);
   }
   $('abilities').append(row);
  }
  $('raw').textContent=pretty(distribution.representative);
  $('eventLog').textContent=pretty((run.timeline||[]).slice(0,200));
  $('results').hidden=false;$('resultEmpty').hidden=true;
  $('exportResults').disabled=false;$('replayResult').disabled=false;
  navigate('results');note('Warrior completed using the established seeded engine.');
 }finally{$('run').disabled=false;}
};
const legacyReplay=replayLastResult;
replayLastResult=async function(){
 if(results?.build?.classKey!=='warrior')return legacyReplay();
 const previous=results.originalSimulationRequest;
 const replay=await post('/api/forever/shared/simulate-iterations',previous);
 if(JSON.stringify(replay.samples)!==JSON.stringify(results.iterationStatistics.samples) ||
    replay.representativeSeed!==results.iterationStatistics.representativeSeed ||
    replay.representative.sim.meanDps!==results.rawServerResponse.sim.meanDps)
  throw Error('Warrior seeded replay disagrees with the exported result.');
 note('Warrior seeded replay matched all sampled DPS values and representative seed.');
};
const originalLoadNonWarrior=loadPreset;
loadPreset=async function(token=generation){
 if($('classKey').value==='warrior')return originalLoadNonWarrior(token);
 $('racialActive').disabled=false;
 $('warriorSettings').hidden=true;
 $('mageSettings').hidden=$('classKey').value!=='mage';
 $('warlockSettings').hidden=$('classKey').value!=='warlock';
 $('hunterSettings').hidden=$('classKey').value!=='hunter';
 $('targetHealthRow').hidden=false;
 await originalLoadNonWarrior(token);
 if(token!==generation||!request)return;
 $('mageArmor').checked=!!request.useMageArmor;
 $('warlockLifeTap').checked=!!request.useLifeTap;
 $('warlockSacrifice').value=request.prePullSacrificedDemon||'';
 $('warlockTemporary').checked=request.allowTemporaryDemon===true;
 $('warlockRitual').checked=request.allowDoomguardRitual===true;
 $('hunterAspect').value=request.useAspectOfTheHawk?'hawk':
  request.useAspectOfTheBeast?'beast':request.useAspectOfTheMonkey?'monkey':'none';
 $('hunterSpecial').checked=!!request.usePetSpecial;
 $('hunterDelay').value=request.petEngagementDelaySeconds||0;
 if($('classKey').value==='warlock')request.assumeExternalHealingForLifeTap=false;
 if($('classKey').value==='hunter'){
  $('priorityEditor').hidden=false;$('rotationRow').hidden=true;
  $('priorityStatus').textContent='Hunter executes runner-supported custom owner priorities. Unsupported actions are rejected.';
 }
};
for(const id of ['warriorStance','warriorRage','warriorDance','warriorIncoming','warriorBerserkerRage','warriorArmor',
 'warriorTank','warriorManualShield','warriorWeaponMode','warriorWeaponMin','warriorWeaponMax','warriorWeaponSpeed','warriorBlockChance','warriorBlockValue',
 'mageArmor','warlockLifeTap','warlockSacrifice','warlockTemporary','warlockRitual','hunterAspect','hunterSpecial','hunterDelay']){
 $(id).addEventListener('change',()=>{buildDirty=true;queueStatRefresh();});
}
$('warriorProtectionExample').addEventListener('click',()=>{
 if(cfg.equipment.items.some(x=>['main-hand','off-hand'].includes(x.slotKey))){
  note('Unequip the main-hand and off-hand items before selecting manual Protection assumptions. Catalog equipment will not be overwritten.');return;
 }
 $('warriorTank').checked=true;$('warriorIncoming').checked=true;$('warriorManualShield').checked=true;
 $('warriorWeaponMode').value='one-hand';$('warriorWeaponMin').value=80;$('warriorWeaponMax').value=120;
 $('warriorWeaponSpeed').value=2;$('warriorBlockChance').value=5;$('warriorBlockValue').value=60;
 $('warriorStance').value='defensive';buildDirty=true;queueStatRefresh();
 note('Manual tank, incoming-attack, one-handed weapon and shield assumptions selected. Review their values above; learn Shield Slam in Talents to use it.');
});
$('addPriorityAction').addEventListener('click',()=>{
 const key=$('priorityActionSelect').value;
 if(!key||priorityRows.some(x=>x.key===key)){note('Select an action not already in the priority.');return;}
 priorityRows.push({key,enabled:true});syncPriorityText();drawPriorityRows();buildDirty=true;
});
async function refreshPriorityActionSelect(){
 if($('classKey').value==='priest'||isEnhancement()||isRogue()||isFeral()){$('priorityActionSelect').replaceChildren();return;}
 const token=generation,c=$('classKey').value,s=$('spec').value;
 try{
  const actions=await api('/api/forever/shared/rotation-actions/'+c+'/'+s);
  if(token!==generation||c!==$('classKey').value||s!==$('spec').value)return;
  $('priorityActionSelect').replaceChildren(...actions.actions.map(x=>option(x,human(x))));
 }catch(e){if(token===generation){$('priorityActionSelect').replaceChildren();note('Action catalog unavailable: '+e.message);}}
}
const previousOriginalLoad=loadPreset;
loadPreset=async function(token=generation){
 await previousOriginalLoad(token);
 if(token===generation&&request)await refreshPriorityActionSelect();
};


function renderSoloScript(){
 const enabled=$('scriptedBossEnabled');enabled.checked=!!soloScript.enabled;
 $('scriptedBossStart').value=soloScript.startingHealthPercent;
 const root=$('bossMilestones');root.replaceChildren();
 (soloScript.bossHealthMilestones||[]).forEach((step,index)=>{
  const row=document.createElement('div');row.className='settings-grid';
  for(const [key,label,min,max] of [['timeSeconds','Time (seconds)',0,1200],
     ['healthPercent','Boss HP (%)',0,100]]){
   const field=document.createElement('input');field.type='number';field.step='1';
   field.min=String(min);field.max=String(max);field.value=step[key];
   field.setAttribute('aria-label','Milestone '+(index+1)+' '+label);
   field.addEventListener('change',()=>{step[key]=Number(field.value);buildDirty=true;});
   const l=document.createElement('label');l.textContent=label;l.append(field);row.append(l);
  }
  const remove=document.createElement('button');remove.type='button';remove.textContent='Remove';
  remove.setAttribute('aria-label','Remove milestone '+(index+1));
  remove.addEventListener('click',()=>{
   soloScript.bossHealthMilestones.splice(index,1);buildDirty=true;renderSoloScript();
  });row.append(remove);root.append(row);
 });
}
$('scriptedBossEnabled').addEventListener('change',()=>{
 soloScript.enabled=$('scriptedBossEnabled').checked;buildDirty=true;
});
$('scriptedBossStart').addEventListener('change',()=>{
 soloScript.startingHealthPercent=Number($('scriptedBossStart').value);buildDirty=true;
});
$('addBossMilestone').addEventListener('click',()=>{
 const last=soloScript.bossHealthMilestones.at(-1);
 const duration=Number($('duration').value);
 soloScript.bossHealthMilestones.push({
  timeSeconds:Math.min(duration,Number(last?.timeSeconds||0)+15),
  healthPercent:Math.max(1,Number(last?.healthPercent??100)-20)
 });
 buildDirty=true;renderSoloScript();
});
const originalSoloBody=body;
body=function(){
 const submitted=originalSoloBody();
 const cls=submitted.classKey,property=typedKey(cls,submitted.specialization);
 if(['shaman','druid','priest','rogue'].includes(cls))return submitted;
 if(cls==='warrior')submitted.warrior.soloEncounter=clone(soloScript);
 else submitted[property].soloEncounter=clone(soloScript);
 return submitted;
};
const originalSoloSave=saveConfig;
saveConfig=function(){
 const saved=originalSoloSave();
 saved.encounter.soloScript=clone(soloScript);
 return saved;
};
const originalSoloApply=applyConfig;
applyConfig=async function(data){
 await originalSoloApply(data);
 soloScript=clone(data.encounter?.soloScript||{enabled:false,startingHealthPercent:100,bossHealthMilestones:[]});
 renderSoloScript();drawPriorityRows();
};
const originalSoloPreset=loadPreset;
loadPreset=async function(token=generation){
 await originalSoloPreset(token);
 if(token===generation&&request){
  soloScript=clone(request.soloEncounter||{enabled:false,startingHealthPercent:100,bossHealthMilestones:[]});
  renderSoloScript();
 }
};


const previousConditionalBody=body;
body=function(){
 validateRemovedPriorityTargets();
 const submitted=previousConditionalBody();
 const cls=submitted.classKey;
 const property=typedKey(cls,submitted.specialization);
 if(['shaman','druid','priest','rogue'].includes(cls))return submitted;
 const typed=cls==='warrior'?submitted.warrior:submitted[property];
 typed.priorityConditions=priorityRows.filter(x=>x.enabled&&
   (x.conditions?.length||x.orGroups?.length||x.targetActorKey)).map(x=>({
  abilityKey:x.key,conditions:clone(x.conditions||[]),
  anyConditionGroups:clone(x.orGroups||[]),
  targetActorKey:x.targetActorKey||null
 }));
 return submitted;
};
const previousConditionalDraw=drawPriorityRows;
drawPriorityRows=function(){
 previousConditionalDraw();
 if(isRestrictedCaster())return;
 const types=[
  ['target-aura-missing','Target debuff missing'],['target-aura-active','Target debuff active'],
  ['source-aura-active','My buff active'],['source-aura-missing','My buff missing'],
  ['target-health-percent','Target HP percent'],['source-health-percent','My HP percent'],
  ['source-resource-percent','Resource percent'],['source-resource-current','Resource amount'],
  ['current-time-seconds','Elapsed seconds'],['encounter-time-remaining-seconds','Time remaining (s)'],
  ['active-enemy-count','Active enemies'],['source-resource-deficit','Missing resource amount'],
  ['source-ability-ready','Ability ready'],
  ['source-ability-cooldown-remaining-seconds','Cooldown remaining (s)'],
  ['source-aura-remaining-seconds','My buff remaining (s)'],
  ['target-aura-remaining-seconds','Target debuff remaining (s)'],
  ['target-aura-stack-count','Target debuff stacks'],
  ['source-ability-on-cooldown','Another action on cooldown'],
  ['encounter-phase-active','Encounter phase active']
 ];
 const comparisons=[['less-than-or-equal','≤'],['greater-than-or-equal','≥'],
  ['less-than','<'],['greater-than','>'],['equal','='],['not-equal','≠']];
 [...$('priorityList').children].forEach((row,index)=>{
  const item=priorityRows[index];if(!item)return;
  item.conditions ||= [];
  const details=document.createElement('details');details.className='priority-conditions';
  const header=document.createElement('summary');header.textContent=
   'Conditions ('+item.conditions.length+')'+(item.conditions.length?
     ': '+item.conditions.map(x=>human(x.conditionType)).join(', '):'');
  details.append(header);
  for(let i=0;i<item.conditions.length;i++){
   const condition=item.conditions[i],line=document.createElement('div');
   line.className='settings-grid';
   const type=document.createElement('select');
   for(const [value,label] of types)type.append(option(value,label));
   type.value=condition.conditionType;
   type.setAttribute('aria-label','Condition type for '+item.key);
   type.addEventListener('change',()=>{condition.conditionType=type.value;
    buildDirty=true;drawPriorityRows();});
   const key=document.createElement('input');key.placeholder='Aura, resource or action key';
   key.value=condition.key||'';
   key.setAttribute('aria-label','Condition key for '+item.key);
   key.addEventListener('change',()=>{condition.key=key.value.trim();buildDirty=true;});
   const op=document.createElement('select');
   for(const [value,label] of comparisons)op.append(option(value,label));
   op.value=condition.comparisonOperator||'less-than-or-equal';
   op.setAttribute('aria-label','Comparison for '+item.key);
   op.addEventListener('change',()=>{condition.comparisonOperator=op.value;buildDirty=true;});
   const numeric=document.createElement('input');numeric.type='number';numeric.step='0.1';
   numeric.value=condition.value??'';
   numeric.placeholder='Threshold';numeric.setAttribute('aria-label','Threshold for '+item.key);
   numeric.addEventListener('change',()=>{condition.value=numeric.value===''?
    null:Number(numeric.value);buildDirty=true;});
   const owner=document.createElement('label');owner.textContent='Own effect only';
   const owned=document.createElement('input');owned.type='checkbox';
   owned.checked=!!condition.requireSourceOwnership;
   owned.addEventListener('change',()=>{condition.requireSourceOwnership=owned.checked;buildDirty=true;});
   owner.append(owned);
   const remove=document.createElement('button');remove.type='button';remove.textContent='Remove condition';
   remove.addEventListener('click',()=>{item.conditions.splice(i,1);drawPriorityRows();buildDirty=true;});
   line.append(type,key,op,numeric,owner,remove);details.append(line);
  }
  const add=document.createElement('button');add.type='button';add.textContent='Add condition';
  add.addEventListener('click',()=>{
   item.conditions.push({conditionType:'target-health-percent',
    comparisonOperator:'less-than-or-equal',value:20,requireSourceOwnership:false});
   drawPriorityRows();buildDirty=true;
  });
  details.append(add);row.append(details);
 });
};
const oldConditionalApply=applyPriority;
applyPriority=async function(){
 if(!request)throw Error('Select a character first.');
 const previous=clone(request.priorityConditions||[]);
 request.priorityConditions=priorityRows.filter(x=>x.enabled&&
    (x.conditions?.length||x.orGroups?.length||x.targetActorKey))
  .map(x=>({abilityKey:x.key,conditions:clone(x.conditions||[]),
    anyConditionGroups:clone(x.orGroups||[]),targetActorKey:x.targetActorKey||null}));
 try{await oldConditionalApply();}
 catch(error){request.priorityConditions=previous;throw error;}
};
const oldConditionalImport=applyConfig;
applyConfig=async function(data){
 await oldConditionalImport(data);
 if(Array.isArray(data.encounter?.priorityRows) &&
    data.encounter.priorityRows.every(x=>typeof x.key==='string'&&typeof x.enabled==='boolean') &&
    data.encounter.priorityRows.some(x=>x.conditions?.length||x.orGroups?.length||x.targetActorKey)){
  priorityRows=clone(data.encounter.priorityRows);drawPriorityRows();syncPriorityText();
 }
 updateIdentity();
};


function formatResultNumber(raw,kind='whole'){
 if(typeof raw!=='string'&&typeof raw!=='number')return String(raw);
 const num=Number(String(raw).replaceAll(',',''));
 if(!Number.isFinite(num))return String(raw);
 const digits=kind==='dps'||kind==='percent'?1:kind==='duration'||kind==='energy'?2:0;
 return num.toLocaleString('en-US',{minimumFractionDigits:kind==='dps'||kind==='percent'?1:0,
  maximumFractionDigits:digits});
}
function readableResultNumbers(){
 const distribution=$('distributionSummary');
 for(const metric of distribution.children){
  const label=metric.querySelector('span')?.textContent||'';
  const value=metric.querySelector('strong');
  if(!value)continue;
  const kind=label.includes('Iteration')?'whole':label.includes('DPS')||
   label.includes('deviation')?'dps':'whole';
  if(label.includes('DPS range')&&value.textContent.includes('±')){
   value.textContent=value.textContent.split('±').map(x=>formatResultNumber(x.trim(),'dps')).join(' ± ');
  }else value.textContent=formatResultNumber(value.textContent,kind);
 }
 for(const metric of $('metrics').children){
  const name=metric.querySelector('span')?.textContent||metric.firstChild?.textContent||'';
  const output=metric.querySelector('strong');if(!output)continue;
  const raw=output.textContent.trim();
  if(raw.startsWith('{')||raw.startsWith('[')){
   // Large item-use/audit objects remain available but no longer occupy
   // hundreds of lines in the default summary.
   const details=document.createElement('details');
   const summary=document.createElement('summary');summary.textContent='Inspect details';
   const pre=document.createElement('pre');pre.textContent=raw;
   details.append(summary,pre);output.replaceChildren(details);continue;
  }
  const kind=results?.originalSimulationRequest?.specialization==='feral'&&/AP|Mana|Energy|Health/i.test(name)?'energy':results?.originalSimulationRequest?.classKey==='rogue'&&/^(Maximum|Starting|Ending)Energy:\s*$/i.test(name)?'energy':
   /DPS|damage per second/i.test(name)?'dps':
   /percent|%/i.test(name)?'percent':
   /duration|seconds/i.test(name)?'duration':'whole';
  output.textContent=formatResultNumber(raw,kind);
 }
 for(const row of $('abilities').querySelectorAll('tr')){
  const fields=row.querySelectorAll('td');
  if(fields.length<5)continue;
  fields[4].textContent=formatResultNumber(fields[4].textContent,'whole');
  for(let i=1;i<=3;i++)fields[i].textContent=formatResultNumber(fields[i].textContent,'whole');
 }
 // The exported report and raw timeline stay unrounded, unchanged.
}
const beforeReadableSimulate=simulate;
simulate=async function(){
 await beforeReadableSimulate();
 if(results && !$('results').hidden)readableResultNumbers();
};


function currentTalentIcon(key){
 const talent=talentCatalog.find(x=>x.key===key);
 return talent&&typeof talent.iconKey==='string'&&
  /^[a-z0-9_]+$/.test(talent.iconKey)&&availableIconKeys.has(talent.iconKey)
  ?talent.iconKey:null;
}
function attachLocalIcon(target,key){
 const icon=document.createElement('span');icon.className='compact-source-icon';
 icon.setAttribute('aria-hidden','true');
 const fallback=document.createElement('span');fallback.textContent='✧';icon.append(fallback);
 const mapped=currentTalentIcon(key);
 if(mapped){
  const picture=document.createElement('img');picture.alt='';picture.loading='lazy';
  picture.width=22;picture.height=22;picture.src='/forever-icons/'+mapped+'.jpg';
  picture.addEventListener('load',()=>{fallback.hidden=true;});
  picture.addEventListener('error',()=>picture.remove());
  icon.append(picture);
 }
 target.prepend(icon);
}
function refreshLocalIconCoverage(){
 const tal=talentCatalog.filter(x=>x.modeled!==false);
 const available=tal.filter(x=>currentTalentIcon(x.key)).length;
 const verified=tal.filter(x=>currentTalentIcon(x.key)&&
  String(x.visualSourceStatus||'').includes('verified-70707')).length;
 const equipment=catalog.filter(x=>x.iconKey&&availableIconKeys.has(x.iconKey)||
  x.iconFileDataId&&availableIconKeys.has('filedata-'+x.iconFileDataId)).length;
 const external=modifiers.filter(x=>x.iconKey&&availableIconKeys.has(x.iconKey)).length;
 $('iconCoverage').textContent='Local icon coverage: '+available+'/'+tal.length+
  ' combat-modeled talent selections ('+verified+' current-build verified, '+
  (available-verified)+' provisional); equipment '+equipment+'/'+catalog.length+
  '; buffs/consumables '+external+'/'+modifiers.length+'. Unresolved items use a neutral fallback.';
}
const oldIconDrawPriority=drawPriorityRows;
drawPriorityRows=function(){
 oldIconDrawPriority();
 [...$('priorityList').children].forEach((row,i)=>{
  const key=priorityRows[i]?.key;if(!key)return;
  const label=row.querySelector('.check-row span');
  if(label)attachLocalIcon(label,key);
 });
};
const oldIconSimulate=simulate;
simulate=async function(){
 await oldIconSimulate();
 if(!$('results').hidden){
  for(const row of $('abilities').querySelectorAll('tr')){
   const first=row.querySelector('td');if(!first)continue;
   const key=first.textContent.trim();
   attachLocalIcon(first,key);
  }
 }
};
const oldIconLoadPreset=loadPreset;
loadPreset=async function(token=generation){
 await oldIconLoadPreset(token);
 if(token===generation&&request)refreshLocalIconCoverage();
};


const previousSourceCoveragePreset=loadPreset;
loadPreset=async function(token=generation){
 await previousSourceCoveragePreset(token);
 if(token!==generation||!request)return;
 try{
  const coverage=await api('/api/forever/talents/source-coverage');
  if(token!==generation||!request)return;
  const cls=$('classKey').value;
  if(['shaman','druid','priest','rogue'].includes(cls)){
   const admitted=talentCatalog.filter(t=>t.historicalValidationSourceStatus);
   const trees=cls==='rogue'?['assassination','combat','subtlety']:cls==='priest'?['discipline','holy','shadow']:cls==='druid'?['balance','feral','restoration']:['elemental','enhancement','restoration'];
   $('talentCoverage').textContent='Historical provisional '+(cls==='rogue'?(isCombatRogue()?'partial Combat starting preset (0/31/0; 31 allocated, 20 unspent), eleven reconciled keys only; no automatic 51-point filler. Lightning Reflexes, Puncturing Wounds and Endurance inactive in fixed-sword Combat; Blade Flurry cleave inactive with one target':'partial Assassination starting preset (38/0/0; 13 unspent), twelve reconciled keys only; other historical talents are unavailable'):cls==='priest'?'partial Shadow starting preset (0/0/36; 15 unspent)':cls==='druid'?(isFeral()?'partial Feral starting preset (0/31/5; 36 allocated, 15 unspent), fourteen retained keys; Furor and Feral Swiftness inactive in fixed Cat':'partial Balance starting preset (35/0/0; 16 unspent)'):isEnhancement()?'partial Enhancement starting preset (0/36/0; 36 allocated, 15 unspent), sixteen selectable keys including Elemental Convection, Concussion, Reverberation and Elemental Devastation. Optional 15/36/0 allocates all 51 points without filler; fully allocated does not mean fully modeled or optimized. Other historical talents remain unavailable':'Elemental allocation')+'. '+trees.map(tree=>
    human(tree)+': '+admitted.filter(t=>t.tree===tree).length+' historically validated runner keys, '+
    talentCatalog.filter(t=>t.tree===tree&&t.modeled===false).length+' historical-only visual keys').join(' | ')+
    '. Source '+[...new Set(admitted.map(t=>t.historicalValidationSourceCommit).filter(Boolean))].join(', ')+
    '. Not current-client certification; rank tooltips and complete mechanical coverage remain unverified.';
   return;
  }
  const rows=(coverage.rows||[]).filter(x=>x.classKey===cls);
  $('talentCoverage').textContent=rows.map(x=>human(x.tree)+': '+
   x.engineDeclaredConnectedKeys+' runner-declared, '+
   x.historicalOnlyKeys+' historical-only').join(' · ')+
   '. Current 70707 rank tooltips are not yet verified in the repository.';
 }catch(e){if(token===generation)$('talentCoverage').textContent='Talent source coverage unavailable: '+e.message;}
};


// Add actor keys are sequential in the non-Warrior runners. Reindex the
// encounter and its references together, before rendering or building requests.
function isRemovedPriorityTarget(item){
 return typeof item.targetActorKey==='string'&&item.targetActorKey.startsWith('removed:');
}
function validateRemovedPriorityTargets(){
 const removed=priorityRows.filter(isRemovedPriorityTarget);
 if(!removed.length)return;
 if(removed.some(x=>x.enabled))
  throw Error('A priority target was removed. Select a configured target before enabling its action.');
 const keys=new Set(removed.map(x=>x.key));
 const custom=$('classKey').value==='warrior'?
  (request?.rotationMode==='custom'?request.customRotation?.filter(x=>x.enabled).map(x=>x.action):[]):
  request?.customPriority;
 // Empty custom priorities mean automatic rotation to the engine, not "do
 // nothing". Never let a disabled removed-target action reappear that way.
 if(!custom?.length||custom.some(key=>keys.has(key)))
  throw Error('Removed-target actions are disabled. Apply a remaining custom priority, retarget the action, or explicitly reset to automatic rotation.');
}
function removeSoloEnemy(index){
 const windows=soloScript.addWindows||[];
 if(!Number.isInteger(index)||index<0||index>=windows.length)return;
 const removedKey=windows[index].targetKey;
 const cls=$('classKey').value;
 const prefix=cls==='mage'||cls==='warlock'?'target-':'add-';
 const survivors=windows.filter((_,i)=>i!==index);
 const remapped=new Map(survivors.map((add,i)=>[add.targetKey,prefix+(i+1)]));
 const disabledKeys=new Set();
 for(const item of priorityRows){
  if(item.targetActorKey===removedKey){
   item.targetActorKey='removed:'+removedKey;
   item.enabled=false;disabledKeys.add(item.key);
  }else if(remapped.has(item.targetActorKey)){
   item.targetActorKey=remapped.get(item.targetActorKey);
  }
 }
 for(const add of survivors)add.targetKey=remapped.get(add.targetKey);
 soloScript.addWindows=survivors;
 // Simulation uses applied action order, whereas conditions come from rows.
 // Remove only deleted-target actions from that order, preserving other edits.
 if(request){
  if(Array.isArray(request.customPriority))
   request.customPriority=request.customPriority.filter(key=>!disabledKeys.has(key));
  for(const action of request.customRotation||[])
   if(disabledKeys.has(action.action))action.enabled=false;
 }
 syncPriorityText();renderSoloScript();drawPriorityRows();buildDirty=true;
 if(disabledKeys.size)$('priorityStatus').textContent=
  'Actions targeting the removed enemy are disabled. Select a new target, enable and Apply to use them again.';
}

const baseSoloAddRender=renderSoloScript;
renderSoloScript=function(){
 baseSoloAddRender();
 const root=$('soloAddWindows');root.replaceChildren();
 soloScript.addWindows ||= [];
 for(const [i,add] of soloScript.addWindows.entries()){
  const section=document.createElement('details');section.open=true;section.className='settings-group';
  const summary=document.createElement('summary');summary.textContent='Add '+(i+1)+': '+add.targetKey;
  section.append(summary);
  const grid=document.createElement('div');grid.className='settings-grid';
  for(const [key,label,minimum,maximum] of [
   ['spawnTimeSeconds','Spawns at (s)',0,1200],
   ['despawnTimeSeconds','Leaves at (s)',0,1200],
   ['startingHealthPercent','Starting HP (%)',1,100]
  ]){
   const field=document.createElement('input');field.type='number';field.step='1';
   field.min=String(minimum);field.max=String(maximum);field.value=add[key];
   field.setAttribute('aria-label','Add '+(i+1)+' '+label);
   field.addEventListener('change',()=>{add[key]=Number(field.value);buildDirty=true;});
   const wrap=document.createElement('label');wrap.textContent=label;wrap.append(field);grid.append(wrap);
  }
  section.append(grid);
  const timing=document.createElement('label');timing.className='check-row';
  const relative=document.createElement('input');relative.type='checkbox';
  relative.checked=!!add.healthMilestonesRelativeToSpawn;
  relative.setAttribute('aria-label','Add '+(i+1)+' health milestones relative to spawn');
  relative.addEventListener('change',()=>{
   add.healthMilestonesRelativeToSpawn=relative.checked;
   // Changing the time coordinate clears old timestamps rather than
   // silently changing the meaning of an existing health script.
   add.healthMilestones=[];renderSoloScript();buildDirty=true;
  });
  timing.append(relative,document.createTextNode(' Health checkpoint times relative to spawn'));
  section.append(timing);
  add.healthMilestones ||= [];
  for(const [j,point] of add.healthMilestones.entries()){
   const line=document.createElement('div');line.className='settings-grid';
   for(const [key,label] of [['timeSeconds','Health checkpoint time (s)'],
       ['healthPercent','Health checkpoint HP (%)']]){
    const input=document.createElement('input');input.type='number';input.step='1';
    input.min='1';input.max=key==='healthPercent'?'100':'1200';input.value=point[key];
    input.setAttribute('aria-label','Add '+(i+1)+' milestone '+(j+1)+' '+label);
    input.addEventListener('change',()=>{point[key]=Number(input.value);buildDirty=true;});
    const wrap=document.createElement('label');wrap.textContent=label;wrap.append(input);line.append(wrap);
   }
   const del=document.createElement('button');del.type='button';del.textContent='Remove checkpoint';
   del.addEventListener('click',()=>{add.healthMilestones.splice(j,1);renderSoloScript();buildDirty=true;});
   line.append(del);section.append(line);
  }
  const addPoint=document.createElement('button');addPoint.type='button';addPoint.textContent='Add health checkpoint';
  addPoint.addEventListener('click',()=>{
   const prev=add.healthMilestones.at(-1);
   const relative=!!add.healthMilestonesRelativeToSpawn;
   const limit=relative?Number(add.despawnTimeSeconds)-Number(add.spawnTimeSeconds):
    Number(add.despawnTimeSeconds);
   const start=relative?0:Number(add.spawnTimeSeconds);
   add.healthMilestones.push({timeSeconds:Math.min(limit,
      Number(prev?.timeSeconds??start)+5),
    healthPercent:Math.max(1,Number(prev?.healthPercent??add.startingHealthPercent)-20)});
   renderSoloScript();buildDirty=true;
  });
  const remove=document.createElement('button');remove.type='button';remove.textContent='Remove enemy';
  remove.setAttribute('aria-label','Remove enemy '+add.targetKey);
  remove.addEventListener('click',()=>removeSoloEnemy(i));
  section.append(addPoint,remove);root.append(section);
 }
};
$('addSoloEnemy').addEventListener('click',()=>{
 soloScript.addWindows ||= [];
 const cls=$('classKey').value;
 const max=cls==='warrior'?3:8;
 const group=Number($('soloAddGroupCount').value);
 if(!Number.isInteger(group)||group<1||group>max ||
   soloScript.addWindows.length+group>max){
  note('Requested group exceeds '+max+' supported adds for '+cls);return;
 }
 soloScript.enabled=true;
 const duration=Number($('duration').value);
 const start=Math.min(duration-1,5);
 for(let i=0;i<group;i++){
  const n=soloScript.addWindows.length+1;
  soloScript.addWindows.push({
   targetKey:(cls==='mage'||cls==='warlock'?'target-':'add-')+n,
   spawnTimeSeconds:start,despawnTimeSeconds:duration,
   startingHealthPercent:100,healthMilestonesRelativeToSpawn:false,
   healthMilestones:[]
  });
 }
 renderSoloScript();drawPriorityRows();buildDirty=true;
});
const baseAddBody=body;
body=function(){
 const b=baseAddBody(),windows=soloScript.addWindows||[];
 if(!soloScript.enabled||!windows.length)return b;
 const c=b.classKey;
 if(c==='warrior'){
  b.warrior.additionalTargets=windows.map((add,i)=>({
   key:add.targetKey,name:'Scripted add '+(i+1),level:63,
   armor:3500,dodgePercent:5,parryPercent:0,blockPercent:0,
   spawnTimeSeconds:add.spawnTimeSeconds,
   despawnTimeSeconds:add.despawnTimeSeconds,inCleaveRange:true
  }));
 }else{
  const key=typedKey(c,b.specialization);
  b[key].additionalTargets=windows.length;
 }
 return b;
};


const priorOrGroupDraw=drawPriorityRows;
drawPriorityRows=function(){
 priorOrGroupDraw();
 if(isRestrictedCaster())return;
 [...$('priorityList').children].forEach((row,i)=>{
  const item=priorityRows[i];if(!item)return;
  const details=row.querySelector('.priority-conditions')||row;
  const targetLabel=document.createElement('label');
  targetLabel.textContent='Target this action';
  const target=document.createElement('select');
  target.setAttribute('aria-label','Priority target for '+item.key);
  target.append(option('','Default boss'));
  for(const add of soloScript.addWindows||[])
   target.append(option(add.targetKey,'Add '+add.targetKey));
  if(item.targetActorKey&&!Array.from(target.options).some(x=>x.value===item.targetActorKey))
   target.append(option(item.targetActorKey,(isRemovedPriorityTarget(item)?
    'Removed '+item.targetActorKey.slice('removed:'.length):item.targetActorKey)+' (not configured)'));
  const enable=row.querySelector('.check-row input[type=checkbox]');
  if(isRemovedPriorityTarget(item)){
   enable.disabled=true;enable.title='Select a configured target before enabling this action.';
  }
  target.value=item.targetActorKey||'';
  target.addEventListener('change',()=>{item.targetActorKey=target.value||null;buildDirty=true;drawPriorityRows();});
  targetLabel.append(target);details.append(targetLabel);
  item.orGroups ||= [];
  const groups=document.createElement('div');groups.className='priority-or-groups';
  const heading=document.createElement('h4');heading.textContent='OR alternatives (each group is AND)';
  groups.append(heading);
  function conditionRow(condition,group,index){
   const line=document.createElement('div');line.className='settings-grid';
   const types=['target-health-percent','target-aura-missing',
    'target-aura-remaining-seconds','target-aura-stack-count',
    'source-aura-active','source-aura-remaining-seconds',
    'source-resource-percent','source-resource-deficit',
    'source-ability-ready','source-ability-cooldown-remaining-seconds',
    'encounter-time-remaining-seconds','active-enemy-count'];
   const select=document.createElement('select');
   for(const key of types)select.append(option(key,human(key)));
   select.value=condition.conditionType;
   select.setAttribute('aria-label','OR condition type '+item.key);
   select.addEventListener('change',()=>{condition.conditionType=select.value;buildDirty=true;});
   const key=document.createElement('input');key.placeholder='Resource, aura or action key';
   key.value=condition.key||'';
   key.setAttribute('aria-label','OR condition key '+item.key);
   key.addEventListener('change',()=>{condition.key=key.value.trim();buildDirty=true;});
   const op=document.createElement('select');
   for(const value of ['less-than-or-equal','greater-than-or-equal','equal',
    'not-equal','less-than','greater-than'])op.append(option(value,human(value)));
   op.value=condition.comparisonOperator||'less-than-or-equal';
   op.setAttribute('aria-label','OR comparison '+item.key);
   op.addEventListener('change',()=>{condition.comparisonOperator=op.value;buildDirty=true;});
   const numeric=document.createElement('input');numeric.type='number';numeric.step='0.1';
   numeric.value=condition.value??'';
   numeric.setAttribute('aria-label','OR threshold '+item.key);
   numeric.addEventListener('change',()=>{condition.value=numeric.value===''?null:Number(numeric.value);buildDirty=true;});
   const own=document.createElement('label');own.textContent='Own aura';
   const checkbox=document.createElement('input');checkbox.type='checkbox';
   checkbox.checked=!!condition.requireSourceOwnership;
   checkbox.addEventListener('change',()=>{condition.requireSourceOwnership=checkbox.checked;buildDirty=true;});
   own.append(checkbox);
   const remove=document.createElement('button');remove.type='button';remove.textContent='Remove condition';
   remove.addEventListener('click',()=>{group.splice(index,1);drawPriorityRows();buildDirty=true;});
   line.append(select,key,op,numeric,own,remove);return line;
  }
  item.orGroups.forEach((group,j)=>{
   const section=document.createElement('details');section.open=true;
   const label=document.createElement('summary');label.textContent='OR group '+(j+1)+' — '+group.length+' AND conditions';
   section.append(label);
   group.forEach((condition,k)=>section.append(conditionRow(condition,group,k)));
   const add=document.createElement('button');add.type='button';add.textContent='Add AND condition';
   add.addEventListener('click',()=>{
    if(group.length>=8){note('Maximum eight predicates per OR group');return;}
    group.push({conditionType:'target-health-percent',
     comparisonOperator:'less-than-or-equal',value:20});
    drawPriorityRows();buildDirty=true;
   });
   const remove=document.createElement('button');remove.type='button';remove.textContent='Remove OR group';
   remove.addEventListener('click',()=>{item.orGroups.splice(j,1);drawPriorityRows();buildDirty=true;});
   section.append(add,remove);groups.append(section);
  });
  const addGroup=document.createElement('button');addGroup.type='button';
  addGroup.textContent='Add OR group';
  addGroup.addEventListener('click',()=>{
   if(item.orGroups.length>=4){note('Maximum four OR groups per action');return;}
   item.orGroups.push([{conditionType:'target-health-percent',
    comparisonOperator:'less-than-or-equal',value:20}]);
   drawPriorityRows();buildDirty=true;
  });
  groups.append(addGroup);details.append(groups);
 });
};

// Report exactly what was configured; actual combat item consumption is separately
// sourced from the engine, never inferred from selected inventory.
const f2aSimulate=simulate;
simulate=async function(){
 await f2aSimulate();
 if(!results||$('results').hidden)return;
 const selected=[...cfg.effects.buffs,...cfg.effects.consumables,...cfg.effects.targetDebuffs];
 const appliedNames=selected.map(key=>modifiers.find(x=>x.key===key)?.name||key);
 results.configuredModifierSelections=clone(cfg.effects);
 const el=document.createElement('div'),label=document.createElement('span'),value=document.createElement('strong');
 label.textContent='Configured buffs, food and debuffs';
 value.textContent=appliedNames.join(' · ')||'None selected';
 el.append(label,value);$('metrics').append(el);
};

// Publish an immutable-comparison input only after the final successful UI run.
// The comparison module never alters engine requests or report exports.
function renderResultWarnings(report){
 const warnings=[...new Set((report?.warnings||[])
  .filter(value=>typeof value==='string'&&value.trim()))];
 $('resultWarningList').replaceChildren();
 for(const warning of warnings){
  const row=document.createElement('div');row.textContent=warning;
  $('resultWarningList').append(row);
 }
 $('resultWarnings').hidden=warnings.length===0;
}
const beforeComparisonPublication=simulate;
simulate=async function(){
 await beforeComparisonPublication();
 if(results&&!$('results').hidden){
  renderResultWarnings(results);
  document.dispatchEvent(new CustomEvent('warcraftsim:simulation-complete',
   {detail:clone(results)}));
 }
};
// Restricted historical caster adapters: reject unsupported imports before changing the build.
function validateRestrictedCasterImport(data){
 if(data.classKey==='druid'&&data.specialization==='feral'){validateFeralImport(data);return;}
 if(data.classKey==='rogue'){validateRogueImport(data);return;}
 if(!['shaman','druid','priest'].includes(data.classKey))return;
 const enhancement=data.classKey==='shaman'&&data.specialization==='enhancement';
 const label=data.classKey==='priest'?'Shadow':data.classKey==='druid'?'Balance':enhancement?'Enhancement':'Elemental';
 const top=new Set(['schema','version','itemizedMode','classKey','specialization','race','provisional','gear','modifiers','preparedCombatConsumables','racialActive','encounter','talents']);
 for(const key of Object.keys(data))if(!top.has(key))throw Error('Unsupported '+label+' build field: '+key);
 const enc=data.encounter||{},script=enc.soloScript||{},effects=data.modifiers||{};
 const permitted=new Set(['duration','seed','iterations','targetHealth','timeline','rotation','customPriority','priorityRows','rotationConditions','combatItems','soloScript','majorManaPotionCharges','useMajorManaPotion','shadowTargetInitialHealth','shadowPriestHealth',...(enhancement?['enhancement']:[])]);
 for(const key of Object.keys(enc))if(!permitted.has(key))throw Error('Unsupported '+label+' encounter setting: '+key);
 if(enhancement){
  if(!data.gear||!Array.isArray(data.gear.items)||Object.keys(data.gear).some(key=>key!=='items'))throw Error('Unsupported Enhancement equipment shape.');
  if(Object.keys(effects).some(key=>!['buffs','consumables','targetDebuffs','targetDebuffMode'].includes(key))||['buffs','consumables','targetDebuffs'].some(key=>!Array.isArray(effects[key])))throw Error('Unsupported Enhancement modifier shape.');
  if(!enc.combatItems||typeof enc.combatItems!=='object'||Array.isArray(enc.combatItems))throw Error('Enhancement combat inventory must be an object.');
  if(data.preparedCombatConsumables!==undefined){
   const selected=Object.entries(enc.combatItems).filter(([key,value])=>key.endsWith('Charges')&&Number(value)>0).map(([key,charges])=>({key,charges:Number(charges)}));
   if(!Array.isArray(data.preparedCombatConsumables)||JSON.stringify([...data.preparedCombatConsumables].sort((a,b)=>String(a.key).localeCompare(String(b.key))))!==JSON.stringify(selected.sort((a,b)=>a.key.localeCompare(b.key))))throw Error('Enhancement prepared inventory differs from combat item charges.');
  }
  for(const key of ['rotation','customPriority','priorityRows','rotationConditions','majorManaPotionCharges','useMajorManaPotion'])if(Object.prototype.hasOwnProperty.call(enc,key))throw Error('Unsupported Enhancement setting: '+key);
  if(effects.buffs?.length||effects.consumables?.length||effects.targetDebuffs?.length)throw Error('Enhancement external modifiers are unavailable.');
  validateEnhancementSettings(enc.enhancement);
 }
 if(data.classKey==='priest'){
  for(const key of ['rotation','customPriority','priorityRows','rotationConditions','combatItems','majorManaPotionCharges','useMajorManaPotion'])
   if(Object.prototype.hasOwnProperty.call(enc,key))throw Error('Unsupported Shadow setting: '+key);
  if(effects.buffs?.length||effects.consumables?.length||effects.targetDebuffs?.length||data.preparedCombatConsumables?.length)
   throw Error('Shadow external modifiers and combat items are unavailable.');
  if(!(Number(enc.shadowTargetInitialHealth??100)>0&&Number(enc.shadowTargetInitialHealth??100)<=100)||!(Number(enc.shadowPriestHealth??3500)>0)||!Number.isFinite(Number(enc.shadowPriestHealth??3500)))
   throw Error('Shadow target initial health and Priest maximum health must be valid distinct values.');
 }else if(enc.shadowTargetInitialHealth!==undefined||enc.shadowPriestHealth!==undefined)throw Error('Shadow health fields are unavailable for this class.');
 if(data.racialActive)throw Error(label+' racial actives are unavailable.');
 if(data.gear?.items?.length)throw Error(label+' equipment is pending eligibility evidence.');
 if(Number(enc.iterations??1)!==1)throw Error(label+' supports one deterministic run only.');
 if(script.enabled||Number(script.startingHealthPercent??100)!==100||script.bossHealthMilestones?.length||script.addWindows?.length)
  throw Error(label+' generic encounter scripts are unavailable.');
 if(Object.keys(script).some(k=>!['enabled','startingHealthPercent','bossHealthMilestones','addWindows'].includes(k)))throw Error('Unknown '+label+' encounter script field.');
 if(effects.targetDebuffs?.length)throw Error(label+' target debuffs are unavailable.');
 if(enc.priorityRows?.some(row=>row.conditions?.length||row.orGroups?.length||row.targetActorKey))throw Error(label+' conditional priorities and target overrides are unavailable.');
 if(Object.keys(enc.rotationConditions||{}).length)throw Error(label+' custom rotation conditions are unavailable.');
 if(Number(enc.majorManaPotionCharges||0)!==0||enc.useMajorManaPotion)throw Error('Use the explicit shared combat-item selection for prepared caster potions.');
}
function validateRogueImport(data){
 const label=data.specialization==='combat'?'Combat':'Assassination';
 const top=['schema','version','itemizedMode','classKey','specialization','race','provisional','gear','modifiers','preparedCombatConsumables','racialActive','encounter','talents'];
 for(const key of Object.keys(data))if(!top.includes(key))throw Error('Unsupported '+label+' build field: '+key);
 if(!['assassination','combat'].includes(data.specialization)||data.itemizedMode!==true)throw Error('Only strict itemized Assassination or Combat is available.');
 if(!data.gear||!Array.isArray(data.gear.items)||data.gear.items.length||Object.keys(data.gear).some(key=>key!=='items'))throw Error(label+' catalog equipment and enchants are unavailable.');
 if(typeof data.provisional!=='boolean'||typeof data.race!=='string'||!data.race.trim())throw Error(label+' baseline mode and race must be explicit.');
 const talents=data.talents,fields=['assassinationPointsSpent','combatPointsSpent','subtletyPointsSpent','ranks'];
 if(!talents||typeof talents!=='object'||Array.isArray(talents)||Object.keys(talents).some(key=>!fields.includes(key))||fields.some(key=>!Object.prototype.hasOwnProperty.call(talents,key))||!talents.ranks||typeof talents.ranks!=='object'||Array.isArray(talents.ranks)||Object.values(talents.ranks).some(rank=>!Number.isInteger(rank)||rank<0)||fields.filter(key=>key!=='ranks').some(key=>!Number.isInteger(talents[key])||talents[key]<0))throw Error(label+' requires explicit ranked talents and three declared tree totals.');
 const effects=data.modifiers;
 if(!effects||Object.keys(effects).some(key=>!['buffs','consumables','targetDebuffs','targetDebuffMode'].includes(key))||['buffs','consumables','targetDebuffs'].some(key=>!Array.isArray(effects[key])||effects[key].length)||effects.targetDebuffMode!=='maintained')throw Error(label+' external modifiers are unavailable.');
 if(data.racialActive!==false||!Array.isArray(data.preparedCombatConsumables)||data.preparedCombatConsumables.length)throw Error(label+' racial actives and combat inventory are unavailable.');
 const enc=data.encounter;
 if(!enc||typeof enc!=='object'||Array.isArray(enc))throw Error(label+' encounter settings required.');
 for(const key of Object.keys(enc))if(!['duration','seed','iterations','targetHealth','timeline','soloScript','rogue'].includes(key))throw Error('Unsupported '+label+' encounter setting: '+key);
 for(const key of ['duration','seed','iterations','targetHealth'])if(!['number','string'].includes(typeof enc[key])||typeof enc[key]==='string'&&!enc[key].trim()||!Number.isFinite(Number(enc[key])))throw Error(label+' '+key+' must be explicit and finite.');
 if(typeof enc.timeline!=='boolean'||Number(enc.duration)<=0||Number(enc.duration)>1200||Number(enc.targetHealth)<=0||!Number.isInteger(Number(enc.seed))||Number(enc.seed)<-2147483648||Number(enc.seed)>2147483647)throw Error('Invalid '+label+' duration, seed, target Health or timeline.');
 if(Number(enc.iterations)!==1)throw Error(label+' supports one deterministic run only.');
 const script=enc.soloScript;
 if(!script||script.enabled!==false||script.startingHealthPercent!==100||!Array.isArray(script.bossHealthMilestones)||script.bossHealthMilestones.length||script.addWindows!==undefined&&(!Array.isArray(script.addWindows)||script.addWindows.length)||Object.keys(script).some(key=>!['enabled','startingHealthPercent','bossHealthMilestones','addWindows'].includes(key)))throw Error(label+' generic scripts and adds are unavailable.');
 validateRogueSettings(enc.rogue,undefined,data.specialization);
}
// The existing import transaction owns Feral too. Snapshot raw DOM drafts rather
// than serializing a potentially invalid AP/shift/seed edit into a valid build.
let importDraftBeforeLock=null;
function captureBuildDraft(){
 return {request:clone(request),loadedClass,loadedSpec,buildDirty,itemizedMode,talentCatalog:clone(talentCatalog),catalog:clone(catalog),catalogAll:clone(catalogAll),enchants:clone(enchants),modifiers:clone(modifiers),cfg:clone(cfg),soloScript:clone(soloScript),priorityRows:clone(priorityRows),
 availability:[...document.querySelectorAll('[id]')].map(el=>({id:el.id,hidden:el.hidden,disabled:el.disabled})),labels:['classCapabilityNotes','rotationSubtitle','priorityStatus','catalogCoverage','talentCoverage','iterationHelp','distributionNotice'].map(id=>({id,text:$(id).textContent})),
 controls:[...document.querySelectorAll('input,select,textarea')].filter(el=>el.id&&el.type!=='file').map(el=>({id:el.id,value:el.value,checked:el.checked,options:el.tagName==='SELECT'?[...el.options].map(o=>({value:o.value,text:o.textContent})):null}))};
}
async function restoreBuildDraft(draft){
 ++generation;++statsRefreshId;++baselineCheckId;
 request=draft.request;loadedClass=draft.loadedClass;loadedSpec=draft.loadedSpec;itemizedMode=draft.itemizedMode;talentCatalog=draft.talentCatalog;catalog=draft.catalog;catalogAll=draft.catalogAll;enchants=draft.enchants;modifiers=draft.modifiers;cfg.equipment=draft.cfg.equipment;cfg.effects=draft.cfg.effects;soloScript=draft.soloScript;priorityRows=draft.priorityRows;
 // A different class can remove owned controls (modifier mode, Mage cooldowns,
 // Hunter actions). Restore existing inputs first so the prior class can render
 // them again; only then replay every raw control, requiring complete recovery.
 const controls=(deferMissing=false)=>{for(const c of draft.controls){const element=$(c.id);if(!element){if(deferMissing)continue;throw Error('Saved build control was not recreated: '+c.id+'.');}if(c.options)element.replaceChildren(...c.options.map(o=>option(o.value,o.text)));element.value=c.value;element.checked=c.checked;}};
 controls(true);renderGear();renderMods();renderTalents();renderPriorityConditions();renderSoloScript();drawPriorityRows();updateIdentity();controls();setRogueScope();for(const item of draft.availability){if(item.hidden!==undefined)$(item.id).hidden=item.hidden;if(item.disabled!==undefined)$(item.id).disabled=item.disabled;}for(const item of draft.labels)$(item.id).textContent=item.text;buildDirty=draft.buildDirty;await checkBaseline();queueStatRefresh();
}
async function applyFeralConfig(data){
 validateFeralImport(data);
 $('classKey').value='druid';await initClass();$('spec').value='feral';await loadPreset();
 if(!request||loadedSpec!=='feral')throw Error('Feral preset loading failed.');
 $('race').value=data.race;$('provisional').checked=true;
 request=feralRequest(data);restoreFeralControls(request);
 for(const [key,id] of [['durationSeconds','duration'],['seed','seed'],['targetMaximumHealth','targetHealth']])$(id).value=request[key];
 $('timeline').checked=request.captureTimeline;$('iterations').value=1;
 cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};soloScript={enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]};priorityRows=[];
 // Do not recompute declarations before validating them against current catalog.
 for(const [tree,key] of [['balance','balancePointsSpent'],['feral','feralPointsSpent'],['restoration','restorationPointsSpent']])if(talentCatalog.filter(t=>t.tree===tree).reduce((n,t)=>n+Number(request.talents.ranks[t.key]||0),0)!==data.talents[key])throw Error('Feral imported '+key+' differs from catalog-derived ranks.');
 if(Object.keys(request.talents.ranks).some(key=>!talentCatalog.some(t=>t.key===key)))throw Error('Feral talent is outside the current catalog.');
 await validateTalentSelection();renderGear();renderMods();renderTalents();renderPriorityConditions();updateIdentity();await checkBaseline();await preview();buildDirty=false;
}
function setRestrictedPanels(){
 setRogueScope();
 const feral=isFeral();$('feralSettings').hidden=!feral;$('feralFixedRotation').hidden=!feral;$('feralTalentScope').hidden=!feral;
 $('rogueSettings').hidden=!isRogue();$('rogueFixedRotation').hidden=!isRogue();$('enhancementSettings').hidden=!isEnhancement();$('enhancementFixedRotation').hidden=!isEnhancement();$('shadowHealthSettings').hidden=$('classKey').value!=='priest';$('shadowFixedRotation').hidden=$('classKey').value!=='priest';
 $('gearRows').hidden=feral;$('validateGear').hidden=feral;
 if(feral){$('priorityEditor').hidden=true;$('rotationRow').hidden=true;$('scriptedBossSection').hidden=true;$('rotationSubtitle').textContent='Fixed Cat configured priority from the exact preparation receipt. Shifting Power may act at t=0 after this boundary.';}
}

const beforeElementalImport=applyConfig;
applyConfig=async function(data){
 validateRestrictedCasterImport(data);
 if(isFeral()||data.classKey==='druid'&&data.specialization==='feral'||isCombatRogue()||data.classKey==='rogue'&&data.specialization==='combat'){
  const draft=importDraftBeforeLock||(request&&loadedClass?captureBuildDraft():null);
  try{return data.classKey==='druid'&&data.specialization==='feral'?await applyFeralConfig(data):await beforeElementalImport(data);}
  catch(error){if(draft)await restoreBuildDraft(draft);throw error;}
 }
 if(!['shaman','druid','priest','rogue'].includes(data.classKey))return beforeElementalImport(data);
 let previous=null,rogueDraft=null;const previousDirty=buildDirty;
 if(request&&loadedClass){
  if(isRogue()){
   // Keep raw dirty controls separately. A valid import must be able to recover
   // from an invalid local edit; rejected imports must restore that same draft.
   const ids=[...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...rogueWeaponTuningKeys].map(key=>'rogue'+key[0].toUpperCase()+key.slice(1)).concat(['duration','seed','iterations','targetHealth','timeline','provisional']);
   rogueDraft={request:clone(request),controls:ids.map(id=>({id,value:$(id).value,checked:$(id).checked}))};
   try{
    restoreRogueSettings(Object.fromEntries([...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueFixedSettings),'tuning'].map(key=>[key,request[key]])));
    previous=clone(saveConfig());previous.provisional=true;
    previous.encounter.duration=String(request.durationSeconds);previous.encounter.seed=String(request.seed);previous.encounter.iterations='1';previous.encounter.targetHealth=String(request.targetMaximumHealth);previous.encounter.timeline=!!request.captureTimeline;
   }finally{for(const control of rogueDraft.controls){$(control.id).value=control.value;$(control.id).checked=control.checked;}}
  }else previous=clone(saveConfig());
 }
 try{await beforeElementalImport(data);}
 catch(error){
  // Server-owned action/talent gates may reject after asynchronous preset loading.
  // Restore the last valid build through the same validation path, never leave
  // a rejected imported priority as the user's active configuration.
  if(previous){
   try{await beforeElementalImport(previous);if(rogueDraft){request=clone(rogueDraft.request);for(const control of rogueDraft.controls){$(control.id).value=control.value;$(control.id).checked=control.checked;}renderTalents();renderPriorityConditions();renderMods();await checkBaseline();}buildDirty=previousDirty;}
   catch(rollback){throw Error(error.message+'; prior build restoration failed: '+rollback.message);}
  }
  throw error;
 }
};
const beforeElementalSave=saveConfig;
saveConfig=function(){
 if(isFeral())return saveFeralConfig();
 const saved=beforeElementalSave();if(!isRestrictedCaster())return saved;
 for(const key of ['petFamily','demonType','companionEnabled','useArcanePower','usePresenceOfMind','hunterMelee','hunterParryEvery','hunterSurvival','classSettings'])delete saved.encounter[key];
 saved.encounter.rotationConditions={};saved.racialActive=false;
 if(isRogue()){
  for(const key of ['rotation','customPriority','priorityRows','rotationConditions','combatItems','majorManaPotionCharges','useMajorManaPotion'])delete saved.encounter[key];
  saved.encounter.rogue=readRogueSettings();
 }
 if(isEnhancement()){
  for(const key of ['rotation','customPriority','priorityRows','rotationConditions','majorManaPotionCharges','useMajorManaPotion'])delete saved.encounter[key];
  saved.encounter.enhancement=readEnhancementSettings();
 }
 if($('classKey').value==='priest'){
  for(const key of ['rotation','customPriority','priorityRows','rotationConditions','combatItems','majorManaPotionCharges','useMajorManaPotion'])delete saved.encounter[key];
  saved.encounter.shadowTargetInitialHealth=Number($('shadowTargetInitialHealth').value);
  saved.encounter.shadowPriestHealth=Number($('shadowPriestHealth').value);
 }
 return saved;
};
const beforeElementalPreset=loadPreset;
loadPreset=async function(token=generation){
 await beforeElementalPreset(token);if(token!==generation||!request)return;
 const elemental=isRestrictedCaster();setRestrictedPanels();
 const shadow=$('classKey').value==='priest',enhancement=isEnhancement(),rogue=isRogue(),feral=isFeral();
 $('feralSettings').hidden=!feral;$('feralFixedRotation').hidden=!feral;$('feralTalentScope').hidden=!feral;
 if(feral){$('rotationSubtitle').textContent='Fixed Cat configured priority from the exact preparation receipt. Shifting Power can act at t=0 after the preview boundary.';}
 $('rogueSettings').hidden=!rogue;$('rogueFixedRotation').hidden=!rogue;
 if(rogue)$('rogueRotationEntries').textContent='Preview the actual fixed configured priority. Live conditions decide execution.';
 $('enhancementSettings').hidden=!enhancement;$('enhancementFixedRotation').hidden=!enhancement;
 if(enhancement){$('enhancementRotationEntries').textContent='Preview the actual fixed initial priority. Live conditions decide which action executes.';}
 $('rotationSubtitle').textContent=shadow?'Shadow uses a fixed toggle-owned priority derived from the selected talents. Custom ordered overrides are unavailable.':'Automatic priorities follow your current talents. A custom ordered list overrides them only when explicitly applied.';
 $('shadowHealthSettings').hidden=!shadow;$('shadowFixedRotation').hidden=!shadow;
 if(shadow){$('shadowTargetInitialHealth').value=request.targetInitialHealthPercent;$('shadowPriestHealth').value=request.priestMaximumHealth;$('shadowRotationEntries').textContent='Preview the actual fixed toggle-owned priority.';}
 if(enhancement)$('rotationSubtitle').textContent='Enhancement uses the engine’s fixed toggle-owned initial priority. Live conditions decide execution; custom priorities are unavailable.';
 if(feral)$('rotationSubtitle').textContent='Fixed Cat configured priority from the exact preparation receipt. Shifting Power may act at t=0 after this boundary.';
 if(rogue)$('rotationSubtitle').textContent=restrictedCasterName()+' uses the existing fixed configured priority. Live combo points, Energy, auras and eligibility decide execution; custom priorities are unavailable.';
 $('priorityEditor').hidden=shadow||enhancement||rogue||feral;$('rotationRow').hidden=shadow||enhancement||rogue||feral||$('classKey').value==='hunter';
 $('racialActive').disabled=elemental;
 if(elemental)$('racialActive').checked=false;
 $('iterations').disabled=elemental;if(elemental)$('iterations').value=1;
 $('iterationHelp').textContent=elemental?restrictedCasterName()+' supports one deterministic run; batch statistics are unavailable.':'One run is deterministic debugging; choose 2-100 independently seeded runs for DPS statistics.';
 const scriptGroup=$('scriptedBossEnabled').closest('.settings-group');if(scriptGroup)scriptGroup.hidden=elemental;
 if(elemental){
  $('classCapabilityNotes').textContent+=' Racial active and combat passive effects are not modeled here beyond explicit baseline primary-stat assumptions. '+(feral?'Historical baseline owns pre-talent Health/Mana; manual AP is not an observation. Troll is extrapolated. Tauren Endurance Health is omitted.':shadow?'Supplied observed or historical-proxy naked Health takes precedence over the manual Priest health fallback.':'Health uses a manual scenario cap only without supplied baseline Health.');
  $('priorityStatus').textContent=restrictedCasterName()+' engine-owned initial action order only; custom predicates and target overrides are unavailable.';
  $('distributionNotice').textContent=restrictedCasterName()+': one deterministic run; batch statistics unavailable.';
  renderMods();
 }
};
$('shadowPreviewRotation').addEventListener('click',async()=>{
 try{const preview=await post('/api/forever/rotation/preview',body());$('shadowRotationEntries').textContent=preview.entries.map(x=>human(x.abilityKey)).join(' > ');}
 catch(error){note(error.message);}
});
for(const id of ['duration','seed','iterations','targetHealth','timeline','shadowTargetInitialHealth','shadowPriestHealth'])$(id).addEventListener('change',()=>{buildDirty=true;queueStatRefresh();});
// A base preset can be internally valid before later wrappers restore class
// controls, encounter scripts and source coverage. Keep that entire preparation
// boundary unavailable to editing, export and execution, including import rollback.
let buildPreparationPending=false,buildImportInFlight=false,pendingClassCatalog=null,catalogLoadId=0;
function setBuildPreparing(preparing){
 buildPreparationPending=preparing;
 const busy=preparing||buildImportInFlight||simulationInFlight;
 const identityReady=!!request&&loadedClass===$('classKey').value&&loadedSpec===$('spec').value;
 const ready=!busy&&identityReady&&(!isFeral()||feralPreviewMatches())&&(!isCombatRogue()||combatPreviewMatches());
 for(const id of ['run','exportBuild','save'])$(id).disabled=!ready;
 for(const id of ['importBuild','load','race','provisional'])$(id).disabled=busy;
 // Class/spec remain available for fast preset switches, but an import is one
 // transaction and must finish validation or restoration before switching.
 $('classKey').disabled=buildImportInFlight||simulationInFlight;$('spec').disabled=buildImportInFlight||simulationInFlight;
 for(const selector of ['.workspace','.sidebar'])document.querySelector(selector).inert=busy;
 $('status').dataset.buildState=busy?'loading':ready?'ready':identityReady&&(isFeral()||isCombatRogue())?'input-required':'unavailable';
}
function requirePreparedBuild(){
 if(isCombatRogue()&&!combatPreviewMatches())throw Error('Enter valid Combat inputs, select an available baseline and wait for the exact current preparation.');
 if(isFeral()&&!feralPreviewMatches())throw Error('Enter valid manual Feral inputs, select the historical baseline and wait for the exact current preparation.');
 if(buildPreparationPending||buildImportInFlight||!request||loadedClass!==$('classKey').value||loadedSpec!==$('spec').value)
  throw Error('Selected build is still loading. Wait for all class settings to finish.');
}
function announcePreparedPreset(){
 const c=$('classKey').value,s=$('spec').value;
 if(isFeral()){note('Partial historical-provisional Feral 0/31/5: 36 allocated, 15 unspent. Enter manual pre-talent Cat AP in Encounter and explicitly select Historical baseline.');return;}
 if(c==='warrior'){
  note(s==='protection'?'Protection needs explicit setup in Encounter: enable tank mode, incoming boss attacks and the labelled manual shield / one-handed weapon assumptions. Allocate talents, including Shield Slam, in Talents.':'Warrior loaded with a legal unallocated tree. Allocate talents or import a proven mixed-tree build.');
  return;
 }
 const points=Object.values(request.talents?.ranks||{}).reduce((sum,rank)=>sum+Number(rank),0);
 note('Class defaults loaded with '+points+' explicitly ranked points and '+(51-points)+' unspent. '+(['druid','priest','rogue'].includes(c)||s==='enhancement'?'Partial historical '+human(s)+' starting allocation; not a complete build. ':'')+'Sources and unmodeled abilities are shown on talent tiles.');
}
// Every preset load invalidates pending stat and rotation responses, including rapid same-class switches.
const beforeRequestScopedPreset=loadPreset;
loadPreset=async function(token){
 const currentToken=token===undefined?++generation:token;
 if(currentToken!==generation)return;
 ++statsRefreshId;++baselineCheckId;baselineAvailable=false;invalidateFeralPreview();invalidateCombatPreview();setBuildPreparing(true);
 note('Loading '+human($('spec').value)+' source-backed starting build…');
 try{
  const catalog=pendingClassCatalog;
  if(catalog?.classKey===$('classKey').value)await catalog.work;
  if(currentToken!==generation)return;
  await beforeRequestScopedPreset(currentToken);
 }
 catch(error){if(currentToken===generation){request=null;loadedClass=null;loadedSpec=null;throw error;}}
 finally{if(currentToken===generation){
  setBuildPreparing(false);
  if(!buildImportInFlight&&request&&loadedClass===$('classKey').value&&loadedSpec===$('spec').value){announcePreparedPreset();queueStatRefresh();}
 }}
};
$('roguePreviewRotation').addEventListener('click',async()=>{
 try{
  if(isCombatRogue()){await previewCombatRogue();return;}
  const submitted=body(),identity=JSON.stringify(submitted),token=generation;
  const preview=await post('/api/forever/rotation/preview',submitted);
  if(token!==generation||!isRogue()||identity!==JSON.stringify(body()))return;
  $('rogueRotationEntries').textContent=preview.entries.map(x=>human(x.abilityKey)).join(' > ');
 }catch(error){note(error.message);}
});
for(const key of [...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...rogueWeaponTuningKeys]){
 $('rogue'+key[0].toUpperCase()+key.slice(1)).addEventListener('change',()=>{buildDirty=true;queueStatRefresh();});
}
$('enhancementPreviewRotation').addEventListener('click',async()=>{
 try{
  const submitted=body(),identity=JSON.stringify(submitted),token=generation;
  const preview=await post('/api/forever/rotation/preview',submitted);
  if(token!==generation||!isEnhancement()||identity!==JSON.stringify(body()))return;
  $('enhancementRotationEntries').textContent=preview.entries.map(x=>human(x.abilityKey)).join(' > ');
 }catch(error){note(error.message);}
});
for(const key of enhancementSettingKeys){
 const id='enhancement'+key[0].toUpperCase()+key.slice(1);
 $(id).addEventListener('change',()=>{buildDirty=true;queueStatRefresh();});
}
// Combat binds the existing controls to current preparation even before blur.
for(const id of [...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...rogueWeaponTuningKeys].map(key=>'rogue'+key[0].toUpperCase()+key.slice(1)).concat(['duration','seed','iterations','targetHealth','timeline','provisional','race'])){
 for(const event of ['input','change'])$(id).addEventListener(event,()=>{if(isCombatRogue()){buildDirty=true;queueStatRefresh();}});
}
// Native input invalidates immediately, including edits that have not blurred.
for(const id of [...feralManualKeys.map(feralControl),...feralBooleanKeys.map(feralControl),'feralTargetArmor','feralPreferredCatOpener','duration','seed','targetHealth','timeline','provisional','race']){
 for(const event of ['input','change'])$(id).addEventListener(event,()=>{if(isFeral()){buildDirty=true;queueStatRefresh();}});
}
$('feralPreviewRotation').addEventListener('click',async()=>{try{await preview();}catch(error){note(error.message);}});
// Reject duplicate catalog keys before any import wrapper mutates the working
// draft. The server remains authoritative for categories, families and values.
function validateImportedModifierDuplicates(data){
 const seen=new Set();
 for(const field of ['buffs','consumables','targetDebuffs']){
  const keys=data?.modifiers?.[field];
  if(keys===undefined)continue;
  if(!Array.isArray(keys))throw Error('Modifier '+field+' must be an array of keys.');
  for(const raw of keys){
   if(typeof raw!=='string'||!raw.trim())throw Error('Modifier keys must be nonempty strings.');
   const key=raw.trim().toLowerCase();
   if(seen.has(key))throw Error("Duplicate modifier '"+key+"'.");
   seen.add(key);
  }
 }
}
const beforePreparedImport=applyConfig;
applyConfig=async function(data){
 if(buildPreparationPending||buildImportInFlight)throw Error('Wait for the current build to finish loading before importing.');
 validateImportedModifierDuplicates(data);
 importDraftBeforeLock=(isFeral()||data?.classKey==='druid'&&data?.specialization==='feral'||isCombatRogue()||data?.classKey==='rogue'&&data?.specialization==='combat')&&request&&loadedClass?captureBuildDraft():null;
 buildImportInFlight=true;setBuildPreparing(false);
 try{return await beforePreparedImport(data);}
 finally{importDraftBeforeLock=null;buildImportInFlight=false;setBuildPreparing(false);if(isCombatRogue()? !combatPreviewMatches():!isFeral()||!feralPreviewMatches())queueStatRefresh();}
};
// Lock immediately, including validation/preview awaits. A second click cannot
// start another request; restore prior availability without undoing result actions
// that successful rendering has explicitly enabled.
let simulationInFlight=false;
const beforeSimulationLock=simulate;
simulate=async function(){
 if(simulationInFlight)return;
 requirePreparedBuild();
 simulationInFlight=true;
 const controls=[...document.querySelectorAll('input,select,textarea,button')].map(element=>({element,disabled:element.disabled}));
 for(const {element} of controls)element.disabled=true;
 try{return await beforeSimulationLock();}
 finally{for(const {element,disabled} of controls)if(element.disabled)element.disabled=disabled;simulationInFlight=false;setBuildPreparing(buildPreparationPending);}
};
function safe(task){return async()=>{try{await task();}catch(e){note(e.message);}};}
$('classKey').addEventListener('change',safe(initClass));
$('spec').addEventListener('change',safe(async()=>{
 const selected=$('spec').value;
 if(buildDirty&&loadedSpec&&selected!==loadedSpec){
   pendingPreset=selected;$('spec').value=loadedSpec;
   $('presetDialog').showModal();return;
 }
 await loadPreset();
}));
$('cancelPreset').addEventListener('click',()=>{pendingPreset=null;$('presetDialog').close();});
$('confirmPreset').addEventListener('click',safe(async()=>{const next=pendingPreset;pendingPreset=null;$('presetDialog').close();if(!next)return;$('spec').value=next;await loadPreset();}));
$('race').addEventListener('change',safe(async()=>{buildDirty=true;await loadCatalog();await checkBaseline();queueStatRefresh();updateIdentity();}));
 $('provisional').addEventListener('change',safe(async()=>{await checkBaseline();queueStatRefresh();}));
$('resetTalents').addEventListener('click',safe(loadPreset));
$('clearTalents').addEventListener('click',()=>{if(!request?.talents)return;request.talents.ranks={};for(const key of Object.keys(request.talents))if(key.endsWith('PointsSpent'))request.talents[key]=0;recalculateTalentTotals();itemizedMode=true;buildDirty=true;renderTalents();note('All talent ranks cleared. Strict itemized mode active.');});$('showCatalogAll').addEventListener('change',()=>{
 showCatalogAll=$('showCatalogAll').checked;pickerPage=1;pickerSelected=null;
 renderPicker();$('pickerDetails').textContent=showCatalogAll?
  'Browsing all offline records. Unavailable entries are inspectable but cannot be equipped.':
  'Select a compatible item to inspect.';
});
$('itemSearch').addEventListener('input',()=>{pickerPage=1;pickerSelected=null;pickerDetailsLoad++;renderPicker();$('pickerDetails').classList.remove('is-loading');$('pickerDetails').removeAttribute('aria-busy');$('pickerDetails').textContent='Select an item to inspect its details.';});
$('validateGear').addEventListener('click',safe(validateGear));
$('run').addEventListener('click',safe(simulate));
$('save').addEventListener('click',safe(async()=>{requirePreparedBuild();localStorage.setItem('warcraftsim-solo-dps-v1',pretty(saveConfig()));note('Build saved in this browser.');}));
$('load').addEventListener('click',safe(async()=>{const saved=localStorage.getItem('warcraftsim-solo-dps-v1');if(!saved)throw Error('No local build exists.');await applyConfig(JSON.parse(saved));note('Build loaded.');}));
$('exportBuild').addEventListener('click',safe(async()=>{requirePreparedBuild();download('warcraftsim-build.json',saveConfig());}));
$('importBuild').addEventListener('click',()=>$('file').click());
$('file').addEventListener('change',safe(async()=>{const f=$('file').files?.[0];if(!f)return;try{await applyConfig(JSON.parse(await f.text()));note('Build imported.');}finally{$('file').value='';}}));
$('priorityText').addEventListener('input',()=>{priorityRows=$('priorityText').value.split(/[\n,]+/).map(x=>x.trim()).filter(Boolean).map(key=>({key,enabled:true}));drawPriorityRows();});
$('loadPriority').addEventListener('click',safe(inspectPriority));
$('applyPriority').addEventListener('click',safe(applyPriority));
$('resetPriority').addEventListener('click',()=>{if(!request)return;request.customPriority=[];if($('classKey').value==='warrior'){request.rotationMode='automatic';request.customRotation=[];}buildDirty=true;$('priorityText').value='';priorityRows=[];drawPriorityRows();$('priorityStatus').textContent='Automatic, talent-aware runner default selected.';});
$('pickerTabItems').addEventListener('click',()=>setPickerTab('items'));
 $('pickerTabEnchants').addEventListener('click',()=>setPickerTab('enchants'));
 $('closePicker').addEventListener('click',()=>$('itemDialog').close());
$('unequip').addEventListener('click',async()=>{const previous=clone(cfg.equipment);cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==activeSlot);try{await validateGear();buildDirty=true;renderGear();$('itemDialog').close();note('Slot cleared.');}catch(e){cfg.equipment=previous;showError(e);}});
for(const tab of document.querySelectorAll('.tab')){tab.addEventListener('click',()=>navigate(tab.dataset.view));tab.addEventListener('keydown',e=>{if(e.key!=='ArrowLeft'&&e.key!=='ArrowRight')return;const tabs=[...document.querySelectorAll('.tab')];const next=tabs[(tabs.indexOf(tab)+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length];next.focus();navigate(next.dataset.view);});}
$('exportResults').addEventListener('click',()=>{if(results)download('warcraftsim-simulation-report.json',results);});
 $('replayResult').addEventListener('click',safe(replayLastResult));
$('petFamily').replaceChildren(...families.map(f=>option(f,f)));
(async()=>{try{
 const capabilities=await api('/api/forever/shared/capabilities');
 classCapabilities=capabilities.classes||[];
 const r=await api('/api/forever/character/races');races=r.races;const iconList=await api('/api/forever/icons/available');availableIconKeys=new Set(iconList.files||[]);await initClass();const initial=new URLSearchParams(window.location.search).get('view');if(['equipment','talents','effects','encounter','rotation','results'].includes(initial))navigate(initial);}catch(e){note('Initialization failed: '+e.message);}})();
})();
