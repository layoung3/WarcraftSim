'use strict';
// Bounded VM exercising the production Feral serializer/projection. Values in
// tests are illustrative software assumptions, never a player's observation.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8'),plain=x=>JSON.parse(JSON.stringify(x));
const part=(from,to)=>{const a=source.indexOf(from),b=source.indexOf(to,a);assert.ok(a>=0&&b>a,from);return source.slice(a,b);};
const keys=['ferocity','heart-of-the-wild','feral-swiftness','shredding-attacks','savage-fury','sharpened-claws','shifting-power','predatory-strikes','blood-frenzy','improved-shifting-power','leader-of-the-pack','natural-instinct','berserk','furor'];
const ranks=Object.fromEntries(keys.map((key,i)=>[key,[5,5,2,3,2,2,1,3,2,2,1,2,1,5][i]]));
function environment(){
 const elements=new Proxy({},{get:(o,id)=>o[id]??(o[id]={id,value:'',checked:false,disabled:false,dataset:{},textContent:'',replaceChildren(){this.children=[];},append(...items){(this.children??=[]).push(...items);}})});
 Object.assign(elements.classKey,{value:'druid'});elements.spec.value='feral';elements.race.value='night-elf';elements.provisional.checked=true;elements.iterations.value='1';
 const c={$:id=>elements[id],clone:plain,pretty:x=>JSON.stringify(x,null,2),human:x=>x,itemizedMode:true,
 cfg:{equipment:{items:[]},effects:{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'}},priorityRows:[],soloScript:{enabled:false,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[]},
 loadedClass:'druid',loadedSpec:'feral',request:null,document:{createElement:()=>({textContent:'',append(...x){this.children=x;}})},generation:1,statsRefreshId:0,statsRefreshTimer:null,buildPreparationPending:false,clearTimeout,setTimeout,setBuildPreparing:()=>{},
 talentCatalog:keys.map(key=>({key,maxRank:ranks[key],tree:key==='furor'?'restoration':'feral',modeled:true,historicalValidationSourceStatus:'historical-provisional-not-current-client-certified',historicalValidationSourceCommit:'1cc5ae1e9abdbf941b7326d2d90421822dce31ba',sourceUrl:'https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json',simulationStatus:['furor','feral-swiftness'].includes(key)?'inactive-in-fixed-cat':'fixed-cat-partial',dataConfidence:'historical-provisional',knownLimitations:'Historical provisional, partial effects.'}))};
 vm.createContext(c);vm.runInContext(part('const specMap=','const slots=')+part('// Strict historical/manual fixed-Cat contract.','const rogueNumericKeys=')+part('function body(){','// Strict historical/manual fixed-Cat contract.')+part('const beforeElementalSave=saveConfig;','const beforeElementalPreset=').replace('const beforeElementalSave=saveConfig;','const beforeElementalSave=()=>{throw Error("Unexpected legacy serializer");};'),c);
 c.request=c.feralDefaults();c.request.manualInputs.preTalentCatAttackPower=800;c.request.talents={ranks:plain(ranks),balancePointsSpent:0,feralPointsSpent:31,restorationPointsSpent:5,totalPoints:36,unspentPoints:15};
 c.restoreFeralControls(c.request);elements.seed.value='144001';elements.duration.value='10';elements.targetHealth.value='1000000';elements.timeline.checked=true;
 return {c,elements};
}
function build(){return plain(environment().c.saveConfig());}
function project(response,saved=build(),options={}){
 const {c}=environment();const b={classKey:'druid',specialization:'feral',itemizedTalentMode:true,itemizedTalentRanks:plain(saved.talents.ranks),feral:plain(c.feralRequest(saved))};
 Object.assign(c,{b,data:response.sim,response,metadata:options.metadata||{ruleset:'wow-forever',simulatorVersion:'synthetic-fixture',sourceCommit:'synthetic-test-source',runtimeTargetBuild:'70707',historicalTalentReferenceCommit:'1cc5ae1e9abdbf941b7326d2d90421822dce31ba',notes:['Illustrative software fixture; not a live observation.']},build:plain(saved),runItemizedMode:true,runTalents:plain(saved.talents),runCatalog:options.catalog||c.talentCatalog,batch:null,gearValidation:{normalizedLoadout:{items:[]}},resolved:response.preparation});
 const expression=part(' results={',' const sim=response.sim||{},summary=sim.run?.summary||{};').trim().replace(/^results=/,'').replace(/;$/,'');
 return plain(vm.runInContext('('+expression+')',c));
}
module.exports={source,part,plain,environment,build,project,ranks};
