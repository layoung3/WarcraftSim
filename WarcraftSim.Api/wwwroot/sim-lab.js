/* CP153: development-only, API-backed simulation viewer. No combat calculations in JS. */
'use strict';
const $ = id => document.getElementById(id);
const endpoints = {
  Hunter: [['Marksmanship', 'marksmanship-hunter'], ['Survival', 'survival-hunter']],
  Druid: [
    ['Balance', 'balance-druid'], ['Feral Cat', 'feral-druid', 'role=cat'],
    ['Feral Bear', 'feral-druid', 'role=bear'], ['Restoration', 'restoration-druid']
  ],
  Rogue: [['Assassination', 'rogue', 'spec=assassination'], ['Combat', 'rogue', 'spec=combat'], ['Subtlety', 'rogue', 'spec=subtlety']],
  Mage: [['Arcane', 'mage', 'spec=arcane'], ['Fire', 'mage', 'spec=fire'], ['Frost', 'mage', 'spec=frost']],
  Warlock: [['Affliction', 'warlock', 'spec=affliction'], ['Demonology', 'warlock', 'spec=demonology'], ['Destruction', 'warlock', 'spec=destruction']],
  Shaman: [['Elemental', 'elemental-shaman'], ['Enhancement', 'enhancement-shaman'], ['Restoration', 'restoration-shaman']],
  Priest: [['Shadow', 'shadow-priest'], ['Holy / Discipline preview', 'healer-preview']]
};
// Only create quick controls if the selected request actually exposes the field.
const simpleFields = [
  ['useRapidFire', 'Rapid Fire'],
  ['enablePet', 'Warlock companion enabled'],
  ['allowTemporaryDemon', 'Permit temporary summon'],
  ['allowDoomguardRitual', 'Permit Doomguard ritual'],
  ['weaveTravelSeconds', 'Movement travel each way (s)'],
  ['weaveMinimumMeleeSeconds', 'Minimum melee time (s)'],
  ['weaveManaReserve', 'Weaving mana reserve'],
  ['trapTriggerDelaySeconds', 'Trap trigger delay (s)'], ['triggerTraps', 'Activate traps'],
  ['useFireTraps', 'Use Fire trap'], ['useFrostTraps', 'Use Frost trap'], ['useSniperShot', 'Sniper Shot'],
  ['useAimedShot', 'Aimed Shot'], ['useMultiShot', 'Multi-Shot'],
  ['useSerpentSting', 'Serpent Sting'], ['useArcaneShot', 'Arcane Shot'],
  ['additionalTargets', 'Extra enemies'], ['targetMaximumHealth', 'Target maximum HP'],
  ['targetArmor', 'Target armor'], ['enableBossAttacks', 'Boss attacks'],
  ['bossHitDamage', 'Boss hit damage'], ['bossSwingSeconds', 'Boss swing (s)'],
  ['bearBaseArmor', 'Bear armor'], ['bearDodgePercent', 'Bear dodge (%)'],
  ['tankBurstWindowSeconds', 'Burst window (s)'], ['incomingBossHitDamage', 'Incoming boss damage'],
  ['bossSwingIntervalSeconds', 'Boss interval (s)'], ['incomingDamage', 'Incoming ally damage'],
  ['damageEverySeconds', 'Ally damage interval (s)'], ['additionalAllies', 'Extra allies'],
  ['allyStartingHealthPercent', 'Allies initial HP (%)'], ['allyMaximumHealth', 'Allies max HP'],
  ['startingManaPercent', 'Starting mana (%)'], ['meleeCritPercent', 'Melee crit (%)'],
  ['spellCritPercent', 'Spell crit (%)'], ['useFeralCooldowns', 'Use Feral cooldowns'],
  ['useFaerieFire', 'Use Faerie Fire'], ['useThorns', 'Use Thorns'],
  ['useHurricane', 'Use Hurricane'], ['useMoonfire', 'Use Moonfire'],
  ['useEclipse', 'Use Eclipse'], ['useBearUtility', 'Bear utility'],
  ['targetBehind', 'Behind target'], ['startInProwl', 'Start in Prowl'],
  ['swiftmendHealthThresholdPercent', 'Swiftmend HP threshold (%)']
];
const eventNames = ['SimulationStarted','SimulationEnded','RotationDecision','AbilityCastStarted','AbilityCastCompleted','AbilityEffectImpact','Damage','Healing','ResourceChanged','AuraApplied','AuraRemoved','AuraExpiration','PeriodicTick','ActorDied','EncounterPhaseStarted','EncounterPhaseEnded','AbilityCastCancelled','ScriptedDamage','EncounterDamagePatternOccurrence','EncounterDamageSequenceHit','ThreatChanged','ForcedTargetApplied','ForcedTargetRemoved','ForcedTargetExpiration','AbsorbApplied','AbsorbConsumed','AbsorbRemoved','AbsorbExpiration','AbilityChannelStarted','AbilityChannelTick','AbilityChannelCompleted','AbilityChannelCancelled','AutoAttackStarted','AutoAttackSwing','AutoAttackStopped','HealthChanged','ReactiveOpportunityGranted','EncounterBossAbilityStart','EncounterBossAbilityImpact','EncounterBossAbilityTick','EncounterBossCastStarted','EncounterBossCastCompleted','EncounterBossInterruptRequested','EncounterBossCastInterrupted'];
const format = v => typeof v === 'number' ? v.toLocaleString(undefined, {maximumFractionDigits:2}) : String(v ?? '—');
const eventName = type => typeof type === 'number' ? (eventNames[type] ?? `Event ${type}`) : String(type ?? 'Unknown');
const textCell = (row,value) => {const td=document.createElement('td');td.textContent=String(value ?? '—');row.append(td);};
function putMetrics(target, pairs){target.replaceChildren();for(const [label,value] of pairs){if(value === null || value === undefined)continue;const tile=document.createElement('div');tile.className='metric';const name=document.createElement('small');name.textContent=label;const val=document.createElement('strong');val.textContent=format(value);tile.append(name,val);target.append(tile);}}
const specs = () => endpoints[$('classSelect').value] || [];
const current = () => specs().find(s => s[0] === $('specSelect').value) || specs()[0];
let last=null, currentRequest=null, timeline=[];
let presetLoadId=0;
const defaultInputLatencyMs = 80;
const status = text => {$('status').textContent=text;};
function download(name,content){const blob=new Blob([content],{type:'application/json'});const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1200);}
function responseBody(request){if (!request || typeof request !== 'object' || Array.isArray(request))throw new Error('Request must be a JSON object.');return request;}
function renderRequest(){
  const q=currentRequest || {};
  // API contracts do not contain this cross-cutting option: it is processed by
  // the shared API middleware and should survive exports and saved setups.
  if(currentRequest && !Object.prototype.hasOwnProperty.call(q, 'inputLatencyMs'))
    q.inputLatencyMs=defaultInputLatencyMs;
  $('inputLatencyMs').value=q.inputLatencyMs ?? defaultInputLatencyMs;
  $('duration').value=q.durationSeconds ?? 120; $('seed').value=q.seed ?? 0;
  $('timelineLabel').hidden=!Object.prototype.hasOwnProperty.call(q,'captureTimeline');$('timeline').checked=q.captureTimeline===true;
  $('iterationNotice').textContent=Object.prototype.hasOwnProperty.call(q,'iterations')?'Iterations are supported by this endpoint and editable in advanced JSON.':'Single encounter per run; this endpoint does not expose an iteration count.';
  const parent=$('scenarioOptions');parent.replaceChildren();for(const [key,label] of simpleFields){if(!Object.prototype.hasOwnProperty.call(q,key))continue;
    const wrap=document.createElement('label');wrap.textContent=label;const value=q[key];const input=document.createElement('input');input.type=typeof value==='boolean'?'checkbox':'number';if(input.type==='checkbox')input.checked=value;else {input.value=value;input.step='any';if(typeof value==='number'&&value>=0)input.min=0;}
    input.addEventListener('change',()=>{currentRequest[key]=input.type==='checkbox'?input.checked:Number(input.value);writeEditor();});wrap.append(input);parent.append(wrap);
  }
  // Warlock selections are request-backed; JSON remains authoritative.
  if($('classSelect').value==='Warlock' && currentRequest){
    const petFamilies=[
      ['none','None'],['imp','Imp'],['voidwalker','Voidwalker'],
      ['succubus','Succubus'],['incubus','Incubus'],['felhunter','Felhunter'],
      ['infernal','Infernal (temporary)'],['doomguard','Doomguard (ritual)']
    ];
    const choices=[
      ['active','Active Demon',currentRequest.enablePet ? currentRequest.petType : 'none',
        petFamilies],
      ['sacrifice','Pre-pull sacrificed demon',currentRequest.prePullSacrificedDemon ?? 'none',
        petFamilies.filter(([key])=>!['infernal','doomguard'].includes(key))]
    ];
    for(const [kind,label,value,options] of choices){
      const wrap=document.createElement('label');wrap.textContent=label;
      const select=document.createElement('select');
      for(const [key,name] of options)select.add(new Option(name,key));
      select.value=value;
      select.addEventListener('change',()=>{
        if(kind==='active'){
          currentRequest.enablePet=select.value!=='none';
          if(currentRequest.enablePet){
            currentRequest.petType=select.value;
            // Selecting a demon never silently grants ritual permissions.
          }
        } else {
          currentRequest.prePullSacrificedDemon=select.value==='none'?null:select.value;
        }
        renderRequest();
      });
      wrap.append(select);parent.prepend(wrap);
    }
    const note=document.createElement('p');note.className='hint';
    note.textContent='Demonic Pact is controlled by the selected talents. Temporary demon permissions and detailed assumptions remain editable in Advanced JSON.';
    parent.append(note);
  }
  // Survival alone exposes melee-first weaving. The old timed hybrid remains
  // accessible only when a legacy setup explicitly requests it.
  if($('classSelect').value==='Hunter' && $('specSelect').value==='Survival' &&
     Object.prototype.hasOwnProperty.call(q, 'combatMode')){
    const wrap=document.createElement('label');wrap.textContent='Survival combat behavior';
    const select=document.createElement('select');
    const modes=[
      ['weave', 'Melee-first: ranged cooldown excursions'],
      ['melee', 'Melee only'],
      ['ranged', 'Ranged only']
    ];
    if(q.combatMode==='hybrid')modes.push(['hybrid', 'Legacy fixed one-way switch']);
    for(const [value,label] of modes)select.add(new Option(label,value));
    select.value=q.combatMode;
    select.addEventListener('change',()=>{
      currentRequest.combatMode=select.value;
      renderRequest();
    });
    wrap.append(select);
    parent.prepend(wrap);
  }
  writeEditor();
}
function writeEditor(){$('requestJson').value=JSON.stringify(currentRequest,null,2);}
function readInputLatencyMs(){
  const input=$('inputLatencyMs').value.trim();
  if(input==='')throw new Error('Input latency is required (0 to 1000 ms).');
  const ms=Number(input);
  if(!Number.isFinite(ms) || ms<0 || ms>1000)
    throw new Error('Input latency must be between 0 and 1000 milliseconds.');
  return ms;
}
function updateFromTop(){
  if(!currentRequest)return;
  try{
    const ms=readInputLatencyMs();
    currentRequest.durationSeconds=Number($('duration').value);
    if(Object.prototype.hasOwnProperty.call(currentRequest,'seed'))
      currentRequest.seed=Number($('seed').value);
    if(Object.prototype.hasOwnProperty.call(currentRequest,'captureTimeline'))
      currentRequest.captureTimeline=$('timeline').checked;
    currentRequest.inputLatencyMs=ms;
    writeEditor();
  }catch(error){status(error.message);}
}
const presetKey = (className, specName) => `${className.toLowerCase()}-${specName.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')}`;
// Talent files are static configuration ONLY. The request still runs against the
// existing API and uses its real request DTO, rules, and execution engine.
async function loadPreset(){
  const s=current(); if(!s)return;
  const generation=++presetLoadId;
  const className=$('classSelect').value;
  const useTalents=$('useTalentPresets').checked;
  currentRequest=null;
  $('requestJson').value='';
  $('run').disabled=true;
  $('defaultTalentInfo').textContent='Loading simulation defaults…';
  status('Loading API defaults…');
  try {
    const path=`/api/sim/${s[1]}/defaults${s[2]?'?'+s[2]:''}`;
    const response=await fetch(path,{cache:'no-store'});
    const data=await response.json();
    if(!response.ok)throw new Error(data.error||data.Error||`HTTP ${response.status}`);
    const request=responseBody(data);
    let note='Using original API defaults (including any API-provided talent ranks).';
    if(useTalents){
      const key=presetKey(className,s[0]);
      const talentResponse=await fetch(`/default-talents/${key}.json`,{cache:'no-store'});
      if(!talentResponse.ok)throw new Error(`Talent preset ${key}.json unavailable (HTTP ${talentResponse.status})`);
      const preset=await talentResponse.json();
      const overrides=preset?.overrides;
      if(preset.id!==key || !overrides || typeof overrides!=='object' || Array.isArray(overrides))
        throw new Error(`Invalid default talent preset: ${key}.json`);
      const talentKey=key==='priest-holy-discipline-preview'?'priestTalents':'talents';
      if(!overrides[talentKey] || !Object.keys(overrides[talentKey].ranks||{}).length || !(talentKey in request))
        throw new Error(`Talent preset ${key} does not match the selected API request`);
      // Override only explicit fields; preserve the server's entire request shape.
      Object.assign(request,overrides);
      const rankCount=Object.keys(overrides[talentKey].ranks).length;
      note=`Default talent build loaded: ${rankCount} modeled talents. ${preset.description||''}`;
    }
    // Switching class/spec can complete older HTTP requests out of order.
    if(generation!==presetLoadId)return;
    currentRequest=request;
    renderRequest();
    $('defaultTalentInfo').textContent=note;
    $('run').disabled=false;
    status(`Loaded ${className} / ${s[0]}.`);
  } catch(error) {
    if(generation!==presetLoadId)return;
    $('defaultTalentInfo').textContent='Preset loading failed. Retry, or uncheck default talents to use API defaults.';
    status('Could not load setup: '+error.message);
    // Fail closed: never silently run the previous specialization's request.
    $('run').disabled=true;
  }
}
function populateSpecs(){const old=$('specSelect').value;$('specSelect').replaceChildren();for(const spec of specs())$('specSelect').add(new Option(spec[0],spec[0]));if(specs().some(x=>x[0]===old))$('specSelect').value=old;loadPreset();}
function applyJson(){try{currentRequest=responseBody(JSON.parse($('requestJson').value));renderRequest();status('Advanced request JSON applied.');}catch(error){status('Invalid JSON: '+error.message);}}
function addDetails(response,summary){const duration=Number(summary.durationSeconds || currentRequest?.durationSeconds || 0);const result=[
  ['Ending mana',response.endingMana],['Maximum mana',response.maximumMana],['Ending energy',response.endingEnergy],['Ending rage',response.endingRage],['Combo points',response.comboPoints],['Omen procs',response.omenProcs],['Clearcasts used',response.clearcastsUsed],
  ['Incoming damage',response.damageTaken ?? summary.damageTaken],['Armor prevented',response.armorPreventedDamage],['Dodged boss hits',response.dodgedIncomingHits],['Maximum burst damage',response.maximumBurstDamage],['Maximum burst / HP (%)',response.maximumBurstPercentOfHealth],
  ['Projected ending HP',response.projectedEndingHealth],['Minimum projected HP',response.minimumProjectedHealth],['Effective self-healing',response.effectiveSelfHealing],['Wasted self-healing',response.wastedSelfHealing],['Threat generated',response.threatGenerated],['TPS',response.threatPerSecond],
  ['Effective healing',response.healingDone ?? summary.healingDone],['Overhealing',response.overhealingDone ?? summary.overhealingDone],['Healing threat',response.run?.customMetrics?.['druid-healing-threat']], ['Processed events',response.run?.processedEventCount]
];
// The engine emits signed resource changes. Summarize only what the captured
// event stream reports; without timeline capture no totals are invented.
const resourceDeltas=new Map();
for(const e of timeline){if(eventName(e.type)!=='ResourceChanged' || !e.resourceKey || typeof e.amount!=='number')continue;
  const key=String(e.resourceKey);const totals=resourceDeltas.get(key)||{gained:0,spent:0};
  if(e.amount>=0)totals.gained+=e.amount;else totals.spent-=e.amount;
  resourceDeltas.set(key,totals);
}
for(const [resource,totals] of resourceDeltas){result.push([resource+' gained (captured)',totals.gained]);result.push([resource+' spent (captured)',totals.spent]);}
putMetrics($('detailsMetrics'),result);const custom=response.run?.customMetrics||{};putMetrics($('customMetrics'),Object.entries(custom));
const damage=response.damageDone??summary.damageDone;const healing=response.healingDone??summary.healingDone;const dps=response.dps??(duration>0&&damage!=null?damage/duration:null);const hps=duration>0&&healing!=null?healing/duration:null;
putMetrics($('metrics'),[['Damage done',damage],['DPS',dps],['Effective healing',healing],['HPS',hps],['Overhealing',response.overhealingDone??summary.overhealingDone],['Threat',response.threatGenerated],['TPS',response.threatPerSecond]]);}
function showAbilities(response,summary){const entries=summary.abilities||{};const damages=response.damageByAbility||summary.damageDoneByAbility||{};const heals=response.healingByAbility||summary.healingDoneByAbility||{};
  const total=Number(response.damageDone??summary.damageDone??0)+Number(response.healingDone??summary.healingDone??0);
  const keys=new Set([...Object.keys(entries),...Object.keys(damages),...Object.keys(heals)]);
  const rows=[...keys].map(key=>{const a=entries[key]||{};return {key,uses:a.castCompletedCount??null,hits:a.damageOccurrenceCount??null,crits:(a.criticalDamageCount!=null||a.criticalHealingCount!=null)?Number(a.criticalDamageCount||0)+Number(a.criticalHealingCount||0):null,damage:a.damageDone??damages[key]??null,healing:a.healingDone??heals[key]??null};}).sort((a,b)=>(Number(b.damage||0)+Number(b.healing||0))-(Number(a.damage||0)+Number(a.healing||0)));
  $('abilitiesBody').replaceChildren();for(const a of rows){const tr=document.createElement('tr');for(const v of [a.key,a.uses,a.hits,a.crits,a.damage==null?null:format(a.damage),a.healing==null?null:format(a.healing),total>0?format((Number(a.damage||0)+Number(a.healing||0))*100/total)+'%':null])textCell(tr,v);$('abilitiesBody').append(tr);}
  if(!rows.length){const tr=document.createElement('tr');textCell(tr,'No server ability breakdown reported.');$('abilitiesBody').append(tr);}}
function showTimeline(){const type=$('typeFilter').value;const query=$('eventSearch').value.trim().toLowerCase();const limit=$('rowLimit').value==='all'?Infinity:Number($('rowLimit').value);const matching=timeline.filter(e=>(!type||eventName(e.type)===type)&&(!query||[e.sourceActorKey,e.targetActorKey,e.abilityKey,e.effectKey,e.description,e.resultKey,e.resourceKey].some(v=>String(v||'').toLowerCase().includes(query))));
  const rows=matching.slice(0,limit);const body=$('timelineBody');body.replaceChildren();const fragment=document.createDocumentFragment();for(const e of rows){const tr=document.createElement('tr');for(const value of [format(e.timeSeconds),eventName(e.type),e.sourceActorKey,e.abilityKey||e.effectKey,e.targetActorKey,e.amount==null?null:format(e.amount),[e.resultKey,e.resourceKey,e.isCritical?'CRIT':'',e.description].filter(Boolean).join(' · ')||null])textCell(tr,value);fragment.append(tr);}body.append(fragment);
  $('timelineNotice').textContent=`Showing ${rows.length.toLocaleString()} of ${matching.length.toLocaleString()} filtered events (${timeline.length.toLocaleString()} captured). ${timeline.length?'':'Enable Capture timeline in configuration to populate this table.'}`;}
function showResult(response){const s=current();const summary=response.run?.summary||{};const length=currentRequest?.durationSeconds??summary.durationSeconds; $('resultsPanel').hidden=false;$('runSummary').textContent=`${$('classSelect').value} · ${s[0]} · ${format(length)} seconds · seed ${format(currentRequest?.seed)}`;
  timeline=Array.isArray(response.run?.timeline)?response.run.timeline:[];$('eventCount').textContent=`${format(timeline.length)} captured events`;addDetails(response,summary);showAbilities(response,summary);
  const types=[...new Set(timeline.map(e=>eventName(e.type)))].sort();$('typeFilter').replaceChildren(new Option('All captured events',''),...types.map(t=>new Option(t,t)));$('eventSearch').value='';showTimeline();$('rawResponse').textContent=last.responseRaw;}
async function run(){try{
  const request=responseBody(JSON.parse($('requestJson').value));
  // The full JSON is authoritative, including explicit values supplied from
  // Advanced JSON. The input field stays synchronized by updateFromTop().
  const ms=Number(request.inputLatencyMs ?? readInputLatencyMs());
  if(!Number.isFinite(ms) || ms<0 || ms>1000)
    throw new Error('Input latency must be between 0 and 1000 milliseconds.');
  request.inputLatencyMs=ms;
  $('inputLatencyMs').value=ms;
  const requestRaw=JSON.stringify(request);
  currentRequest=request;
  writeEditor();
  $('run').disabled=true;$('exportResults').disabled=true;status('Running simulation…');const s=current();const url=`/api/sim/${s[1]}/run`;const resp=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:requestRaw});const responseRaw=await resp.text();let response;try{response=JSON.parse(responseRaw);}catch{throw new Error(`HTTP ${resp.status}: ${responseRaw.slice(0,250)}`);}if(!resp.ok)throw new Error(response.error||response.Error||`HTTP ${resp.status}`);
  last={requestRaw,responseRaw,className:$('classSelect').value,specName:s[0],endpoint:s[1]};showResult(response);$('exportResults').disabled=false;status('Simulation completed. Export includes the entire returned payload.');
}catch(error){status('Simulation error: '+error.message);}finally{$('run').disabled=false;}}
function exportResults(){if(!last)return;const meta=JSON.stringify({class:last.className,specialization:last.specName,endpoint:last.endpoint,exportedAtUtc:new Date().toISOString(),iterations:1});download(`warcraftsim-${last.endpoint}-${last.specName.toLowerCase().replace(/[^a-z0-9]+/g,'-')}.json`,`{"metadata":${meta},"request":${last.requestRaw},"response":${last.responseRaw}}`);}
$('loadDefault').addEventListener('click',loadPreset);$('useTalentPresets').addEventListener('change',loadPreset);$('classSelect').addEventListener('change',populateSpecs);$('specSelect').addEventListener('change',loadPreset);
for(const id of ['duration','seed','timeline','inputLatencyMs'])$(id).addEventListener('change',updateFromTop);
$('applyJson').addEventListener('click',applyJson);$('run').addEventListener('click',run);$('exportResults').addEventListener('click',exportResults);
$('exportSetup').addEventListener('click',()=>{try{download('warcraftsim-setup.json',JSON.stringify(responseBody(JSON.parse($('requestJson').value)),null,2));}catch(e){status(e.message);}});
$('importSetup').addEventListener('click',()=>$('setupFile').click());$('setupFile').addEventListener('change',async()=>{const file=$('setupFile').files?.[0];if(!file)return;try{const obj=JSON.parse(await file.text());currentRequest=responseBody(obj.request??obj);renderRequest();status('Setup imported. Check the class/spec selection before running.');}catch(e){status('Import error: '+e.message);}finally{$('setupFile').value='';}});
for(const id of ['typeFilter','eventSearch','rowLimit'])$(id).addEventListener(id==='eventSearch'?'input':'change',showTimeline);
for(const name of Object.keys(endpoints))$('classSelect').add(new Option(name,name));$('classSelect').value='Druid';populateSpecs();
