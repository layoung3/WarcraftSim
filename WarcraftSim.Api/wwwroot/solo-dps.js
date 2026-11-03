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
const specMap={mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery'],warrior:['arms','fury','protection']};
const slots=['head','neck','shoulder','back','chest','wrist','hands','waist','legs','feet','finger1','finger2','trinket1','trinket2','main-hand','off-hand','ranged'];
const families=['bat','bear','bird-of-prey','boar','carrion-bird','cat','core-hound','crab','crocolisk','fox','gorilla','hyena','raptor','scorpid','spider','tallstrider','turtle','wind-serpent','wolf'];
let classCapabilities=[],races=[],catalog=[],catalogAll=[],showCatalogAll=false,enchants=[],modifiers=[],talentCatalog=[],request=null,results=null,generation=0,activeSlot=null,pickerPage=1,pickerSelected=null,pickerTimer=null,pickerDetailsLoad=0,priorityRows=[],availableIconKeys=new Set(),presetLoadId=0,loadedClass=null,loadedSpec=null,buildDirty=false,pendingPreset=null,statsRefreshId=0,statsRefreshTimer=null,baselineCheckId=0,baselineAvailable=false,itemizedMode=true,pickerTab='items',pickerCompatible=null,pickerEnchantSelection={permanent:0,temporary:0};
const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}};
let soloScript={enabled:false,startingHealthPercent:100,bossHealthMilestones:[]};
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
 if(itemizedMode)r.talents.ranks=Object.fromEntries(Object.entries(r.talents.ranks||{}).filter(([key])=>
   talentCatalog.some(t=>t.key===key&&t.modeled!==false)));
 return {classKey:c,specialization:s,itemizedTalentMode:itemizedMode,
  ...(itemizedMode?{itemizedTalentRanks:clone(request.talents.ranks||{})}:{}),[property]:r};
}
function resetSelections(){cfg.equipment={items:[]};cfg.effects={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'};renderGear();renderMods();}
async function initClass(){
 const token=++generation,c=$('classKey').value;baselineAvailable=false;request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+c+' configuration…');$('spec').replaceChildren(...specMap[c].map(s=>option(s,s.replaceAll('-',' '))));
 const eligible=races.filter(r=>r.classes.includes(c));$('race').replaceChildren(...eligible.map(r=>option(r.key,(r.key==='high-order-skyborne'?'Skyborne (Alliance)':r.key==='windshaper-skyborne'?'Skyborne (Horde)':r.name))));
 resetSelections();await loadCatalog();if(token!==generation)return;await loadPreset(token);if(token===generation)updateIdentity();
}
async function loadPreset(token=generation){
 if(token!==generation)return;
 const c=$('classKey').value,s=$('spec').value,loadId=++presetLoadId;
 request=null;loadedClass=null;loadedSpec=null;$('run').disabled=true;note('Loading '+human(s)+' source-backed starting build…');
 try{
  const route=c==='hunter'?'/api/sim/'+s+'-hunter/defaults':'/api/sim/'+c+'/defaults?spec='+encodeURIComponent(s);
  const initial=await api(route);
  const preset=await api('/api/forever/talents/'+c+'/'+s+'/default-itemized');
  if(token!==generation||loadId!==presetLoadId)return;
  request=initial;itemizedMode=true;
  const talentData=await api('/api/forever/talents/'+c+'/'+s);
  if(token!==generation||loadId!==presetLoadId)return;talentCatalog=talentData.talents;
  await augmentItemizedCatalog();
  if(token!==generation||loadId!==presetLoadId)return;
  if(!request.talents||!preset?.ranks||preset.totalRankedPoints!==51||
    Object.keys(preset.ranks).some(key=>!talentCatalog.some(t=>t.key===key)))
   throw Error('The source-backed 51-point build does not match the known talent catalog.');
  request.talents.ranks=clone(preset.ranks);
  if(c==='mage'){
   request.useArcanePower=Number(request.talents.ranks['arcane-power']||0)>0;
   request.usePresenceOfMind=Number(request.talents.ranks['presence-of-mind']||0)>0;
   // New builder never inherits an invisible prepared Mana Potion.
   request.useMajorManaPotion=false;request.majorManaPotionCharges=0;request.combatItems.majorManaPotionCharges=0;
  }
  recalculateTalentTotals();
  await validateTalentSelection();
  if(token!==generation||loadId!==presetLoadId)return;
  $('seed').value=request.seed;$('duration').value=request.durationSeconds;$('targetHealth').value=request.targetMaximumHealth;
  $('timeline').checked=!!request.captureTimeline;
  const capability=classCapabilities.find(x=>x.classKey===c);
  if(!capability?.executesThroughCurrentUnifiedEndpoint)
   throw Error('This class does not have a certified unified level-60 character adapter.');
  const encounterControls=new Set(capability.encounterControls||[]);
  $('petFamilyRow').hidden=!encounterControls.has('pet-family');
  $('demonRow').hidden=!encounterControls.has('pet-type');
  $('companionRow').hidden=!(encounterControls.has('pet-family')||encounterControls.has('pet-type'));
  $('rotationRow').hidden=!capability.hasRotationPreview;
  $('classCapabilityNotes').textContent=(capability.knownRestrictions||[]).join(' · ') ||
    'The selected class uses server-backed encounter and rotation controls.';
  $('companionEnabled').checked=c==='warlock'?!!request.enablePet:c==='hunter'?(s==='beast-mastery'?!!request.petEnabled:!!request.hasPet):false;
  $('petFamily').value=request.petFamily||'cat';$('demonType').value=request.petType||'succubus';$('rotation').value=request.rotationPolicy||'adaptive';
  $('priorityEditor').hidden=!capability.hasRotationPreview;$('priorityText').value='';priorityRows=[];drawPriorityRows();renderPriorityConditions();$('priorityStatus').textContent=c==='hunter'?'Hunter typed rotation retains supported automatic and custom priority inputs.':'Automatic priority follows the selected talents and class runner.';
  renderTalents();renderMods();updateIdentity();
  note('Class defaults loaded with 51 explicitly ranked points. Sources and unmodeled abilities are shown on talent tiles.');
  loadedClass=c;loadedSpec=s;buildDirty=false;$('run').disabled=false;
  queueStatRefresh();
  await checkBaseline();
 }catch(error){if(token===generation&&loadId===presetLoadId){request=null;note('Cannot load authoritative ranked preset: '+error.message);}}
}
async function augmentItemizedCatalog(){
 const c=$('classKey').value,s=$('spec').value;
 const extra=await api('/api/forever/talents/'+c+'/'+s+'/itemized-catalog');
 talentCatalog=[...talentCatalog.filter(t=>t.modeled!==false).map(t=>({...t,modeled:true})),...extra.unmodeled];
}
const talentTrees={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',
 affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',
 marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent'};
function recalculateTalentTotals(){
 if(!request?.talents)return;
 const talents=request.talents,ranks=talents.ranks||{};
 for(const [tree,key] of Object.entries(talentTrees))
  if(key in talents)talents[key]=talentCatalog.filter(t=>t.tree===tree)
    .reduce((sum,t)=>sum+Number(ranks[t.key]||0),0);
}
async function validateTalentSelection(){
 const endpoint='/api/forever/talents/'+$('classKey').value+'/'+$('spec').value;
 if(itemizedMode)await post(endpoint+'/validate-itemized',request.talents);
 const typed=clone(request.talents);
 if(itemizedMode)typed.ranks=Object.fromEntries(Object.entries(typed.ranks||{}).filter(([key])=>
   talentCatalog.some(t=>t.key===key&&t.modeled!==false)));
 await post(endpoint+'/validate',typed);
}
function renderTalents(){
 queueStatRefresh();
 const root=$('talents');for(const grid of root.querySelectorAll('.talent-tree-grid'))grid._talentObserver?.disconnect();root.replaceChildren();if(!request)return;
 const talents=request.talents||{},ranks=talents.ranks||{};
 const mapping={arcane:'arcanePointsSpent',fire:'firePointsSpent',frost:'frostPointsSpent',affliction:'afflictionPointsSpent',demonology:'demonologyPointsSpent',destruction:'destructionPointsSpent',marksmanship:'marksmanshipPointsSpent','beast-mastery':'beastMasteryPointsSpent',survival:'survivalPointsSpent'};
 const treeNames=$('classKey').value==='mage'?['arcane','fire','frost']:$('classKey').value==='warlock'?['affliction','demonology','destruction']:$('classKey').value==='warrior'?['arms','fury','protection']:['beast-mastery','marksmanship','survival'];
 async function setRank(t,delta){
  const ranks=talents.ranks||{},key=t.key,prior=ranks[key]||0,next=Math.min(t.maxRank,Math.max(0,prior+delta));
  if(next===prior)return;
  const oldTotals=Object.fromEntries(Object.entries(talents).filter(([k])=>k.endsWith('PointsSpent')));
  const cooldownFlag=key==='arcane-power'?'useArcanePower':key==='presence-of-mind'?'usePresenceOfMind':null;
  const oldFlag=cooldownFlag?request[cooldownFlag]:null;
  if(next)ranks[key]=next;else delete ranks[key];
  if(cooldownFlag&&(prior===0||next===0))request[cooldownFlag]=next>0;
  recalculateTalentTotals();
  try{await validateTalentSelection();
   // On talent removal, scrub its old custom action. Otherwise a saved
   // rotation can keep offering a learned-only spell after it is unlearned.
   if(next===0 && (
     $('classKey').value==='mage' &&
      ['arcane-power','presence-of-mind','arcane-blast','arcane-wrath',
       'combustion','pyroblast','blast-wave','ice-lance','ice-barrier',
       'cold-snap','ice-block'].includes(key) ||
     $('classKey').value==='warlock' &&
      ['amplify-curse','wrack','siphon-life','shadowburn',
       'bane-of-havoc','incinerate','conflagrate'].includes(key))){
     request.customPriority=(request.customPriority||[]).filter(x=>x!==key);
     priorityRows=priorityRows.filter(x=>x.key!==key);
     drawPriorityRows();syncPriorityText();
   }
   buildDirty=true;renderTalents();renderPriorityConditions();
   const updated=[...document.querySelectorAll('.talent-node')].find(x=>x.dataset.talentKey===key);updated?.focus({preventScroll:true});}
  catch(error){if(prior)ranks[key]=prior;else delete ranks[key];
   for(const [k,v] of Object.entries(oldTotals))talents[k]=v;
   if(cooldownFlag)request[cooldownFlag]=oldFlag;
   note('Talent allocation rejected: '+error.message);}
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
   if((['warrior','mage','warlock'].includes($('classKey').value)) && t.sourceBuild){
    source.textContent='Forever '+t.sourceBuild+' · source-backed provisional';
   }
   node.append(face,label,source);
   if(['warrior','mage','warlock'].includes($('classKey').value)){
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
 showTalentSummary();
}
function showTalentSummary(){
 const ranked=Object.values(request?.talents?.ranks||{}).reduce((sum,n)=>sum+Number(n||0),0);
 $('talentSummary').textContent=ranked+' rank-derived points · '+Math.max(0,51-ranked)+' remaining / 51. Source-backed historical-only ranks count without awarding combat effects.';
}
async function checkBaseline(){
 const token=++baselineCheckId;baselineAvailable=false;
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
 let page=1;catalog=[];
 while(page<=100){const filters=new URLSearchParams({page:String(page),pageSize:'200',usableOnly:'true',classKey:$('classKey').value,race:$('race').value,level:'60'});
 const data=await api('/api/forever/equipment/items?'+filters);catalog.push(...data.items);if(catalog.length>=data.total)break;page++;}
 page=1;catalogAll=[];
 while(page<=100){const data=await api('/api/forever/equipment/items?page='+page+'&pageSize=200');
  catalogAll.push(...data.items);if(catalogAll.length>=data.total)break;page++;}
 const coverage=await api('/api/forever/equipment/coverage');
 $('catalogCoverage').textContent='Curated local data: '+coverage.totalItems+' items; '+
  coverage.executableEnchants+' executable enchants; '+
  coverage.unsupportedEquipmentEffects+' unmodeled effects.';
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
function modifierTooltip(m){
 const stats=Object.entries(m.stats||{}).map(([k,v])=>human(k)+': '+v).join(' · ')||'No stat effect listed';
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
    disabled:m.modeled===false,iconKey:m.iconKey||null,sourceEntry:m,onToggle:checked=>{
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
 upkeepSelect.value=cfg.effects.targetDebuffMode||'maintained';
 upkeepSelect.addEventListener('change',()=>{cfg.effects.targetDebuffMode=upkeepSelect.value;buildDirty=true;});
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
// The stat snapshot is computed only by the backend. Token checks prevent a
// slower previous request from overwriting newer race, gear or modifier data.
function queueStatRefresh(){
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
 if(statsRefreshTimer!==null){clearTimeout(statsRefreshTimer);statsRefreshTimer=null;}
 const token=refreshToken??++statsRefreshId;
 const character=buildCharacter();
 if(request?.talents?.ranks){
  character.talents={selections:Object.entries(request.talents.ranks)
   .filter(([,rank])=>rank>0).map(([talentKey,rank])=>({talentKey,rank}))};
 }
 const response=await post('/api/forever/character/generate-level60',{
  character,modifiers:mods(),useReviewedBaseline:!validMode(),useProvisionalBaseline:validMode()
 });
 if(token!==statsRefreshId)return response;
 const stats=response.effectiveStats||{};
 const values=stats.values||stats;
 $('stats').replaceChildren();
 for(const [key,value] of Object.entries(values)){
  if(value==null||typeof value==='object')continue;
  const el=document.createElement('div');const name=document.createElement('span');name.textContent=human(key);
  const number=document.createElement('strong');number.textContent=String(value);el.append(name,number);
  $('stats').append(el);
 }
 $('contributions').textContent=pretty(response.contributions||[]);
 $('baselineStatus').textContent='Baseline: '+response.sourceStatus;
 $('statStatus').textContent='Up to date';
 return response;
}
async function simulate(){
 const b=body();
 const iterations=Number($('iterations').value);
 if(!Number.isInteger(iterations)||iterations<1||iterations>100)
  throw Error('Choose between 1 and 100 real seeded iterations.');
 await validateTalentSelection();
 const gearValidation=await validateGear();const resolved=await preview();
 const build=clone(saveConfig());
 const metadata=await api('/api/forever/character/report-metadata');
 note('Simulating…');$('run').disabled=true;
 try{
 const batch=iterations>1?await post('/api/forever/shared/simulate-iterations',{
  configuration:b,iterations
 }):null;
 const response=batch?batch.representative:await post('/api/forever/character/simulate-level60',b);
 const data=response.sim||{};
 results={
  schema:'warcraftsim.solo-dps-report',version:1,metadata,build,
  originalSimulationRequest:clone(b),
  iterationStatistics:batch?clone(batch):null,
  talentAllocation:{
   mode:itemizedMode?'strict-rank-derived':'legacy-typed-provisional',
   totalSelectedRanks:Object.values(request.talents?.ranks||{}).reduce((sum,value)=>sum+Number(value||0),0),
   selected:talentCatalog.filter(t=>Number(request.talents?.ranks?.[t.key]||0)>0)
    .map(t=>({key:t.key,name:t.name,tree:t.tree,rank:request.talents.ranks[t.key],
      maxRank:t.maxRank,row:t.row,column:t.column,modeled:t.modeled!==false,
      provenance:t.visualSourceStatus||t.iconStatus||'unresolved',
      prerequisite:t.enginePrerequisiteKey||null})),
   treePoints:Object.fromEntries(Object.entries(request.talents||{}).filter(([k])=>k.endsWith('PointsSpent')))
  },
  validatedLoadout:clone(gearValidation),

  resolvedCharacter:clone(resolved),
  baselineProvenance:clone(response.baseline||resolved.baseline||null),
  combatSummary:{
    dps:data.dps??null,damageDone:data.damageDone??null,
    ownerDamage:data.hunterDamage??data.warlockDamage??null,petDamage:data.petDamage??null,
    damageByAbility:clone(data.damageByAbility||{}),abilityMetrics:clone(data.run?.summary?.abilities||{}),
    combatItemUses:clone(data.combatItemUses||{}),consumableDecisions:clone(data.combatItemDecisionReasons||[]),
    timelineEventCount:data.run?.timeline?.length??null
  },
  warnings:[...clone(metadata.notes||[]),...clone(data.modelingNotes||[]),
    ...talentCatalog.filter(t=>t.modeled===false&&Number(request.talents?.ranks?.[t.key]||0)>0)
      .map(t=>'Unmodeled talent '+t.key+' spends points but has no executable combat effect ('+
        (t.visualSourceStatus||'historical provisional')+').'),
    ...(Object.keys(request.talents||{}).some(key=>key.endsWith('PointsSpent'))?
      ['Legacy aggregate talent totals may be unitemized; inspect selected ranks before interpreting results.']:[])],
  rawServerResponse:clone(response)
 };
 const sim=response.sim||{},summary=sim.run?.summary||{};
 const distribution=$('distributionSummary');distribution.replaceChildren();
 distribution.hidden=!batch;
 $('distributionNotice').textContent=batch?
  'DPS statistics from '+batch.iterations+' independent seeded simulations. '+batch.standardDeviationMethod+'. '+batch.percentileMethod+'.':
  'Single deterministic run. Select 2–100 iterations to compute a meaningful DPS distribution.';
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
   ['Duration (seconds)',b[b.classKey==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[b.specialization]):b.classKey].durationSeconds,'Duration used by each iteration']
  ];
  for(const [label,value,help] of rows){
   const div=document.createElement('div');div.title=help;
   const name=document.createElement('span');name.textContent=label;
   const number=document.createElement('strong');number.textContent=String(value);
   div.append(name,number);distribution.append(div);
  }
 }
 $('results').hidden=false;$('resultEmpty').hidden=true;$('exportResults').disabled=false;$('replayResult').disabled=false;$('metrics').replaceChildren();
 for(const [name,value] of Object.entries({DPS:sim.dps,Damage:sim.damageDone,Duration:b[b.classKey==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[b.specialization]):b.classKey].durationSeconds,PetDamage:sim.petDamage,EndingMana:sim.endingMana})){
  if(value===undefined)continue;const el=document.createElement('div');el.textContent=name+': ';const strong=document.createElement('strong');strong.textContent=String(value);el.append(strong);$('metrics').append(el);
 }
 $('abilities').replaceChildren();
 const extras={OwnerDamage:sim.hunterDamage??sim.warlockDamage, PetDamage:sim.petDamage, DemonMeleeDamage:sim.demonMeleeDamage, DemonSpellDamage:sim.demonSpellDamage, ProcActivations:sim.missileBarrageProcs, EndingMana:sim.endingMana, MaximumMana:sim.maximumMana, Consumables:sim.combatItemUses,ConsumableDecisions:sim.combatItemDecisionReasons};
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
function replaySignature(result){
 const sim=result?.sim||{},r=sim.run||{};
 return JSON.stringify({
  damageDone:sim.damageDone??null,dps:sim.dps??null,
  damageByAbility:sim.damageByAbility??{},
  petDamage:sim.petDamage??null,
  abilitySummary:r.summary?.abilities??{},
  consumableUses:sim.combatItemUses??{},
  consumableDecisions:sim.combatItemDecisionReasons??[]
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
 note('Deterministic replay matched damage, DPS, ability summary, pets and consumables.');
}
function saveConfig(){return {schema:'warcraftsim.solo-dps-build',version:1,itemizedMode,classKey:$('classKey').value,specialization:$('spec').value,race:$('race').value,provisional:$('provisional').checked,gear:cfg.equipment,modifiers:cfg.effects,
 preparedCombatConsumables:[
  ...Object.entries(request?.combatItems||{}).filter(([key,value])=>key.endsWith('Charges')&&Number(value)>0)
    .map(([key,charges])=>({key,charges:Number(charges)}))
 ],racialActive:$('racialActive').checked,encounter:{duration:$('duration').value,seed:$('seed').value,iterations:$('iterations').value,targetHealth:$('targetHealth').value,timeline:$('timeline').checked,petFamily:$('petFamily').value,demonType:$('demonType').value,companionEnabled:$('companionEnabled').checked,rotation:$('rotation').value,
 useArcanePower:!!request?.useArcanePower,usePresenceOfMind:!!request?.usePresenceOfMind,
 useMajorManaPotion:false,
 majorManaPotionCharges:0,
 customPriority:clone(request?.customPriority||[]),priorityRows:clone(priorityRows),rotationConditions:request?Object.fromEntries(['majorManaPotionBelowManaPercent','evocationBelowManaPercent','lifeTapBelowManaPercent','lifeTapAboveHealthPercent'].filter(k=>k in request).map(k=>[k,request[k]])):{},combatItems:clone(request?.combatItems||{})},talents:request?.talents};}
async function applyConfig(data){
 if(data?.schema==='warcraftsim.solo-dps-report')throw Error('A completed simulation report cannot be imported as a build; extract its embedded build first.');
 if(data?.schema!=='warcraftsim.solo-dps-build'||data.version!==1)throw Error('Unsupported build schema/version.');
 if(!specMap[data.classKey]?.includes(data.specialization))throw Error('Unsupported class/spec.');
 $('classKey').value=data.classKey;await initClass();$('spec').value=data.specialization;await loadPreset();
 if(![...$('race').options].some(x=>x.value===data.race))throw Error('Race is not eligible for this class.');
 $('race').value=data.race;$('provisional').checked=!!data.provisional;
 cfg.equipment=clone(data.gear||{items:[]});
 cfg.effects=clone(data.modifiers||{buffs:[],consumables:[],targetDebuffs:[]});
 cfg.effects.targetDebuffMode ||= 'maintained';
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
 if(request&&data.encounter?.combatItems&&request.combatItems)request.combatItems=clone(data.encounter.combatItems);if(request&&data.classKey==='mage'){
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
 for(const [key,id] of Object.entries({duration:'duration',seed:'seed',iterations:'iterations',targetHealth:'targetHealth',timeline:'timeline',petFamily:'petFamily',demonType:'demonType',companionEnabled:'companionEnabled',rotation:'rotation'})){if(data.encounter?.[key]!==undefined){if(id==='timeline'||id==='companionEnabled')$(id).checked=!!data.encounter[key];else $(id).value=data.encounter[key];}}
 await validateTalentSelection();
 // A valid imported talent allocation is not enough if the saved custom
 // priority still refers to an unlearned spell. Validate against the
 // existing server-owned rotation builder before accepting the import.
 if(['mage','warlock'].includes(data.classKey) && request?.customPriority?.length)
   await post('/api/forever/rotation/preview',body());
 await validateGear();
 renderGear();renderMods();renderTalents();buildDirty=false;await checkBaseline();queueStatRefresh();
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

const legacyBody=body;
body=function(){
 if($('classKey').value!=='warrior'){
  const result=legacyBody(),c=$('classKey').value,s=$('spec').value;
  const child=result[c==='hunter'?({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[s]):c];
  if(c==='mage')child.useMageArmor=$('mageArmor').checked;
  if(c==='warlock'){
   child.useLifeTap=$('warlockLifeTap').checked;
   child.assumeExternalHealingForLifeTap=false;
   child.prePullSacrificedDemon=$('warlockSacrifice').value||null;
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
  request=await api('/api/forever/shared/warrior/defaults?spec='+encodeURIComponent(s));
  if(token!==generation||loadId!==presetLoadId)return;
  request.talents={ranks:clone(request.talentRanks||{})};request.customRotation=[];
  itemizedMode=true;
  talentCatalog=(await api('/api/forever/shared/warrior/talents')).talents;
  if(token!==generation||loadId!==presetLoadId)return;
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
  $('warriorBerserkerRage').checked=(request.majorCooldowns||[]).some(x=>x.abilityKey==='berserker-rage');
  syncWarriorRacialControl();
  $('warriorArmor').value=request.targetArmor;
  $('racialActive').disabled=!['orc','troll'].includes($('race').value);
  $('racialActive').checked=false;
  $('classCapabilityNotes').textContent='Warrior uses a server-prepared race, equipment and talent build, executed in the existing seeded engine. Race baselines are historical-provisional.';
  $('priorityEditor').hidden=false;priorityRows=[];drawPriorityRows();
  $('priorityText').value='';$('priorityStatus').textContent='Automatic Warrior policy; inspect to create an executable custom order.';
  renderTalents();renderMods();updateIdentity();
  loadedClass='warrior';loadedSpec=s;buildDirty=false;$('run').disabled=false;
  await checkBaseline();queueStatRefresh();
  note('Warrior loaded with a legal unallocated tree. Allocate talents or import a proven mixed-tree build.');
 }catch(e){note('Warrior builder unavailable: '+e.message);request=null;}
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
 $('contributions').textContent=pretty(result.contributions||[]);
 $('statStatus').textContent='Backend Warrior stats resolved';return result;
};
const legacyPreviewPriority=previewPriority;
previewPriority=async function(){
 if($('classKey').value==='warrior'){
  const response=await post('/api/forever/shared/warrior/rotation-preview',body().warrior);
  if(response.warnings?.length)$('priorityStatus').textContent=response.warnings.join('; ');
  return response.entries.filter(x=>x.enabled).map(x=>x.abilityKey).filter(Boolean);
 }
 if($('classKey').value==='hunter'){
  const result=await api('/api/forever/shared/rotation-actions/hunter/'+$('spec').value);
  const keys=request.customPriority?.length?request.customPriority:result.defaultActions;
  for(const key of keys)if(!result.actions.includes(key))throw Error('Unavailable Hunter action: '+key);
  return keys;
 }
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
  $('priorityStatus').textContent='Custom Warrior rotation accepted by the server.';}
 catch(e){request.customRotation=current;request.rotationMode=mode;throw e;}
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
  startingStance:$('warriorStance').value,startingRage:Number($('warriorRage').value),
  targetArmor:Number($('warriorArmor').value),
  enableStanceDancing:$('warriorDance').checked,
  enableBossAutoAttacks:$('warriorIncoming').checked,
  majorCooldowns:[...(request.majorCooldowns||[]).filter(x=>x.abilityKey!=='berserker-rage'),
    ...($('warriorBerserkerRage').checked?[{abilityKey:'berserker-rage',mode:'on-cooldown'}]:[])],
  rotationMode:request.rotationMode,customRotation:clone(request.customRotation||[])
 };
 else built.encounter.classSettings={
  mageArmor:$('mageArmor').checked,
  warlockLifeTap:$('warlockLifeTap').checked,
  warlockSacrifice:$('warlockSacrifice').value,
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
  for(const [key,value] of Object.entries(w))if(key in request)request[key]=clone(value);
  $('warriorStance').value=request.startingStance||'automatic';
  $('warriorRage').value=request.startingRage||0;
  $('warriorArmor').value=request.targetArmor||3500;
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
   combatSummary:{dps:sim.meanDps,damageDone:warrior.damageDone,
    damageByAbility:warrior.damageDoneByAbility||{},abilities:warrior.abilities||{},
    combatItemUses:clone(sim.combatItemUses||{})},
   rawServerResponse:clone(distribution.representative),
   warnings:clone(sim.modelingNotes||[])
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
 if(!request)return;
 $('mageArmor').checked=!!request.useMageArmor;
 $('warlockLifeTap').checked=!!request.useLifeTap;
 $('warlockSacrifice').value=request.prePullSacrificedDemon||'';
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
 'mageArmor','warlockLifeTap','warlockSacrifice','hunterAspect','hunterSpecial','hunterDelay']){
 $(id).addEventListener('change',()=>{buildDirty=true;queueStatRefresh();});
}
$('addPriorityAction').addEventListener('click',()=>{
 const key=$('priorityActionSelect').value;
 if(!key||priorityRows.some(x=>x.key===key)){note('Select an action not already in the priority.');return;}
 priorityRows.push({key,enabled:true});syncPriorityText();drawPriorityRows();buildDirty=true;
});
async function refreshPriorityActionSelect(){
 const c=$('classKey').value,s=$('spec').value;
 try{
  const actions=await api('/api/forever/shared/rotation-actions/'+c+'/'+s);
  $('priorityActionSelect').replaceChildren(...actions.actions.map(x=>option(x,human(x))));
 }catch(e){$('priorityActionSelect').replaceChildren();note('Action catalog unavailable: '+e.message);}
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
 const cls=submitted.classKey,property=cls==='hunter'?
  ({marksmanship:'marksmanship',survival:'survival','beast-mastery':'beastMastery'}[submitted.specialization]):cls;
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
 renderSoloScript();
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
 const submitted=previousConditionalBody();
 const cls=submitted.classKey;
 const property=cls==='hunter'?({marksmanship:'marksmanship',survival:'survival',
  'beast-mastery':'beastMastery'}[submitted.specialization]):cls;
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
    data.encounter.priorityRows.some(x=>x.conditions?.length)){
  priorityRows=clone(data.encounter.priorityRows);drawPriorityRows();syncPriorityText();
 }
};


function formatResultNumber(raw,kind='whole'){
 if(typeof raw!=='string'&&typeof raw!=='number')return String(raw);
 const num=Number(String(raw).replaceAll(',',''));
 if(!Number.isFinite(num))return String(raw);
 const digits=kind==='dps'||kind==='percent'?1:kind==='duration'?2:0;
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
  const kind=/DPS|damage per second/i.test(name)?'dps':
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
  const cls=$('classKey').value;
  const rows=(coverage.rows||[]).filter(x=>x.classKey===cls);
  $('talentCoverage').textContent=rows.map(x=>human(x.tree)+': '+
   x.engineDeclaredConnectedKeys+' runner-declared, '+
   x.historicalOnlyKeys+' historical-only').join(' · ')+
   '. Current 70707 rank tooltips are not yet verified in the repository.';
 }catch(e){$('talentCoverage').textContent='Talent source coverage unavailable: '+e.message;}
};


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
  remove.addEventListener('click',()=>{
   soloScript.addWindows.splice(i,1);
   const cls=$('classKey').value;
   const prefix=cls==='mage'||cls==='warlock'?'target-':'add-';
   soloScript.addWindows.forEach((x,j)=>x.targetKey=prefix+(j+1));
   renderSoloScript();buildDirty=true;
  });
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
 renderSoloScript();buildDirty=true;
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
  const key=c==='hunter'?({marksmanship:'marksmanship',survival:'survival',
   'beast-mastery':'beastMastery'}[b.specialization]):c;
  b[key].additionalTargets=windows.length;
 }
 return b;
};


const priorOrGroupDraw=drawPriorityRows;
drawPriorityRows=function(){
 priorOrGroupDraw();
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
   target.append(option(item.targetActorKey,item.targetActorKey+' (not configured)'));
  target.value=item.targetActorKey||'';
  target.addEventListener('change',()=>{item.targetActorKey=target.value||null;buildDirty=true;});
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
$('race').addEventListener('change',safe(async()=>{await loadCatalog();await checkBaseline();queueStatRefresh();}));
 $('provisional').addEventListener('change',safe(async()=>{await checkBaseline();queueStatRefresh();}));
$('resetTalents').addEventListener('click',safe(loadPreset));
$('clearTalents').addEventListener('click',()=>{if(!request?.talents)return;request.talents.ranks={};for(const key of Object.keys(request.talents))if(key.endsWith('PointsSpent'))request.talents[key]=0;itemizedMode=true;buildDirty=true;renderTalents();note('All talent ranks cleared. Strict itemized mode active.');});$('showCatalogAll').addEventListener('change',()=>{
 showCatalogAll=$('showCatalogAll').checked;pickerPage=1;pickerSelected=null;
 renderPicker();$('pickerDetails').textContent=showCatalogAll?
  'Browsing all offline records. Unavailable entries are inspectable but cannot be equipped.':
  'Select a compatible item to inspect.';
});
$('itemSearch').addEventListener('input',()=>{pickerPage=1;pickerSelected=null;pickerDetailsLoad++;renderPicker();$('pickerDetails').classList.remove('is-loading');$('pickerDetails').removeAttribute('aria-busy');$('pickerDetails').textContent='Select an item to inspect its details.';});
$('validateGear').addEventListener('click',safe(validateGear));
$('run').addEventListener('click',safe(simulate));
$('save').addEventListener('click',safe(async()=>{localStorage.setItem('warcraftsim-solo-dps-v1',pretty(saveConfig()));note('Build saved in this browser.');}));
$('load').addEventListener('click',safe(async()=>{const saved=localStorage.getItem('warcraftsim-solo-dps-v1');if(!saved)throw Error('No local build exists.');await applyConfig(JSON.parse(saved));note('Build loaded.');}));
$('exportBuild').addEventListener('click',()=>download('warcraftsim-build.json',saveConfig()));
$('importBuild').addEventListener('click',()=>$('file').click());
$('file').addEventListener('change',safe(async()=>{const f=$('file').files?.[0];if(!f)return;await applyConfig(JSON.parse(await f.text()));$('file').value='';note('Build imported.');}));
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