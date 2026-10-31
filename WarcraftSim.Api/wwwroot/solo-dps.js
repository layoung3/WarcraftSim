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
const specMap={mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery']};
const slots=['head','neck','shoulder','back','chest','wrist','hands','waist','legs','feet','finger1','finger2','trinket1','trinket2','main-hand','off-hand','ranged'];
const families=['bat','bear','bird-of-prey','boar','carrion-bird','cat','core-hound','crab','crocolisk','fox','gorilla','hyena','raptor','scorpid','spider','tallstrider','turtle','wind-serpent','wolf'];
let races=[],catalog=[],enchants=[],modifiers=[],request=null,results=null,generation=0;
const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[]}};
const note=(msg)=>{$('status').textContent=msg;};
function slotsFor(item){return item.allowedSlots||[];}
function selectedRace(){return races.find(x=>x.key===$('race').value);}
function buildCharacter(){return {name:'Solo DPS Character',rulesetKey:'wow-forever',classKey:$('classKey').value,specializationKey:$('spec').value,level:60,race:$('race').value,equipment:clone(cfg.equipment)};}
function mods(){return clone(cfg.effects);}
function validMode(){if(!$('race').value)throw Error('Select an eligible race.');return $('provisional').checked;}
function body(){
 if(!request)throw Error('No class defaults loaded.');
 const c=$('classKey').value,s=$('spec').value,r=clone(request);
 r.specialization=s;r.useReviewedLevel60Baseline=!validMode();r.useProvisionalLevel60Baseline=validMode();
 r.useRacialActive=$('racialActive').checked;r.externalModifiers=mods();
 r.durationSeconds=Number($('duration').value);r.seed=Number($('seed').value);r.targetMaximumHealth=Number($('targetHealth').value);r.captureTimeline=$('timeline').checked;
 if(!Number.isInteger(r.seed)||r.durationSeconds<=0||r.durationSeconds>1200||r.targetMaximumHealth<=0)throw Error('Check encounter duration, seed and target health.');
 if(c==='hunter'){r.race=$('race').value;r.equipment=clone(cfg.equipment);r.petFamily=$('petFamily').value;}
 else {r.character=buildCharacter();}
 const property=c==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[s]):c;
 return {classKey:c,specialization:s,[property]:r};
}
function resetSelections(){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[]};renderGear();renderMods();}
async function initClass(){
 const token=++generation,c=$('classKey').value;note('Loading '+c+' configuration…');$('spec').replaceChildren(...specMap[c].map(s=>option(s,s.replaceAll('-',' '))));
 const eligible=races.filter(r=>r.classes.includes(c));$('race').replaceChildren(...eligible.map(r=>option(r.key,r.name+' · '+r.faction)));
 resetSelections();await loadPreset(token);
}
async function loadPreset(token=generation){
 const c=$('classKey').value,s=$('spec').value;
 try{
  const route=c==='hunter'?'/api/sim/'+s+'-hunter/defaults':'/api/sim/'+c+'/defaults?spec='+encodeURIComponent(s);
  const initial=await api(route);
  const preset=await api('/default-talents/'+c+'-'+s+'.json').catch(()=>null);
  if(token!==generation)return;
  request=initial;
  // The typed defaults endpoint owns the validated specialization build.\n  // JSON demonstration presets are intentionally not substituted here: several\n  // carry provisional allocations and may not satisfy current backend rules.
  if(preset?.overrides?.petTalents)request.petTalents={...request.petTalents,...preset.overrides.petTalents};
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;$('targetHealth').value=request.targetMaximumHealth;
  $('timeline').checked=!!request.captureTimeline;
  renderTalents();note('Class defaults loaded. Select a baseline mode and resolve stats.');
  await checkBaseline();
 }catch(error){if(token===generation){request=null;note('Cannot load class configuration: '+error.message);}}
}
function renderTalents(){
 const root=$('talents');root.replaceChildren();if(!request)return;
 const talents=request.talents||{},ranks=talents.ranks||{};
 for(const [key,value] of Object.entries(ranks)){
  const line=document.createElement('label');line.className='selection';
  const name=document.createElement('span');name.textContent=key;line.append(name);
  const input=document.createElement('input');input.type='number';input.min='0';input.max='5';input.step='1';input.value=value;
  input.setAttribute('aria-label','Rank for '+key);
  input.addEventListener('change',()=>{const rank=Number(input.value);if(!Number.isInteger(rank)||rank<0||rank>5){note('Invalid rank: '+key);input.value=ranks[key];return;}ranks[key]=rank;showTalentSummary();});
  line.append(input);root.append(line);
 }
 if(!Object.keys(ranks).length){const p=document.createElement('p');p.textContent='No preset rank entries exposed for this specialization.';root.append(p);}
 showTalentSummary();
}
function showTalentSummary(){
 const t=request?.talents||{},points=Object.values(t.ranks||{}).reduce((sum,n)=>sum+Number(n),0);
 $('talentSummary').textContent=points+' explicit rank points from '+Object.keys(t.ranks||{}).length+' talent keys. Other talent-tree point totals are preserved from the backend preset.';
}
async function checkBaseline(){
 if(!$('race').value)return;
 try{
 const qs=new URLSearchParams({race:$('race').value,classKey:$('classKey').value,level:'60',provisional:String($('provisional').checked)});
 const result=await api('/api/forever/character/base-stats?'+qs);
 $('baselineStatus').textContent=pretty(result).slice(0,600);
 }catch(e){$('baselineStatus').textContent='Baseline unavailable: '+e.message;}
}
async function loadCatalog(){
 let page=1;catalog=[];
 while(page<=100){const data=await api('/api/forever/equipment/items?page='+page+'&pageSize=200');catalog.push(...data.items);if(catalog.length>=data.total)break;page++;}
 page=1;enchants=[];
 while(page<=100){const data=await api('/api/forever/equipment/enchants?page='+page+'&pageSize=200');enchants.push(...data.enchants);if(enchants.length>=data.total)break;page++;}
 const m=await api('/api/forever/character/modifiers');modifiers=m.modifiers;renderGear();renderMods();
}
function renderGear(){
 const root=$('gearRows'),query=$('itemSearch').value.trim().toLowerCase();root.replaceChildren();
 for(const slot of slots){
  const row=document.createElement('div');row.className='gear-row';
  const title=document.createElement('span');title.textContent=slot;row.append(title);
  const list=catalog.filter(item=>slotsFor(item).includes(slot)&&(!query||item.name.toLowerCase().includes(query)||String(item.id).includes(query)));
  const select=document.createElement('select');select.setAttribute('aria-label','Item in '+slot);
  select.add(option('','Empty slot'));for(const item of list)select.add(option(String(item.id),item.name+' (#'+item.id+')'));
  const equipped=cfg.equipment.items.find(x=>x.slotKey===slot);
  select.value=equipped?.itemId==null?'':String(equipped.itemId);
  select.addEventListener('change',()=>{cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==slot);if(select.value)cfg.equipment.items.push({slotKey:slot,itemId:Number(select.value)});renderGear();$('gearStatus').textContent='Equipment changed; validate before running.';});
  row.append(select);
  const enchant=document.createElement('select');enchant.setAttribute('aria-label','Permanent enchant in '+slot);
  enchant.add(option('','No enchant'));enchant.disabled=!equipped;
  if(equipped){\n   const item=catalog.find(x=>x.id===equipped.itemId);\n   for(const x of enchants.filter(e=>e.allowedSlots.includes(slot)&&(!e.requiresWeapon||!!item?.weapon)&&(!e.requiresTwoHandedWeapon||item?.weapon?.hand==='two-hand')&&!e.isTemporary))enchant.add(option(String(x.spellId),x.name+(x.modeled?'':' (not modeled)')));\n  }
  enchant.value=equipped?.enchantSpellId==null?'':String(equipped.enchantSpellId);
  enchant.addEventListener('change',()=>{const x=cfg.equipment.items.find(x=>x.slotKey===slot);if(x){delete x.enchantSpellId;if(enchant.value)x.enchantSpellId=Number(enchant.value);}});
  row.append(enchant);\n  const temp=document.createElement('select');temp.setAttribute('aria-label','Temporary enchant in '+slot);temp.add(option('','No temporary enchant'));temp.disabled=!equipped;\n  if(equipped){const item=catalog.find(x=>x.id===equipped.itemId);for(const x of enchants.filter(e=>e.isTemporary&&e.allowedSlots.includes(slot)&&(!e.requiresWeapon||!!item?.weapon)&&(!e.requiresTwoHandedWeapon||item?.weapon?.hand==='two-hand')))temp.add(option(String(x.spellId),x.name+(x.modeled?'':' (not modeled)')));}\n  temp.value=equipped?.temporaryEnchantSpellId==null?'':String(equipped.temporaryEnchantSpellId);\n  temp.addEventListener('change',()=>{const x=cfg.equipment.items.find(x=>x.slotKey===slot);if(x){delete x.temporaryEnchantSpellId;if(temp.value)x.temporaryEnchantSpellId=Number(temp.value);}});\n  row.append(temp);root.append(row);
 }
}
function renderMods(){
 const root=$('modifiers');root.replaceChildren();
 for(const category of ['external-buff','consumable','target-debuff']){
  const heading=document.createElement('h3');heading.textContent=category;root.append(heading);
  const target={'external-buff':'buffs','consumable':'consumables','target-debuff':'targetDebuffs'}[category];
  for(const m of modifiers.filter(x=>x.category===category)){
   const label=document.createElement('label');label.className='selection';
   const checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.checked=cfg.effects[target].includes(m.key);
   checkbox.addEventListener('change',()=>{
    if(checkbox.checked)cfg.effects[target].push(m.key);else cfg.effects[target]=cfg.effects[target].filter(k=>k!==m.key);
    const selected=modifiers.filter(x=>Object.values(cfg.effects).flat().includes(x.key));
    const groups=selected.filter(x=>x.exclusiveGroup&&x.exclusiveGroup===m.exclusiveGroup);
    if(groups.length>1){checkbox.checked=false;cfg.effects[target]=cfg.effects[target].filter(k=>k!==m.key);note('Exclusive effect conflict: '+m.exclusiveGroup);}
   });
   const span=document.createElement('span');span.textContent=m.name+' · '+m.verificationStatus;
   label.append(checkbox,span);root.append(label);
  }
 }
}
async function validateGear(){
 try{const r=await post('/api/forever/equipment/resolve',{classKey:$('classKey').value,level:60,race:$('race').value,equipment:cfg.equipment});
 $('gearStatus').textContent='Server validated '+r.normalizedLoadout.items.length+' items; '+(r.unmodeledEquippedEffects?.length||0)+' unmodeled effects.';return r;
 }catch(e){$('gearStatus').textContent=e.message;throw e;}
}
async function preview(){
 const c=$('classKey').value;
 const character=buildCharacter();\n if(request?.talents?.ranks){character.talents={selections:Object.entries(request.talents.ranks).filter(([,rank])=>rank>0).map(([talentKey,rank])=>({talentKey,rank}))};}\n const response=await post('/api/forever/character/generate-level60',{character,modifiers:mods(),useReviewedBaseline:!validMode(),useProvisionalBaseline:validMode()});
 const stats=response.effectiveStats||{};
 $('stats').replaceChildren();
 const values=stats.values||stats;
 for(const [key,value] of Object.entries(values)){if(value==null||typeof value==='object')continue;const el=document.createElement('div');const name=document.createElement('span');name.textContent=key;const number=document.createElement('strong');number.textContent=String(value);el.append(name,number);$('stats').append(el);}
 $('contributions').textContent=pretty(response.contributions||[]);
 $('baselineStatus').textContent='Server baseline: '+response.sourceStatus;
 return response;
}
async function simulate(){
 const b=body();await validateGear();await preview();
 note('Simulating…');$('run').disabled=true;
 try{
 const response=await post('/api/forever/character/simulate-level60',b);results={configuration:b,response};
 const sim=response.sim||{},summary=sim.run?.summary||{};
 $('results').hidden=false;$('metrics').replaceChildren();
 for(const [name,value] of Object.entries({DPS:sim.dps,Damage:sim.damageDone,Duration:b[b.classKey==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[b.specialization]):b.classKey].durationSeconds,PetDamage:sim.petDamage,EndingMana:sim.endingMana})){
  if(value===undefined)continue;const el=document.createElement('div');el.textContent=name+': ';const strong=document.createElement('strong');strong.textContent=String(value);el.append(strong);$('metrics').append(el);
 }
 $('abilities').replaceChildren();
 const damages=sim.damageByAbility||summary.damageDoneByAbility||{};
 for(const [name,damage] of Object.entries(damages).sort((a,b)=>b[1]-a[1])){const tr=document.createElement('tr');for(const v of [name,damage]){const td=document.createElement('td');td.textContent=String(v);tr.append(td);} $('abilities').append(tr);}
 $('raw').textContent=pretty(response);note('Simulation completed using server results.');
 }finally{$('run').disabled=false;}
}
function saveConfig(){return {schema:'warcraftsim.solo-dps-build',version:1,classKey:$('classKey').value,specialization:$('spec').value,race:$('race').value,provisional:$('provisional').checked,gear:cfg.equipment,modifiers:cfg.effects,racialActive:$('racialActive').checked,encounter:{duration:$('duration').value,seed:$('seed').value,targetHealth:$('targetHealth').value,timeline:$('timeline').checked,petFamily:$('petFamily').value},talents:request?.talents};}
async function applyConfig(data){
 if(data?.schema!=='warcraftsim.solo-dps-build'||data.version!==1)throw Error('Unsupported build schema/version.');
 if(!specMap[data.classKey]?.includes(data.specialization))throw Error('Unsupported class/spec.');
 $('classKey').value=data.classKey;await initClass();$('spec').value=data.specialization;await loadPreset();
 if(![...$('race').options].some(x=>x.value===data.race))throw Error('Race is not eligible for this class.');
 $('race').value=data.race;$('provisional').checked=!!data.provisional;
 cfg.equipment=clone(data.gear||{items:[]});cfg.effects=clone(data.modifiers||{buffs:[],consumables:[],targetDebuffs:[]});
 if(request&&data.talents)request.talents=clone(data.talents);
 $('racialActive').checked=!!data.racialActive;
 for(const [key,id] of Object.entries({duration:'duration',seed:'seed',targetHealth:'targetHealth',timeline:'timeline',petFamily:'petFamily'})){if(data.encounter?.[key]!==undefined){if(id==='timeline')$(id).checked=!!data.encounter[key];else $(id).value=data.encounter[key];}}
 renderGear();renderMods();renderTalents();await checkBaseline();
}
function download(name,data){const url=URL.createObjectURL(new Blob([pretty(data)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();URL.revokeObjectURL(url);}
function safe(task){return async()=>{try{await task();}catch(e){note(e.message);}};}
$('classKey').addEventListener('change',safe(initClass));$('spec').addEventListener('change',safe(async()=>{await loadPreset();}));$('race').addEventListener('change',safe(checkBaseline));$('provisional').addEventListener('change',safe(checkBaseline));
$('resetTalents').addEventListener('click',safe(loadPreset));$('itemSearch').addEventListener('input',renderGear);
$('validateGear').addEventListener('click',safe(validateGear));$('preview').addEventListener('click',safe(async()=>{await preview();note('Character stats resolved by server.');}));
$('run').addEventListener('click',safe(simulate));
$('save').addEventListener('click',safe(async()=>{localStorage.setItem('warcraftsim-solo-dps-v1',pretty(saveConfig()));note('Build saved in this browser.');}));
$('load').addEventListener('click',safe(async()=>{const saved=localStorage.getItem('warcraftsim-solo-dps-v1');if(!saved)throw Error('No local build exists.');await applyConfig(JSON.parse(saved));note('Build loaded.');}));
$('exportBuild').addEventListener('click',()=>download('warcraftsim-build.json',saveConfig()));
$('importBuild').addEventListener('click',()=>$('file').click());
$('file').addEventListener('change',safe(async()=>{const f=$('file').files?.[0];if(!f)return;await applyConfig(JSON.parse(await f.text()));$('file').value='';note('Build imported.');}));
$('exportResults').addEventListener('click',()=>{if(results)download('warcraftsim-results.json',results);});
$('petFamily').replaceChildren(...families.map(f=>option(f,f)));
(async()=>{try{const r=await api('/api/forever/character/races');races=r.races;await loadCatalog();await initClass();}catch(e){note('Initialization failed: '+e.message);}})();
})();