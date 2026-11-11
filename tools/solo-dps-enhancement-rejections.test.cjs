'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {enhancementRejectionCases,serverTalentRejection,importRejectionMatches,rejectionExpression}=require('./solo-dps-enhancement-rejections.cjs');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
const browser=fs.readFileSync('tools/solo-dps-enhancement-browser.test.cjs','utf8');
const fragment=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const production=[fragment('const $=','const pretty='),fragment('const specMap=','const isRestrictedCaster='),
 fragment('const enhancementNumericKeys=','function resetSelections()'),fragment('const talentTrees=','function renderTalents()'),
 fragment('function validateRestrictedCasterImport(data){','const beforeElementalImport='),
 fragment('async function applyConfig(data){','const hunterSurvivalFlags=')].join('\n');
const preset=JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/shaman-enhancement.json'));
const settings={targetArmor:2100,maximumHealth:4900,weaponMinDamage:180,weaponMaxDamage:240,weaponSpeedSeconds:2.6,offHandWeaponMinDamage:100,offHandWeaponMaxDamage:145,offHandWeaponSpeedSeconds:1.8,incomingBossHitDamage:0,incomingBossSwingSeconds:4,dualWield:true,useStormstrike:true,useRageOfTheFarseer:true,useEarthShock:true,useMaelstromLightningBolt:true,useLightningBoltFiller:false,useWindfuryTotem:false,deployWindfuryTotem:false,useLightningShield:false,mainHandImbue:'rockbiter',offHandImbue:'flametongue'};
const valid=()=>({schema:'warcraftsim.solo-dps-build',version:1,classKey:'shaman',specialization:'enhancement',race:'orc',provisional:true,itemizedMode:true,gear:{items:[]},modifiers:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'},preparedCombatConsumables:[],racialActive:false,talents:{elementalPointsSpent:0,enhancementPointsSpent:36,restorationPointsSpent:0,ranks:structuredClone(preset.ranks)},encounter:{duration:30,seed:42,iterations:1,targetHealth:200000,timeline:true,enhancement:structuredClone(settings),combatItems:{majorManaPotionCharges:0,majorHealingPotionCharges:0},soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]}}});
function sandbox(){
 const elements=new Proxy({race:{options:[{value:'orc'}]}},{get:(o,key)=>o[key]??(o[key]={value:'',checked:false})});
 const receipts=[],c={isFeral:()=>false,document:{getElementById:id=>elements[id]},request:null,talentCatalog:Object.keys(preset.ranks).map(key=>({key,tree:'enhancement',modeled:true})),modifiers:[],cfg:{},itemizedMode:true,initClass:async()=>{},
  loadPreset:async()=>{c.request={talents:structuredClone(valid().talents),combatItems:structuredClone(valid().encounter.combatItems)};},
  fetch:async(url,options)=>{
   const request=JSON.parse(options.body);assert.equal(url,serverTalentRejection.server.url);assert.equal(request.ranks['thundering-strikes'],6);
   const response={valid:false,error:serverTalentRejection.error};receipts.push({sequence:101,url,status:400,request,response});
   return {ok:false,json:async()=>response};
  }};
 vm.createContext(c);vm.runInContext(production,c);return {c,receipts};
}
function evaluatePredicate(state,fixture,after=100){
 const document={querySelector:selector=>selector==='#file'?{value:state.fileValue}:{textContent:state.status}};
 const window={__fileChanged:state.fileChanged,__statusChanged:state.statusChanged,__enhancementHttp:state.receipts};
 return vm.runInNewContext(rejectionExpression(fixture,after),{document,window});
}
let checks=0;
(async()=>{
 assert.ok(browser.includes('for(const fixture of enhancementRejectionCases)'));
 assert.ok(browser.includes('await until(rejectionExpression(fixture,before))'));
 assert.ok(browser.includes('await until(rejectionExpression(serverTalentRejection,before))'));
 assert.ok(!browser.includes('/unavailable|unsupported|invalid|pending|only|reject'));
 // The server outcome is controlled, but its exact wording is tied to the real
 // controller and flows through the actual UI fetch/error/import pipeline.
 const controller=fs.readFileSync('WarcraftSim.Api/Controllers/ForeverSoloTalentController.cs','utf8');
 assert.ok(controller.includes('throw new ArgumentException($"Unrecognized talent or invalid rank: {choice.Name}.")'));
 for(const fixture of [...enhancementRejectionCases,serverTalentRejection]){
  const data=valid();fixture.mutate(data);const {c,receipts}=sandbox();let actual;
  try{c.validateRestrictedCasterImport(data);await c.applyConfig(data);}catch(error){actual=error.message;}
  assert.equal(actual,fixture.error,fixture.name+' must match the actual validation error, not a keyword approximation');checks++;
  const completed={fileChanged:true,statusChanged:true,fileValue:'',status:actual,receipts};
  assert.equal(evaluatePredicate(completed,fixture),true,fixture.name+' actual browser predicate');assert.equal(importRejectionMatches(completed,fixture,100),true);checks+=2;
  for(const delta of [{fileChanged:false},{statusChanged:false},{fileValue:'C:\\fakepath\\pending.json'},{status:'Build imported.'},{status:'Loading Enhancement source-backed starting build…'},{status:'Unrelated failure'}]){
   assert.equal(evaluatePredicate({...completed,...delta},fixture),false,fixture.name+' cannot accept stale, successful or pending state');checks++;
  }
  if(fixture.server){
   for(const mutate of [x=>x.sequence=100,x=>x.status=200,x=>x.url='/api/forever/character/preview-enhancement',x=>x.request.ranks['thundering-strikes']=5,x=>x.response.error='Unrelated server error']){
    const wrong=structuredClone(receipts);mutate(wrong[0]);assert.equal(evaluatePredicate({...completed,receipts:wrong},fixture),false,'Server rejection must correlate request, response and operation');checks++;
   }
   assert.equal(evaluatePredicate({...completed,receipts:[]},fixture),false);checks++;
  }else assert.equal(receipts.length,0,fixture.name+' rejects locally');
 }
 // Execute the actual browser observation helper. Old status and old file
 // changes cannot satisfy a new attempt, including repeated identical errors.
 const begin=browser.slice(browser.indexOf('async function beginImportObservation('),browser.indexOf('async function importBuild(file)'));
 const listeners=new Map(),observers=[],file={value:'',addEventListener:(name,fn)=>listeners.set(name,fn),removeEventListener:(name,fn)=>{if(listeners.get(name)===fn)listeners.delete(name);}},status={textContent:enhancementRejectionCases[0].error};
 const page={window:{__fileChanged:true,__statusChanged:true},document:{querySelector:x=>x==='#file'?file:status},MutationObserver:class{constructor(fn){this.fn=fn;observers.push(this);}observe(){}disconnect(){this.disconnected=true;}}};
 vm.createContext(page);const test={evaluate:async expression=>vm.runInContext(expression,page)};vm.createContext(test);vm.runInContext(begin,test);
 await test.beginImportObservation();assert.equal(page.window.__statusChanged,false);assert.equal(page.window.__fileChanged,false);
 assert.equal(importRejectionMatches({fileChanged:false,statusChanged:false,fileValue:'',status:status.textContent,receipts:[]},enhancementRejectionCases[0],100),false);checks++;
 listeners.get('change')();observers.at(-1).fn();assert.equal(page.window.__fileChanged,true);assert.equal(page.window.__statusChanged,true);
 await test.beginImportObservation();assert.equal(page.window.__fileChanged,false);assert.equal(page.window.__statusChanged,false);assert.equal(observers[0].disconnected,true);checks++;
 // Execute the actual remaining rotation-readiness expression; a nonempty
 // placeholder is not a rendered runtime priority.
 const rotationLine=browser.split('\n').find(line=>line.includes("await until(`")&&line.includes("#enhancementRotationEntries")&&line.includes('response.entries'));
 assert.ok(rotationLine);
 for(const rendered of ['Lightning Shield > Stormstrike','Preview the actual fixed initial priority.']){
  let observed;const page={window:{__enhancementHttp:[{sequence:101,url:'/api/forever/rotation/preview',status:200,request:{specialization:'enhancement'},response:{entries:[{abilityKey:'lightning-shield'},{abilityKey:'stormstrike'}]}}]},document:{querySelector:()=>({textContent:rendered})}};
  const context={start:100,until:async expression=>{observed=vm.runInNewContext(expression,page);}};
  vm.createContext(context);await vm.runInContext('(async()=>{'+rotationLine+'})()',context);
  assert.equal(observed,rendered==='Lightning Shield > Stormstrike');checks++;
 }
 // Test the actual geometry assertions against controlled DOM measurements.
 // This validates the harness, not real browser rendering (that is the CI gate).
 const layoutHelper=browser.slice(browser.indexOf('async function assertReadableLayout('),browser.indexOf('async function screenshot('));
 for(const view of ['results','encounter'])for(const mode of ['fits','document-overflow','row-overflow','clipped-height']){
  const row={textContent:'Complete provenance',clientWidth:300,scrollWidth:mode==='row-overflow'?600:300,clientHeight:40,scrollHeight:mode==='clipped-height'?80:40,getClientRects:()=>[{}],getBoundingClientRect:()=>({left:20,right:320})};
  const page={document:{documentElement:{clientWidth:390,scrollWidth:mode==='document-overflow'?650:390},querySelectorAll:()=>[row]}};
  const test={assert,click:async()=>{},gateReceipts:[],evaluate:async expression=>vm.runInNewContext(expression,page)};
  vm.createContext(test);vm.runInContext(layoutHelper,test);
  if(mode==='fits')await test.assertReadableLayout(view,mode);else await assert.rejects(()=>test.assertReadableLayout(view,mode));checks++;
 }
 console.log(`Enhancement actual browser rejection gates passed: ${checks} checks, all 11 local fixtures and correlated server-rank failure; status/file freshness, successful-import exclusion and exact production validator errors.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
