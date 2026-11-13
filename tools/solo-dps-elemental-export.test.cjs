'use strict';
// Deterministic scheduling model, not Chromium acceptance. Execute production
// rejected-import cleanup, debounce, preview renderer and export handler.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-loading-contract.test.cjs');
const {createBrowserClick}=require('./solo-dps-browser-click.cjs');
function exportSource(suite){
 const browser=fs.readFileSync('tools/solo-dps-'+suite+'-browser.test.cjs','utf8'),start=browser.indexOf('const exportBuild='),end=browser.indexOf(suite==='elemental'?'async function run(name)':'function fixture(name,data)',start);
 assert.ok(start>=0&&end>start,'Extract the actual '+suite+' export wrapper');return browser.slice(start,end);
}
async function fixture({suite='elemental',classKey='mage',fail=false,hold=false,miss=false}={}){
 const e=environment(),{c,elements}=e;
 const specialization=classKey==='druid'?'balance':'arcane',race=classKey==='druid'?'night-elf':'gnome';
 elements.classKey.value=classKey;elements.spec.value=specialization;elements.race.value=race;
 await c.loadPreset();elements.duration.value=23;c.buildDirty=true;
 const before=plain(c.saveConfig());
 let now=0,next=0,frames=0;
 const timers=new Map(),listeners=new Map(),commands=[],stages=[];
 c.setTimeout=(callback,delay)=>{const id=++next;timers.set(id,{callback,at:now+delay});return id;};
 c.clearTimeout=id=>timers.delete(id);
 vm.runInContext(part('function buildCharacter(){','// Strict historical/manual fixed-Cat contract.')+part('function formatResultNumber(','function readableResultNumbers(){')+part('function queueStatRefresh(){','async function simulate(){'),c);
 const endpoint=classKey==='druid'?'/api/forever/character/preview-balance':'/api/forever/character/generate-level60';
 const values=classKey==='druid'?{strength:62,agility:65,stamina:69,intellect:100,spirit:110,'attack-power':124,armor:130,'attack-critical-chance-percent':3.3,'spell-critical-chance-percent':22.7,'spell-power':0,health:2173,mana:2444,'spell-hit-chance-percent':87}:{intellect:175,health:3500,mana:4000};
 e.responses.set(endpoint,{effectiveStats:{values},contributions:[],sourceStatus:'controlled-export-fixture'});
 if(fail)e.failures.set(endpoint,'Controlled preview failure');
 elements.stats.children??=[];
 const replace=elements.stats.replaceChildren;
 elements.stats.replaceChildren=function(...children){replace.apply(this,children);stages.push('render');};
 const target=elements.exportBuild,other={tagName:'DIV'};
 // Model only stacked layout. Actual production appends the rows; their
 // presence moves the footer after the helper's previously stable sample.
 Object.assign(target,{tagName:'BUTTON',isConnected:true,scrollIntoView(){},getBoundingClientRect(){return {x:16.15625,y:445+(elements.stats.children?.length||0)*32,width:124,height:35};},contains(node){return node===this;}});
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
 vm.runInContext(exportSource(suite)+'\nglobalThis.exportCurrent=exportBuild;',h);
 if(suite==='elemental'){
  // Early schema rejection still uses the actual outer cleanup/refresh queue.
  await assert.rejects(()=>c.applyConfig({classKey:'shaman',specialization:'elemental'}));
 }else if(suite==='feral'){
  const bad=require('./solo-dps-feral-fixture.cjs').build();bad.manualInputs.shapeshiftMana=999999;
  e.failures.set('/api/forever/character/preview-feral','Controlled unaffordable Feral shift');
  await assert.rejects(()=>c.applyConfig(bad),/Controlled unaffordable Feral shift/);
  assert.ok(e.calls.some(x=>x.url==='/api/forever/character/preview-feral'&&x.data.manualInputs.shapeshiftMana===999999),'Foreign Feral import reaches the actual rejected preparation before rollback');
 }else{
  const combat=environment();combat.elements.classKey.value='rogue';combat.elements.spec.value='combat';combat.elements.race.value='human';await combat.c.loadPreset();
  const bad=plain(combat.c.saveConfig());bad.talents.ranks.precision=4;bad.talents.combatPointsSpent=32;
  const fetch=c.fetch;
  c.fetch=async(url,options)=>{
   const data=options?.body?JSON.parse(options.body):null;
   if(url==='/api/forever/talents/rogue/combat/validate-itemized'&&data.ranks.precision===4){e.calls.push({url,data});return {ok:false,json:async()=>({error:'Controlled invalid Combat rank'})};}
   return fetch(url,options);
  };
  await assert.rejects(()=>c.applyConfig(bad),/Controlled invalid Combat rank/);
  assert.ok(e.calls.some(x=>x.url==='/api/forever/talents/rogue/combat/validate-itemized'&&x.data.ranks.precision===4),'Foreign Combat import reaches its actual validator before rollback');
 }
 assert.equal(elements.status.dataset.buildState,'ready');assert.equal(target.disabled,false);
 assert.equal(elements.statStatus.textContent,'Updating…');assert.equal(timers.size,1);
 assert.deepEqual(plain(c.saveConfig()),before,'The complete dirty foreign draft was restored before its queued preview');
 elements.spec.value=classKey==='druid'?'feral':'fire';await elements.spec.listeners.change();assert.equal(elements.presetDialog.open,true);
 elements.cancelPreset.listeners.click();assert.equal(elements.presetDialog.open,false);
 stages.length=0;
 return {...e,before,endpoint,values,specialization,race,commands,stages,frames:()=>frames,raw:()=>click('#exportBuild'),run:()=>h.exportCurrent('dirty-preserved')};
}
async function runExportChecks(){
 let checks=0;
 for(const options of [{suite:'elemental',classKey:'mage'},{suite:'feral',classKey:'druid'},{suite:'feral',classKey:'mage'},{suite:'combat',classKey:'mage'}]){
  const label=options.suite+' wrapper / '+options.classKey;
  const old=await fixture(options);await assert.rejects(()=>old.raw(),/Trusted click did not reach/);
  assert.equal(old.commands.length,2);assert.equal(old.downloads.length,0);assert.ok(old.frames()>=3);
  assert.ok(old.c.window.__soloDpsClickCurrent.events.every(e=>e.trusted&&!e.matched&&e.target==='DIV'));checks++;
  const current=await fixture(options);const exported=await current.run();
  assert.equal(current.commands.length,2);assert.equal(current.stages.length,1);
  assert.deepEqual(exported.data,current.before,label+': complete dirty state survives rejected import and Cancel');
  assert.equal(current.calls.at(-1).url,current.endpoint);
  assert.equal(current.calls.at(-1).data.character.classKey,options.classKey);
  assert.equal(current.calls.at(-1).data.character.specializationKey,current.specialization);
  assert.equal(current.calls.at(-1).data.character.race,current.race);
  assert.equal(current.elements.stats.children.length,Object.keys(current.values).length,'Actual preview renderer produced every stat row before sampling');
  assert.ok(current.c.window.__soloDpsClickCurrent.events.every(e=>e.trusted&&e.matched));checks++;
  for(const problem of [{fail:true},{hold:true}]){const e=await fixture({...options,...problem});await assert.rejects(()=>e.run(),/Timed out/);assert.equal(e.commands.length,0);assert.equal(e.downloads.length,0);checks++;}
  const miss=await fixture({...options,miss:true});await assert.rejects(()=>miss.run(),/Trusted click did not reach/);assert.equal(miss.commands.length,2);assert.equal(miss.downloads.length,0);checks++;
  // Start a genuine production preview, then change identity with the actual
  // preset loader while its response is held. Releasing the old response must
  // not render it or admit export while the new preview is still queued.
  const stale=await fixture({...options,hold:true}),held=stale.hold(stale.endpoint),preview=stale.c.preview();await held.started;
  stale.elements.classKey.value='mage';stale.elements.spec.value='fire';stale.elements.race.value='gnome';await stale.c.loadPreset();
  const paint=plain(stale.elements.stats.children),renders=stale.stages.length;
  held.release();await preview;assert.deepEqual(plain(stale.elements.stats.children),paint);assert.equal(stale.stages.length,renders);
  await assert.rejects(()=>stale.run(),/Timed out/);assert.equal(stale.commands.length,0);assert.equal(stale.downloads.length,0);checks++;
  console.log(label+': original miss, full export, pending/error/missed input and stale identity checks passed.');
 }
 console.log('Elemental/Feral/Combat export readiness: '+checks+' actual production queue/renderer/handler checks; Balance and Mage rollback, Cancel and exact full-build preservation. Chromium/API NOT RUN.');
 return checks;
}
if(require.main===module)runExportChecks().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={runExportChecks,fixture};
