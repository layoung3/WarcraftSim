'use strict';
// Execute current loading/import/readiness and talent-edit wrappers, with held
// response boundaries. Browser behavior is separately gated in Actions.
const assert=require('node:assert/strict'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-loading-contract.test.cjs');
const {build}=require('./solo-dps-feral-fixture.cjs');const {preparation}=require('./solo-dps-feral-contract.test.cjs');
function env(){
 const e=environment();
 vm.runInContext(part('const talentTrees=','async function validateTalentSelection()')+part('async function preview(refreshToken){','async function simulate(){'),e.c);
 e.responses.set('/api/forever/character/preview-feral',request=>preparation(request));
 vm.runInContext(part('// Native input invalidates immediately,','const beforePreparedImport=applyConfig;'),e.c);
 e.elements.classKey.value='druid';e.elements.spec.value='feral';e.elements.race.value='night-elf';
 return e;
}
// Real catalog ordering and display through actual class/spec/race handlers.
// Controlled numeric replies here test lifecycle only, not race-stat evidence.
function actualRaceCatalog(){
 const fs=require('node:fs'),catalog=fs.readFileSync('WarcraftSim.Api/Simulation/ForeverPlayableRaceCatalog.cs','utf8');
 const races=[...catalog.matchAll(/new\("([^"]+)","([^"]+)","([^"]+)",\[([^\]]+)\],SourceBuild,SourceUrl\)/g)].map(([,key,name,faction,classes])=>({key,name,faction,classes:[...classes.matchAll(/"([^"]+)"/g)].map(x=>x[1])}));
 assert.equal(races.length,10,'Use every actual playable race, not a reduced baseline fixture');
 assert.match(fs.readFileSync('WarcraftSim.Api/Controllers/ForeverCharacterController.cs','utf8'),/races=source\.OrderBy\(x=>x\.Key\)\.ToArray\(\)/);
 return races.sort((a,b)=>a.key.localeCompare(b.key));
}
function disposableDomEnvironment(){
 const e=env(),fs=require('node:fs'),html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8'),staticNodes=[];
 const decorate=(el,tag)=>{
  let value=String(el.value??'');Object.defineProperty(el,'value',{configurable:true,get:()=>value,set:x=>{value=String(x??'');}});
  el.tagName=tag.toUpperCase();el.children=[];el.listeners??={};el.dataset??={};el.classList={toggle(){},contains(){return false;},add(){},remove(){}};
  el.setAttribute=(key,v)=>{el[key]=String(v);};
  el.append=(...children)=>{el.children.push(...children);if(el.tagName==='SELECT'&&!el.value)el.value=el.children[0]?.value??'';};
  el.replaceChildren=(...children)=>{el.children=children;if(el.tagName==='SELECT')el.value=children[0]?.value??'';};
  Object.defineProperty(el,'options',{configurable:true,get:()=>el.children});
  Object.defineProperty(el,'selectedOptions',{configurable:true,get:()=>el.children.filter(x=>x.value===el.value)});
  Object.defineProperty(el,'childElementCount',{configurable:true,get:()=>el.children.length});
  el.addEventListener=(event,fn)=>{el.listeners[event]=fn;};return el;
 };
 const make=tag=>decorate({id:'',value:'',checked:false,disabled:false,hidden:false,children:[],dataset:{},textContent:''},tag);
 for(const [,tag,attributes,id] of html.matchAll(/<([a-z][a-z0-9]*)\b([^>]*\bid="([^"]+)"[^>]*)>/gi)){
  const el=decorate(e.elements[id],tag);el.type=attributes.match(/\btype="([^"]+)"/)?.[1]||(tag==='input'?'text':'');staticNodes.push(el);
 }
 const walk=node=>[node,...(node.children||[]).flatMap(walk)],all=()=>staticNodes.flatMap(walk),statics=new Map(staticNodes.map(el=>[el.id,el]));
 e.c.document.createElement=make;e.c.document.createTextNode=text=>({textContent:text});
 e.c.document.getElementById=id=>statics.get(id)||all().find(el=>el.id===id)||null;
 e.c.document.querySelectorAll=selector=>selector==='[id]'?all().filter(el=>el.id):selector==='input,select,textarea'?all().filter(el=>['INPUT','SELECT','TEXTAREA'].includes(el.tagName)):selector==='input,select,textarea,button'?all().filter(el=>['INPUT','SELECT','TEXTAREA','BUTTON'].includes(el.tagName)):all();
 e.c.Option=function(text,value){const el=make('option');el.textContent=text;el.value=value;return el;};
 for(const [,id,body] of html.matchAll(/<select\b[^>]*id="([^"]+)"[^>]*>([\s\S]*?)<\/select>/g)){
  const prior=e.elements[id].value;e.elements[id].replaceChildren(...[...body.matchAll(/<option\b[^>]*value="([^"]*)"[^>]*>([^<]*)<\/option>/g)].map(([,value,text])=>new e.c.Option(text,value)));if(prior)e.elements[id].value=prior;
 }
 e.c.attachSourceIndicators=()=>{};e.c.renderPriorityConditions=()=>{};
 const ui=require('./solo-dps-loading-contract.test.cjs').source;
 vm.runInContext(part('function modifierTile({','async function validateGear(){')+part('const hunterSurvivalFlags=','function drawPriorityRows(){')+part('function readWarriorAssumptions(){','const legacyBody=body;')+part('function renderSoloScript(){',"$('scriptedBossEnabled').addEventListener")+'\n'+ui.match(/const human=[^\n]+/)[0]+'\n'+part('function selectedRace(){','function buildCharacter(){')+part('function updateIdentity(){','const qualityClass=')+'\n'+ui.split('\n').find(line=>line.startsWith("$('file').addEventListener")),e.c);
 return {...e,find:id=>e.c.document.getElementById(id),all};
}
async function verifyDisposableControlRollback(){
 const routes=[['druid','balance'],['mage','arcane'],['warlock','affliction'],['hunter','survival'],['warrior','arms'],['priest','shadow'],['rogue','assassination'],['shaman','elemental'],['shaman','enhancement']];
 for(const [classKey,specialization] of routes)for(const invalid of [false,true]){
  const e=disposableDomEnvironment(),initial=build();initial.manualInputs.preTalentCatAttackPower=801;Object.assign(initial.scenario,{durationSeconds:20,seed:144002,targetMaximumHealth:999999});
  await e.c.loadPreset();await e.c.applyConfig(initial);
  e.c.races=actualRaceCatalog();e.elements.classKey.value=classKey;await e.c.initClass();
  if(e.elements.spec.value!==specialization){e.elements.spec.value=specialization;await e.c.loadPreset();}
  const ownsModifierMode=!['priest','rogue'].includes(classKey),beforeSelect=e.find('targetDebuffMode');assert.equal(!!beforeSelect,ownsModifierMode,'Prior class owns only its actual rendered modifier controls');
  if(beforeSelect){beforeSelect.value='initial-only';beforeSelect.disabled=true;}
  if(classKey==='mage'){assert.ok(e.find('useArcanePower'));assert.ok(e.find('usePresenceOfMind'));e.find('useArcanePower').checked=false;e.find('useArcanePower').disabled=true;e.find('usePresenceOfMind').checked=false;e.c.request.evocationBelowManaPercent=27.5;}
  if(classKey==='hunter'){Object.assign(e.c.request,{useMeleeAuto:true,trapTriggerDelaySeconds:.5,fireTrapKind:'explosive'});e.c.renderPriorityConditions();assert.ok(e.find('hunter-useMeleeAuto'));assert.ok(e.find('hunter-trapTriggerDelaySeconds'));assert.ok(e.find('hunter-fireTrapKind'));e.find('hunter-useMeleeAuto').checked=false;e.find('hunter-trapTriggerDelaySeconds').value=invalid?'':'7.25';e.find('hunter-fireTrapKind').value='immolation';}
  if(classKey==='warlock'){Object.assign(e.c.request,{lifeTapBelowManaPercent:31,lifeTapAboveHealthPercent:29});e.c.renderPriorityConditions();}
  if(['mage','warlock','hunter','warrior'].includes(classKey)){const key={mage:'arcane-missiles',warlock:'shadow-bolt',hunter:'steady-shot',warrior:'heroic-strike'}[classKey];e.c.priorityRows=[{key,enabled:true}];e.c.request.customPriority=[key];e.c.cfg.effects.targetDebuffMode='initial-only';e.c.soloScript={enabled:true,startingHealthPercent:73,bossHealthMilestones:[{timeSeconds:10,healthPercent:50}],addWindows:[]};e.c.renderSoloScript();}
  e.elements.duration.value=invalid?'':classKey==='druid'?'23':'47';e.elements.seed.value=invalid?'1.5':classKey==='druid'?'134001':'177001';e.elements.targetHealth.value=invalid?'-1':classKey==='druid'?'200000':'345678';e.elements.timeline.checked=false;
  e.elements.feralPreTalentCatAttackPower.value=invalid?'':'801';e.elements.feralShapeshiftMana.value=invalid?'-3':'250';e.elements.feralStartInProwl.checked=true;e.elements.feralTargetBehind.checked=false;e.c.buildDirty=true;
  const raw=()=>e.c.document.querySelectorAll('input,select,textarea').filter(el=>el.id&&el.type!=='file').map(el=>({id:el.id,value:el.value,checked:el.checked}));
  const controls=plain(raw()),saved=plain(e.c.saveConfig()),request=plain(e.c.request),catalog=plain(e.c.talentCatalog),cfg=plain(e.c.cfg),script=plain(e.c.soloScript),priority=plain(e.c.priorityRows),selects=Object.fromEntries(e.c.document.querySelectorAll('input,select,textarea').filter(el=>el.id&&el.tagName==='SELECT').map(el=>[el.id,el.options.map(x=>({value:x.value,text:x.textContent}))])),labels=Object.fromEntries(['characterTitle','characterSubtitle','classCapabilityNotes','rotationSubtitle','priorityStatus','catalogCoverage','talentCoverage'].map(id=>[id,e.elements[id].textContent])),availability=Object.fromEntries(e.c.document.querySelectorAll('[id]').map(el=>[el.id,{hidden:el.hidden,disabled:el.disabled}]));
  const bad=plain(initial);bad.manualInputs.preTalentCatAttackPower=800;bad.manualInputs.shapeshiftMana=999999;
  const url='/api/forever/character/preview-feral';e.failures.set(url,'Controlled unaffordable Feral shift');const held=e.hold(url);
  e.elements.file.value='foreign-feral.json';e.elements.file.files=[{text:async()=>JSON.stringify(bad)}];const importing=e.elements.file.listeners.change();await held.started;
  assert.equal(e.find('targetDebuffMode'),null,'Feral actually removes the prior dynamic select; lookup must not fabricate a replacement');
  if(classKey==='mage'){assert.equal(e.find('useArcanePower'),null);assert.equal(e.find('usePresenceOfMind'),null);}
  if(classKey==='hunter')for(const id of ['hunter-useMeleeAuto','hunter-trapTriggerDelaySeconds','hunter-fireTrapKind'])assert.equal(e.find(id),null,'Prior Hunter-owned node is genuinely absent during Feral');
  assert.equal(e.elements.duration.value,'20');assert.equal(e.elements.seed.value,'144002');assert.equal(e.elements.targetHealth.value,'999999');
  held.release();await importing;
  assert.equal(e.elements.status.textContent,'Controlled unaffordable Feral shift','Rollback must complete and preserve the original server failure, not fail on an absent DOM node');
  assert.deepEqual(plain(raw()),controls,classKey+' preserves every static and recreated dynamic raw control');
  assert.deepEqual(plain(e.c.saveConfig()),saved,classKey+' complete exported build remains exactly the prior draft');
  assert.deepEqual(plain(e.c.request),request);assert.deepEqual(plain(e.c.talentCatalog),catalog);assert.equal(e.c.loadedClass,classKey);assert.equal(e.c.loadedSpec,specialization);assert.equal(e.c.buildDirty,true);
  assert.deepEqual(plain(e.c.cfg),cfg);assert.deepEqual(plain(e.c.soloScript),script);assert.deepEqual(plain(e.c.priorityRows),priority);
  for(const [id,options] of Object.entries(selects))assert.deepEqual(e.find(id).options.map(x=>({value:x.value,text:x.textContent})),options,id+' option inventory restored');
  for(const [id,value] of Object.entries(labels))assert.equal(e.elements[id].textContent,value,id);
  for(const [id,value] of Object.entries(availability))assert.deepEqual({hidden:e.find(id).hidden,disabled:e.find(id).disabled},value,id);
  if(ownsModifierMode)assert.equal(e.find('targetDebuffMode').value,'initial-only');assert.equal(e.elements.file.value,'');
  if(classKey==='warlock')assert.deepEqual(e.all().filter(el=>el['aria-label']==='Life Tap below mana (%)'||el['aria-label']==='Life Tap above health (%)').map(el=>el.value),['31','29'],'Unidentified conditional fields rebuild from the restored request');
  await e.elements.exportBuild.listeners.click();assert.deepEqual(plain(e.downloads.at(-1).data),saved);
 }
 const missing=disposableDomEnvironment();missing.elements.classKey.value='mage';missing.elements.spec.value='arcane';await missing.c.loadPreset();const draft=missing.c.captureBuildDraft();missing.elements.modifiers.replaceChildren();missing.c.renderMods=()=>{};
 await assert.rejects(()=>missing.c.restoreBuildDraft(draft),/Saved build control was not recreated: targetDebuffMode\./,'Only the initial pass may defer a missing owned control; final restoration must fail explicitly');
 console.log('Actual disposable-control rollback passed: 18 valid/invalid drafts across all eight class families and both Shaman specs; distinct shared/hidden Feral fields, genuinely absent/recreated modifier/Mage/Hunter controls, exact exports, labels, availability and request ownership.');
}
async function verifyActualImportRefresh(manualAp){
 const e=env(),ui=require('./solo-dps-loading-contract.test.cjs').source;
 // Run production queue callbacks on a controlled clock. Unlike the ordinary
 // wrapper fixture, this does not replace queueStatRefresh with a no-op.
 const timers=new Map();let now=0,nextTimer=0;
 e.c.setTimeout=(fn,delay)=>{timers.set(++nextTimer,{at:now+delay,fn});return nextTimer;};
 e.c.clearTimeout=id=>timers.delete(id);
 const advance=async ms=>{now+=ms;for(const [id,task] of [...timers])if(task.at<=now){timers.delete(id);await task.fn();}};
 vm.runInContext(part('function queueStatRefresh(){','async function preview(refreshToken){')+'\n'+ui.split('\n').find(line=>line.startsWith("$('file').addEventListener")),e.c);
 e.elements.file.type='file';
 const importFile=async data=>{e.elements.file.value='selected-file.json';e.elements.file.files=[{text:async()=>JSON.stringify(data)}];await e.elements.file.listeners.change();};
 const controls=()=>Object.fromEntries(['classKey','spec','race','provisional','feralPreTalentCatAttackPower','feralShapeshiftMana','seed','feralStartInProwl','feralTargetBehind'].map(id=>[id,{value:e.elements[id].value,checked:e.elements[id].checked}]));
 const blocked=()=>{for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,true,id);assert.equal(e.panels['.workspace'].inert,false);assert.equal(e.elements.provisional.disabled,false);assert.throws(()=>e.c.requirePreparedBuild(),/manual Feral inputs/);};
 const initialBuild=build();initialBuild.manualInputs.preTalentCatAttackPower=manualAp;
 await e.c.loadPreset();await e.c.applyConfig(initialBuild);
 const valid=plain(e.c.saveConfig()),request=plain(e.c.feralRequest(valid)),previewUrl='/api/forever/character/preview-feral';
 e.c.buildDirty=true;
 const initialCalls=e.calls.filter(x=>x.url===previewUrl).length;
 const malformed=plain(valid);delete malformed.manualInputs.preTalentCatAttackPower;
 await importFile(malformed);
 assert.equal(e.elements.file.value,'');assert.match(e.elements.status.textContent,/manual inputs/);
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.exportBuild.disabled,false);
 assert.equal(e.calls.filter(x=>x.url===previewUrl).length,initialCalls,'Early local rejection retains the still-current receipt without demanding another request');
 assert.deepEqual(plain(e.c.saveConfig()),valid);assert.equal(e.c.buildDirty,true);
 // This exact zero-rank unknown key reaches catalog validation after preset
 // loading, reproducing CI's deep rejection and queued restored preparation.
 const unknown=plain(valid);unknown.talents.ranks['unknown-rank']=0;
 const before=controls();await importFile(unknown);
 assert.equal(e.elements.file.value,'');assert.equal(e.elements.status.textContent,'Feral talent is outside the current catalog.');
 assert.equal(e.elements.status.dataset.buildState,'input-required');assert.equal(e.elements.statStatus.textContent,'Updating…');blocked();
 assert.deepEqual(controls(),before);assert.deepEqual(plain(e.c.saveConfig()),valid);assert.equal(e.c.buildDirty,true);
 assert.equal(e.calls.filter(x=>x.url===previewUrl).length,initialCalls,'File completion precedes the queued preview request');
 await advance(179);blocked();assert.equal(e.calls.filter(x=>x.url===previewUrl).length,initialCalls);
 const held=e.hold(previewUrl),refresh=advance(1);await held.started;
 blocked();assert.deepEqual(plain(e.calls.filter(x=>x.url===previewUrl).at(-1).data),request,'Refresh submits the complete restored request, never the rejected candidate');
 held.release();await refresh;
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.status.dataset.buildState,'ready');
 assert.equal(e.elements.statStatus.textContent,'Up to date');for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,false,id);
 assert.equal(e.elements.status.textContent,'Feral talent is outside the current catalog.','Successful background preparation does not erase the import rejection');
 await e.elements.exportBuild.listeners.click();assert.deepEqual(plain(e.downloads.at(-1).data),valid,'Actual export is exactly the accepted prior build once prepared');
 assert.deepEqual(controls(),before);assert.equal(e.c.buildDirty,true);
 // A restored invalid draft has a different terminal condition: it must not
 // gain a receipt or enabled export merely because the import callback ended.
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.feralShapeshiftMana.value='-3';e.elements.seed.value='.5';e.elements.feralStartInProwl.checked=true;e.elements.feralTargetBehind.checked=false;e.elements.feralPreTalentCatAttackPower.listeners.input();
 assert.equal(e.elements.status.dataset.buildState,'ready','The prior label can remain ready after native invalidation; it is not proof of preparation');
 blocked();assert.equal(e.c.feralPreviewMatches(),false,'Exact receipt and actual controls define readiness, not a stale status label');
 const invalidControls=controls(),invalidRequest=plain(e.c.request),count=e.calls.filter(x=>x.url===previewUrl).length;
 await importFile(unknown);assert.equal(e.elements.file.value,'');blocked();assert.deepEqual(controls(),invalidControls);assert.deepEqual(plain(e.c.request),invalidRequest);assert.equal(e.c.buildDirty,true);
 await advance(180);blocked();assert.match(e.elements.statStatus.textContent,/manual pre-talent total Cat AP/);assert.equal(e.calls.filter(x=>x.url===previewUrl).length,count,'Invalid raw AP does not reach the preview endpoint');assert.deepEqual(controls(),invalidControls);
 await importFile(valid);assert.equal(e.elements.status.textContent,'Build imported.');assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.exportBuild.disabled,false);assert.deepEqual(plain(e.c.saveConfig()),valid);assert.equal(e.c.buildDirty,false);
 console.log('Actual import/file/queued-preview boundary passed for AP '+manualAp+': immediate rejection completion stays blocked until the exact restored preview settles; retained local receipt, invalid raw-draft blocking, complete source equality and valid recovery verified.');
}
async function verifyActualRaceLifecycle(){
 const e=env();e.c.races=actualRaceCatalog();
 assert.deepEqual(e.c.races.filter(r=>r.classes.includes('druid')).map(r=>r.key),['high-order-skyborne','night-elf','tauren','troll','windshaper-skyborne']);
 Object.defineProperty(e.elements.race,'selectedOptions',{get(){return this.options.filter(x=>x.value===this.value);}});
 const timers=new Map();let nextTimer=0;e.c.setTimeout=fn=>{timers.set(++nextTimer,fn);return nextTimer;};e.c.clearTimeout=id=>timers.delete(id);
 const flush=async()=>{for(const [id,fn] of [...timers]){timers.delete(id);await fn();}};
 // Preserve native listener composition: immediate Feral invalidation and the
 // existing asynchronous catalog/baseline handler both receive race changes.
 for(const id of ['race','provisional']){e.elements[id].listeners={};e.elements[id].addEventListener=function(event,fn){(this.listeners[event]??=[]).push(fn);};}
 const emit=async(id,event='change')=>{const listeners=e.elements[id].listeners[event]||[];await Promise.all((Array.isArray(listeners)?listeners:[listeners]).map(fn=>fn()));};
 const ui=require('./solo-dps-loading-contract.test.cjs').source;
 vm.runInContext(ui.match(/const human=[^\n]+/)[0]+'\n'+part('function selectedRace(){','function buildCharacter(){')+part('function updateIdentity(){','const qualityClass=')+part('function queueStatRefresh(){','async function preview(refreshToken){')+part('// Native input invalidates immediately,','const beforePreparedImport=applyConfig;')+'\n'+ui.split('\n').filter(line=>line.startsWith("$('race').addEventListener")||line.trimStart().startsWith("$('provisional').addEventListener")).join('\n'),e.c);
 for(const race of ['high-order-skyborne','windshaper-skyborne'])e.failures.set('/api/forever/character/base-stats?race='+race+'&classKey=druid&level=60&provisional=true','No defensible historical level-60 proxy for '+race+'/druid. Skyborne or unsupported combinations require direct Forever observations.');
 const label=()=>e.elements.race.selectedOptions[0].textContent;
 const identity=()=>assert.equal(e.elements.characterSubtitle.textContent,label()+' · Level 60','Sidebar must match the selected faction label without changing its canonical race key');
 const blocked=()=>{for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,true,id);for(const id of ['race','provisional'])assert.equal(e.elements[id].disabled,false,id);assert.equal(e.panels['.workspace'].inert,false);assert.equal(e.c.feralPreviewMatches(),false);};
 e.elements.classKey.value='mage';await e.c.initClass();assert.equal(e.elements.race.value,'gnome');identity();
 e.elements.classKey.value='druid';await e.c.initClass();assert.equal(e.elements.spec.value,'balance');assert.equal(e.elements.race.value,'high-order-skyborne');assert.equal(label(),'Skyborne (Alliance)');identity();
 e.elements.spec.value='feral';await emit('spec');assert.equal(e.c.loadedSpec,'feral');assert.equal(e.elements.race.value,'high-order-skyborne');identity();assert.equal(e.elements.characterTitle.textContent,'Feral Druid');assert.equal(e.elements.feralPreTalentCatAttackPower.value,'');assert.equal(e.elements.provisional.checked,false);blocked();
 assert.equal(e.calls.some(x=>x.url==='/api/forever/character/preview-feral'),false,'Blank/unsupported setup cannot request a preparation');
 // Playable class eligibility does not imply a supported historical baseline.
 e.elements.feralPreTalentCatAttackPower.value='800.125';e.elements.feralShapeshiftMana.value='251.5';e.elements.feralUseFaerieFire.checked=true;e.elements.provisional.checked=true;await emit('provisional');await flush();
 assert.match(e.elements.baselineStatus.textContent,/No defensible historical/);assert.equal(e.c.baselineAvailable,false);blocked();assert.throws(()=>e.c.saveFeralConfig(),/historical-provisional fixed-Cat identity/);
 e.elements.race.value='windshaper-skyborne';await emit('race');await flush();identity();assert.equal(label(),'Skyborne (Horde)');assert.equal(e.elements.race.value,'windshaper-skyborne');assert.equal(e.elements.feralPreTalentCatAttackPower.value,'800.125');blocked();
 // An explicit supported race never fills AP or resets authored controls.
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.race.value='night-elf';await emit('race');await flush();identity();assert.equal(e.elements.feralPreTalentCatAttackPower.value,'');assert.equal(e.elements.feralShapeshiftMana.value,'251.5');assert.equal(e.elements.feralUseFaerieFire.checked,true);blocked();assert.match(e.elements.statStatus.textContent,/manual pre-talent total Cat AP/);
 for(const race of ['tauren','troll','night-elf']){e.elements.feralPreTalentCatAttackPower.value='-1';e.elements.race.value=race;await emit('race');await flush();identity();assert.equal(e.elements.race.value,race);blocked();assert.match(e.elements.statStatus.textContent,/finite nonnegative/);}
 e.elements.feralPreTalentCatAttackPower.value='0';e.elements.feralPreTalentCatAttackPower.listeners.input();await flush();identity();assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.run.disabled,false);assert.equal(e.calls.filter(x=>x.url==='/api/forever/character/preview-feral').at(-1).data.character.race,'night-elf');assert.equal(e.c.saveFeralConfig().manualInputs.preTalentCatAttackPower,0);
 // A delayed older baseline cannot restore stale race identity/preparation.
 const delayed=e.hold('/api/forever/character/base-stats?race=tauren&classKey=druid&level=60&provisional=true');e.elements.race.value='tauren';const old=emit('race');await delayed.started;e.elements.race.value='troll';await emit('race');await flush();identity();assert.equal(e.c.feralPreviewMatches(),true);delayed.release();await old;await flush();identity();assert.equal(e.elements.race.value,'troll');assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.calls.filter(x=>x.url==='/api/forever/character/preview-feral').at(-1).data.character.race,'troll');
 assert.equal(e.calls.some(x=>x.url==='/api/forever/character/preview-feral'&&x.data.character.race.includes('skyborne')),false,'Unsupported Skyborne never becomes a prepared request');
 console.log('Actual race-catalog lifecycle passed: Mage to Balance to Feral, both Skyborne labels and unavailable baselines, no silent default replacement, blank/invalid AP identity, preserved controls, zero recovery and delayed race-baseline isolation.');
}
(async()=>{
 await verifyDisposableControlRollback();
 for(const ap of [800,0])await verifyActualImportRefresh(ap);
 await verifyActualRaceLifecycle();
 const example=JSON.parse(require('node:fs').readFileSync('docs/acceptance/cp175/cp175-feral-historical-manual-cat.json','utf8'));const imported=env();await imported.c.loadPreset();await imported.c.applyConfig(example);assert.deepEqual(plain(imported.c.saveConfig()),example,'Checked-in illustrative fixture passes actual import wrapper path');
 const e=env();e.c.cfg.effects.buffs=['stale-balance-buff'];await e.c.loadPreset();assert.deepEqual(plain(e.c.cfg.effects.buffs),[]);
 assert.equal(e.c.loadedSpec,'feral');assert.equal(e.elements.feralPreTalentCatAttackPower.value,'');assert.equal(e.elements.provisional.checked,false);assert.equal(e.elements.status.dataset.buildState,'input-required');
 for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,true);assert.equal(e.elements.provisional.disabled,false);assert.equal(e.panels['.workspace'].inert,false);
 assert.equal(e.calls.some(x=>x.url==='/api/sim/feral-druid/defaults'),false);assert.equal(e.calls.some(x=>x.url.includes('simulate')),false);
 e.elements.feralPreTalentCatAttackPower.value='0';e.elements.provisional.checked=true;await e.c.checkBaseline();await e.c.preview();
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.run.disabled,false);assert.equal(e.elements.status.dataset.buildState,'ready');
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.feralPreTalentCatAttackPower.listeners.input();assert.throws(()=>e.c.requirePreparedBuild(),/manual/);e.c.invalidateFeralPreview();assert.equal(e.elements.run.disabled,true);assert.equal(e.elements.provisional.disabled,false);
 e.elements.feralPreTalentCatAttackPower.value='800';await e.c.preview();const valid=plain(e.c.saveConfig());
 // A fresh request changes state while an older result is delayed.
 const held=e.hold('/api/forever/character/preview-feral');const old=e.c.preview();await held.started;e.elements.feralPreTalentCatAttackPower.value='900';e.c.invalidateFeralPreview();await e.c.preview();held.release();await old;
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.stats.children.find(row=>row.children[0].textContent==='Manual pre-talent Cat AP').children[1].textContent,'900');
 // Exact raw invalid AP/shift/seed/checkbox draft and dirty state survive a
 // backend-only rank rejection after class/preset loading has started.
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.feralShapeshiftMana.value='-3';e.elements.seed.value='1.5';e.elements.feralUseFaerieFire.checked=true;e.elements.provisional.checked=false;e.c.buildDirty=true;
 const raw=Object.fromEntries(['feralPreTalentCatAttackPower','feralShapeshiftMana','seed','feralUseFaerieFire','provisional'].map(id=>[id,{value:e.elements[id].value,checked:e.elements[id].checked}]));
 const bad=plain(valid);bad.talents.ranks.ferocity=6;bad.talents.feralPointsSpent++;bad.talents.totalPoints++;bad.talents.unspentPoints--;

 await assert.rejects(()=>e.c.applyConfig(bad),/Controlled Feral rejected rank/);
 for(const [id,value] of Object.entries(raw)){assert.equal(e.elements[id].value,value.value,id);assert.equal(e.elements[id].checked,value.checked,id);}
 assert.equal(e.c.buildDirty,true);assert.equal(e.c.loadedSpec,'feral');assert.equal(e.elements.run.disabled,true);assert.equal(e.panels['.workspace'].inert,false);
 await e.c.applyConfig(valid);assert.deepEqual(plain(e.c.saveConfig()),valid);assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.run.disabled,false);
 // A rejected imported declaration is not silently recomputed.
 const mismatch=plain(valid);mismatch.talents.feralPointsSpent--;mismatch.talents.balancePointsSpent++;
 await assert.rejects(()=>e.c.applyConfig(mismatch),/catalog-derived/);assert.deepEqual(plain(e.c.saveConfig()),valid);
 // Old Feral source-coverage responses cannot publish over same-class Balance.
 const delayed=e.hold('/api/forever/talents/source-coverage');const first=e.c.loadPreset();await delayed.started;e.elements.spec.value='balance';await e.c.loadPreset();delayed.release();await first;
 assert.equal(e.c.loadedSpec,'balance');assert.equal(e.elements.feralSettings.hidden,true);assert.equal(e.elements.priorityEditor.hidden,false);
 // Cross-class rollback restores not only values but availability and labels.
 e.elements.classKey.value='mage';e.elements.spec.value='arcane';await e.c.loadPreset();e.c.buildDirty=true;
 const labels=['classCapabilityNotes','rotationSubtitle','catalogCoverage','talentCoverage'].map(id=>[id,e.elements[id].textContent]);const availability=['mageSettings','priorityEditor','iterations','racialActive'].map(id=>[id,e.elements[id].hidden,e.elements[id].disabled]);
 e.failures.set('/api/forever/talents/druid/feral/validate-itemized','Controlled imported Feral failure');await assert.rejects(()=>e.c.applyConfig(bad));
 assert.equal(e.c.loadedClass,'mage');assert.equal(e.c.buildDirty,true);for(const [id,hidden,disabled] of availability){assert.equal(e.elements[id].hidden,hidden,id);assert.equal(e.elements[id].disabled,disabled,id);}for(const [id,text] of labels)assert.equal(e.elements[id].textContent,text,id);
 // Hold preview inside the real run lock, and exercise its unlock callback.
 const locked=env();await locked.c.loadPreset();locked.elements.provisional.checked=true;locked.elements.feralPreTalentCatAttackPower.value='800';await locked.c.preview();
 let release;const pending=new Promise(r=>release=r);locked.c.beforeSimulationLock=async()=>{}; // lexical wrapper is exercised below in an isolated actual-source closure.
 vm.runInContext('{simulate=async function(){await preview();await holdRun;};'+part('const beforeSimulationLock=simulate;','function safe(task)')+'}',Object.assign(locked.c,{holdRun:pending}));
 locked.elements.statStatus.textContent='Awaiting preflight';const run=locked.c.simulate();for(let n=0;n<30&&locked.elements.statStatus.textContent!=='Up to date';n++)await Promise.resolve();assert.equal(locked.elements.statStatus.textContent,'Up to date');assert.equal(locked.elements.run.disabled,true);assert.equal(locked.elements.classKey.disabled,true);assert.equal(locked.elements.provisional.disabled,true);release();await run;assert.equal(locked.elements.run.disabled,false);
 // Actual rank closure: stale failure after reset or identity switch cannot
 // restore old totals; failed increments preserve an explicitly present zero.
 const talent=env();await talent.c.loadPreset();
 vm.runInContext('function renderTalents(){const talents=request.talents;'+part(' let talentEditPending=false;',' function eligibility(t){')+'lastRenderedRankHandler=setRank;}',talent.c);
 talent.c.document.querySelectorAll=()=>[];talent.c.renderTalents();talent.c.validateTalentSelection=async()=>{};await talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},-5);assert.equal(Object.hasOwn(talent.c.request.talents.ranks,'furor'),true);assert.equal(talent.c.request.talents.ranks.furor,0);assert.equal(talent.c.request.talents.unspentPoints,20);
 talent.c.request.talents.ranks.furor=0;talent.c.recalculateTalentTotals();
 let releaseTalent;talent.c.validateTalentSelection=()=>new Promise((_,reject)=>releaseTalent=()=>reject(Error('controlled talent rejection')));
 const failed=talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},5);talent.c.renderTalents();await talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},-5);assert.equal(talent.c.request.talents.ranks.furor,5,'Re-render cannot bypass pending talent edit');releaseTalent();await failed;assert.equal(Object.hasOwn(talent.c.request.talents.ranks,'furor'),true);assert.equal(talent.c.request.talents.ranks.furor,0);
 const resetting=talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},5);talent.c.request.talents.ranks={};talent.c.recalculateTalentTotals();talent.c.renderTalents();releaseTalent();await resetting;assert.equal(talent.c.request.talents.totalPoints,0);assert.deepEqual(plain(talent.c.request.talents.ranks),{});
 const switching=talent.c.lastRenderedRankHandler({key:'ferocity',maxRank:5},1);talent.c.generation++;talent.c.request={talents:{ranks:{'natures-grace':1}}};releaseTalent();await switching;assert.deepEqual(plain(talent.c.request.talents.ranks),{'natures-grace':1});
 console.log('Feral actual loading/import readiness passed: blank-to-zero recovery, stale exact-request preview, invalid raw-draft rollback, valid import recovery, declared-total rejection, Balance/Feral race, cross-class panel availability and simulation lock after preview.');
})().catch(error=>{console.error(error);process.exitCode=1;});
