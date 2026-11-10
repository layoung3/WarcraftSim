'use strict';
// Execute the real asynchronous preset/import/export wrapper chain with delayed
// API boundaries. No replacement loader or rollback implementation is used.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
const plain=x=>JSON.parse(JSON.stringify(x));
const part=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const presets=Object.fromEntries(['mage-arcane','mage-fire','mage-frost','warlock-affliction','warlock-demonology','warlock-destruction','hunter-marksmanship','hunter-survival','hunter-beast-mastery','shaman-elemental','shaman-enhancement','druid-balance','priest-shadow','rogue-assassination'].map(key=>[key,JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/'+key+'.json'))]));
const settings={targetArmor:2100,maximumHealth:4900,weaponMinDamage:180,weaponMaxDamage:240,weaponSpeedSeconds:2.6,offHandWeaponMinDamage:100,offHandWeaponMaxDamage:145,offHandWeaponSpeedSeconds:1.8,incomingBossHitDamage:0,incomingBossSwingSeconds:4,dualWield:true,useStormstrike:true,useRageOfTheFarseer:true,useEarthShock:true,useMaelstromLightningBolt:true,useLightningBoltFiller:false,useWindfuryTotem:false,deployWindfuryTotem:false,useLightningShield:false,mainHandImbue:'rockbiter',offHandImbue:'flametongue'};
const saveCode=[part('function saveConfig(){','async function applyConfig(data){'),part('const legacySave=saveConfig;','const legacyApplyConfig='),part('const originalSoloSave=saveConfig;','const originalSoloApply='),part('const beforeElementalSave=saveConfig;','const beforeElementalPreset=')].join('\n');
const imports=[part('async function applyConfig(data){','const hunterSurvivalFlags='),part('const legacyApplyConfig=applyConfig;','function syncWarriorRacialControl()'),part('const originalSoloApply=applyConfig;','const originalSoloPreset='),part('const oldConditionalImport=applyConfig;','function formatResultNumber('),part('const beforeElementalImport=applyConfig;','const beforeElementalSave=')].join('\n');
function environment(){
 const elements=new Proxy({},{get:(o,id)=>o[id]??(o[id]={id,value:'',checked:false,disabled:false,hidden:false,open:false,dataset:{},options:[],textContent:'',showModal(){this.open=true;},close(){this.open=false;},closest:()=>({hidden:false}),replaceChildren(...items){this.options=items;this.value=items[0]?.value??'';},listeners:{},addEventListener(event,fn){this.listeners[event]=fn;}})});
 Object.assign(elements.classKey,{value:'mage'});Object.assign(elements.spec,{value:'arcane'});Object.assign(elements.race,{value:'gnome',options:[{value:'gnome'}]});elements.provisional.checked=true;elements.iterations.value=1;elements.warlockLifeTap.checked=true;
 const panels={'.workspace':{inert:false},'.sidebar':{inert:false}};
 const holds=new Map(),failures=new Map(),responses=new Map(),calls=[],messages=[],downloads=[],stored=[];let simulationCalls=0;
 const c={document:{getElementById:id=>elements[id],querySelector:selector=>panels[selector],querySelectorAll:()=>Object.values(elements)},Option:function(label,value){this.value=value;this.textContent=label;},URLSearchParams,
  download:(name,data)=>downloads.push({name,data}),localStorage:{setItem:(key,value)=>stored.push({key,value})},simulate:async()=>{simulationCalls++;},
  generation:0,presetLoadId:0,baselineCheckId:0,statsRefreshId:0,baselineAvailable:false,request:null,loadedClass:null,loadedSpec:null,buildDirty:false,pendingPreset:null,itemizedMode:true,priorityRows:[],talentCatalog:[],catalog:[],catalogAll:[],enchants:[],modifiers:[],availableIconKeys:new Set(),
  classCapabilities:['mage','shaman','warlock','hunter','druid','priest','rogue'].map(classKey=>({classKey,executesThroughCurrentUnifiedEndpoint:true,hasRotationPreview:true})),
  races:[{key:'gnome',name:'Gnome',classes:['mage']},{key:'orc',name:'Orc',classes:['shaman']},{key:'human',name:'Human',classes:['rogue']}],cfg:{equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}},soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},
  note:msg=>{messages.push(msg);elements.status.textContent=msg;},human:x=>x,queueStatRefresh:()=>{},loadCatalog:async()=>{},augmentItemizedCatalog:async()=>{},recalculateTalentTotals:()=>{},renderTalents:()=>{},renderGear:()=>{},renderMods:()=>{},renderPriorityConditions:()=>{},updateIdentity:()=>{},drawPriorityRows:()=>{},renderSoloScript:()=>{},refreshLocalIconCoverage:()=>{},restoreWarriorAssumptions:()=>{},syncWarriorRacialControl:()=>{},validateGear:async()=>{},hunterSurvivalSelection:()=>({}),loadPriorityRows:()=>{},syncPriorityText:()=>{},body:()=>({enhancement:{}}),
  fetch:async(url,options)=>{
   const data=options?.body?JSON.parse(options.body):null;calls.push({url,data});
   if(holds.has(url)){const gate=holds.get(url);holds.delete(url);gate.enter();await gate.pending;}
   if(failures.has(url))return {ok:false,json:async()=>({error:failures.get(url)})};
   const cls=elements.classKey.value,spec=elements.spec.value,preset=presets[cls+'-'+spec];let payload={};
   if(responses.has(url))payload=plain(responses.get(url));
   else if(url.includes('/defaults'))payload={seed:42,durationSeconds:30,targetMaximumHealth:200000,captureTimeline:true,talents:{ranks:{}},combatItems:{majorManaPotionCharges:0,majorHealingPotionCharges:0},...(spec==='enhancement'?settings:cls==='rogue'?{talents:{assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0,ranks:{}},targetArmor:0,targetDodgePercent:0,targetParryPercent:0,rogueMaximumHealth:4000,startingEnergyPercent:100,poisonChargesPerWeapon:100,startInStealth:true,isBehindTarget:true,useExposeArmor:false,mainHandPoison:'instant',offHandPoison:'instant',tuning:{mainHandWeaponDamage:125,offHandWeaponDamage:80,mainHandSpeedSeconds:2.6,offHandSpeedSeconds:1.8}}:{})};
   else if(url.includes('/equipment/items?'))payload={items:[],total:0};
   else if(url.includes('/equipment/enchants?'))payload={enchants:[],total:0};
   else if(url==='/api/forever/character/modifiers')payload={modifiers:[]};
   else if(url.endsWith('/itemized-catalog'))payload={unmodeled:[]};
   else if(url.endsWith('/default-itemized'))payload=plain(preset);
   else if(url.endsWith('/validate-itemized')&&data?.ranks?.['thundering-strikes']===6)return {ok:false,json:async()=>({error:'Unrecognized talent or invalid rank: thundering-strikes.'})};
   else if(url.endsWith('/validate-itemized')&&data?.ranks?.malice===6)return {ok:false,json:async()=>({error:'Unrecognized talent or invalid rank: malice.'})};
   else if(url==='/api/forever/shared/warrior/talents')payload={talents:[]};
   else if(url.includes('/shared/capabilities/'))payload={executesThroughCurrentUnifiedEndpoint:true,encounterControls:[],hasRotationPreview:true};
   else if(url.includes('/shared/rotation-actions/'))payload={actions:[]};
   else if(url.endsWith('/source-coverage'))payload={rows:[]};
   else if(/\/talents\/(mage|shaman|warlock|hunter|druid|priest|rogue)\/[a-z-]+$/.test(url))payload={talents:Object.keys(preset.ranks).map(key=>({key,modeled:true,...(cls==='rogue'?{tree:'assassination'}:{})}))};
   return {ok:true,json:async()=>payload};
  }};
 vm.createContext(c);
 const chunks=[part('const $=','const pretty='),part('const option=','const slots='),part('const rogueNumericKeys=','function resetSelections()'),part('function resetSelections(){','async function augmentItemizedCatalog()'),part('async function validateTalentSelection(){','function renderTalents()'),part('async function checkBaseline(){','async function loadCatalog()'),part('async function loadCatalog(){','function showItemIcon('),part('async function augmentItemizedCatalog(){','const talentTrees='),saveCode,part('const legacyLoadPreset=loadPreset;','const legacyValidateTalents='),part('const legacyBaseline=checkBaseline;','const legacyPreview='),
  part('const originalLoadNonWarrior=loadPreset;',"for(const id of ['warriorStance'"),part('async function refreshPriorityActionSelect(){','function renderSoloScript()'),part('const originalSoloPreset=loadPreset;','const previousConditionalBody='),part('const oldIconLoadPreset=loadPreset;','// Add actor keys'),part('const beforeElementalPreset=loadPreset;',"$('shadowPreviewRotation').addEventListener"),part('function validateRestrictedCasterImport(data){','const beforeElementalImport='),imports];
 // New readiness boundary is deliberately included only when production has it:
 // the same test first reproduced the old early-ready defect before the fix.
 if(source.includes('function setBuildPreparing('))chunks.push(part('let buildPreparationPending=','// Every preset load invalidates'));
 chunks.push(part('const beforeRequestScopedPreset=loadPreset;',"$('enhancementPreviewRotation').addEventListener"));
 if(source.includes('const beforePreparedImport='))chunks.push(part('const beforePreparedImport=applyConfig;','// Lock immediately'));
 chunks.push(part('let simulationInFlight=false;','function safe(task)'));
 chunks.push(source.split('\n').find(line=>line.startsWith('function safe(task)')));
 chunks.push(part("$('spec').addEventListener('change',safe(async()=>{","$('race').addEventListener"));
 chunks.push(...source.split('\n').filter(line=>line.startsWith("$('save').addEventListener")||line.startsWith("$('exportBuild').addEventListener")));
 vm.runInContext(chunks.join('\n'),c);
 const hold=url=>{let enter,release;const started=new Promise(r=>enter=r),pending=new Promise(r=>release=r);holds.set(url,{enter,pending});return {started,release};};
 return {c,elements,panels,calls,messages,hold,failures,responses,downloads,stored,simulationCalls:()=>simulationCalls};
}
(async()=>{
 const {c,elements,panels,calls,messages,hold,downloads,stored,simulationCalls}=environment();
 const delayed=hold('/api/forever/character/base-stats?race=gnome&classKey=mage&level=60&provisional=true');
 const loading=c.loadPreset();await delayed.started;
 // This was the exact real-CI interval: old ready text/Run enabled while hidden
 // LifeTap was still true; the outer wrapper later changed the export to false.
 const early=plain(c.saveConfig());assert.equal(early.encounter.classSettings.warlockLifeTap,true);
 assert.equal(elements.run.disabled,true,'No Run before the outer preset wrappers settle');
 assert.equal(elements.exportBuild.disabled,true,'No export of a partially initialized build');
 assert.equal(elements.save.disabled,true,'No browser save of a partially initialized build');
 assert.ok(!messages.some(x=>x.startsWith('Class defaults loaded')),'No early success status');
 assert.equal(elements.status.dataset.buildState,'loading');assert.equal(panels['.workspace'].inert,true);
 await elements.exportBuild.listeners.click();await elements.save.listeners.click();await assert.rejects(()=>c.simulate(),/still loading/);
 assert.equal(downloads.length,0);assert.equal(stored.length,0);assert.equal(simulationCalls(),0);
 delayed.release();await loading;
 assert.equal(plain(c.saveConfig()).encounter.classSettings.warlockLifeTap,false,'Actual late class settings must finish first');
 assert.equal(elements.status.dataset.buildState,'ready');assert.equal(elements.run.disabled,false);assert.equal(elements.exportBuild.disabled,false);assert.equal(panels['.workspace'].inert,false);
 assert.match(elements.status.textContent,/^Class defaults loaded/);
 // Full stable Mage exports, including deliberately authored inactive settings,
 // survive a rejected cross-class import through the actual restoration path.
 elements.warlockLifeTap.checked=true;elements.warlockSacrifice.value='imp';elements.hunterAspect.value='hawk';elements.hunterSpecial.checked=true;elements.hunterDelay.value=4;elements.duration.value=33;c.buildDirty=true;
 const prior=plain(c.saveConfig());
 const enhancement={schema:'warcraftsim.solo-dps-build',version:1,classKey:'shaman',specialization:'enhancement',race:'orc',provisional:true,itemizedMode:true,gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},preparedCombatConsumables:[],racialActive:false,talents:{ranks:{...presets['shaman-enhancement'].ranks,'thundering-strikes':6}},encounter:{duration:30,seed:42,iterations:1,targetHealth:200000,timeline:true,enhancement:plain(settings),combatItems:{majorManaPotionCharges:0,majorHealingPotionCharges:0},soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]}}};
 const blocked=hold('/api/forever/talents/shaman/enhancement/validate-itemized');
 const importing=c.applyConfig(enhancement);await blocked.started;
 assert.equal(elements.run.disabled,true);assert.equal(elements.exportBuild.disabled,true);assert.equal(elements.classKey.disabled,true,'Import holds identity until validation/rollback finish');
 blocked.release();await assert.rejects(()=>importing,/Unrecognized talent or invalid rank: thundering-strikes/);
 assert.deepEqual(plain(c.saveConfig()),prior,'Restore every active/inactive exported field');assert.equal(c.buildDirty,true,'Restore the prior dirty guard');
 assert.equal(elements.status.dataset.buildState,'ready');assert.equal(elements.run.disabled,false);assert.equal(elements.exportBuild.disabled,false);
 // Use the real spec/cancel handlers after failed-import rollback. Cancelling
 // repeatedly must not alter readiness, dirty state or any exported field.
 for(const specialization of ['fire','frost','fire']){
  const request=c.request,callCount=calls.length,downloadCount=downloads.length;
  elements.spec.value=specialization;await elements.spec.listeners.change();
  assert.equal(elements.presetDialog.open,true);assert.equal(elements.spec.value,'arcane');assert.equal(c.pendingPreset,specialization);
  elements.cancelPreset.listeners.click();
  assert.equal(elements.presetDialog.open,false);assert.equal(c.pendingPreset,null);assert.equal(c.request,request);assert.equal(c.buildDirty,true);assert.equal(calls.length,callCount,'Cancel must not load a replacement preset');
  assert.equal(elements.status.dataset.buildState,'ready');assert.equal(elements.exportBuild.disabled,false);
  await elements.exportBuild.listeners.click();assert.equal(downloads.length,downloadCount+1,'Every cancelled-switch export produces exactly one fresh download');assert.equal(downloads.at(-1).name,'warcraftsim-build.json');assert.deepEqual(plain(downloads.at(-1).data),prior,'Cancel then export preserves complete rollback');
 }
 // Confirm after repeated cancellations must still execute the chosen preset,
 // rather than treating a stale cancelled selection as an accepted transition.
 elements.spec.value='frost';await elements.spec.listeners.change();
 await elements.confirmPreset.listeners.click();assert.equal(elements.presetDialog.open,false);assert.equal(c.pendingPreset,null);assert.equal(c.loadedSpec,'frost');assert.equal(elements.status.dataset.buildState,'ready');
 const confirmedDownloadCount=downloads.length;await elements.exportBuild.listeners.click();assert.equal(downloads.length,confirmedDownloadCount+1);assert.equal(downloads.at(-1).data.specialization,'frost');
 // The old operation must not unlock a newer same-class preset while that
 // newer operation is still awaiting its own baseline response.
 const racing=environment(),oldSource=racing.hold('/api/forever/talents/source-coverage');
 const oldLoad=racing.c.loadPreset();await oldSource.started;
 racing.elements.spec.value='fire';const newBaseline=racing.hold('/api/forever/character/base-stats?race=gnome&classKey=mage&level=60&provisional=true');
 const newLoad=racing.c.loadPreset();await newBaseline.started;oldSource.release();await oldLoad;
 assert.equal(racing.elements.status.dataset.buildState,'loading');assert.equal(racing.elements.run.disabled,true);assert.equal(racing.elements.exportBuild.disabled,true);
 newBaseline.release();await newLoad;assert.equal(racing.elements.status.dataset.buildState,'ready');assert.equal(racing.c.loadedSpec,'fire');
 // Failed preparation never advertises a ready/exportable request.
 racing.failures.set('/api/sim/mage/defaults?spec=fire','Controlled defaults failure');await racing.c.loadPreset();
 assert.equal(racing.elements.status.dataset.buildState,'unavailable');assert.equal(racing.elements.run.disabled,true);assert.equal(racing.elements.exportBuild.disabled,true);assert.match(racing.elements.status.textContent,/Controlled defaults failure/);
 // Hold catalog loading before the preset itself has begun.
 const catalog=environment();let releaseCatalog,enterCatalog;
 const catalogStarted=new Promise(r=>enterCatalog=r),catalogPending=new Promise(r=>releaseCatalog=r);
 catalog.c.loadCatalog=async()=>{enterCatalog();await catalogPending;};
 const beforeStatsToken=catalog.c.statsRefreshId,beforeBaselineToken=catalog.c.baselineCheckId;const initializing=catalog.c.initClass();await catalogStarted;assert.ok(catalog.c.statsRefreshId>beforeStatsToken,'Class selection invalidates pending stats before catalog loading');assert.ok(catalog.c.baselineCheckId>beforeBaselineToken,'Class selection invalidates pending baseline before catalog loading');assert.equal(catalog.elements.status.dataset.buildState,'loading');assert.equal(catalog.elements.run.disabled,true);assert.equal(catalog.elements.exportBuild.disabled,true);
 catalog.elements.spec.value='fire';const catalogSwitch=catalog.c.loadPreset();await Promise.resolve();await Promise.resolve();
 assert.equal(catalog.elements.status.dataset.buildState,'loading');assert.equal(catalog.elements.exportBuild.disabled,true);assert.equal(catalog.calls.some(x=>x.url==='/api/sim/mage/defaults?spec=fire'),false,'A fast spec load waits for the applicable class catalog');
 releaseCatalog();await Promise.all([initializing,catalogSwitch]);assert.equal(catalog.elements.status.dataset.buildState,'ready');assert.equal(catalog.c.loadedSpec,'fire');
 // Older Warrior defaults success/failure and delayed talent catalogs must not
 // replace a newer, already-ready Mage request or catalog.
 for(const phase of ['defaults-success','defaults-failure','talent-catalog']){
  const e=environment();e.elements.classKey.value='warrior';e.elements.spec.value='arms';
  const url=phase==='talent-catalog'?'/api/forever/shared/warrior/talents':'/api/forever/shared/warrior/defaults?spec=arms';
  const delayed=e.hold(url);if(phase==='defaults-failure')e.failures.set(url,'Stale Warrior failure');
  const old=e.c.loadPreset();await delayed.started;
  e.elements.classKey.value='mage';e.elements.spec.value='arcane';await e.c.loadPreset();
  const currentRequest=e.c.request,currentCatalog=plain(e.c.talentCatalog),status=e.elements.status.textContent;
  delayed.release();await old;
  assert.equal(e.c.request,currentRequest,phase+' preserves the exact new request');assert.deepEqual(plain(e.c.talentCatalog),currentCatalog);assert.equal(e.elements.status.dataset.buildState,'ready');assert.equal(e.elements.status.textContent,status);
 }
 // An old Mage's historical visual catalog cannot append keys after Enhancement
 // becomes ready; the production augment function performs its own token check.
 const augmented=environment(),augmentUrl='/api/forever/talents/mage/arcane/itemized-catalog';
 augmented.responses.set(augmentUrl,{unmodeled:[{key:'old-mage-visual',modeled:false}]});const augmentHeld=augmented.hold(augmentUrl);
 const oldAugment=augmented.c.loadPreset();await augmentHeld.started;
 augmented.elements.classKey.value='shaman';augmented.elements.spec.value='enhancement';await augmented.c.loadPreset();
 const enhancedCatalog=plain(augmented.c.talentCatalog);augmentHeld.release();await oldAugment;assert.deepEqual(plain(augmented.c.talentCatalog),enhancedCatalog);assert.equal(augmented.elements.status.dataset.buildState,'ready');
 // Likewise, an old class's equipment page cannot write global catalogs after
 // the newly selected class has completed its own catalog and preset.
 const equipment=environment(),itemsUrl='/api/forever/equipment/items?page=1&pageSize=200&usableOnly=true&classKey=mage&race=gnome&level=60';
 equipment.responses.set(itemsUrl,{items:[{id:999,source:'stale-mage-page'}],total:1});const oldPage=equipment.hold(itemsUrl);
 const oldClass=equipment.c.initClass();await oldPage.started;
 equipment.elements.classKey.value='shaman';await equipment.c.initClass();const currentBuild=plain(equipment.c.saveConfig());
 oldPage.release();await oldClass;assert.deepEqual(plain(equipment.c.catalog),[]);assert.deepEqual(plain(equipment.c.saveConfig()),currentBuild);assert.equal(equipment.elements.status.dataset.buildState,'ready');
 // A stale failed catalog may have both initClass and a later spec waiter.
 // Neither rejection may reach safe() and replace the newer ready status.
 const failedCatalog=environment(),failedPage=failedCatalog.hold(itemsUrl);
 failedCatalog.failures.set(itemsUrl,'Old Mage catalog failed');const oldInitialization=failedCatalog.c.safe(()=>failedCatalog.c.initClass())();await failedPage.started;
 failedCatalog.elements.spec.value='fire';const oldSpecWaiter=failedCatalog.c.safe(()=>failedCatalog.c.loadPreset())();await Promise.resolve();
 failedCatalog.elements.classKey.value='shaman';await failedCatalog.c.initClass();const readyStatus=failedCatalog.elements.status.textContent,readyBuild=plain(failedCatalog.c.saveConfig());
 failedPage.release();await Promise.all([oldInitialization,oldSpecWaiter]);
 assert.equal(failedCatalog.elements.status.textContent,readyStatus,'Stale catalog rejection cannot overwrite the current ready status');assert.equal(failedCatalog.elements.status.dataset.buildState,'ready');assert.deepEqual(plain(failedCatalog.c.saveConfig()),readyBuild);
 // All seventeen admitted class/spec routes use the same terminal boundary,
 // including Warrior's separate loader and the restricted late controls.
 const routes=vm.runInContext('specMap',c);let routesChecked=0;
 for(const [classKey,specs] of Object.entries(routes))for(const specialization of specs){
  const e=environment();e.elements.classKey.value=classKey;e.elements.spec.value=specialization;
  await e.c.loadPreset();assert.equal(e.elements.status.dataset.buildState,'ready',classKey+'/'+specialization);assert.equal(e.elements.run.disabled,false);assert.equal(e.elements.exportBuild.disabled,false);routesChecked++;
 }
 // Rogue actual import wrappers retain complete legacy tuning and restore
 // both valid and invalid dirty drafts after asynchronous server rejection.
 const rogue=environment();rogue.elements.classKey.value='rogue';await rogue.c.initClass();
 const validRogue=plain(rogue.c.saveConfig());validRogue.encounter.rogue.tuning.mutilateBonusDamagePerHand=17.25;validRogue.encounter.rogue.tuning.improvedExposeArmorFivePointRefund=5;
 rogue.responses.set('/api/sim/rogue/defaults?spec=assassination',{seed:42,durationSeconds:30,targetMaximumHealth:200000,captureTimeline:true,talents:{assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0,ranks:{}},...plain(validRogue.encounter.rogue),tuning:{...plain(validRogue.encounter.rogue.tuning),mutilateBonusDamagePerHand:13,improvedExposeArmorFivePointRefund:null}});
 await rogue.c.applyConfig(validRogue);assert.deepEqual(plain(rogue.c.saveConfig()),validRogue,'Import preserves every explicit legacy tuning field and complete saved build');
 for(const dirtyInvalid of [false,true])for(const provisional of [false,true]){
  rogue.elements.provisional.checked=provisional;
  rogue.elements.rogueMainHandWeaponDamage.value=177;rogue.elements.duration.value='33';rogue.c.buildDirty=true;
  if(dirtyInvalid)rogue.elements.rogueMainHandSpeedSeconds.value=0;
  const beforeRequest=plain(rogue.c.request),beforeValid=dirtyInvalid?null:plain(rogue.c.saveConfig());
  const bad=plain(validRogue);bad.talents.ranks.malice=6;
  await assert.rejects(()=>rogue.c.applyConfig(bad),/Unrecognized talent or invalid rank: malice/);
  assert.equal(rogue.elements.rogueMainHandWeaponDamage.value,177);assert.equal(rogue.elements.duration.value,'33');assert.equal(rogue.c.buildDirty,true);assert.deepEqual(plain(rogue.c.request),beforeRequest);
  assert.equal(rogue.elements.provisional.checked,provisional,'Unavailable reviewed baseline remains an explicit user draft');
  if(dirtyInvalid){assert.equal(rogue.elements.rogueMainHandSpeedSeconds.value,0);assert.throws(()=>rogue.c.saveConfig(),/weapon tuning/);}else assert.deepEqual(plain(rogue.c.saveConfig()),beforeValid);
  await rogue.c.applyConfig(validRogue);assert.deepEqual(plain(rogue.c.saveConfig()),validRogue,'Valid import recovers an invalid local draft');
 }
 // Execute the actual nested rank handler with its production closure owner.
 // A rollback must rebind that handler to the final restored request object.
 vm.runInContext(part('const talentTrees=','async function validateTalentSelection()')+'\nfunction renderTalents(){const talents=request.talents;'+part(' async function setRank(t,delta){',' function eligibility(t){')+'lastRenderedRankHandler=setRank;}',rogue.c);
 rogue.c.renderTalents();
 const rejectThenEdit=plain(validRogue);rejectThenEdit.talents.ranks.malice=6;rejectThenEdit.talents.assassinationPointsSpent=39;
 await assert.rejects(()=>rogue.c.applyConfig(rejectThenEdit),/Unrecognized talent or invalid rank/);
 rogue.c.talentCatalog=rogue.c.talentCatalog.map(t=>({...t,tree:'assassination'}));
 await rogue.c.lastRenderedRankHandler({key:'improved-expose-armor',maxRank:2},-1);
 await rogue.c.lastRenderedRankHandler({key:'improved-expose-armor',maxRank:2},-1);
 assert.equal(rogue.c.request.talents.assassinationPointsSpent,36);assert.equal(rogue.c.saveConfig().talents.ranks['improved-expose-armor'],undefined,'Post-rollback actual rank handlers mutate active request');
 await rogue.c.applyConfig(validRogue);
 const badUnknown=plain(validRogue);badUnknown.encounter.rogue.tuning.unknown=1;rogue.c.buildDirty=true;
 await assert.rejects(()=>rogue.c.applyConfig(badUnknown),/tuning fields/);assert.deepEqual(plain(rogue.c.saveConfig()),validRogue);assert.equal(rogue.c.buildDirty,true);
 for(const mutate of [x=>delete x.talents,x=>x.talents=null,x=>delete x.encounter.duration,x=>delete x.encounter.targetHealth]){
  const malformed=plain(validRogue);mutate(malformed);const callsBefore=rogue.calls.length;
  await assert.rejects(()=>rogue.c.applyConfig(malformed));assert.equal(rogue.calls.length,callsBefore,'Required-field rejection happens before any load');assert.deepEqual(plain(rogue.c.saveConfig()),validRogue);assert.equal(rogue.c.buildDirty,true);
 }
 console.log('Actual Rogue import transactions passed: complete legacy tuning roundtrip, unknown-key rejection, required-field atomicity, rejected dirty drafts and valid recovery from invalid speed.');
 assert.equal(routesChecked,17);
 console.log('Actual delayed preset/import wrapper readiness passed: all 17 routes, blocked run/export/save, complete inactive-field dirty rollback, repeated dirty cancel/fresh export and confirmed switch, delayed catalog/baseline/source coverage, fast spec switches, stale Warrior/default/talent/equipment responses and failed loading.');
})().catch(error=>{console.error(error);process.exitCode=1;});
