'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {environment,build,plain,part,source,ranks,project}=require('./solo-dps-feral-fixture.cjs');
const {c,elements}=environment(),saved=build(),strict=plain(c.body());
assert.deepEqual(JSON.parse(fs.readFileSync('docs/acceptance/cp175/cp175-feral-historical-manual-cat.json','utf8')),saved,'Checked-in illustrative fixture is produced by actual current serializer');
assert.deepEqual(strict.itemizedTalentRanks,ranks);assert.equal(strict.feral.character.specializationKey,'feral');
assert.equal(c.feralDefaults().manualInputs.preTalentCatAttackPower,null,'Never inherit legacy800 or Balance AP');
assert.deepEqual(Object.keys(strict).sort(),['classKey','feral','itemizedTalentMode','itemizedTalentRanks','specialization']);
assert.deepEqual(Object.keys(strict.feral.character).sort(),['classKey','level','race','rulesetKey','specializationKey']);
assert.equal(strict.feral.manualInputs.preTalentCatAttackPower,800);assert.equal(strict.feral.level60Baseline,null);
for(const key of ['gear','modifiers','encounter','tuning','observations'])assert.equal(key in saved,false);
let rejected=0;
const invalid=[x=>x.inputMode='authored-inclusive',x=>delete x.inputMode,x=>x.provisional=false,x=>x.provisional='true',x=>x.race='human',x=>x.specialization='balance',x=>x.itemizedMode=false,
 x=>x.manualInputs=null,x=>delete x.manualInputs.preTalentCatAttackPower,x=>x.manualInputs.preTalentCatAttackPower=null,x=>x.manualInputs.preTalentCatAttackPower='800',
 x=>x.scenario.startInProwl=true,x=>x.scenario.role='bear',x=>x.scenario.targetLevel=60,x=>x.scenario.targetInitialHealthPercent=99,x=>x.scenario.preferredCatOpener='claw',x=>x.scenario.seed=1.5,x=>x.scenario.seed=2147483648,x=>x.scenario.targetMaximumHealth=0,x=>x.scenario.durationSeconds=1201,
 x=>x.talents.ranks.furor=4,x=>x.talents.totalPoints=51,x=>x.talents.ranks.ferocity='5'];
// Start-in-Prowl itself is valid; only an impossible front Ravage is rejected.
invalid[invalid.findIndex(f=>f.toString().includes('startInProwl=true'))]=x=>{x.scenario.startInProwl=true;x.scenario.targetBehind=false;};
for(const key of Object.keys(saved.manualInputs))for(const value of [null,'1',-1,Infinity])invalid.push(x=>x.manualInputs[key]=value);
for(const key of Object.keys(saved.talents).filter(k=>k!=='ranks'))for(const value of [null,'0',.5,-1])invalid.push(x=>x.talents[key]=value);
for(const key of ['level60Baseline','gear','tuning','maximumHealth','maximumMana','combatItems','externalModifiers','customPriority','companions','formTransitions','initialComboPoints','incomingBossHitDamage','inputLatencyMs']){
 invalid.push(x=>x[key]={});invalid.push(x=>x.scenario[key]={});invalid.push(x=>x.manualInputs[key]={});
}
for(const mutate of invalid){const x=plain(saved);mutate(x);assert.throws(()=>c.validateFeralImport(x));rejected++;}
for(const value of [0,.125,800.25]){elements.feralPreTalentCatAttackPower.value=String(value);assert.equal(c.saveConfig().manualInputs.preTalentCatAttackPower,value);assert.equal(c.body().feral.manualInputs.preTalentCatAttackPower,value);}
for(const raw of ['', ' ', 'NaN','-1']){elements.feralPreTalentCatAttackPower.value=raw;assert.throws(()=>c.saveConfig());}
elements.feralPreTalentCatAttackPower.value='800';c.request.talents.ranks.furor=0;c.request.talents.restorationPointsSpent=0;c.request.talents.totalPoints=31;c.request.talents.unspentPoints=20;
assert.equal(Object.hasOwn(c.body().itemizedTalentRanks,'furor'),true);assert.equal(c.body().feral.talents.ranks.furor,0);
assert.equal(JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/druid-feral.json')).totalRankedPoints,36);
function preparation(request){return {inputMode:saved.inputMode,request:plain(request),baseline:{race:request.character.race,classKey:'druid',level:60,strength:62,agility:65,stamina:69,intellect:100,spirit:110,observedUnbuffedMaximumHealth:2173,observedUnbuffedMaximumMana:2444,sourceKind:'historical-classic-stat-proxy',verificationStatus:'provisional-documented-proxy',includesRacialPassives:false},directRequest:{},preview:{initialForm:'cat',inputMode:'authored-inclusive',authoredAttackPower:request.manualInputs.preTalentCatAttackPower,heartOfTheWildAttackPowerIncrement:12.4,predatoryStrikesAttackPower:90,spellbookAttackPower:request.manualInputs.preTalentCatAttackPower+102.4,maximumHealth:2173,maximumMana:2594,initialShiftManaCost:250,manaAfterInitialShift:2344,maximumEnergy:100,startingEnergy:0,maximumRage:100,startingRage:0,abilities:[],configuredRotation:{id:'11111111-1111-4111-8111-111111111111',entries:[{abilityKey:'shifting-power'}]},modelingNotes:['Configured priority is not immediate eligibility.']},modelingNotes:['Manual AP is not an observation.']};}
if(require.main===module)(async()=>{
 const env=environment(),ctx=env.c,e=env.elements;let prepared=0;
 ctx.setBuildPreparing=()=>prepared++;
 ctx.post=async(url,input)=>{assert.equal(url,'/api/forever/character/preview-feral');const p=preparation(input);p.request=Object.fromEntries(Object.entries(p.request).reverse());return p;};
 await ctx.previewFeral();assert.equal(ctx.feralPreviewMatches(),true);assert.equal(e.statStatus.textContent,'Up to date');assert.ok(e.stats.children.some(row=>row.children[1].textContent==='902.4'));
 assert.match(e.feralBaseline.textContent,/2444/);assert.match(e.feralRotationEntries.textContent,/shifting-power/);
 for(const mutate of [()=>e.feralPreTalentCatAttackPower.value='',()=>e.feralPreTalentCatAttackPower.value='900',()=>e.provisional.checked=false,()=>e.seed.value='1.5',()=>e.feralUseFaerieFire.checked=true]){
  const previous={ap:e.feralPreTalentCatAttackPower.value,mode:e.provisional.checked,seed:e.seed.value,ff:e.feralUseFaerieFire.checked};mutate();assert.equal(ctx.feralPreviewMatches(),false);Object.assign(e.feralPreTalentCatAttackPower,{value:previous.ap});e.provisional.checked=previous.mode;e.seed.value=previous.seed;e.feralUseFaerieFire.checked=previous.ff;
 }
 for(const change of ['manual','talents','spec','generation','mode']){
  const late=environment();let release;const pending=new Promise(r=>release=r);late.c.post=()=>pending;const before=late.c.previewFeral();
  if(change==='manual')late.elements.feralPreTalentCatAttackPower.value='900';if(change==='talents')late.c.request.talents.ranks.furor=0;if(change==='spec')late.elements.spec.value='balance';if(change==='generation')late.c.generation++;if(change==='mode')late.elements.provisional.checked=false;
  release(preparation(strict.feral));await before;assert.equal(late.c.feralPreviewMatches(),false);assert.notEqual(late.elements.statStatus.textContent,'Up to date');
 }
 const staleFailure=environment();let rejectOld;staleFailure.c.post=()=>new Promise((_,reject)=>rejectOld=reject);const pendingFailure=staleFailure.c.previewFeral();staleFailure.elements.feralPreTalentCatAttackPower.value='900';staleFailure.c.post=async(_,input)=>preparation(input);await staleFailure.c.previewFeral();rejectOld(Error('Late stale server rejection'));await pendingFailure;assert.equal(staleFailure.c.feralPreviewMatches(),true);assert.equal(staleFailure.elements.statStatus.textContent,'Up to date');
 const replay={preparation:preparation(strict.feral),inputTiming:{additionalInputLatencyMilliseconds:0},sim:{endingMana:5,run:{timeline:[{abilityExecutionId:'22222222-2222-4222-8222-222222222222',amount:1},{abilityExecutionId:'22222222-2222-4222-8222-222222222222',amount:2}]}}};
 const normalized=plain(replay);normalized.preparation.preview.configuredRotation.id='33333333-3333-4333-8333-333333333333';for(const ev of normalized.sim.run.timeline)ev.abilityExecutionId='44444444-4444-4444-8444-444444444444';
 assert.equal(ctx.feralReplaySignature(replay),ctx.feralReplaySignature(normalized));
 for(const mutate of [x=>x.sim.endingMana++,x=>x.preparation.preview.configuredRotation.id='ENGINE-ID-0',x=>x.inputTiming.additionalInputLatencyMilliseconds=80,x=>x.preparation.preview.maximumMana++,x=>x.preparation.baseline.sourceReference='99999999-9999-4999-8999-999999999999',x=>x['preparation.preview.configuredRotation.id']='99999999-9999-4999-8999-999999999999',x=>x.sim.run.timeline.reverse(),x=>x.sim.run.timeline[1].abilityExecutionId='55555555-5555-4555-8555-555555555555']){const x=plain(normalized);mutate(x);assert.notEqual(ctx.feralReplaySignature(replay),ctx.feralReplaySignature(x));}
 console.log(`Feral actual serializers, partial36 and preparation renderer passed ${rejected} strict rejections, blank/zero/fraction AP, exact-zero ranks, request-bound stale guards and complete relationship-preserving replay.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={preparation};
if(require.main===module)(async()=>{
 class Element{
  constructor(tag='div'){this.tag=tag;this.children=[];this.listeners={};this.dataset={};}
  set textContent(x){this.children=[{textContent:String(x)}];}get textContent(){return this.children.map(x=>x.textContent).join('');}get firstChild(){return this.children[0];}
  append(...items){this.children.push(...items);}replaceChildren(...items){this.children=items;}addEventListener(event,fn){this.listeners[event]=fn;}
  querySelectorAll(tag){return this.children.flatMap(x=>[...(x.tag===tag?[x]:[]),...(x.querySelectorAll?.(tag)||[])]);}querySelector(tag){return this.querySelectorAll(tag)[0]||null;}
 }
 const e=environment(),ctx=e.c,old=e.elements,nodes=new Proxy({},{get:(o,id)=>o[id]??(o[id]=Object.assign(new Element(),{value:old[id].value,checked:old[id].checked}))});ctx.$=id=>nodes[id];ctx.document={createElement:tag=>new Element(tag)};
 const p=preparation(plain(ctx.body().feral));p.preview.maximumEnergy=100.125;p.preview.startingEnergy=0;p.preview.manaAfterInitialShift=2344.125;
 const raw={classKey:'druid',specialization:'feral',preparation:p,baseline:p.baseline,inputTiming:{additionalInputLatencyMilliseconds:0},talentModelingNotes:['Omen executes provisional 6%/10s; unresolved eligibility.'],sim:{dps:123.456,damageDone:1234.56,endingMana:17.125,endingEnergy:0,endingRage:0,damageByAbility:{claw:1234.56},run:{summary:{abilities:{claw:{castCompletedCount:3,damageOccurrenceCount:3,criticalDamageCount:1}},actorSummaries:{druid:{startingResources:{mana:2594,energy:0,rage:0},endingResources:{mana:17.125,energy:0,rage:0}}}},timeline:[{resourceKey:'energy',resourceTotalAfter:0}]}}};
 const downloads=[];Object.assign(ctx,{results:null,api:async()=>({notes:[]}),post:async()=>plain(raw),preview:async()=>plain(p),validateGear:async()=>({items:[]}),validateTalentSelection:async()=>{},note:()=>{},navigate:()=>{},download:(name,value)=>downloads.push({name,value:plain(value)})});
 vm.runInContext(part('async function simulate(){','function replaySignature(result){')+part('function formatResultNumber(','function currentTalentIcon(')+part("$('exportResults').addEventListener('click',", " $('replayResult').addEventListener"),ctx);
 await ctx.simulate();nodes.exportResults.listeners.click();const cards=Object.fromEntries(nodes.metrics.children.filter(x=>x.querySelector('strong')).map(x=>[x.querySelector('span').textContent,x.querySelector('strong').textContent]));
 assert.equal(cards['Prepared spellbook AP'],'902.4');assert.equal(cards['HOTW AP increment'],'12.4');assert.equal(cards['Starting Energy'],'0');assert.equal(cards['Ending Energy'],'0');assert.equal(cards['Post-shift Mana'],'2,344.13');assert.equal(cards['Ending Mana'],'17.13');assert.equal(cards['Actor starting Mana (before shift)'],'2,594');
 assert.deepEqual(downloads[0].value.rawServerResponse,raw);assert.equal(downloads[0].value.combatSummary.manaAfterInitialShift,2344.125);assert.equal(downloads[0].value.combatSummary.endingMana,17.125);assert.equal(downloads[0].value.combatSummary.endingHealth,null);assert.equal(downloads[0].value.combatSummary.actorKey,'druid');assert.ok(downloads[0].value.warnings.some(x=>x.includes('Omen')));
 console.log('Feral actual simulation renderer/export preserves AP902.4/HOTW12.4, exact raw fractions, zero Energy, distinct pre/post-shift Mana and complete notes.');
})().catch(error=>{console.error(error);process.exitCode=1;});
