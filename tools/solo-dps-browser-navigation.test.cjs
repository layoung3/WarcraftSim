'use strict';
// Execute the actual preset/queue/preview and core navigation code with an
// explicit late-sidebar geometry boundary. This is not Chromium acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {environment,part}=require('./solo-dps-loading-contract.test.cjs');
const {createBrowserClick}=require('./solo-dps-browser-click.cjs');
const browser=fs.readFileSync('tools/solo-dps-browser-interactions.cjs','utf8');
const core=browser.slice(browser.indexOf('async function poll('),browser.indexOf('async function captureReport('));
const sequence=browser.slice(browser.indexOf(' // All nine loaded presets'),browser.indexOf(' const selected=await evalJs(',browser.indexOf(' // All nine loaded presets')));
const readiness=' await poll("document.querySelector(\'#statStatus\').textContent===\'Up to date\'");\n';
assert.ok(core.includes('const trustedClick=createBrowserClick('));
assert.ok(sequence.includes(readiness),'Actual Mage transition must wait for its pending sidebar preview');

function navigationEnvironment({failPreview=false,holdPreview=false,miss=false,suppressNavigation=false,rerenderOnContext=false}={}){
 const fixture=environment(),{c,elements}=fixture;
 let now=0,nextTimer=0,shift=0,scroll=0,loading=null,activeView='view-results';
 const timers=new Map(),pendingTimers=new Set(),commands=[],evaluations=[],listeners=new Map(),stages=[];
 // Fixed geometry is intentional: only a real production sidebar replacement
 // moves this modeled stacked-layout target. The native hit test uses its
 // current position, not the helper's previously sampled coordinates.
 const unrelated={id:'',tagName:'DIV'},tab={id:'tab-talents',tagName:'BUTTON',isConnected:true,disabled:false,dataset:{view:'talents'},
  selected:false,scrollIntoView(){scroll=shift;window.scrollY=250+scroll;},
  getBoundingClientRect(){return {x:124,y:228+shift-scroll,width:90,height:36};},
  contains(target){return target===this;},getAttribute(name){return name==='aria-selected'?String(this.selected):null;}};
 const panels={'#view-talents':{id:'view-talents',hidden:true},'#view-results':{id:'view-results',hidden:false}};
 const window={scrollX:0,scrollY:0,scrollTo(){scroll=0;this.scrollY=0;}};
 const oldQuery=c.document.querySelector;
 c.document.querySelector=selector=>selector==='[data-view=talents]'?tab:panels[selector]||
  (selector==='.view:not([hidden])'?panels['#'+activeView]:selector.startsWith('#')?elements[selector.slice(1)]:oldQuery(selector));
 c.document.elementFromPoint=(x,y)=>{const r=tab.getBoundingClientRect();return x>=r.x&&x<r.x+r.width&&y>=r.y&&y<r.y+r.height?tab:unrelated;};
 c.document.addEventListener=(type,listener)=>{if(!listeners.has(type))listeners.set(type,[]);listeners.get(type).push(listener);};
 c.window=window;c.Event=function(type){this.type=type;};
 c.requestAnimationFrame=callback=>{now+=16;callback(now);};
 c.setTimeout=(callback,delay)=>{const id=++nextTimer;timers.set(id,{callback,at:now+delay});return id;};
 c.clearTimeout=id=>timers.delete(id);
 // Replace the loading test's intentionally empty queue stub with production
 // scheduling, actual HTTP preview ownership checks and sidebar rendering.
 vm.runInContext(part('function buildCharacter(){','function body(){')+part('function queueStatRefresh(){','async function simulate(){'),c);
 const originalReplace=elements.stats.replaceChildren;
 elements.stats.replaceChildren=function(...children){originalReplace.apply(this,children);shift=120;stages.push({type:'sidebar-render',at:now});};
 elements.stats.children=[{textContent:'Previous Hunter stats'}];
 elements.classKey.value='hunter';elements.spec.value='beast-mastery';elements.race.value='orc';elements.statStatus.textContent='Up to date';
 c.loadedClass='hunter';c.loadedSpec='beast-mastery';
 const endpoint='/api/forever/character/generate-level60';
 fixture.responses.set(endpoint,{effectiveStats:{values:{health:4111,mana:3555,intellect:211}},sourceStatus:'historical-provisional',contributions:[]});
 if(failPreview)fixture.failures.set(endpoint,'Controlled preview failure');
 const held=holdPreview?fixture.hold(endpoint):null;
 elements.classKey.dispatchEvent=event=>{assert.equal(event.type,'change');loading=c.initClass();};

 async function runDue(){
  for(const [id,timer]of [...timers])if(timer.at<=now){
   timers.delete(id);const work=Promise.resolve(timer.callback());pendingTimers.add(work);work.finally(()=>pendingTimers.delete(work));
   if(!holdPreview)await work;
  }
 }
 async function evalJs(expression,awaitPromise=false){
  evaluations.push({expression,awaitPromise});
  const value=await vm.runInContext(expression,c);
  // Model the preset finishing within this CDP round trip. Its independently
  // queued 180ms preview still has not run when the ready status is read.
  if(expression.includes("x.dispatchEvent(new Event('change'"))await loading;
  return value===undefined?undefined:JSON.parse(JSON.stringify(value));
 }
 async function sleep(milliseconds){now+=milliseconds;await runDue();}
 async function command(method,params){
  assert.equal(method,'Input.dispatchMouseEvent');
  if(params.type==='mousePressed'&&timers.size){
   // The pending response lands after three stable rAF samples but before
   // native input. This was never excluded by the old readiness predicate.
   now=Math.max(now,...[...timers.values()].map(timer=>timer.at));await runDue();
  }
  commands.push({method,...params});
  const target=miss||!tab.isConnected?unrelated:c.document.elementFromPoint(params.x,params.y);
  const fire=type=>{
   for(const listener of listeners.get(type)||[])listener({type,target,isTrusted:true,clientX:params.x,clientY:params.y,button:params.button==='right'?2:0});
   if(type==='click'&&target===tab&&!suppressNavigation){tab.selected=true;panels['#view-talents'].hidden=false;panels['#view-results'].hidden=true;activeView='view-talents';}
  };
  if(params.type==='mousePressed'){
   fire('mousedown');if(params.button==='right'){fire('contextmenu');if(rerenderOnContext)tab.isConnected=false;}
  }else{fire('mouseup');if(params.button!=='right')fire('click');}
 }
 const harness=vm.createContext({assert,createBrowserClick,evalJs,command,sleep,Date:{now:()=>now}});
 vm.runInContext(core,harness);
 return {...fixture,commands,evaluations,stages,window,tab,endpoint,
  run:source=>vm.runInContext('(async()=>{'+source+'})()',harness),
  click:(selector,button)=>harness.click(selector,button),
  cleanup:async()=>{if(held){held.release();await Promise.all(pendingTimers);}},
  state:()=>({now,activeView,shift,statStatus:elements.statStatus.textContent,geometry:tab.getBoundingClientRect()})};
}

async function runNavigationChecks(){
 let checks=0;
 const oldSequence=sequence.replace(readiness,'');assert.notEqual(oldSequence,sequence);
 const old=navigationEnvironment();
 await assert.rejects(()=>old.run(oldSequence),/Trusted click did not reach/);
 assert.equal(old.commands.length,2,'Missing readiness produces one miss, never a replay');
 assert.equal(old.state().activeView,'view-results');assert.equal(old.state().statStatus,'Up to date');
 assert.equal(old.window.__soloDpsClickCurrent.point.y,246);
 assert.equal(old.window.__cp175InputGeometry[0].expectedBounds.y,348,'Sidebar moved the target after a valid point sample');
 assert.ok(old.window.__soloDpsClickCurrent.events.every(event=>event.target==='DIV'&&event.trusted&&!event.matched));checks++;

 const current=navigationEnvironment();await current.run(sequence);
 assert.equal(current.state().activeView,'view-talents');assert.equal(current.commands.length,2);
 assert.equal(current.stages.filter(stage=>stage.type==='sidebar-render').length,1,'Actual preview renderer ran');
 assert.deepEqual(current.elements.stats.children.map(row=>row.children[1].textContent),['4111','3555','211']);
 const submitted=current.calls.find(call=>call.url===current.endpoint)?.data;
 assert.equal(submitted.character.classKey,'mage');assert.equal(submitted.character.specializationKey,'arcane');assert.equal(submitted.character.race,'gnome');
 assert.equal(submitted.character.talents.selections.reduce((sum,talent)=>sum+talent.rank,0),51);
 assert.ok(current.window.__soloDpsClickCurrent.events.every(event=>event.trusted&&event.matched));
 assert.ok(current.window.__cp175InputGeometry.every(event=>event.statStatus==='Up to date'&&event.expectedBounds.y===228));
 assert.ok(current.evaluations.filter(e=>e.expression.includes('async()=>')).every(e=>e.awaitPromise),'Core adapts async shared helper to awaitPromise=true');checks++;

 for(const options of [{failPreview:true},{holdPreview:true}]){
  const e=navigationEnvironment(options);
  try{await assert.rejects(()=>e.run(sequence),/Timed out waiting:.*Up to date/);assert.equal(e.commands.length,0,'Unfinished/failed required preview must fail before input');assert.equal(e.state().activeView,'view-results');}
  finally{await e.cleanup();}checks++;
 }
 const missed=navigationEnvironment({miss:true});await assert.rejects(()=>missed.run(sequence),/Trusted click did not reach.*input geometry=/);assert.equal(missed.commands.length,2);checks++;
 const unmoved=navigationEnvironment({suppressNavigation:true});await assert.rejects(()=>unmoved.run(sequence),/Timed out waiting:.*aria-selected.*input geometry=/);assert.equal(unmoved.commands.length,2,'Trusted receipt never replaces actual navigation postcondition');checks++;
 // Completion is required only by this scenario, not by shared/core clicking.
 const unavailable=navigationEnvironment();unavailable.elements.statStatus.textContent='Select Historical baseline to preview provisional stats';await unavailable.click('[data-view=talents]');assert.equal(unavailable.commands.length,2);assert.equal(unavailable.state().activeView,'view-talents');checks++;
 await unavailable.click('[data-view=talents]');assert.equal(unavailable.commands.length,4);assert.equal(unavailable.window.__soloDpsClickCurrent.sequence,2);assert.equal(unavailable.window.__cp175InputGeometry.length,3,'Supplemental diagnostics contain only the latest dispatch');checks++;
 for(const rerenderOnContext of [false,true]){
  const right=navigationEnvironment({rerenderOnContext});await right.click('[data-view=talents]','right');assert.equal(right.commands.length,2);assert.equal(right.state().activeView,'view-results','Right click does not require left-navigation postcondition');assert.equal(right.window.__soloDpsClickCurrent.events.length,3);checks++;
 }
 console.log('Core navigation readiness: '+checks+' offline causal checks; actual preset/180ms queue/preview/core adapter, one dispatch, strict receipts and navigation postconditions. Chromium NOT RUN.');
 return checks;
}
if(require.main===module)runNavigationChecks().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={runNavigationChecks};
