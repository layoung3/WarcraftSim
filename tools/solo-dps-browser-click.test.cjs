'use strict';
// Execute the actual browser click helper against an explicit DOM/compositor
// boundary. This verifies input synchronization, not real-browser acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const suites=['elemental','balance','shadow','enhancement'];
function environment({obscured=false,disabled=false,moving=false,miss=false,reposition=false,preparationClick=false,wrongButton=false,wrongPoint=false,untrusted=false,descendant=false,fractional=false,rerenderOnContext=false,missAfterFirst=false}={}){
 let frame=0,compositorReady=false,activations=0,repositioned=false;
 const commands=[],listeners=new Map(),window={scrollX:0,scrollY:0,scrollTo(){this.scrollX=0;this.scrollY=0;}};
 const other={id:'unrelated-heading',tagName:'H1'};
 const element={id:'exportBuild',tagName:'BUTTON',disabled,isConnected:true,dataset:{},
  scrollIntoView({block}){window.scrollY=250;compositorReady=false;if(block==='nearest')repositioned=true;},
  getBoundingClientRect:()=>({x:fractional?10.25:10,y:moving?200+frame:200,width:120,height:36}),
  contains:target=>target===element||target===child};
 const child={id:'export-icon',tagName:'SPAN'};
 const document={querySelector:selector=>selector==='#exportBuild'?element:null,
  elementFromPoint:()=>obscured||reposition&&!repositioned?other:descendant?child:element,
  addEventListener(type,listener){listeners.set(type,listener);},
  removeEventListener(type){listeners.delete(type);}};
 const context=vm.createContext({document,window,
  requestAnimationFrame:callback=>{frame++;compositorReady=true;if(preparationClick)for(const type of ['mousedown','mouseup','click'])listeners.get(type)?.({type,target:element,clientX:70,clientY:218,button:0,isTrusted:true});callback(frame*16);}});
 async function evaluate(code){return JSON.parse(JSON.stringify(await vm.runInContext(code,context)));}
 async function command(method,params){
  commands.push({method,...params});assert.equal(method,'Input.dispatchMouseEvent');
  const target=compositorReady&&element.isConnected&&!miss&&!(missAfterFirst&&commands.length>2)?(descendant?child:element):other;
  const fire=type=>listeners.get(type)?.({type,target,clientX:Math.floor(params.x)+(wrongPoint?10:0),clientY:params.y,button:wrongButton?(params.button==='right'?0:2):params.button==='right'?2:0,isTrusted:!untrusted});
  if(params.type==='mousePressed'){
   fire('mousedown');if(params.button==='right'){fire('contextmenu');if(element.contains(target))activations++;if(rerenderOnContext)element.isConnected=false;}
  }else if(params.type==='mouseReleased'){
   fire('mouseup');if(params.button!=='right'){fire('click');if(element.contains(target))activations++;}
  }
 }
 return {evaluate,command,commands,frame:()=>frame,activations:()=>activations};
}
function suiteClick(suite,e){
 const source=fs.readFileSync('tools/solo-dps-'+suite+'-browser.test.cjs','utf8');
 if(source.includes('const click=createBrowserClick({evaluate,command});')){
  return require('./solo-dps-browser-click.cjs').createBrowserClick(e);
 }
 // Before the correction, run the suites' actual immediate-coordinate helper
 // to demonstrate the missing post-scroll rendered-frame boundary.
 const start=source.indexOf('async function click('),end=source.indexOf('async function setInput(',start);
 assert.ok(start>=0&&end>start);
 return vm.runInNewContext(source.slice(start,end)+'\nclick;',{...e,assert});
}
(async()=>{
 for(const suite of suites){
  const e=environment();await suiteClick(suite,e)('#exportBuild');
  assert.equal(e.activations(),1,suite+': native export click must reach the post-scroll rendered target');
  assert.equal(e.commands.length,2,'Exactly one press/release, no replay');assert.ok(e.frame()>=3,'Consecutive rendered geometry samples before input');
 }
 const {createBrowserClick}=require('./solo-dps-browser-click.cjs');
 for(const problem of ['obscured','disabled','moving']){
  const e=environment({[problem]:true});
  await assert.rejects(()=>createBrowserClick(e)('#exportBuild'),/obscured|disabled|settle/);
  assert.equal(e.commands.length,0,problem+' must fail before native input');
 }
 for(const options of [{miss:true},{miss:true,preparationClick:true},{wrongButton:true},{wrongPoint:true},{untrusted:true}]){
  const e=environment(options);
  await assert.rejects(()=>createBrowserClick(e)('#exportBuild'),/Trusted click did not reach/);
  assert.equal(e.commands.length,2,'Uncorrelated input is reported, never retried');
  if(options.miss)assert.equal(e.activations(),0);
 }
 for(const options of [{descendant:true},{fractional:true},{reposition:true}]){
  const e=environment(options);await createBrowserClick(e)('#exportBuild');assert.equal(e.activations(),1);assert.equal(e.commands.length,2);
  if(options.reposition)assert.ok(e.frame()>=6,'Reposition is re-settled before the single click');
 }
 for(const rerenderOnContext of [false,true]){
  const right=environment({rerenderOnContext});await createBrowserClick(right)('#exportBuild','right');assert.equal(right.commands.length,2);assert.equal(right.activations(),1);
 }
 for(const options of [{miss:true},{wrongButton:true},{wrongPoint:true}]){
  const right=environment(options);await assert.rejects(()=>createBrowserClick(right)('#exportBuild','right'),/Trusted click did not reach/);assert.equal(right.commands.length,2);
 }
 const stale=environment({missAfterFirst:true}),click=createBrowserClick(stale);await click('#exportBuild');
 await assert.rejects(()=>click('#exportBuild'),/Trusted click did not reach/);assert.equal(stale.commands.length,4);assert.equal(stale.activations(),1,'An earlier trusted click cannot satisfy a newer missed input');
 console.log('Four browser suites share stable rendered input, exact trusted-click receipts and fail-closed single-dispatch checks.');
 await require('./solo-dps-browser-navigation.test.cjs').runNavigationChecks();
})().catch(error=>{console.error(error);process.exitCode=1;});
