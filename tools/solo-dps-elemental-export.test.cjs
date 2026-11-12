'use strict';
// Deterministic scheduling model, not Chromium acceptance. Execute production
// rejected-import cleanup, debounce, preview renderer and export handler.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-loading-contract.test.cjs');
const {createBrowserClick}=require('./solo-dps-browser-click.cjs');
const browser=fs.readFileSync('tools/solo-dps-elemental-browser.test.cjs','utf8');
const exportSource=browser.slice(browser.indexOf('const exportBuild='),browser.indexOf('async function run(name)'));
async function fixture({fail=false,hold=false,miss=false}={}){
 const e=environment(),{c,elements}=e;
 await c.loadPreset();c.buildDirty=true;
 const before=plain(c.saveConfig());
 let now=0,next=0,shift=0,frames=0;
 const timers=new Map(),listeners=new Map(),commands=[],stages=[];
 c.setTimeout=(callback,delay)=>{const id=++next;timers.set(id,{callback,at:now+delay});return id;};
 c.clearTimeout=id=>timers.delete(id);
 vm.runInContext(part('function buildCharacter(){','function body(){')+part('function queueStatRefresh(){','async function simulate(){'),c);
 const endpoint='/api/forever/character/generate-level60';
 e.responses.set(endpoint,{effectiveStats:{values:{intellect:175,health:3500,mana:4000}},contributions:[]});
 if(fail)e.failures.set(endpoint,'Controlled preview failure');
 const replace=elements.stats.replaceChildren;
 elements.stats.replaceChildren=function(...children){replace.apply(this,children);shift=-123;stages.push('render');};
 const target=elements.exportBuild,other={tagName:'ASIDE'};
 Object.assign(target,{tagName:'BUTTON',isConnected:true,scrollIntoView(){},getBoundingClientRect(){return {x:16,y:732+shift,width:124,height:35};},contains(node){return node===this;}});
 c.window={scrollX:0,scrollY:0,scrollTo(){}};
 const query=c.document.querySelector;
 c.document.querySelector=s=>s.startsWith('#')?elements[s.slice(1)]:query(s);
 c.document.elementFromPoint=(x,y)=>{const r=target.getBoundingClientRect();return x>=r.x&&x<r.x+r.width&&y>=r.y&&y<r.y+r.height?target:other;};
 c.document.addEventListener=(type,fn)=>listeners.set(type,fn);
 c.requestAnimationFrame=fn=>{frames++;now+=16;fn(now);};
 async function due(){if(hold)return;for(const [id,timer]of [...timers])if(timer.at<=now){timers.delete(id);await timer.callback();}}
 const evaluate=async code=>plain(await vm.runInContext(code,c));
 async function command(method,params){
  assert.equal(method,'Input.dispatchMouseEvent');
  // Deliver the pending production preview after stable geometry, before input.
  if(params.type==='mousePressed'&&timers.size){now=Math.max(now,...[...timers.values()].map(t=>t.at));await due();}
  commands.push(params);const hit=miss?other:c.document.elementFromPoint(params.x,params.y);
  async function fire(type){listeners.get(type)?.({type,target:hit,isTrusted:true,clientX:Math.floor(params.x),clientY:Math.floor(params.y),button:0});if(type==='click'&&hit===target)await target.listeners.click();}
  if(params.type==='mousePressed')await fire('mousedown');else{await fire('mouseup');await fire('click');}
 }
 const click=createBrowserClick({evaluate,command});
 async function until(expression){for(let i=0;i<20;i++){if(await evaluate(expression))return;now+=100;await due();}throw Error('Timed out: '+expression);}
 const h=vm.createContext({until,downloaded:async(filename,selector)=>{const count=e.downloads.length;await click(selector);assert.equal(e.downloads.length,count+1,'Exactly one fresh real export handler download');return {data:plain(e.downloads.at(-1).data)};}});
 vm.runInContext(exportSource+'\nglobalThis.exportCurrent=exportBuild;',h);
 // An invalid Elemental import uses the real outer finally, even when early
 // validation rejects before changing the Mage identity.
 await assert.rejects(()=>c.applyConfig({classKey:'shaman',specialization:'elemental'}));
 assert.equal(elements.status.dataset.buildState,'ready');assert.equal(target.disabled,false);
 assert.equal(elements.statStatus.textContent,'Updating…');assert.equal(timers.size,1);
 elements.spec.value='fire';await elements.spec.listeners.change();assert.equal(elements.presetDialog.open,true);
 elements.cancelPreset.listeners.click();assert.equal(elements.presetDialog.open,false);
 return {...e,before,commands,stages,frames:()=>frames,raw:()=>click('#exportBuild'),run:()=>h.exportCurrent('dirty-preserved')};
}
async function runExportChecks(){
 const old=await fixture();await assert.rejects(()=>old.raw(),/Trusted click did not reach/);
 assert.equal(old.commands.length,2);assert.equal(old.downloads.length,0);assert.ok(old.frames()>=3);
 assert.ok(old.c.window.__soloDpsClickCurrent.events.every(e=>e.trusted&&!e.matched&&e.target==='ASIDE'));
 const current=await fixture();const exported=await current.run();
 assert.equal(current.commands.length,2);assert.equal(current.stages.length,1);
 assert.deepEqual(exported.data,current.before,'Complete dirty Mage state survives rejected import and Cancel');
 assert.equal(current.calls.at(-1).url,'/api/forever/character/generate-level60');
 assert.equal(current.calls.at(-1).data.character.classKey,'mage');
 assert.equal(current.calls.at(-1).data.character.specializationKey,'arcane');
 assert.ok(current.c.window.__soloDpsClickCurrent.events.every(e=>e.trusted&&e.matched));
 for(const options of [{fail:true},{hold:true}]){const e=await fixture(options);await assert.rejects(()=>e.run(),/Timed out/);assert.equal(e.commands.length,0);assert.equal(e.downloads.length,0);}
 const miss=await fixture({miss:true});await assert.rejects(()=>miss.run(),/Trusted click did not reach/);assert.equal(miss.commands.length,2);assert.equal(miss.downloads.length,0);
 console.log('Elemental export: production rejected-import queue/render, Cancel, full fresh export, failed/pending preview and missed-input checks passed. Chromium NOT RUN.');
}
if(require.main===module)runExportChecks().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={runExportChecks};
