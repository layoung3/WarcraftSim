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
let races=[],catalog=[],enchants=[],modifiers=[],talentCatalog=[],request=null,results=null,generation=0,activeSlot=null,pickerPage=1,pickerSelected=null,pickerTimer=null;
const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[]}};
const note=msg=>{$('status').textContent=msg;};
const human=s=>String(s||'').replaceAll('-',' ').replace(/\b\w/g,c=>c.toUpperCase());
function navigate(view){for(const tab of document.querySelectorAll('.tab')){const active=tab.dataset.view===view;tab.classList.toggle('active',active);tab.setAttribute('aria-selected',String(active));tab.tabIndex=active?0:-1;}for(const section of document.querySelectorAll('.view')){const active=section.id==='view-'+view;section.hidden=!active;section.classList.toggle('active',active);} }
function updateIdentity(){const r=selectedRace();$('characterTitle').textContent=human($('spec').value)+' '+human($('classKey').value);$('characterSubtitle').textContent=(r?.name||'Choose race')+' · Level 60';}
const qualityClass=q=>'quality-'+String(q||'').toLowerCase();
const showError=e=>note('Action unavailable: '+(e?.message||e));

function slotsFor(item){return item.allowedSlots||[];}
function selectedRace(){return races.find(x=>x.key===$('race').value);}
function buildCharacter(){return {name:'Solo DPS Character',rulesetKey:'wow-forever',classKey:$('classKey').value,specializationKey:$('spec').value,level:60,race:$('race').value,equipment:clone(cfg.equipment)};}
function mods(){return clone(cfg.effects);}
function validMode(){if(!$('race').value)throw Error('Select an eligible race.');return $('provisional').checked;}
function body(){
 if(!request)throw Error('No class defaults loaded.');
 const c=$('classKey').value,s=$('spec').value,r=clone(request);
 if(c!=='hunter')r.specialization=s;
 r.useReviewedLevel60Baseline=!validMode();r.useProvisionalLevel60Baseline=validMode();
 r.useRacialActive=$('racialActive').checked;r.externalModifiers=mods();
 r.durationSeconds=Number($('duration').value);r.seed=Number($('seed').value);r.targetMaximumHealth=Number($('targetHealth').value);r.captureTimeline=$('timeline').checked;
 if(!Number.isInteger(r.seed)||r.durationSeconds<=0||r.durationSeconds>1200||r.targetMaximumHealth<=0)throw Error('Check encounter duration, seed and target health.');
 if(c==='hunter'){r.race=$('race').value;r.equipment=clone(cfg.equipment);r.petFamily=$('petFamily').value;if(s==='beast-mastery')r.petEnabled=$('companionEnabled').checked;else r.hasPet=$('companionEnabled').checked;}
 else {r.character=buildCharacter();r.rotationPolicy=$('rotation').value;if(c==='warlock'){r.petType=$('demonType').value;r.enablePet=$('companionEnabled').checked;}}
 const property=c==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[s]):c;
 return {classKey:c,specialization:s,[property]:r};
}
function resetSelections(){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[]};renderGear();renderMods();}
async function initClass(){
 const token=++generation,c=$('classKey').value;note('Loading '+c+' configuration…');$('spec').replaceChildren(...specMap[c].map(s=>option(s,s.replaceAll('-',' '))));
 const eligible=races.filter(r=>r.classes.includes(c));$('race').replaceChildren(...eligible.map(r=>option(r.key,(r.key==='high-order-skyborne'?'Skyborne (Alliance)':r.key==='windshaper-skyborne'?'Skyborne (Horde)':r.name))));
 resetSelections();await loadCatalog();await loadPreset(token);updateIdentity();
}
async function loadPreset(token=generation){
 const c=$('classKey').value,s=$('spec').value;
 try{
  const route=c==='hunter'?'/api/sim/'+s+'-hunter/defaults':'/api/sim/'+c+'/defaults?spec='+encodeURIComponent(s);
  const initial=await api(route);
  const preset=await api('/default-talents/'+c+'-'+s+'.json').catch(()=>null);
  if(token!==generation)return;
  request=initial;
  const talentData=await api('/api/forever/talents/'+c+'/'+s);
  if(token!==generation)return;talentCatalog=talentData.talents;
  // The typed defaults endpoint owns the validated specialization build.
  // JSON demonstration presets are intentionally not substituted here: several
  // carry provisional allocations and may not satisfy current backend rules.
  if(preset?.overrides?.petTalents)request.petTalents={...request.petTalents,...preset.overrides.petTalents};
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;$('targetHealth').value=request.targetMaximumHealth;
  $('timeline').checked=!!request.captureTimeline;
  $('petFamilyRow').hidden=c!=='hunter';$('demonRow').hidden=c!=='warlock';$('companionRow').hidden=c==='mage';$('rotationRow').hidden=c==='hunter';
  $('companionEnabled').checked=c==='warlock'?!!request.enablePet:c==='hunter'?(s==='beast-mastery'?!!request.petEnabled:!!request.hasPet):false;
  $('petFamily').value=request.petFamily||'cat';$('demonType').value=request.petType||'succubus';$('rotation').value=request.rotationPolicy||'adaptive';
  renderTalents();updateIdentity();note('Class defaults loaded. Select a baseline mode and resolve stats.');
  await checkBaseline();
 }catch(error){if(token===generation){request=null;note('Cannot load class configuration: '+error.message);}}
}
function renderTalents(){
 const root=$('talents');root.replaceChildren();if(!request)return;
 const talents=request.talents||{},ranks=talents.ranks||{};
 const mapping={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent'};
 const treeNames=$('classKey').value==='mage'?['arcane','fire','frost']:$('classKey').value==='warlock'?['affliction','demonology','destruction']:['beast-mastery','marksmanship','survival'];
 for(const tree of treeNames){
   const group=document.createElement('section');group.className='talent-tree';
   const heading=document.createElement('h2');heading.textContent=human(tree);group.append(heading);
   const field=mapping[tree];if(field in talents){
     const total=document.createElement('label');total.className='tree-points';total.textContent='Allocated tree points';
     const input=document.createElement('input');input.type='number';input.min='0';input.max='51';input.value=talents[field];input.setAttribute('aria-label',human(tree)+' allocated points');
     input.addEventListener('change',()=>{const value=Number(input.value);if(!Number.isInteger(value)||value<0||value>51){input.value=talents[field];return;}talents[field]=value;showTalentSummary();});total.append(input);group.append(total);
   }
   const choices=talentCatalog.filter(t=>t.tree===tree);
   if(!choices.length){const p=document.createElement('p');p.className='empty-hint';p.textContent='No modeled talents exposed for this tree in this specialization.';group.append(p);}
   for(const t of choices){
     const row=document.createElement('div');row.className='talent-row';
     const name=document.createElement('span');name.textContent=human(t.key);name.title=t.description||'Modeled talent; full Forever tooltip not sourced';
     const buttons=document.createElement('div');buttons.className='rank-controls';
     const minus=document.createElement('button');minus.type='button';minus.textContent='−';minus.setAttribute('aria-label','Remove rank from '+human(t.key));
     const count=document.createElement('output');count.textContent=(ranks[t.key]||0)+'/'+t.maxRank;
     const plus=document.createElement('button');plus.type='button';plus.textContent='+';plus.setAttribute('aria-label','Add rank to '+human(t.key));
     async function set(delta){const prior=ranks[t.key]||0,next=Math.min(t.maxRank,Math.max(0,prior+delta));if(next===prior)return;if(next)ranks[t.key]=next;else delete ranks[t.key];try{await post('/api/forever/talents/'+$('classKey').value+'/'+$('spec').value+'/validate',talents);renderTalents();}catch(error){if(prior)ranks[t.key]=prior;else delete ranks[t.key];note('Talent allocation rejected: '+error.message);}}
     minus.addEventListener('click',()=>set(-1));plus.addEventListener('click',()=>set(1));name.tabIndex=0;name.setAttribute('role','button');name.setAttribute('aria-label',human(t.key)+' talent: left click to increase rank, right click to decrease; Enter increases, Delete decreases.');name.addEventListener('click',()=>set(1));name.addEventListener('contextmenu',e=>{e.preventDefault();set(-1);});name.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();set(1);}else if(e.key==='Delete'||e.key==='Backspace'){e.preventDefault();set(-1);}});minus.disabled=!(ranks[t.key]>0);plus.disabled=(ranks[t.key]||0)>=t.maxRank;
     buttons.append(minus,count,plus);row.append(name,buttons);group.append(row);
   }
   root.append(group);
 }
 showTalentSummary();
}
function showTalentSummary(){
 const t=request?.talents||{},points=Object.values(t.ranks||{}).reduce((sum,n)=>sum+Number(n),0);
 const spent=Object.entries(t).filter(([k,v])=>k.endsWith('PointsSpent')&&typeof v==='number').reduce((sum,[,v])=>sum+v,0);
 $('talentSummary').textContent=points+' modeled points · '+(spent||points)+' allocated · '+(51-(spent||points))+' remaining / 51. Validation is enforced server-side before simulation.';
}
async function checkBaseline(){
 if(!$('race').value)return;
 try{
 const qs=new URLSearchParams({race:$('race').value,classKey:$('classKey').value,level:'60',provisional:String($('provisional').checked)});
 const result=await api('/api/forever/character/base-stats?'+qs);
 $('baselineStatus').textContent=$('provisional').checked?'Historical provisional · unverified Forever':(result.sourceStatus||'Reviewed baseline');
 }catch(e){$('baselineStatus').textContent='Baseline unavailable: '+e.message;}
}
async function loadCatalog(){
 let page=1;catalog=[];
 while(page<=100){const filters=new URLSearchParams({page:String(page),pageSize:'200',usableOnly:'true',classKey:$('classKey').value,race:$('race').value,level:'60'});
 const data=await api('/api/forever/equipment/items?'+filters);catalog.push(...data.items);if(catalog.length>=data.total)break;page++;}
 page=1;enchants=[];
 while(page<=100){const data=await api('/api/forever/equipment/enchants?page='+page+'&pageSize=200');enchants.push(...data.enchants);if(enchants.length>=data.total)break;page++;}
 const m=await api('/api/forever/character/modifiers');modifiers=m.modifiers;renderGear();renderMods();
}
const slotSymbols={'head':'◉','neck':'◇','shoulder':'◆','back':'▧','chest':'▤','wrist':'⌁','hands':'✥','waist':'▰','legs':'▥','feet':'▱','finger1':'◌','finger2':'◌','trinket1':'✦','trinket2':'✦','main-hand':'⚔','off-hand':'⛨','ranged':'➶'};
function renderGear(){
 const root=$('gearRows');root.replaceChildren();
 for(const slot of slots){
  const equipped=cfg.equipment.items.find(x=>x.slotKey===slot),item=catalog.find(x=>x.id===equipped?.itemId);
  const button=document.createElement('button');button.type='button';button.className='gear-slot';button.dataset.slot=slot;button.setAttribute('aria-label',human(slot)+': '+(item?.name||'Empty')+'. Open item picker');
  const symbol=document.createElement('span');symbol.className='slot-icon';symbol.textContent=slotSymbols[slot]||'◇';
  const copy=document.createElement('span');copy.className='slot-copy';
  const label=document.createElement('small');label.textContent=human(slot);
  const name=document.createElement('strong');name.textContent=item?.name||'Empty slot';name.className=item?qualityClass(item.quality):'empty-item';
  copy.append(label,name);
  if(equipped?.enchantSpellId||equipped?.temporaryEnchantSpellId){const line=document.createElement('em');line.textContent=[equipped.enchantSpellId?'Enchanted':'',equipped.temporaryEnchantSpellId?'Temporary effect':''].filter(Boolean).join(' · ');copy.append(line);}
  button.append(symbol,copy);button.addEventListener('click',()=>openPicker(slot));root.append(button);
 }
}
function openPicker(slot){activeSlot=slot;pickerPage=1;pickerSelected=null;$('pickerTitle').textContent=human(slot);$('itemSearch').value='';renderPicker();$('itemDialog').showModal();$('itemSearch').focus();}
function pickerCandidates(){const q=$('itemSearch').value.trim().toLowerCase();return catalog.filter(i=>slotsFor(i).includes(activeSlot)&&(!q||i.name.toLowerCase().includes(q)||String(i.id).includes(q)));}
function renderPicker(){
 const matches=pickerCandidates(),pageSize=12,pages=Math.max(1,Math.ceil(matches.length/pageSize));pickerPage=Math.max(1,Math.min(pages,pickerPage));
 const root=$('pickerItems');root.replaceChildren();for(const item of matches.slice((pickerPage-1)*pageSize,pickerPage*pageSize)){
  const b=document.createElement('button');b.type='button';b.className='picker-item'+(pickerSelected?.id===item.id?' selected':'');
  const name=document.createElement('strong');name.className=qualityClass(item.quality);name.textContent=item.name;
  const sub=document.createElement('small');sub.textContent='Item #'+item.id+' · '+(item.quality||'Unclassified');b.append(name,sub);
  b.addEventListener('click',()=>{pickerSelected=item;renderPicker();renderPickerDetails().catch(showError);});root.append(b);
 }
 if(!matches.length){const p=document.createElement('p');p.className='empty-hint';p.textContent='No eligible catalog items match this search.';root.append(p);}
 const controls=$('pickerPagination');controls.replaceChildren();const prev=document.createElement('button');prev.className='subtle';prev.textContent='Previous';prev.disabled=pickerPage===1;prev.addEventListener('click',()=>{pickerPage--;renderPicker();});
 const text=document.createElement('span');text.textContent='Page '+pickerPage+' of '+pages+' · '+matches.length+' items';
 const next=document.createElement('button');next.className='subtle';next.textContent='Next';next.disabled=pickerPage>=pages;next.addEventListener('click',()=>{pickerPage++;renderPicker();});controls.append(prev,text,next);
}
// Enchant compatibility is returned by the same server endpoint used for build validation.
async function renderPickerDetails(){
 const root=$('pickerDetails');root.replaceChildren();const item=pickerSelected,slot=activeSlot;if(!item)return;
 const compatible=await api('/api/forever/equipment/items/'+item.id+'/compatible-enchants?slot='+encodeURIComponent(slot));
 if(item!==pickerSelected||slot!==activeSlot)return;
 const title=document.createElement('h3');title.textContent=item.name;title.className=qualityClass(item.quality);root.append(title);
 const p=document.createElement('p');p.className='inline-note';p.textContent='Requires level '+item.requiredLevel+' · '+(item.verificationStatus||item.sourceStatus||'Verification not reported')+(item.setKey?' · Set: '+item.setKey:'');root.append(p);
 const stats=document.createElement('p');stats.textContent=Object.entries(item.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No static stats listed';root.append(stats);
 const restrictions=document.createElement('p');restrictions.className='inline-note';restrictions.textContent=[item.allowedClasses?.length?'Classes: '+item.allowedClasses.join(', '):'',item.allowedFactions?.length?'Factions: '+item.allowedFactions.join(', '):'',item.weapon?.hand?'Weapon: '+item.weapon.hand:''].filter(Boolean).join(' · ');root.append(restrictions);
 const equipped=cfg.equipment.items.find(x=>x.slotKey===activeSlot),same=equipped?.itemId===item.id;
 const fields=document.createElement('div');fields.className='settings-grid';
 function enchantField(temporary){const label=document.createElement('label');label.textContent=temporary?'Temporary enchant':'Permanent enchant';const sel=document.createElement('select');sel.add(new Option('None',''));for(const e of compatible.enchants.filter(e=>!!e.isTemporary===temporary))sel.add(new Option(e.name+(e.modeled?'':' · unmodeled'),String(e.spellId)));sel.value=same?String((temporary?equipped.temporaryEnchantSpellId:equipped.enchantSpellId)||''):'';label.append(sel);fields.append(label);return sel;}
 const permanent=enchantField(false),temporary=enchantField(true);root.append(fields);
 const equip=document.createElement('button');equip.type='button';equip.className='simulate';equip.textContent=same?'Update enchants':'Equip item';equip.style.marginTop='12px';
 equip.addEventListener('click',async()=>{const previous=clone(cfg.equipment);cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==activeSlot);const entry={slotKey:activeSlot,itemId:item.id};if(permanent.value)entry.enchantSpellId=Number(permanent.value);if(temporary.value)entry.temporaryEnchantSpellId=Number(temporary.value);cfg.equipment.items.push(entry);try{await validateGear();renderGear();$('itemDialog').close();note('Equipment validated by server.');}catch(e){cfg.equipment=previous;showError(e);}});
 root.append(equip);
}

function renderMods(){
 const root=$('modifiers');root.replaceChildren();
 const sections=[{title:'Flasks & elixirs',category:'consumable',match:m=>m.exclusiveGroup!=='well-fed-food',field:'consumables'}, {title:'Food',category:'consumable',match:m=>m.exclusiveGroup==='well-fed-food',field:'consumables'}, {title:'External buffs',category:'external-buff',field:'buffs'}, {title:'Target debuffs',category:'target-debuff',field:'targetDebuffs'}];
 for(const group of sections){
  const section=document.createElement('section');section.className='modifier-group modifier-tiles';const h=document.createElement('h2');h.textContent=group.title;section.append(h);
  for(const m of modifiers.filter(x=>x.category===group.category&&(!group.match||group.match(x)))){
   const label=document.createElement('label');label.className='modifier-row modifier-tile';const checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.checked=cfg.effects[group.field].includes(m.key);
   checkbox.addEventListener('change',()=>{if(checkbox.checked)cfg.effects[group.field].push(m.key);else cfg.effects[group.field]=cfg.effects[group.field].filter(k=>k!==m.key);
    const selected=modifiers.filter(x=>Object.values(cfg.effects).flat().includes(x.key));
    const same=selected.filter(x=>x.exclusiveGroup&&x.exclusiveGroup===m.exclusiveGroup);
    if(same.length>1){checkbox.checked=false;cfg.effects[group.field]=cfg.effects[group.field].filter(k=>k!==m.key);note('Cannot stack '+m.name+': '+m.exclusiveGroup+' is already selected.');}
   });
   const icon=document.createElement('span');icon.className='effect-icon';icon.textContent='✧';icon.setAttribute('aria-hidden','true');label.append(icon);label.title=[m.name,Object.entries(m.stats||{}).map(([k,v])=>k+' '+v).join(', '),'Duration: '+m.durationSeconds+' seconds',m.verificationStatus].join(' · ');const copy=document.createElement('span');const name=document.createElement('strong');name.textContent=m.name;const detail=document.createElement('small');detail.textContent=Object.entries(m.stats||{}).map(([k,v])=>human(k)+' '+v).join(' · ')+' · '+(m.verificationStatus||'');
   copy.append(name,detail);label.append(checkbox,copy);label.classList.toggle('selected',checkbox.checked);checkbox.addEventListener('change',()=>label.classList.toggle('selected',checkbox.checked));section.append(label);
  }
  root.append(section);
 }
}

async function validateGear(){
 try{const r=await post('/api/forever/equipment/resolve',{classKey:$('classKey').value,level:60,race:$('race').value,equipment:cfg.equipment});
 $('gearStatus').textContent='Server validated '+r.normalizedLoadout.items.length+' items; '+(r.unmodeledEquippedEffects?.length||0)+' unmodeled effects.';return r;
 }catch(e){$('gearStatus').textContent=e.message;throw e;}
}
async function preview(){
 const character=buildCharacter();
 if(request?.talents?.ranks){character.talents={selections:Object.entries(request.talents.ranks).filter(([,rank])=>rank>0).map(([talentKey,rank])=>({talentKey,rank}))};}
 const response=await post('/api/forever/character/generate-level60',{character,modifiers:mods(),useReviewedBaseline:!validMode(),useProvisionalBaseline:validMode()});
 const stats=response.effectiveStats||{};
 $('stats').replaceChildren();
 const values=stats.values||stats;
 for(const [key,value] of Object.entries(values)){if(value==null||typeof value==='object')continue;const el=document.createElement('div');const name=document.createElement('span');name.textContent=human(key);const number=document.createElement('strong');number.textContent=String(value);el.append(name,number);$('stats').append(el);}
 $('contributions').textContent=pretty(response.contributions||[]);
 $('baselineStatus').textContent='Baseline: '+response.sourceStatus;
 return response;
}
async function simulate(){
 const b=body();
 await post('/api/forever/talents/'+b.classKey+'/'+b.specialization+'/validate',request.talents);
 await validateGear();await preview();
 note('Simulating…');$('run').disabled=true;
 try{
 const response=await post('/api/forever/character/simulate-level60',b);results={configuration:b,response};
 const sim=response.sim||{},summary=sim.run?.summary||{};
 $('results').hidden=false;$('resultEmpty').hidden=true;$('exportResults').disabled=false;$('metrics').replaceChildren();
 for(const [name,value] of Object.entries({DPS:sim.dps,Damage:sim.damageDone,Duration:b[b.classKey==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[b.specialization]):b.classKey].durationSeconds,PetDamage:sim.petDamage,EndingMana:sim.endingMana})){
  if(value===undefined)continue;const el=document.createElement('div');el.textContent=name+': ';const strong=document.createElement('strong');strong.textContent=String(value);el.append(strong);$('metrics').append(el);
 }
 $('abilities').replaceChildren();
 const extras={OwnerDamage:sim.hunterDamage??sim.warlockDamage, PetDamage:sim.petDamage, DemonMeleeDamage:sim.demonMeleeDamage, DemonSpellDamage:sim.demonSpellDamage, ProcActivations:sim.missileBarrageProcs, EndingMana:sim.endingMana, MaximumMana:sim.maximumMana, Consumables:sim.combatItemUses};
 for(const [name,value] of Object.entries(extras)){if(value==null)continue;const el=document.createElement('div');const label=document.createElement('span');label.textContent=name;const strong=document.createElement('strong');strong.textContent=typeof value==='object'?pretty(value):String(value);el.append(label,strong);$('metrics').append(el);}
 const damages=sim.damageByAbility||summary.damageDoneByAbility||{};
 for(const [name,damage] of Object.entries(damages).sort((a,b)=>b[1]-a[1])){const tr=document.createElement('tr');const a=summary.abilities?.[name]||{};for(const v of [name,a.castCompletedCount??'—',a.damageOccurrenceCount??'—',a.criticalDamageCount??'—',damage]){const td=document.createElement('td');td.textContent=String(v);tr.append(td);} $('abilities').append(tr);}
 $('raw').textContent=pretty(response);
 const timeline=sim.run?.timeline||[];
 $('eventLog').textContent=timeline.length?pretty(timeline.slice(0,200)):'No captured events. Enable timeline capture in Encounter.';
 navigate('results');
 note('Simulation completed using server results.');
 }finally{$('run').disabled=false;}
}
function saveConfig(){return {schema:'warcraftsim.solo-dps-build',version:1,classKey:$('classKey').value,specialization:$('spec').value,race:$('race').value,provisional:$('provisional').checked,gear:cfg.equipment,modifiers:cfg.effects,racialActive:$('racialActive').checked,encounter:{duration:$('duration').value,seed:$('seed').value,targetHealth:$('targetHealth').value,timeline:$('timeline').checked,petFamily:$('petFamily').value,demonType:$('demonType').value,companionEnabled:$('companionEnabled').checked,rotation:$('rotation').value},talents:request?.talents};}
async function applyConfig(data){
 if(data?.schema!=='warcraftsim.solo-dps-build'||data.version!==1)throw Error('Unsupported build schema/version.');
 if(!specMap[data.classKey]?.includes(data.specialization))throw Error('Unsupported class/spec.');
 $('classKey').value=data.classKey;await initClass();$('spec').value=data.specialization;await loadPreset();
 if(![...$('race').options].some(x=>x.value===data.race))throw Error('Race is not eligible for this class.');
 $('race').value=data.race;$('provisional').checked=!!data.provisional;
 cfg.equipment=clone(data.gear||{items:[]});cfg.effects=clone(data.modifiers||{buffs:[],consumables:[],targetDebuffs:[]});
 if(request&&data.talents)request.talents=clone(data.talents);
 $('racialActive').checked=!!data.racialActive;
 for(const [key,id] of Object.entries({duration:'duration',seed:'seed',targetHealth:'targetHealth',timeline:'timeline',petFamily:'petFamily',demonType:'demonType',companionEnabled:'companionEnabled',rotation:'rotation'})){if(data.encounter?.[key]!==undefined){if(id==='timeline'||id==='companionEnabled')$(id).checked=!!data.encounter[key];else $(id).value=data.encounter[key];}}
 await post('/api/forever/talents/'+data.classKey+'/'+data.specialization+'/validate',request.talents);
 await validateGear();
 renderGear();renderMods();renderTalents();await checkBaseline();
}
function download(name,data){const url=URL.createObjectURL(new Blob([pretty(data)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();URL.revokeObjectURL(url);}
function safe(task){return async()=>{try{await task();}catch(e){note(e.message);}};}
$('classKey').addEventListener('change',safe(initClass));$('spec').addEventListener('change',safe(async()=>{await loadPreset();}));$('race').addEventListener('change',safe(async()=>{await loadCatalog();await checkBaseline();}));$('provisional').addEventListener('change',safe(checkBaseline));
$('resetTalents').addEventListener('click',safe(loadPreset));
$('clearTalents').addEventListener('click',()=>{if(!request?.talents)return;request.talents.ranks={};for(const key of Object.keys(request.talents))if(key.endsWith('PointsSpent'))request.talents[key]=0;renderTalents();note('Talent ranks cleared.');});$('itemSearch').addEventListener('input',()=>{pickerPage=1;pickerSelected=null;renderPicker();$('pickerDetails').replaceChildren();});
$('validateGear').addEventListener('click',safe(validateGear));$('preview').addEventListener('click',safe(async()=>{await preview();note('Character stats resolved by server.');}));
$('run').addEventListener('click',safe(simulate));
$('save').addEventListener('click',safe(async()=>{localStorage.setItem('warcraftsim-solo-dps-v1',pretty(saveConfig()));note('Build saved in this browser.');}));
$('load').addEventListener('click',safe(async()=>{const saved=localStorage.getItem('warcraftsim-solo-dps-v1');if(!saved)throw Error('No local build exists.');await applyConfig(JSON.parse(saved));note('Build loaded.');}));
$('exportBuild').addEventListener('click',()=>download('warcraftsim-build.json',saveConfig()));
$('importBuild').addEventListener('click',()=>$('file').click());
$('file').addEventListener('change',safe(async()=>{const f=$('file').files?.[0];if(!f)return;await applyConfig(JSON.parse(await f.text()));$('file').value='';note('Build imported.');}));
$('closePicker').addEventListener('click',()=>$('itemDialog').close());
$('unequip').addEventListener('click',async()=>{const previous=clone(cfg.equipment);cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==activeSlot);try{await validateGear();renderGear();$('itemDialog').close();note('Slot cleared.');}catch(e){cfg.equipment=previous;showError(e);}});
for(const tab of document.querySelectorAll('.tab')){tab.addEventListener('click',()=>navigate(tab.dataset.view));tab.addEventListener('keydown',e=>{if(e.key!=='ArrowLeft'&&e.key!=='ArrowRight')return;const tabs=[...document.querySelectorAll('.tab')];const next=tabs[(tabs.indexOf(tab)+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length];next.focus();navigate(next.dataset.view);});}
$('exportResults').addEventListener('click',()=>{if(results)download('warcraftsim-results.json',results);});
$('petFamily').replaceChildren(...families.map(f=>option(f,f)));
(async()=>{try{const r=await api('/api/forever/character/races');races=r.races;await initClass();}catch(e){note('Initialization failed: '+e.message);}})();
})();