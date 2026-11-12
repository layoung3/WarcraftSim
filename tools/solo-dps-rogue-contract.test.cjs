'use strict';
// Exercise actual production wrappers and serializers, not a parallel builder.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8'),plain=x=>JSON.parse(JSON.stringify(x));
const part=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const identities=part('const specMap=','const slots=');
const helper=part('const rogueNumericKeys=','const enhancementNumericKeys=');
const preset=()=>JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json'));
const settings={targetArmor:2100,targetDodgePercent:4,targetParryPercent:2,rogueMaximumHealth:4700,startingEnergyPercent:65,poisonChargesPerWeapon:75,
 startInStealth:false,isBehindTarget:true,useExposeArmor:true,mainHandPoison:'deadly',offHandPoison:'wound',useInstantPoison:true,bothWeaponsDaggers:true,mainHandWeaponType:'dagger',offHandWeaponType:'dagger',useUtilityAbilities:false,additionalTargets:0,enableBossAttacks:false,
 tuning:{mainHandWeaponDamage:150,offHandWeaponDamage:100,mainHandSpeedSeconds:1.8,offHandSpeedSeconds:1.4,mutilateBonusDamagePerHand:17.25,improvedExposeArmorFivePointRefund:5}};
function environment(){
 const values={classKey:'rogue',spec:'assassination',race:'human',duration:'30',seed:'42',iterations:'1',targetHealth:'200000',rotation:'adaptive',hunterAspect:'hawk',hunterDelay:'0'};
 const elements=new Proxy({},{get:(o,key)=>o[key]??(o[key]={value:values[key]??'',checked:['provisional','timeline'].includes(key)})});
 for(const [key,value] of Object.entries({...settings,...settings.tuning}))elements['rogue'+key[0].toUpperCase()+key.slice(1)][typeof value==='boolean'?'checked':'value']=value;
 const request={specialization:'assassination',...plain(settings),talents:{assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0,ranks:plain(preset().ranks)}};
 const cfg={equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}};
 const c={$:id=>elements[id],request,loadedClass:'rogue',loadedSpec:'assassination',clone:plain,itemizedMode:true,talentCatalog:Object.keys(preset().ranks).map(key=>({key,modeled:true})),cfg,priorityRows:[],soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},
 validateRemovedPriorityTargets:()=>{},validMode:()=>true,mods:()=>plain(cfg.effects),hunterSurvivalSelection:()=>({}),
 buildCharacter:()=>({classKey:'rogue',specializationKey:'assassination',level:60,race:'human',equipment:{items:[]}}),SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')};
 vm.createContext(c);vm.runInContext([identities,helper,part('const enhancementNumericKeys=','function resetSelections()'),
 part('function validateRestrictedCasterImport(data){','const beforeElementalImport='),
 part('function body(){','const rogueNumericKeys='),part('function readWarriorAssumptions(){','function restoreWarriorAssumptions()'),
 part('const legacyBody=body;','const legacyLoadPreset='),part('const originalSoloBody=body;','const originalSoloSave='),
 part('const previousConditionalBody=body;','const previousConditionalDraw='),part('const baseAddBody=body;','const priorOrGroupDraw='),
 part('function saveConfig(){','async function applyConfig(data){'),part('const legacySave=saveConfig;','const legacyApplyConfig='),
 part('const originalSoloSave=saveConfig;','const originalSoloApply='),part('const beforeElementalSave=saveConfig;','const beforeElementalPreset=')].join('\n'),c);
 return {c,elements};
}
const {c,elements}=environment(),before=plain(c.request),built=plain(c.body()),saved=plain(c.saveConfig());
assert.deepEqual(built.rogue.tuning,settings.tuning);assert.deepEqual(saved.encounter.rogue,settings);assert.deepEqual(c.request,before);
for(const key of ['externalModifiers','rotationPolicy','customPriority','priorityConditions','soloEncounter','combatItems','majorManaPotionCharges','useRacialActive'])assert.equal(Object.hasOwn(built.rogue,key),false,key);
for(const key of ['rotation','customPriority','priorityRows','rotationConditions','classSettings','combatItems','majorManaPotionCharges','useMajorManaPotion'])assert.equal(Object.hasOwn(saved.encounter,key),false,key);
c.validateRestrictedCasterImport(saved);
for(const value of [0,13,17.25]){const x=plain(saved);x.encounter.rogue.tuning.mutilateBonusDamagePerHand=value;c.validateRestrictedCasterImport(x);c.validateRogueSettings(x.encounter.rogue,settings.tuning);}
for(const value of [null,0,1,2,5]){const x=plain(saved);x.encounter.rogue.tuning.improvedExposeArmorFivePointRefund=value;c.validateRogueSettings(x.encounter.rogue,settings.tuning);}
let count=0;
for(const mutate of [x=>x.encounter.rogue=null,x=>x.encounter.rogue.other=true,x=>x.encounter.rogue.targetArmor=-1,x=>x.encounter.rogue.startingEnergyPercent=101,
 x=>x.encounter.rogue.poisonChargesPerWeapon=.5,x=>x.encounter.rogue.rogueMaximumHealth=0,x=>x.encounter.rogue.mainHandPoison='bogus',x=>x.encounter.rogue.useInstantPoison=false,
 x=>x.encounter.rogue.mainHandWeaponType='sword',x=>x.encounter.rogue.bothWeaponsDaggers=false,x=>x.encounter.rogue.useUtilityAbilities=true,x=>x.encounter.rogue.additionalTargets=1,x=>x.encounter.rogue.enableBossAttacks=true,
 x=>x.encounter.rogue.targetDodgePercent=101,x=>x.encounter.rogue.tuning.mainHandSpeedSeconds=0,x=>x.encounter.rogue.tuning.mutilateBonusDamagePerHand='17.25',x=>x.encounter.rogue.tuning.improvedExposeArmorFivePointRefund=6,
 x=>x.encounter.rotation='adaptive',x=>x.encounter.combatItems={},x=>x.encounter.customPriority=[],x=>x.encounter.priorityRows=[],x=>x.encounter.targetInitialHealthPercent=65,
 x=>x.gear.items.push({slotKey:'main-hand',itemId:1}),x=>x.gear.extra=true,x=>x.modifiers.buffs.push('foo'),x=>x.modifiers.extra=[],x=>x.racialActive=true,x=>x.preparedCombatConsumables=[{}],x=>x.encounter.iterations=2,x=>x.encounter.soloScript.enabled=true,x=>x.encounter.soloScript.addWindows=[{}],x=>x.level60Baseline={},x=>delete x.talents,x=>x.talents=null,x=>delete x.talents.ranks,x=>x.talents.ranks=null,x=>x.talents.ranks=[],x=>delete x.talents.combatPointsSpent,x=>x.talents.extra=0,x=>x.provisional='true',x=>delete x.encounter.duration,x=>delete x.encounter.targetHealth,x=>delete x.encounter.seed,x=>delete x.encounter.timeline,x=>x.encounter.timeline='true',x=>x.encounter.duration='',x=>x.encounter.seed=1.5]){const x=plain(saved);mutate(x);assert.throws(()=>c.validateRestrictedCasterImport(x));count++;}
const unknown=plain(settings);unknown.tuning.unknown=1;assert.throws(()=>c.validateRogueSettings(unknown,settings.tuning),/tuning/);
const missing=plain(settings);delete missing.tuning.mutilateBonusDamagePerHand;assert.throws(()=>c.validateRogueSettings(missing,settings.tuning),/tuning/);
const legal=plain(saved);delete legal.talents.ranks['improved-expose-armor'];legal.talents.assassinationPointsSpent=36;c.validateRestrictedCasterImport(legal);assert.equal(Object.values(legal.talents.ranks).reduce((a,b)=>a+b),36);
const expression='({'+[...source.matchAll(/combatSummary:\{([\s\S]*?)\n  \},\n  warnings:/g)][0][1]+'})';
const data={dps:100,damageDone:3000,maximumEnergy:110,endingEnergy:43,maximumHealth:3000,endingHealth:3000,run:{summary:{actorSummaries:{rogue:{startingResources:{energy:71.5}}},abilities:{}}},damageByAbility:{mutilate:3000}};
// Synthetic summary/request unit fixture, completed with the bounded talent contract.
const a={schema:'warcraftsim.solo-dps-report',version:1,build:saved,originalSimulationRequest:built,combatSummary:plain(vm.runInNewContext(expression,{b:built,data,clone:plain})),rawServerResponse:{classKey:'rogue',specialization:'assassination',level:60,allocatedTalentPoints:38,unspentTalentPoints:13,sim:{...data,specialization:'assassination'}},talentAllocation:{mode:'strict-rank-derived',totalSelectedRanks:38,unspentPoints:13,treePoints:{assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0},selected:Object.entries(saved.talents.ranks).map(([key,rank])=>({key,rank}))}};
const warningExpression=part('  warnings:[','  rawServerResponse:clone(response)').trim().replace(/^warnings:/,'').replace(/,$/,'');
assert.deepEqual(plain(vm.runInNewContext(warningExpression,{metadata:{notes:['metadata']},response:{talentModelingNotes:['decoded-source caveat','partial Cold Blood']},data:{modelingNotes:['combat']},runCatalog:[],runTalents:{},runItemizedMode:true,clone:plain})),['metadata','decoded-source caveat','partial Cold Blood','combat'],'Exported/rendered warnings retain exact strict source and partial mechanics notes');
const {inspectReport,compareReports}=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
assert.equal(a.combatSummary.startingEnergy,71.5);assert.equal(a.combatSummary.maximumEnergy,110);assert.equal(a.combatSummary.ownerDamage,3000);
assert.equal(compareReports(a,a).comparable,true);
const weapon=plain(a);weapon.build.encounter.rogue.tuning.mainHandWeaponDamage=200;weapon.originalSimulationRequest.rogue.tuning.mainHandWeaponDamage=200;
assert.equal(compareReports(a,weapon).comparable,true);assert.ok(compareReports(a,weapon).changes.some(x=>/mainHandWeaponDamage/.test(x)));
const target=plain(a);target.build.encounter.rogue.targetDodgePercent=0;target.originalSimulationRequest.rogue.targetDodgePercent=0;target.combatSummary.targetSemantics.targetDodgePercent=0;assert.equal(compareReports(a,target).comparable,false);
const tamper=plain(a);tamper.build.encounter.rogue.tuning.mutilateBonusDamagePerHand=13;assert.ok(inspectReport(tamper).integrityWarnings.some(x=>/tuning/.test(x)));
for(const key of ['tuning','mainHandPoison','startingEnergyPercent','bothWeaponsDaggers']){const missing=plain(a);delete missing.originalSimulationRequest.rogue[key];assert.throws(()=>inspectReport(missing),/required/);}
for(const key of ['maximumEnergy','startingEnergy','endingEnergy','maximumHealth','endingHealth']){const missing=plain(a);delete missing.combatSummary[key];assert.throws(()=>inspectReport(missing),/required/);}
const mixed=plain(a);mixed.originalSimulationRequest.shadow={};assert.throws(()=>inspectReport(mixed));
const p=preset();assert.equal(p.totalRankedPoints,38);assert.equal(Object.keys(p.ranks).length,12);assert.match(p.provenance.statement,/13 unspent/);
console.log(`Rogue actual request/export/summary/comparison contracts passed; ${count} strict rejections and legacy tuning preservation.`);
(async()=>{
 // Execute the actual simulation renderer, readable-number wrapper and export
 // listener in a bounded DOM model. This is not a browser/pixel acceptance run.
 class Element{
  constructor(tag='div'){this.tag=tag;this.children=[];this.listeners={};}
  set textContent(value){this.children=[{textContent:String(value)}];}
  get textContent(){return this.children.map(child=>child.textContent).join('');}
  get firstChild(){return this.children[0];}
  append(...children){this.children.push(...children);}
  replaceChildren(...children){this.children=children;}
  querySelectorAll(tag){return this.children.flatMap(child=>[...(child.tag===tag?[child]:[]),...(child.querySelectorAll?.(tag)||[])]);}
  querySelector(tag){return this.querySelectorAll(tag)[0]||null;}
  addEventListener(name,listener){this.listeners[name]=listener;}
 }
 async function render(classKey,specialization,energy){
  const elements=new Proxy({},{get:(object,key)=>object[key]??(object[key]=new Element())});
  elements.classKey.value=classKey;elements.spec.value=specialization;elements.iterations.value='1';
  const simulation={dps:123.456,damageDone:2345.678,petDamage:234.567,endingMana:1234.567,maximumMana:2345.678,
   maximumEnergy:energy[0],endingEnergy:energy[2],maximumHealth:4900.567,endingHealth:4321.234,
   damageByAbility:{mutilate:2345.678},run:{summary:{actorSummaries:{rogue:{startingResources:{energy:energy[1]}}},abilities:{mutilate:{castCompletedCount:3,damageOccurrenceCount:6,criticalDamageCount:1}}},timeline:[{resourceTotalAfter:energy[2]}]}};
  const response={classKey,specialization,sim:simulation},originalResponse=plain(response),downloads=[];
  const context={$:id=>elements[id],document:{createElement:tag=>new Element(tag)},clone:plain,pretty:value=>JSON.stringify(value,null,2),
   request:{talents:{ranks:{}}},generation:1,talentCatalog:[],itemizedMode:true,results:null,
   saveConfig:()=>({classKey,specialization}),validateTalentSelection:async()=>{},validateGear:async()=>({items:[]}),
   preview:async()=>({maximumEnergy:energy[0],startingEnergy:energy[1]}),api:async()=>({notes:[]}),
   post:async url=>{assert.equal(url,'/api/forever/character/simulate-level60');return response;},
   note:()=>{},navigate:()=>{},download:(name,data)=>downloads.push({name,data:plain(data)})};
  vm.createContext(context);vm.runInContext(identities,context);
  const requestBody={classKey,specialization,[vm.runInContext('typedKey($("classKey").value,$("spec").value)',context)]:{durationSeconds:20.125,targetArmor:2100,targetDodgePercent:3,targetParryPercent:7}};
  context.body=()=>plain(requestBody);
  vm.runInContext(part('async function simulate(){','function replaySignature(result){'),context);
  vm.runInContext('const testRender=simulate;simulate=async function(){await testRender();beforeFormatting=JSON.stringify(results);};',context);
  vm.runInContext(part('function formatResultNumber(','function currentTalentIcon('),context);
  vm.runInContext(part("$('exportResults').addEventListener('click',", " $('replayResult').addEventListener"),context);
  await context.simulate();
  const metrics=Object.fromEntries(elements.metrics.children.map(metric=>[(metric.querySelector('span')?.textContent||metric.firstChild.textContent).replace(/:\s*$/,''),metric.querySelector('strong').textContent]));
  assert.equal(JSON.stringify(context.results),context.beforeFormatting,'Readable formatting must not mutate the complete report');
  context.readableResultNumbers();
  assert.equal(JSON.stringify(context.results),context.beforeFormatting,'Repeated formatting must not mutate the complete report');
  assert.deepEqual(response,originalResponse,'Source response remains byte-for-byte JSON-equivalent');
  elements.exportResults.listeners.click();
  assert.equal(downloads.length,1);assert.equal(downloads[0].name,'warcraftsim-simulation-report.json');
  assert.deepEqual(downloads[0].data,JSON.parse(context.beforeFormatting),'Actual export listener preserves the entire pre-format report');
  assert.deepEqual(downloads[0].data.rawServerResponse,originalResponse);
  for(const [key,value] of Object.entries({maximumEnergy:energy[0],startingEnergy:energy[1],endingEnergy:energy[2]}))assert.equal(downloads[0].data.combatSummary[key],value,key+' export precision/zero');
  assert.equal(elements.raw.textContent,JSON.stringify(originalResponse,null,2));
  for(const [key,value] of Object.entries({DPS:'123.5',Damage:'2,346',Duration:'20.13',PetDamage:'235',EndingMana:'1,235',MaximumMana:'2,346',MaximumHealth:'4,901',EndingHealth:'4,321'}))assert.equal(metrics[key],value,classKey+' unchanged '+key);
  assert.deepEqual(elements.abilities.querySelectorAll('td').map(cell=>cell.textContent),['mutilate','3','6','1','2,346']);
  return metrics;
 }
 for(const [raw,expected] of [
  [[110,27.5,17.5],['110','27.5','17.5']],
  [[110,0,0],['110','0','0']],
  [[0,0,0],['0','0','0']],
  [[110.125,27.125,17.125],['110.13','27.13','17.13']]
 ]){
  const metrics=await render('rogue','assassination',raw);
  assert.deepEqual(['MaximumEnergy','StartingEnergy','EndingEnergy'].map(key=>metrics[key]),expected,'Rogue Energy cards keep up to two decimals, no trailing zeros or dropped zero');
 }
 const routes=vm.runInNewContext(identities+';Object.entries(specMap).flatMap(([classKey,specs])=>specs.map(spec=>[classKey,spec]));');
 for(const [classKey,specialization] of routes.filter(([classKey,spec])=>classKey!=='rogue'&&spec!=='feral')){
  const metrics=await render(classKey,specialization,[110,27.5,17.5]);
  assert.deepEqual(['MaximumEnergy','StartingEnergy','EndingEnergy'].map(key=>metrics[key]),['110','28','18'],classKey+'/'+specialization+' retains its existing Energy formatting');
 }
 console.log('Actual renderer/export preserves Rogue Energy 110/27.5/17.5, two-decimal precision and zero; complete reports/raw values and all other admitted class metrics remain unchanged.');
})().catch(error=>{console.error(error);process.exitCode=1;});

(async()=>{
 // Run the actual async preview with delayed HTTP resolution. No stale result
 // may render after class/catalog, race or baseline mode has changed.
 for(const change of ['generation','class','race','baseline']){
  let release;const pending=new Promise(resolve=>release=resolve);
  const controls={classKey:{value:'rogue'},spec:{value:'assassination'},race:{value:'human'},provisional:{checked:true}};
  let painted=0;
  const state={isFeral:()=>false,isCombatRogue:()=>false,$:id=>controls[id]??{replaceChildren:()=>painted++},statsRefreshTimer:null,statsRefreshId:7,generation:1,request:{talents:{ranks:{}}},
   buildCharacter:()=>({}),isRestrictedCaster:()=>true,typedKey:()=> 'rogue',body:()=>({rogue:{}}),post:()=>pending,document:{createElement:()=>{painted++;throw Error('Unexpected stale rendering');}}};
  vm.createContext(state);vm.runInContext(part('async function preview(refreshToken){','async function simulate(){'),state);
  const work=state.preview(7);
  if(change==='generation')state.generation++;
  if(change==='class')controls.classKey.value='mage';
  if(change==='race')controls.race.value='orc';
  if(change==='baseline')controls.provisional.checked=false;
  release({effectiveStats:{values:{energy:110}},startingEnergy:55});await work;assert.equal(painted,0,'Stale '+change+' preview cannot paint');
 }
 console.log('Rogue actual preview rejects stale generation, class, race and baseline responses.');
})().catch(error=>{console.error(error);process.exitCode=1;});

const replayContext={};vm.createContext(replayContext);vm.runInContext(part('function replaySignature(result){','async function replayLastResult(){'),replayContext);
const replayA={sim:{damageDone:100,dps:10,maximumEnergy:110,endingEnergy:50,maximumHealth:4000,endingHealth:4000,run:{summary:{actorSummaries:{rogue:{startingResources:{energy:110},endingResources:{energy:50}}}},timeline:[{auraInstanceId:'first-run-guid',amount:100}]}}};
const replayB=plain(replayA);replayB.sim.run.timeline[0].auraInstanceId='second-run-guid';
assert.equal(replayContext.replaySignature(replayA),replayContext.replaySignature(replayB),'Fresh non-gameplay engine GUIDs do not invalidate exact-seed replay');
replayB.sim.endingEnergy=49;assert.notEqual(replayContext.replaySignature(replayA),replayContext.replaySignature(replayB),'Actual ending resource differences invalidate replay');

for(const [field,value] of [['timeSeconds',1],['targetActorKey','other-target'],['amount',101],['abilityKey','another-ability'],['resourceTotalAfter',49]]){
 const changed=plain(replayA);changed.sim.run.timeline[0][field]=value;
 assert.notEqual(replayContext.replaySignature(replayA),replayContext.replaySignature(changed),'Gameplay timeline '+field+' remains replay-significant');
}

(async()=>{
 // Exercise the actual browser helper without starting Chromium. Fresh-preview
 // gates must reject a native no-op before dispatching input or waiting.
 const browser=fs.readFileSync('tools/solo-dps-rogue-browser.test.cjs','utf8');
 const from=browser.indexOf('async function setRogueSetting(field,value){');
 const to=browser.indexOf('async function selectFile(',from);
 assert.ok(from>=0&&to>from,'Browser requires an explicit changed-setting helper before fresh-preview waits');
 const controls={rogueOffHandPoison:{value:'instant'},rogueTargetArmor:{value:'2100'}};
 const actions=[],waits=[],reads=[];let sequence=34;
 const dom={document:{querySelector:selector=>controls[selector.slice(1)]}};vm.createContext(dom);
 const actual={assert,evaluate:async expression=>vm.runInContext(expression,dom),
  sequence:async()=>{reads.push(sequence);return sequence;},
  setInput:async(selector,value)=>{actions.push({selector,value});controls[selector.slice(1)].value=String(value);sequence++;},
  ready:async(after,fields)=>waits.push({after,fields:plain(fields)})};
 vm.createContext(actual);vm.runInContext(browser.slice(from,to),actual);
 await assert.rejects(()=>actual.setRogueSetting('offHandPoison','instant'),/requires a changed value/);
 await assert.rejects(()=>actual.setRogueSetting('targetArmor',2100),/requires a changed value/);
 assert.deepEqual(actions,[],'No-op rejects before native input');assert.deepEqual(waits,[],'No-op never waits for an impossible newer response');assert.deepEqual(reads,[],'No-op rejects before capturing a predecessor');
 await actual.setRogueSetting('offHandPoison','none');await actual.setRogueSetting('offHandPoison','instant');
 assert.deepEqual(actions,[{selector:'#rogueOffHandPoison',value:'none'},{selector:'#rogueOffHandPoison',value:'instant'}]);
 assert.deepEqual(waits,[{after:34,fields:{offHandPoison:'none'}},{after:35,fields:{offHandPoison:'instant'}}]);
 assert.deepEqual(reads,[34,35]);assert.equal(controls.rogueOffHandPoison.value,'instant');
 console.log('Actual Rogue browser setting helper rejects no-ops and gates Instant → None → Instant on exact fresh predecessors.');
})().catch(error=>{console.error(error);process.exitCode=1;});

(async()=>{
 // Execute the real browser Energy gate against DOM/HTTP fixtures. Rounded,
 // missing, duplicate or stale values must fail before recording a passed gate.
 const browser=fs.readFileSync('tools/solo-dps-rogue-browser.test.cjs','utf8');
 const start=browser.indexOf('async function assertEnergyCards(report,label){'),end=browser.indexOf('async function main(){',start);
 assert.ok(start>=0&&end>start);assert.ok(browser.includes("await assertEnergyCards(a.data,'rogue-manual-desktop');"));
 function fixture(energy=[110,27.5,17.5]){
  const receipt={status:200,sequence:42,response:{sim:{maximumEnergy:energy[0],endingEnergy:energy[2],run:{summary:{actorSummaries:{rogue:{startingResources:{energy:energy[1]}}}}}}}};
  const report={rawServerResponse:plain(receipt.response)};
  const format=value=>value.toLocaleString('en-US',{maximumFractionDigits:2});
  const cards=['MaximumEnergy','StartingEnergy','EndingEnergy'].map((name,index)=>[name,format(energy[index])]);
  const sidebar=[['Maximum Energy',format(energy[0])],['Starting Energy',format(energy[1])]];
  const dom={window:{__rogueHttp:[{url:'/api/forever/character/simulate-level60',...receipt}]},document:{querySelectorAll:selector=>(selector==='#metrics > div'?cards:sidebar).map(([name,value])=>({
   firstChild:{textContent:name+': '},querySelector:tag=>tag==='strong'?{textContent:value}:selector==='#stats > div'?{textContent:name}:null
  }))}};
  vm.createContext(dom);
  // CDP returns plain JSON; normalize VM realm prototypes for those same values.
  const context={assert:{...assert,deepEqual:(actual,expected,message)=>assert.deepEqual(plain(actual),plain(expected),message)},
   evaluate:async expression=>plain(vm.runInContext(expression,dom)),gateReceipts:[],checks:0};
  vm.createContext(context);vm.runInContext(browser.slice(start,end),context);
  return {context,report,cards,sidebar};
 }
 for(const energy of [[110,27.5,17.5],[110,0,0]]){
  const f=fixture(energy);await f.context.assertEnergyCards(f.report,'fixture');
  assert.equal(f.context.checks,1);assert.equal(f.context.gateReceipts.length,1);
  assert.deepEqual(plain(f.context.gateReceipts[0].resources),{MaximumEnergy:energy[0],StartingEnergy:energy[1],EndingEnergy:energy[2]});
 }
 for(const mutate of [
  f=>{f.cards[1][1]='28';f.cards[2][1]='18';},
  f=>f.cards.splice(1,1),f=>f.cards.push(f.cards[0]),
  f=>{f.sidebar[1][1]='28';},f=>{f.report.rawServerResponse.sim.endingEnergy=18;}
 ]){
  const f=fixture();mutate(f);await assert.rejects(()=>f.context.assertEnergyCards(f.report,'rejected'));
  assert.equal(f.context.checks,0);assert.deepEqual(f.context.gateReceipts,[]);
 }
 const zero=fixture([110,0,0]);zero.cards.splice(1,2);
 await assert.rejects(()=>zero.context.assertEnergyCards(zero.report,'dropped-zero'),/Energy cards/);
 assert.equal(zero.context.checks,0);
 console.log('Actual browser Energy gate accepts server/card/sidebar precision and zero; rejects rounded, missing, duplicate, stale and dropped-zero values.');
})().catch(error=>{console.error(error);process.exitCode=1;});

(async()=>{
 // Execute the real capture geometry/framing helper against a bounded DOM model.
 // These checks are not screenshots or evidence that browser pixels were seen.
 const {segmentOffsets,createRegionCapture}=require('./solo-dps-rogue-capture.cjs');
 assert.deepEqual(segmentOffsets(500,528),[0]);assert.deepEqual(segmentOffsets(1000,528),[0,472]);
 assert.deepEqual(segmentOffsets(650,360),[0,290]);assert.throws(()=>segmentOffsets(1,32),/geometry/);
 assert.throws(()=>segmentOffsets(100000,528),/sixteen-segment/);
 function fixture({height=1100,header=284,width=390,viewport=844,tableWidth=650,top=1200,documentHeight=5000,hidden=false,unstable=false,outside=false}={}){
  let scrollY=0,scrollLeft=0,frames=0;const commands=[],files=[],receipts=[],scrolls=[];
  const wrapper={getBoundingClientRect:()=>({left:outside?-10:15,right:width-15}),clientWidth:width-30,scrollWidth:tableWidth,get scrollLeft(){return scrollLeft;},set scrollLeft(value){scrollLeft=Math.max(0,Math.min(value,tableWidth-this.clientWidth));}};
  const row={children:[{textContent:'Mutilate'},{textContent:'1'},{textContent:'2'},{textContent:'1'},{textContent:'525'}]};
  const table={id:'',textContent:'Ability Uses Damage events Crits Damage Mutilate 1 2 1 525',
   getClientRects:()=>hidden?[]:[{}],getBoundingClientRect:()=>({top:top-scrollY+(unstable?frames:0),bottom:top+height-scrollY+(unstable?frames:0),left:15,right:width-15}),
   closest:selector=>selector==='.table-wrap'?wrapper:null,querySelectorAll:selector=>selector==='tr'?[row]:[]};
  const body={id:'abilities',closest:selector=>selector==='table'?table:null};
  const context={document:{querySelectorAll:selector=>selector==='#abilities'?[body]:[],querySelector:()=>({getBoundingClientRect:()=>({bottom:header})})},
   window:{innerWidth:width,innerHeight:viewport,get scrollY(){return scrollY;},scrollTo:position=>{assert.equal(position.behavior,'instant');scrollY=Math.max(0,Math.min(position.top,documentHeight-viewport));scrolls.push({...position,actual:scrollY});}},
   requestAnimationFrame:resolve=>{frames++;resolve();}};
  vm.createContext(context);
  const capture=createRegionCapture({evaluate:async expression=>plain(await vm.runInContext(expression,context)),
   command:async(method,args)=>{commands.push({method,args});return {data:'test-image-placeholder'};},
   save:async(filename,data)=>{assert.equal(data,'test-image-placeholder');files.push(filename);},receipts});
  return {capture,commands,files,receipts,scrolls,position:()=>({scrollY,scrollLeft}),frames:()=>frames};
 }
 const mobile=fixture();const receipt=await mobile.capture('mobile-abilities','#abilities');
 assert.equal(receipt.verticalSegments,3);assert.equal(receipt.horizontalSegments,2);assert.equal(mobile.files.length,6);
 assert.deepEqual(mobile.position(),{scrollY:0,scrollLeft:0},'Capture restores initial scrolling after stable rendering');
 assert.equal(receipt.selector,'#abilities');assert.equal(receipt.tiles[0].top,1200,'tbody capture must include closest table header');
 assert.ok(receipt.tiles.every(tile=>tile.headerBottom===284&&tile.viewportWidth===390&&tile.rows[0].length===5));
 for(const horizontal of [0,290]){
  const tiles=receipt.tiles.filter(tile=>tile.horizontal.left===horizontal);
  assert.equal(tiles[0].from,1200);assert.equal(tiles.at(-1).to,2300);
  for(let i=1;i<tiles.length;i++)assert.ok(tiles[i].from<=tiles[i-1].to,'No hidden row gaps');
 }
 assert.ok(mobile.frames()>=3*(mobile.files.length+1),'Every frame must settle before screenshot');
 assert.ok(mobile.commands.every(call=>call.method==='Page.captureScreenshot'&&call.args.captureBeyondViewport===false));
 const desktop=fixture({height:500,header:76,width:1440,viewport:1000,tableWidth:1410});
 const desktopReceipt=await desktop.capture('desktop-abilities','#abilities');assert.equal(desktopReceipt.tiles.length,1);assert.equal(desktopReceipt.tiles[0].visibleTop,1200);
 const bottom=fixture({height:700,top:4000,documentHeight:4720});
 const bottomReceipt=await bottom.capture('bottom-abilities','#abilities');assert.equal(bottomReceipt.tiles.at(-1).to,4700,'Clamped document-bottom scroll still captures final row');
 await assert.rejects(()=>fixture({outside:true}).capture('outside','#abilities'),/horizontally outside viewport/);
 await assert.rejects(()=>fixture({hidden:true}).capture('hidden','#abilities'),/Hidden capture/);
 await assert.rejects(()=>fixture({unstable:true}).capture('unstable','#abilities'),/did not settle/);
 await assert.rejects(()=>fixture().capture('missing','#missing'),/Missing capture/);
 const browser=fs.readFileSync('tools/solo-dps-rogue-browser.test.cjs','utf8');
 for(const [name,selector] of [
  ['rogue-controls-desktop','#rogueSettings .settings-grid'],['rogue-controls-mobile','#rogueSettings .settings-grid'],
  ['rogue-capability-notes-desktop','#classCapabilityNotes'],['rogue-capability-notes-mobile','#classCapabilityNotes'],
  ['rogue-metrics-desktop','#metrics'],['rogue-metrics-mobile','#metrics'],['rogue-abilities-desktop','#abilities'],['rogue-abilities-mobile','#abilities'],
  ['rogue-talents-mobile','#talentSummary,#talentCoverage,#talents'],['rogue-results-notes-mobile','#resultWarnings'],['rogue-source-notes-mobile','#rogueAssassinationScope > p'],
  ...['weapon','armor'].flatMap(kind=>['desktop','mobile'].map(size=>['rogue-'+kind+'-comparison-values-'+size,'.comparison-workspace']))
 ])assert.ok(browser.includes("await captureRegion('"+name+"','"+selector+"')"),'Purposeful bounded region capture: '+name);
 const order=["captureRegion('rogue-weapon-comparison-values-desktop'","captureViewport(390,844,true)","captureRegion('rogue-weapon-comparison-values-mobile'","captureViewport(1440,1000,false)","captureRegion('rogue-armor-comparison-values-desktop'","captureViewport(390,844,true)","captureRegion('rogue-armor-comparison-values-mobile'"];
 let at=0;for(const token of order){at=browser.indexOf(token,at);assert.ok(at>=0,'Comparison viewport order: '+token);at+=token.length;}
 console.log('Actual Rogue capture helper passed bounded sticky-header, table-header/all-column, bottom-clamp, stable-frame and capture-order model contracts; browser pixels remain Actions-only.');
})().catch(error=>{console.error(error);process.exitCode=1;});

// Resolve capture selectors against actual production markup, not only strings.
require('./solo-dps-rogue-markup.test.cjs').run().catch(error=>{console.error(error);process.exitCode=1;});

// Invoked by the existing Rogue contract CI gate, with no new workflow hook.
require('./solo-dps-assassination-report.test.cjs').run().catch(error=>{console.error(error);process.exitCode=1;});

require('./solo-dps-rogue-nullable-tuning.test.cjs');
