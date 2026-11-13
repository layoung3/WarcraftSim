'use strict';
// Execute the actual browser click helper against an explicit DOM/compositor
// boundary. This verifies input synchronization, not real-browser acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const suites=['elemental','balance','shadow','enhancement'];
function environment({obscured=false,disabled=false,moving=false,miss=false,reposition=false,preparationClick=false,wrongButton=false,wrongPoint=false,untrusted=false,descendant=false,fractional=false,rerenderOnContext=false,missAfterFirst=false,visualOffset=null,movingViewport=false,settlingViewport=false,shiftDuringInput=false,pageScale=1,devicePixelRatio=1}={}){
 let frame=0,compositorReady=false,activations=0,repositioned=false,viewportShift=0;
 const commands=[],listeners=new Map(),window={scrollX:0,scrollY:0,scrollTo(){this.scrollX=0;this.scrollY=0;}};
 window.devicePixelRatio=devicePixelRatio;
 if(visualOffset)window.visualViewport={
  get offsetLeft(){return visualOffset.x;},
  get offsetTop(){return visualOffset.y+viewportShift+(movingViewport?frame:settlingViewport?Math.min(frame,5):0);},
  get pageLeft(){return window.scrollX+this.offsetLeft;},get pageTop(){return window.scrollY+this.offsetTop;},
  width:390,height:844,scale:pageScale};
 const other={id:'unrelated-heading',tagName:'H1'};
 const element={id:'exportBuild',tagName:'BUTTON',disabled,isConnected:true,dataset:{},
  scrollIntoView({block}){window.scrollY=250;compositorReady=false;if(block==='nearest')repositioned=true;},
  getBoundingClientRect:()=>({x:visualOffset?17.84375:fractional?10.25:10,y:visualOffset?414.828125:moving?200+frame:200,width:120,height:36}),
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
  if(shiftDuringInput&&commands.length===1)viewportShift=5;
  const target=compositorReady&&element.isConnected&&!miss&&!(missAfterFirst&&commands.length>2)?(descendant?child:element):other;
  // Independent Chromium boundary: CDP applies page scale, Blink undoes it and
  // adds the floored visual origin, then ordinary MouseEvent getters floor.
  const widget={x:params.x*pageScale,y:params.y*pageScale},v=window.visualViewport;
  const fire=type=>listeners.get(type)?.({type,target,clientX:Math.floor(widget.x/pageScale+Math.floor(v?.offsetLeft??0))+(wrongPoint?10:0),clientY:Math.floor(widget.y/pageScale+Math.floor(v?.offsetTop??0)),button:wrongButton?(params.button==='right'?0:2):params.button==='right'?2:0,isTrusted:!untrusted});
  if(params.type==='mousePressed'){
   fire('mousedown');if(params.button==='right'){fire('contextmenu');if(element.contains(target))activations++;if(rerenderOnContext)element.isConnected=false;}
  }else if(params.type==='mouseReleased'){
   fire('mouseup');if(params.button!=='right'){fire('click');if(element.contains(target))activations++;}
  }
 }
 return {evaluate,command,commands,frame:()=>frame,activations:()=>activations,trace:()=>window.__soloDpsClickCurrent};
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
 // Retained native failure: client center (77.84375,432.828125), with an
 // independently modeled fractional visual offset. The old helper dispatches
 // y432.828125 and receives y443; this helper dispatches y421.5 and receives432.
 for(const offset of [{x:0,y:11.328125},{x:4.875,y:11.875},{x:4.125,y:11.125}])for(const pageScale of [1,2]){
  const e=environment({visualOffset:offset,pageScale});await createBrowserClick(e)('#exportBuild');
  assert.equal(e.commands.length,2);assert.equal(e.activations(),1);
  assert.equal(e.commands[0].x,77.5-Math.floor(offset.x));assert.equal(e.commands[0].y,432.5-Math.floor(offset.y));
  assert.equal(e.trace().point.x,77.84375);assert.equal(e.trace().point.y,432.828125);
  assert.equal(e.trace().point.viewport.scale,pageScale);
  assert.ok(e.trace().events.every(x=>x.x===77&&x.y===432&&x.viewport.offsetTop===offset.y));
 }
 const settling=environment({visualOffset:{x:0,y:11.328125},settlingViewport:true});
 await createBrowserClick(settling)('#exportBuild');assert.ok(settling.frame()>=7,'Visual-only movement must settle before input');assert.equal(settling.commands.length,2);
 for(const options of [{visualOffset:{x:0,y:11.328125},movingViewport:true},{visualOffset:{x:0,y:900}},{devicePixelRatio:2}]){
  const e=environment(options);await assert.rejects(()=>createBrowserClick(e)('#exportBuild'),/settle|obscured|unit device scale/);assert.equal(e.commands.length,0);
 }
 for(const options of [{visualOffset:{x:0,y:11.328125},wrongPoint:true},{visualOffset:{x:0,y:11.328125},shiftDuringInput:true}]){
  const e=environment(options);await assert.rejects(()=>createBrowserClick(e)('#exportBuild'),/Trusted click did not reach/);assert.equal(e.commands.length,2,'Viewport drift or wrong coordinates do not trigger a retry');
 }
 for(const rerenderOnContext of [false,true]){
  const right=environment({rerenderOnContext});await createBrowserClick(right)('#exportBuild','right');assert.equal(right.commands.length,2);assert.equal(right.activations(),1);
 }
 for(const options of [{miss:true},{wrongButton:true},{wrongPoint:true}]){
  const right=environment(options);await assert.rejects(()=>createBrowserClick(right)('#exportBuild','right'),/Trusted click did not reach/);assert.equal(right.commands.length,2);
 }
 const stale=environment({missAfterFirst:true}),click=createBrowserClick(stale);await click('#exportBuild');
 await assert.rejects(()=>click('#exportBuild'),/Trusted click did not reach/);assert.equal(stale.commands.length,4);assert.equal(stale.activations(),1,'An earlier trusted click cannot satisfy a newer missed input');
 console.log('Four browser suites share stable rendered input, exact trusted-click receipts and fail-closed single-dispatch checks; 12 visual-viewport, scale, drift and coordinate cases passed.');
 await require('./solo-dps-browser-navigation.test.cjs').runNavigationChecks();
 await require('./solo-dps-elemental-export.test.cjs').runExportChecks();
})().catch(error=>{console.error(error);process.exitCode=1;});
