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
let races=[],catalog=[],enchants=[],modifiers=[],talentCatalog=[],request=null,results=null,generation=0,activeSlot=null,pickerPage=1,pickerSelected=null,pickerTimer=null,pickerDetailsLoad=0,priorityRows=[],availableIconKeys=new Set(),presetLoadId=0,loadedClass=null,loadedSpec=null,buildDirty=false,pendingPreset=null,pickerTab='items',pickerCompatible=null,pickerEnchantSelection={permanent:0,temporary:0};
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
 if(!request||loadedClass!==$('classKey').value||loadedSpec!==$('spec').value)throw Error('Selected class and specialization are still loading. Run again after defaults finish.');
 const c=$('classKey').value,s=$('spec').value,r=clone(request);
 if(c!=='hunter')r.specialization=s;
 r.useReviewedLevel60Baseline=!validMode();r.useProvisionalLevel60Baseline=validMode();
 r.useRacialActive=$('racialActive').checked;r.externalModifiers=mods();
 r.durationSeconds=Number($('duration').value);r.seed=Number($('seed').value);r.targetMaximumHealth=Number($('targetHealth').value);r.captureTimeline=$('timeline').checked;
 if(!Number.isInteger(r.seed)||r.durationSeconds<=0||r.durationSeconds>1200||r.targetMaximumHealth<=0)throw Error('Check encounter duration, seed and target health.');
 if(c==='hunter'){r.race=$('race').value;r.equipment=clone(cfg.equipment);r.petFamily=$('petFamily').value;if(s==='beast-mastery')r.petEnabled=$('companionEnabled').checked;else r.hasPet=$('companionEnabled').checked;}
 else {r.character=buildCharacter();r.rotationPolicy=$('rotation').value;if(c==='warlock'){r.petType=$('demonType').value;r.enablePet=$('companionEnabled').checked;r.talentAwareAutoRotation=r.rotationPolicy==='adaptive';}}
 const property=c==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[s]):c;
 return {classKey:c,specialization:s,[property]:r};
}
function resetSelections(){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[]};renderGear();renderMods();}
async function initClass(){
 const token=++generation,c=$('classKey').value;request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+c+' configuration…');$('spec').replaceChildren(...specMap[c].map(s=>option(s,s.replaceAll('-',' '))));
 const eligible=races.filter(r=>r.classes.includes(c));$('race').replaceChildren(...eligible.map(r=>option(r.key,(r.key==='high-order-skyborne'?'Skyborne (Alliance)':r.key==='windshaper-skyborne'?'Skyborne (Horde)':r.name))));
 resetSelections();await loadCatalog();if(token!==generation)return;await loadPreset(token);if(token===generation)updateIdentity();
}
async function loadPreset(token=generation){
 if(token!==generation)return;
 const c=$('classKey').value,s=$('spec').value,loadId=++presetLoadId;
 request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+human(s)+' defaults…');
 try{
  const route=c==='hunter'?'/api/sim/'+s+'-hunter/defaults':'/api/sim/'+c+'/defaults?spec='+encodeURIComponent(s);
  const initial=await api(route);
  const preset=await api('/default-talents/'+c+'-'+s+'.json').catch(()=>null);
  if(token!==generation||loadId!==presetLoadId)return;
  request=initial;
  const talentData=await api('/api/forever/talents/'+c+'/'+s);
  if(token!==generation||loadId!==presetLoadId)return;talentCatalog=talentData.talents;
  // The typed defaults endpoint owns the validated specialization build.
  // JSON demonstration presets are intentionally not substituted here: several
  // carry provisional allocations and may not satisfy current backend rules.
  if(preset?.overrides?.petTalents)request.petTalents={...request.petTalents,...preset.overrides.petTalents};
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;$('targetHealth').value=request.targetMaximumHealth;
  $('timeline').checked=!!request.captureTimeline;
  $('petFamilyRow').hidden=c!=='hunter';$('demonRow').hidden=c!=='warlock';$('companionRow').hidden=c==='mage';$('rotationRow').hidden=c==='hunter';
  $('companionEnabled').checked=c==='warlock'?!!request.enablePet:c==='hunter'?(s==='beast-mastery'?!!request.petEnabled:!!request.hasPet):false;
  $('petFamily').value=request.petFamily||'cat';$('demonType').value=request.petType||'succubus';$('rotation').value=request.rotationPolicy||'adaptive';
  $('priorityEditor').hidden=c==='hunter';$('priorityText').value='';priorityRows=[];drawPriorityRows();renderPriorityConditions();$('priorityStatus').textContent=c==='hunter'?'Hunter rotation preview is not yet supported by the unified contract. The existing runner retains its configured priority.':'Auto priority follows the current class, spec and talents.';renderTalents();renderMods();updateIdentity();note('Class defaults loaded. Select a baseline mode and resolve stats.');
  loadedClass=c;loadedSpec=s;buildDirty=false;$('run').disabled=false;
  await checkBaseline();
 }catch(error){if(token===generation&&loadId===presetLoadId){request=null;note('Cannot load class configuration: '+error.message);}}
}
function renderTalents(){
 const root=$('talents');for(const grid of root.querySelectorAll('.talent-tree-grid'))grid._talentObserver?.disconnect();root.replaceChildren();if(!request)return;
 const talents=request.talents||{},ranks=talents.ranks||{};
 const mapping={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent'};
 const treeNames=$('classKey').value==='mage'?['arcane','fire','frost']:$('classKey').value==='warlock'?['affliction','demonology','destruction']:['beast-mastery','marksmanship','survival'];
 async function setRank(t,delta){
   const key=t.key,prior=ranks[key]||0,next=Math.min(t.maxRank,Math.max(0,prior+delta));
   if(next===prior)return;
   if(next)ranks[key]=next;else delete ranks[key];
   try{await post('/api/forever/talents/'+$('classKey').value+'/'+$('spec').value+'/validate',talents);buildDirty=true;renderTalents();const updated=[...document.querySelectorAll('.talent-node')].find(x=>x.dataset.talentKey===key);updated?.focus({preventScroll:true});}
   catch(error){if(prior)ranks[key]=prior;else delete ranks[key];note('Talent allocation rejected: '+error.message);}
 }
 function eligibility(t){
   const own=ranks[t.key]||0;
   const field=mapping[t.tree],required=t.requiredEarlierPoints||0;
   const earlier=field in talents
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
   node.setAttribute('aria-disabled',String(rank===0&&elig.locked));
   node.setAttribute('aria-label',human(t.key)+', rank '+rank+' of '+t.maxRank+'. Enter adds, right-click or Delete removes.');
   node.title=(t.name||human(t.key))+' · '+rank+'/'+t.maxRank+'\n'+
     (t.historicalRankDescriptions?.[Math.max(0,rank-1)]||t.historicalRankDescriptions?.[0]||'No verified tooltip description')+
     '\nHistorical reference only; gameplay validation remains server-owned.';
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
     t.visualSourceStatus==='engine-hotfix-position-historical-tooltip'?'Hotfix position · old tooltip reference':
     'Historical visual reference';
   node.append(face,label,source);
   if(t.enginePrerequisiteKey){const prereq=document.createElement('span');prereq.className='talent-prereq';prereq.textContent='Requires '+(t.enginePrerequisiteRank||1)+' rank(s) of '+human(t.enginePrerequisiteKey);node.append(prereq);node.title+='\\n'+prereq.textContent;}
   node.addEventListener('click',()=>{if(rank===0&&elig.locked){note('Talent locked: '+elig.reason);return;}setRank(t,1);});
   node.addEventListener('contextmenu',e=>{e.preventDefault();setRank(t,-1);});
   node.addEventListener('keydown',e=>{if(e.key==='Delete'||e.key==='Backspace'){e.preventDefault();setRank(t,-1);}});
   return node;
 }
 for(const tree of treeNames){
   const section=document.createElement('section');section.className='talent-tree';
   const h=document.createElement('h2');h.textContent=human(tree);section.append(h);
   const field=mapping[tree];if(field in talents){
     const total=document.createElement('label');total.className='tree-points';total.textContent='Allocated tree points';
     const input=document.createElement('input');input.type='number';input.min='0';input.max='51';input.value=talents[field];input.setAttribute('aria-label',human(tree)+' allocated points');
     input.addEventListener('change',async()=>{const value=Number(input.value),before=talents[field];if(!Number.isInteger(value)||value<0||value>51){input.value=before;return;}talents[field]=value;try{await post('/api/forever/talents/'+$('classKey').value+'/'+$('spec').value+'/validate',talents);buildDirty=true;showTalentSummary();}catch(error){talents[field]=before;input.value=before;note('Invalid tree-point allocation: '+error.message);}});
     total.append(input);section.append(total);
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
  button.append(symbol,copy);button.addEventListener('click',()=>openPicker(slot,'items'));
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
 activeSlot=slot;pickerPage=1;pickerSelected=null;pickerCompatible=null;pickerDetailsLoad++;
 $('pickerDetails').classList.remove('is-loading');$('pickerDetails').removeAttribute('aria-busy');
 $('pickerDetails').textContent='Select an item to inspect its details.';$('pickerTitle').textContent=human(slot);$('itemSearch').value='';
 const equipped=cfg.equipment.items.find(x=>x.slotKey===slot);
 if(equipped)pickerSelected=catalog.find(x=>x.id===equipped.itemId)||null;
 renderPicker();$('itemDialog').showModal();setPickerTab(tab);
 if(pickerSelected)renderPickerDetails().catch(showError);
 if(tab==='items')$('itemSearch').focus();else $('pickerTabEnchants').focus();
}
function pickerCandidates(){const q=$('itemSearch').value.trim().toLowerCase();return catalog.filter(i=>slotsFor(i).includes(activeSlot)&&(!q||i.name.toLowerCase().includes(q)||String(i.id).includes(q)));}
function renderPicker(){
 const matches=pickerCandidates(),pageSize=12,pages=Math.max(1,Math.ceil(matches.length/pageSize));pickerPage=Math.max(1,Math.min(pages,pickerPage));
 const root=$('pickerItems');root.replaceChildren();for(const item of matches.slice((pickerPage-1)*pageSize,pickerPage*pageSize)){
  const b=document.createElement('button');b.type='button';b.className='picker-item'+(pickerSelected?.id===item.id?' selected':'');b.dataset.itemId=String(item.id);
  const name=document.createElement('strong');name.className=qualityClass(item.quality);name.textContent=item.name;
  const sub=document.createElement('small');sub.textContent='Lv '+(item.itemLevel||item.requiredLevel||60)+' · '+(item.quality||'Unclassified');const icon=document.createElement('span');icon.className='slot-icon';icon.textContent='◇';showItemIcon(icon,item);b.append(icon,name,sub);
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
 const title=document.createElement('h3');title.textContent=item.name;title.className=qualityClass(item.quality);header.append(portrait,title);root.append(header);
 const p=document.createElement('p');p.className='inline-note';p.textContent='Level '+item.requiredLevel+' · '+(item.verificationStatus||item.sourceStatus||'Source pending');root.append(p);
 const stats=document.createElement('p');stats.textContent=Object.entries(item.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No static stats listed';root.append(stats);
 const actions=document.createElement('div');actions.className='picker-actions';
 const equip=document.createElement('button');equip.type='button';equip.className='simulate';equip.textContent='Equip item';equip.addEventListener('click',()=>commitPicker(item));
 const enchant=document.createElement('button');enchant.type='button';enchant.className='subtle';enchant.textContent='Enchants';enchant.addEventListener('click',()=>setPickerTab('enchants'));
 actions.append(equip,enchant);root.append(actions);
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
  const title=document.createElement('h3');title.textContent=enchant.name;inspection.append(title);
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
 function drawChoices(){
  fields.replaceChildren();
  for(const temporary of [false,true]){
   const kind=temporary?'temporary':'permanent',group=document.createElement('fieldset');group.className='enchant-group';
   const legend=document.createElement('legend');legend.textContent=temporary?'Temporary enchants':'Permanent enchants';group.append(legend);
   const options=document.createElement('div');options.className='enchant-options';
   const matches=pickerCompatible.enchants.filter(e=>!!e.isTemporary===temporary&&
     (e.name.toLowerCase().includes(search.value.trim().toLowerCase())||String(e.spellId).includes(search.value.trim())));
   // Removal is always visible, including while filtered.
   for(const e of [{spellId:0,name:'Remove enchant',modeled:true},...matches]){
    const button=document.createElement('button');button.type='button';button.className='enchant-tile';button.dataset.enchantSpellId=String(e.spellId);
    const selected=pickerEnchantSelection[kind]===e.spellId;button.classList.toggle('selected',selected);button.setAttribute('aria-pressed',String(selected));
    const details=[e.name,Object.entries(e.stats||{}).map(([k,v])=>human(k)+' '+v).join(' · '),e.verificationStatus||e.sourceStatus||''].filter(Boolean).join(' · ');
    button.title=details;button.dataset.tooltip=details;button.setAttribute('aria-label',e.name+' ('+kind+')');
    const icon=document.createElement('span');icon.className='enchant-icon';icon.textContent=e.spellId?'✦':'∅';
    const copy=document.createElement('span');copy.className='enchant-copy';
    const name=document.createElement('strong');name.textContent=e.name;
    const summary=document.createElement('small');summary.textContent=e.spellId?(Object.entries(e.stats||{}).map(([k,v])=>human(k)+' '+v).join(' · ')||'Effect details'):'Clear '+kind;
    copy.append(name,summary);
    const marker=document.createElement('span');marker.className='enchant-selected-mark';marker.textContent=selected?'✓':'';
    marker.setAttribute('aria-hidden','true');
    button.append(icon,copy,marker);
    button.addEventListener('click',()=>{
     pickerEnchantSelection[kind]=e.spellId;
     for(const x of options.querySelectorAll('.enchant-tile')){
      const on=x===button;x.classList.toggle('selected',on);x.setAttribute('aria-pressed',String(on));
      x.querySelector('.enchant-selected-mark').textContent=on?'✓':'';
     }
     inspect(e,temporary);
    });
    options.append(button);
   }
   group.append(options);fields.append(group);
  }
 }
 search.addEventListener('input',drawChoices);
 drawChoices();layout.append(fields,inspection);root.append(layout);
 const apply=document.createElement('button');apply.type='button';apply.className='simulate';apply.textContent='Apply enchants';apply.addEventListener('click',()=>commitPicker(item));root.append(apply);
}
function modifierTooltip(m){
 const stats=Object.entries(m.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No stat effect listed';
 return [m.name,stats,m.durationSeconds?'Duration: '+m.durationSeconds+' seconds':'Duration: engine-defined',
  m.exclusiveGroup?'Stacking: '+m.exclusiveGroup:'',m.verificationStatus||'Source not verified',
  m.modeled===false?'Not executed by simulator':'Modeled effect; server validates selection'].filter(Boolean).join('\n');
}
function modifierTile({name,selected,onToggle,tooltip,disabled=false,iconKey=null}){
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
 button.append(symbol,check);
 button.addEventListener('click',()=>{onToggle(!button.classList.contains('selected'));});
 return button;
}
function renderMods(){
 const root=$('modifiers');root.replaceChildren();
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
    disabled:m.modeled===false,iconKey:m.iconKey||null,onToggle:checked=>{
     const previous=cfg.effects[group.field];
     if(checked){
      const taken=modifiers.find(x=>x.exclusiveGroup&&x.exclusiveGroup===m.exclusiveGroup&&Object.values(cfg.effects).flat().includes(x.key)&&x.key!==m.key);
      if(taken){note('Cannot stack '+m.name+' with '+taken.name+': '+m.exclusiveGroup);return;}
      if(!previous.includes(m.key))cfg.effects[group.field]=[...previous,m.key];
     }else cfg.effects[group.field]=previous.filter(k=>k!==m.key);
     buildDirty=true;renderMods();
    }});
   tile.dataset.modifierKey=m.key;tiles.append(tile);
  }
  section.append(tiles);root.append(section);
 }
 if(request?.combatItems&&$('classKey').value!=='hunter'){
  const section=document.createElement('section');section.className='modifier-group modifier-tiles';
  const heading=document.createElement('h2');heading.textContent='Supported combat consumables';section.append(heading);
  const tiles=document.createElement('div');tiles.className='modifier-tile-grid';
  const options=[['manaRubyCharges','Mana Ruby'],['darkRuneCharges','Dark Rune'],['demonicRuneCharges','Demonic Rune'],['majorHealingPotionCharges','Major Healing Potion'],['denseDynamiteCharges','Dense Dynamite'],['goblinSapperCharges','Goblin Sapper Charge']];
  for(const [key,name] of options){
   if(key==='manaRubyCharges'&&$('classKey').value!=='mage')continue;
   const wrapper=document.createElement('div');wrapper.className='combat-item-entry';
   const tile=modifierTile({name,selected:Number(request.combatItems[key]||0)>0,
    tooltip:name+'\nAutomatic use: engine-owned personal/shared cooldown and charge limits apply. Quantities represent real inventory.',
    onToggle:checked=>{request.combatItems[key]=checked?Math.max(1,Number(request.combatItems[key]||0)):0;buildDirty=true;renderMods();}});
   tile.dataset.combatItem=key;wrapper.append(tile);
   const quantity=document.createElement('input');quantity.type='number';quantity.min='0';quantity.max='20';quantity.step='1';
   quantity.value=String(request.combatItems[key]||0);quantity.setAttribute('aria-label',name+' available charges');
   quantity.title=name+' available uses (0 disables; automatic policy)';
   quantity.addEventListener('change',()=>{
    const value=Number(quantity.value);
    if(!Number.isInteger(value)||value<0||value>20){quantity.value=String(request.combatItems[key]||0);note('Consumable quantity must be 0–20.');return;}
    request.combatItems[key]=value;buildDirty=true;renderMods();
   });
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
 tiles.append(modifierTile({name:'Use supported racial active ability',selected:checkbox.checked,
 tooltip:'Use supported racial active ability\nActual racial availability and execution are determined by the selected race and simulator.',
 onToggle:checked=>{checkbox.checked=checked;checkbox.dispatchEvent(new Event('change',{bubbles:true}));buildDirty=true;renderMods();}}));
 racial.append(tiles);root.append(racial);
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
function saveConfig(){return {schema:'warcraftsim.solo-dps-build',version:1,classKey:$('classKey').value,specialization:$('spec').value,race:$('race').value,provisional:$('provisional').checked,gear:cfg.equipment,modifiers:cfg.effects,racialActive:$('racialActive').checked,encounter:{duration:$('duration').value,seed:$('seed').value,targetHealth:$('targetHealth').value,timeline:$('timeline').checked,petFamily:$('petFamily').value,demonType:$('demonType').value,companionEnabled:$('companionEnabled').checked,rotation:$('rotation').value,customPriority:clone(request?.customPriority||[]),priorityRows:clone(priorityRows),rotationConditions:request?Object.fromEntries(['majorManaPotionBelowManaPercent','evocationBelowManaPercent','lifeTapBelowManaPercent','lifeTapAboveHealthPercent'].filter(k=>k in request).map(k=>[k,request[k]])):{},combatItems:clone(request?.combatItems||{})},talents:request?.talents};}
async function applyConfig(data){
 if(data?.schema!=='warcraftsim.solo-dps-build'||data.version!==1)throw Error('Unsupported build schema/version.');
 if(!specMap[data.classKey]?.includes(data.specialization))throw Error('Unsupported class/spec.');
 $('classKey').value=data.classKey;await initClass();$('spec').value=data.specialization;await loadPreset();
 if(![...$('race').options].some(x=>x.value===data.race))throw Error('Race is not eligible for this class.');
 $('race').value=data.race;$('provisional').checked=!!data.provisional;
 cfg.equipment=clone(data.gear||{items:[]});cfg.effects=clone(data.modifiers||{buffs:[],consumables:[],targetDebuffs:[]});
 if(request&&data.talents)request.talents=clone(data.talents);if(request&&data.encounter?.combatItems&&request.combatItems)request.combatItems=clone(data.encounter.combatItems);if(request&&data.encounter?.rotationConditions){const permitted=data.classKey==='mage'?['majorManaPotionBelowManaPercent','evocationBelowManaPercent']:data.classKey==='warlock'?['lifeTapBelowManaPercent','lifeTapAboveHealthPercent']:[];for(const [key,value] of Object.entries(data.encounter.rotationConditions)){if(!permitted.includes(key)||!(key in request)||typeof value!=='number'||!Number.isFinite(value)||value<0||value>100)throw Error('Unsupported or invalid rotation condition: '+key);request[key]=value;}renderPriorityConditions();}if(request&&Array.isArray(data.encounter?.customPriority)){request.customPriority=clone(data.encounter.customPriority);loadPriorityRows(request.customPriority);if(Array.isArray(data.encounter.priorityRows)&&data.encounter.priorityRows.every(x=>typeof x.key==='string'&&typeof x.enabled==='boolean')){priorityRows=clone(data.encounter.priorityRows);drawPriorityRows();}syncPriorityText();}
 $('racialActive').checked=!!data.racialActive;
 for(const [key,id] of Object.entries({duration:'duration',seed:'seed',targetHealth:'targetHealth',timeline:'timeline',petFamily:'petFamily',demonType:'demonType',companionEnabled:'companionEnabled',rotation:'rotation'})){if(data.encounter?.[key]!==undefined){if(id==='timeline'||id==='companionEnabled')$(id).checked=!!data.encounter[key];else $(id).value=data.encounter[key];}}
 await post('/api/forever/talents/'+data.classKey+'/'+data.specialization+'/validate',request.talents);
 await validateGear();
 renderGear();renderMods();renderTalents();buildDirty=false;await checkBaseline();
}
function renderPriorityConditions(){
 const root=$('priorityConditions');root.replaceChildren();if(!request)return;
 const cls=$('classKey').value;
 const fields=cls==='mage'?[
  ['majorManaPotionBelowManaPercent','Mana potion below mana (%)'],
  ['evocationBelowManaPercent','Evocation below mana (%)']
 ]:cls==='warlock'?[
  ['lifeTapBelowManaPercent','Life Tap below mana (%)'],
  ['lifeTapAboveHealthPercent','Life Tap above health (%)']
 ]:[];
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
 if($('classKey').value==='hunter')throw Error('Hunter priority preview is not implemented; existing runner defaults remain active.');
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
$('race').addEventListener('change',safe(async()=>{await loadCatalog();await checkBaseline();}));$('provisional').addEventListener('change',safe(checkBaseline));
$('resetTalents').addEventListener('click',safe(loadPreset));
$('clearTalents').addEventListener('click',()=>{if(!request?.talents)return;request.talents.ranks={};for(const key of Object.keys(request.talents))if(key.endsWith('PointsSpent'))request.talents[key]=0;buildDirty=true;renderTalents();note('Talent ranks cleared.');});$('itemSearch').addEventListener('input',()=>{pickerPage=1;pickerSelected=null;pickerDetailsLoad++;renderPicker();$('pickerDetails').classList.remove('is-loading');$('pickerDetails').removeAttribute('aria-busy');$('pickerDetails').textContent='Select an item to inspect its details.';});
$('validateGear').addEventListener('click',safe(validateGear));$('preview').addEventListener('click',safe(async()=>{await preview();note('Character stats resolved by server.');}));
$('run').addEventListener('click',safe(simulate));
$('save').addEventListener('click',safe(async()=>{localStorage.setItem('warcraftsim-solo-dps-v1',pretty(saveConfig()));note('Build saved in this browser.');}));
$('load').addEventListener('click',safe(async()=>{const saved=localStorage.getItem('warcraftsim-solo-dps-v1');if(!saved)throw Error('No local build exists.');await applyConfig(JSON.parse(saved));note('Build loaded.');}));
$('exportBuild').addEventListener('click',()=>download('warcraftsim-build.json',saveConfig()));
$('importBuild').addEventListener('click',()=>$('file').click());
$('file').addEventListener('change',safe(async()=>{const f=$('file').files?.[0];if(!f)return;await applyConfig(JSON.parse(await f.text()));$('file').value='';note('Build imported.');}));
$('priorityText').addEventListener('input',()=>{priorityRows=$('priorityText').value.split(/[\n,]+/).map(x=>x.trim()).filter(Boolean).map(key=>({key,enabled:true}));drawPriorityRows();});
$('loadPriority').addEventListener('click',safe(inspectPriority));
$('applyPriority').addEventListener('click',safe(applyPriority));
$('resetPriority').addEventListener('click',()=>{if(!request)return;request.customPriority=[];buildDirty=true;$('priorityText').value='';priorityRows=[];drawPriorityRows();$('priorityStatus').textContent='Automatic, talent-aware runner default selected.';});
$('pickerTabItems').addEventListener('click',()=>setPickerTab('items'));
 $('pickerTabEnchants').addEventListener('click',()=>setPickerTab('enchants'));
 $('closePicker').addEventListener('click',()=>$('itemDialog').close());
$('unequip').addEventListener('click',async()=>{const previous=clone(cfg.equipment);cfg.equipment.items=cfg.equipment.items.filter(x=>x.slotKey!==activeSlot);try{await validateGear();buildDirty=true;renderGear();$('itemDialog').close();note('Slot cleared.');}catch(e){cfg.equipment=previous;showError(e);}});
for(const tab of document.querySelectorAll('.tab')){tab.addEventListener('click',()=>navigate(tab.dataset.view));tab.addEventListener('keydown',e=>{if(e.key!=='ArrowLeft'&&e.key!=='ArrowRight')return;const tabs=[...document.querySelectorAll('.tab')];const next=tabs[(tabs.indexOf(tab)+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length];next.focus();navigate(next.dataset.view);});}
$('exportResults').addEventListener('click',()=>{if(results)download('warcraftsim-results.json',results);});
$('petFamily').replaceChildren(...families.map(f=>option(f,f)));
(async()=>{try{const r=await api('/api/forever/character/races');races=r.races;const iconList=await api('/api/forever/icons/available');availableIconKeys=new Set(iconList.files||[]);await initClass();const initial=new URLSearchParams(window.location.search).get('view');if(['equipment','talents','effects','encounter','results'].includes(initial))navigate(initial);}catch(e){note('Initialization failed: '+e.message);}})();
})();