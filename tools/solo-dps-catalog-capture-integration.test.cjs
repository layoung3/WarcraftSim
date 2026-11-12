'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const {captureCatalogRegions}=require('./solo-dps-catalog-capture.cjs');
const core=fs.readFileSync('tools/solo-dps-browser-interactions.cjs','utf8');
const catalog=fs.readFileSync('tools/solo-dps-catalog-browser.cjs','utf8');
const a=core.indexOf('async function evalJs('),b=core.indexOf('async function poll(',a);
assert.ok(a>=0&&b>a);
const start=catalog.indexOf(' await captureCatalogRegions('),end=catalog.indexOf('\n',start);
assert.ok(start>=0&&end>start);
const actualBoundary=catalog.slice(start,end);
async function exercise(boundary){
 const receipt=[],saved=[],evaluations=[],commands=[];
 let scrollY=0,width=1440,height=1000,hovered=null;
 const keys=['elixir-of-the-mongoose','elixir-of-the-grizzly','elixir-of-the-owl'];
 const text='Independent: agility: 25\nShared elixir critical chance (counted once across selected elixirs): attack-critical-chance-percent: 2 · spell-critical-chance-percent: 2';
 const body={},header={getBoundingClientRect:()=>({bottom:74})};
 const elements=keys.map((key,i)=>({id:'',dataset:{modifierKey:key,tooltip:text},textContent:'',parentElement:body,
  getBoundingClientRect:()=>({left:20+i*50,right:64+i*50,top:300-scrollY,bottom:344-scrollY,width:44,height:44}),
  getClientRects(){return [this.getBoundingClientRect()];},closest:()=>null,querySelectorAll:()=>[],contains:()=>false,
  getAttribute:name=>name==='aria-pressed'?'true':key,matches(){return hovered===this;}}));
 const select=selector=>selector==='.topbar'?header:selector==='dialog[open]'?null:elements.find(e=>selector.includes(e.dataset.modifierKey));
 const at=(x,y)=>elements.find(e=>{const r=e.getBoundingClientRect();return x>=r.left&&x<=r.right&&y>=r.top&&y<=r.bottom;});
 const style={display:'block',visibility:'visible',content:JSON.stringify(text),opacity:'1',boxSizing:'content-box',width:'250px',height:'180px',paddingLeft:'11px',paddingRight:'11px',paddingTop:'9px',paddingBottom:'9px',borderLeftWidth:'1px',borderRightWidth:'1px',borderTopWidth:'1px',borderBottomWidth:'1px',left:'0px',top:'50px'};
 const page={document:{body,querySelector:select,querySelectorAll:selector=>selector.split(',').map(select),elementFromPoint:at},
  requestAnimationFrame:fn=>Promise.resolve().then(fn),getComputedStyle:(_e,p)=>p?style:{borderLeftWidth:'1px',borderTopWidth:'1px'}};
 Object.defineProperties(page,{innerWidth:{get:()=>width},innerHeight:{get:()=>height}});
 page.window={get scrollY(){return scrollY;},scrollX:0,get innerWidth(){return width;},get innerHeight(){return height;},scrollTo:o=>{scrollY=Math.max(0,o.top);},scrollBy:o=>{scrollY=Math.max(0,scrollY+o.top);}};
 vm.createContext(page);
 async function command(method,params){
  commands.push(method);
  if(method==='Runtime.evaluate'){
   const value=vm.runInContext(params.expression,page),promise=!!value&&typeof value.then==='function';
   evaluations.push({awaitPromise:params.awaitPromise,promise,expression:params.expression});
   // CDP returns a remote Promise without result.value unless awaitPromise is true.
   if(promise&&!params.awaitPromise){value.catch(()=>{});return {result:{type:'object',subtype:'promise'}};}
   const resolved=promise?await value:value;return {result:{value:resolved===undefined?undefined:JSON.parse(JSON.stringify(resolved))}};
  }
  if(method==='Page.captureScreenshot')return {data:Buffer.from('test-only-not-browser-pixels').toString('base64')};
  if(method==='Input.dispatchMouseEvent'){assert.equal(params.type,'mouseMoved');hovered=at(params.x,params.y);return {};}
  if(method==='Emulation.setDeviceMetricsOverride'){width=params.width;height=params.height;return {};}
  if(method==='Emulation.clearDeviceMetricsOverride'){width=1440;height=1000;return {};}
  throw Error('Unexpected command '+method);
 }
 const caller={command,captureCatalogRegions,captureReceipts:receipt,screenshotDir:'/test-only',path,Buffer,fs:{writeFileSync:(name,data)=>saved.push({name,bytes:data.length})}};
 vm.createContext(caller);vm.runInContext(core.slice(a,b),caller);
 await vm.runInContext('(async()=>{'+boundary+'})()',caller);
 assert.ok(evaluations.filter(e=>e.promise).length>=10,'Actual region, position and RAF paths execute');
 assert.ok(evaluations.every(e=>e.awaitPromise===true),'All helper evaluations use explicit await adapter, including future async expressions');
 assert.equal(receipt.filter(r=>r.gate==='pixel-region-coverage').length,3);
 assert.equal(receipt.filter(r=>r.gate==='native-hover-tooltip').length,2);
 assert.equal(saved.length,5);assert.equal(width,1440);assert.equal(height,1000);
 assert.ok(commands.includes('Emulation.clearDeviceMetricsOverride'));
 return {evaluations:evaluations.length,async:evaluations.filter(e=>e.promise).length};
}
(async()=>{
 const legacy=actualBoundary.replace('evaluate:expression=>evalJs(expression,true)','evaluate:evalJs');
 await assert.rejects(()=>exercise(legacy),/positive measured geometry|undefined/,'Original unawaited boundary must fail real region helper before pixels');
 const result=await exercise(actualBoundary);
 console.log('Catalog actual caller/core-evalJs/capture integration passed: '+result.async+' async evaluations, '+result.evaluations+' total; legacy boundary rejected. VM transport proof only, not browser pixels.');
})().catch(error=>{console.error(error);process.exitCode=1;});
