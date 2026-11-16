'use strict';
// Actual production rank validation and actual native-harness readiness, with
// controlled HTTP completion. This model does not launch an API or Chromium.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
const browser=fs.readFileSync('tools/solo-dps-rogue-browser.test.cjs','utf8');
const plain=value=>JSON.parse(JSON.stringify(value));
function part(text,from,to){const a=text.indexOf(from),b=text.indexOf(to,a);assert.ok(a>=0&&b>a,from);return text.slice(a,b);}
const preset=JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json','utf8'));
const talents=()=>({assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0,ranks:plain(preset.ranks)});
function expected(points){const value=talents();value.assassinationPointsSpent=points;if(points===36)delete value.ranks['improved-expose-armor'];else value.ranks['improved-expose-armor']=points-36;return value;}

function fixture(){
 const controls={classKey:{value:'rogue'},spec:{value:'assassination'},race:{value:'dwarf'},status:{dataset:{buildState:'ready'},textContent:'Build imported.'},run:{disabled:false},exportBuild:{disabled:false},statStatus:{textContent:'Up to date'}};
 const setting={id:'rogueTargetArmor',tagName:'INPUT',type:'number',value:'2100'};
 const statRows=[['Maximum Energy','110'],['Maximum player Health','2,303'],['Starting Energy','27.5']].map(([label,value])=>({querySelector:selector=>({textContent:selector==='span'?label:value})}));
 let rendered=talents(),nodes=[],totals=[];
 function paint(value){
  rendered=plain(value);
  nodes=Object.keys(preset.ranks).map(key=>({dataset:{talentKey:key},querySelector:selector=>selector==='.talent-rank'?{textContent:(rendered.ranks[key]||0)+'/'+(key==='improved-expose-armor'?2:5)}:null,focus:()=>{}}));
  totals=['assassination','combat','subtlety'].map(tree=>({dataset:{tree},querySelector:selector=>selector==='output'?{textContent:String(rendered[tree+'PointsSpent'])}:null}));
 }
 const window={__latestRoguePreview:361,__rogueHttp:[]};
 function preview(value,sequence){window.__latestRoguePreview=sequence;window.__rogueHttp.push({sequence,url:'/api/forever/character/preview-rogue',status:200,request:{character:{race:'dwarf'},targetArmor:2100,tuning:{},talents:plain(value)},response:{effectiveStats:{values:{energy:110,health:2303}},startingEnergy:27.5}});}
 const document={querySelector:selector=>controls[selector.slice(1)],querySelectorAll:selector=>selector==='[id^=rogue]'?[setting]:selector==='#stats > div'?statRows:selector==='.talent-node'||selector==='#talents .talent-node'?nodes:selector==='#talents .tree-points'?totals:[]};
 const posts=[],notes=[];
 const context={window,document,performance:{now:()=>1000},$:id=>controls[id],request:{talents:talents()},generation:1,itemizedMode:true,
  talentCatalog:Object.keys(preset.ranks).map(key=>({key,tree:'assassination',modeled:true})),clone:plain,isFeral:()=>false,isCombatRogue:()=>false,
  renderPriorityConditions:()=>{},note:value=>{controls.status.textContent=value;notes.push(value);},paint,
  post:(url,data)=>new Promise(resolve=>posts.push({url,data:plain(data),resolve})),buildDirty:false};
 vm.createContext(context);
 vm.runInContext(part(source,'const talentTrees=','function renderTalents(){')+
  'function renderTalents(){const talents=request.talents;'+part(source,' let talentEditPending=false;',' function eligibility(t){')+
  'paint(talents);lastRenderedRankHandler=setRank;}',context);
 context.renderTalents();preview(talents(),361);
 let clock=0,afterMatch=null;
 const harness={gateReceipts:[],structuredClone:plain,Date:{now:()=>clock},sleep:async milliseconds=>{assert.equal(milliseconds,100);clock+=40001;},
  evaluate:async expression=>{const value=plain(vm.runInContext(expression,context));if(value&&typeof value==='object'&&Object.hasOwn(value,'sequence')&&Object.hasOwn(value,'at'))afterMatch?.();return value;}};
 vm.createContext(harness);vm.runInContext(part(browser,'async function until(expression,timeout=40000){','const click=')+part(browser,'async function ready(after,fields={}', 'const sequence='),harness);
 const probe=async(after,wanted)=>{try{await harness.ready(after,{},wanted);return true;}catch(error){if(/^Timed out:/.test(error.message))return false;throw error;}};
 const release=async index=>{assert.ok(posts[index],'Expected production validation '+index);posts[index].resolve({valid:true});await Promise.resolve();await Promise.resolve();};
 return {context,harness,controls,window,posts,notes,preview,paint,probe,release,rendered:()=>plain(rendered),nodes:()=>nodes,totals:()=>totals,
  afterMatch:callback=>afterMatch=callback,remove:()=>context.lastRenderedRankHandler({key:'improved-expose-armor',maxRank:2},-1)};
}

async function run(){
 let checks=0;
 const f=fixture(),edit=f.remove();
 assert.equal(f.context.request.talents.ranks['improved-expose-armor'],1);
 assert.equal(f.rendered().ranks['improved-expose-armor'],2,'The first edit has not passed its actual validators');
 await f.remove();assert.equal(f.notes.at(-1),'Wait for the current talent edit to validate.');
 assert.equal(f.posts.length,1,'The unchanged production guard rejects another edit while validation is pending');
 const premature=await f.probe(359,expected(37));
 console.log('Controlled post-rollback boundary: prior preview361, pending rank2→1, second edit guarded; readiness accepted='+premature+'.');
 assert.equal(premature,false,'An unrelated late rank2 preview cannot complete the pending rank1 edit');checks++;
 // Even a matching request cannot qualify until the actual accepted talent
 // render has completed both server validators.
 f.preview(expected(37),362);assert.equal(await f.probe(359,expected(37)),false);checks++;
 await f.release(0);assert.equal(f.posts[1].url.endsWith('/validate'),true);
 assert.equal(await f.probe(359,expected(37)),false);checks++;
 await f.release(1);await edit;
 assert.deepEqual(f.rendered(),expected(37));
 f.preview(talents(),363);assert.equal(await f.probe(359,expected(37)),false,'Accepted DOM cannot use a stale same-stat preview');checks++;
 f.preview(expected(37),364);assert.equal(await f.probe(359,expected(37)),true);checks++;
 assert.deepEqual(plain(f.harness.gateReceipts.at(-1).expectedTalents),expected(37));
 assert.equal(f.harness.gateReceipts.at(-1).sequence,364);
 const second=f.remove();assert.equal(await f.probe(364,expected(36)),false);checks++;
 await f.release(2);await f.release(3);await second;
 assert.deepEqual(f.rendered(),expected(36));assert.equal(Object.hasOwn(f.context.request.talents.ranks,'improved-expose-armor'),false);
 f.preview(expected(36),367);assert.equal(await f.probe(364,expected(36)),true);checks++;
 assert.deepEqual(plain(f.harness.gateReceipts.at(-1).expectedTalents),expected(36));
 assert.deepEqual(f.posts.map(x=>[x.url.split('/').at(-1),x.data.assassinationPointsSpent,x.data.ranks['improved-expose-armor']??0]),[['validate-itemized',37,1],['validate',37,1],['validate-itemized',36,0],['validate',36,0]]);

 for(const [name,mutate]of [
  ['same-total-other-rank',x=>{const r=x.window.__rogueHttp.at(-1).request.talents.ranks;r.malice--;r['improved-expose-armor']=1;}],
  ['explicit-zero-instead-of-removal',x=>x.window.__rogueHttp.at(-1).request.talents.ranks['improved-expose-armor']=0],
  ['missing-tree-total',x=>delete x.window.__rogueHttp.at(-1).request.talents.subtletyPointsSpent],
  ['different-off-tree-total',x=>x.window.__rogueHttp.at(-1).request.talents.combatPointsSpent=1],
  ['old-rendered-ranks',x=>x.paint(expected(37))],
  ['wrong-rendered-total',x=>x.totals()[1].querySelector=()=>({textContent:'1'})],
  ['blank-rendered-zero-total',x=>x.totals()[1].querySelector=()=>({textContent:''})],
  ['missing-rendered-total-output',x=>x.totals()[1].querySelector=()=>null],
  ['missing-rendered-tree',x=>x.totals().pop()],
  ['duplicate-rendered-tree',x=>x.totals()[1]=x.totals()[0]],
  ['duplicate-rendered-talent',x=>x.nodes().push(x.nodes()[0])],
  ['failed-current-preview',x=>x.window.__rogueHttp.at(-1).status=400],
  ['wrong-current-race',x=>x.controls.race.value='human'],
  ['locked-export',x=>x.controls.exportBuild.disabled=true]
 ]){
  const x=fixture();x.paint(expected(36));x.preview(expected(36),367);mutate(x);
  assert.equal(await x.probe(364,expected(36)),false,name+' cannot produce a ready receipt');
  assert.equal(x.harness.gateReceipts.length,0);checks++;
 }
 const atomic=fixture();atomic.paint(expected(36));atomic.preview(expected(36),367);
 atomic.afterMatch(()=>{atomic.window.__latestRoguePreview=999;atomic.context.performance.now=()=>2000;});
 assert.equal(await atomic.probe(364,expected(36)),true);
 assert.equal(atomic.harness.gateReceipts[0].sequence,367,'Receipt belongs to the preview that actually satisfied the predicate');
 assert.equal(atomic.harness.gateReceipts[0].at,1000,'Completion time is captured in the same evaluation');checks++;
 const unchanged=fixture();assert.equal(await unchanged.probe(359,null),true,'Existing non-talent setting readiness remains available');checks++;
 console.log('Actual Rogue talent readiness passed: '+checks+' controlled checks; delayed production validators/pending guard, complete rank/totals and rendered-state correlation, zero-key removal and fresh preview. Chromium/API NOT RUN.');
 return checks;
}
if(require.main===module)run().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={run};
