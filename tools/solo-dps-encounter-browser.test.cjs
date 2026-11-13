"use strict";
// Actual Chrome editor controls, downloaded builds/reports and backend replay.
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp175-encounter-reports"));
fs.mkdirSync(reportDir,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-encounter-browser-")),port=19352;
const proc=spawn(process.env.CP174_CHROME||"/usr/bin/google-chrome",[
 "--headless","--no-sandbox","--disable-gpu","--disable-dev-shm-usage",`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,"about:blank"],{stdio:"ignore"});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));let ws,id=0,pending=new Map(),events=[];
function command(method,params={}){return new Promise((resolve,reject)=>{
 const key=++id,timer=setTimeout(()=>{pending.delete(key);reject(Error(method+" timed out"));},20000);
 pending.set(key,{resolve:value=>{clearTimeout(timer);resolve(value);},reject:error=>{clearTimeout(timer);reject(error);}});
 ws.send(JSON.stringify({id:key,method,params}));
});}
async function evaluate(expression){
 const r=await command("Runtime.evaluate",{expression,returnByValue:true,awaitPromise:true});
 if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result?.value;
}
async function until(expression){
 for(let i=0;i<350;i++){if(await evaluate(expression))return;await sleep(100);}
 throw Error("Timed out: "+expression+"; "+await evaluate("document.querySelector('#status')?.textContent"));
}
async function click(selector){
 const point=await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('Missing click target');e.scrollIntoView({block:'center'});let r=e.getBoundingClientRect(),x=r.x+r.width/2,y=r.y+r.height/2,hit=document.elementFromPoint(x,y);if(!hit||!(hit===e||e.contains(hit))){window.scrollTo(0,0);e.scrollIntoView({block:'nearest'});r=e.getBoundingClientRect();x=r.x+r.width/2;y=r.y+r.height/2;hit=document.elementFromPoint(x,y);}return{x,y,disabled:!!e.disabled,visible:!!hit&&(hit===e||e.contains(hit))};})()`);
 assert.ok(point.visible&&!point.disabled,"Usable control "+selector+": "+JSON.stringify(point));
 for(const type of ["mousePressed","mouseReleased"])await command("Input.dispatchMouseEvent",{type,x:point.x,y:point.y,button:"left",clickCount:1});
}
async function encounter(){
 await click('[data-view=encounter]');
 if(!await evaluate("document.querySelector('#scriptedBossSection').open"))await click('#scriptedBossSection>summary');
}
async function importBuild(data,name){
 const file=path.join(reportDir,name+"-input.json");fs.writeFileSync(file,JSON.stringify(data,null,2));
 await evaluate("document.querySelector('#status').textContent='Importing acceptance fixture'");
 const {root}=await command("DOM.getDocument");const {nodeId}=await command("DOM.querySelector",{nodeId:root.nodeId,selector:"#file"});
 await command("DOM.setFileInputFiles",{nodeId,files:[file]});
 await until("document.querySelector('#status').textContent==='Build imported.'");
}
async function download(button,filename,name){
 await click(button);const file=path.join(reportDir,filename);
 for(let i=0;i<150;i++){
  if(fs.existsSync(file)&&!fs.readdirSync(reportDir).some(x=>x.endsWith('.crdownload'))){
   try{const data=JSON.parse(fs.readFileSync(file,"utf8"));fs.renameSync(file,path.join(reportDir,name+".json"));return data;}catch(e){if(i===149)throw e;}
  }
  await sleep(100);
 }
 throw Error("Download unavailable: "+file);
}
async function simulate(name,cls){
 await evaluate("document.querySelector('#status').textContent='Starting acceptance simulation'");
 await click('#run');
 await until("document.querySelector('#status').textContent.includes('completed')&&!document.querySelector('#run').disabled");
 const report=await download('#exportResults','warcraftsim-simulation-report.json',name+'-report');
 await click('#replayResult');
 await until("document.querySelector('#status').textContent.includes('replay matched')");
 assert.equal(report.build.classKey,cls);return report;
}
async function screenshot(name){
 if(!process.env.CP174_SCREENSHOT_DIR)return;
 fs.mkdirSync(process.env.CP174_SCREENSHOT_DIR,{recursive:true});
 const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
 fs.writeFileSync(path.join(process.env.CP174_SCREENSHOT_DIR,name+'.png'),Buffer.from(shot.data,'base64'));
}
function fixture(){
 const build=JSON.parse(fs.readFileSync('docs/acceptance/cp175/cp175-warlock-180s-or-add-target.json','utf8'));
 const encounter=build.encounter;encounter.duration='50';encounter.companionEnabled=false;
 encounter.customPriority=['immolate','incinerate','shadow-bolt'];
 encounter.priorityRows=encounter.customPriority.map((key,i)=>({key,enabled:true,targetActorKey:'target-'+(i+1),
  conditions:[{conditionType:'current-time-seconds',comparisonOperator:'greater-than-or-equal',value:0}],
  orGroups:[[{conditionType:'source-resource-percent',key:'mana',comparisonOperator:'greater-than-or-equal',value:0,requireSourceOwnership:false}]]}));
 encounter.soloScript={enabled:true,startingHealthPercent:100,bossHealthMilestones:[],addWindows:[
  {targetKey:'target-1',spawnTimeSeconds:2,despawnTimeSeconds:12,startingHealthPercent:100,healthMilestonesRelativeToSpawn:true,healthMilestones:[{timeSeconds:3,healthPercent:60}]},
  {targetKey:'target-2',spawnTimeSeconds:15,despawnTimeSeconds:27,startingHealthPercent:90,healthMilestonesRelativeToSpawn:false,healthMilestones:[{timeSeconds:20,healthPercent:50}]},
  {targetKey:'target-3',spawnTimeSeconds:30,despawnTimeSeconds:45,startingHealthPercent:80,healthMilestonesRelativeToSpawn:true,healthMilestones:[{timeSeconds:5,healthPercent:40}]}
 ]};return build;
}
async function main(){
 let tabs;for(let i=0;i<100;i++){try{tabs=await(await fetch(`http://127.0.0.1:${port}/json`)).json();if(tabs.some(t=>t.type==='page'))break;}catch{}await sleep(100);}
 assert.ok(tabs?.some(t=>t.type==='page'),'Chrome debugging port');
 ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
 ws.addEventListener('message',event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(p)m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}else events.push(m);});
 await command('Page.enable');await command('Runtime.enable');await command('Log.enable');
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:900,deviceScaleFactor:1,mobile:false});
 await command('Page.setDownloadBehavior',{behavior:'allow',downloadPath:reportDir});
 await command('Page.navigate',{url:BASE+'/solo-dps.html'});
 await until("document.querySelectorAll('.gear-slot').length===17&&!document.querySelector('#run').disabled");
 for(const removedIndex of [0,1,2]){
  const input=fixture(),removed=input.encounter.priorityRows[removedIndex],name='encounter-remove-'+(removedIndex+1);
  await importBuild(input,name);await encounter();
  await click('[aria-label="Remove enemy target-'+(removedIndex+1)+'"]');
  assert.equal(await evaluate("document.querySelectorAll('#soloAddWindows>details').length"),2);
  await click('[data-view=rotation]');
  const removedSelector='[aria-label="Priority target for '+removed.key+'"]';
  const state=await evaluate(`(()=>{const target=document.querySelector(${JSON.stringify(removedSelector)}),row=target.closest('.priority-entry'),enable=row.querySelector('.check-row input');return {value:target.value,label:target.selectedOptions[0].textContent,enabled:enable.checked,locked:enable.disabled};})()`);
  assert.deepEqual(state,{value:'removed:target-'+(removedIndex+1),label:'Removed target-'+(removedIndex+1)+' (not configured)',enabled:false,locked:true});
  if(removedIndex===0){
   await evaluate(`document.querySelector(${JSON.stringify(removedSelector)}).closest('details').open=true`);await screenshot('cp175-removed-target-disabled');
  }
  await encounter();await click('#addSoloEnemy');
  assert.equal(await evaluate("document.querySelectorAll('#soloAddWindows>details').length"),3);
  await click('[data-view=rotation]');
  assert.equal(await evaluate(`Array.from(document.querySelector(${JSON.stringify(removedSelector)}).options).filter(x=>x.value==='target-3').length`),1,'New target appears without resetting removed selection');
  assert.equal(await evaluate(`document.querySelector(${JSON.stringify(removedSelector)}).value`),'removed:target-'+(removedIndex+1));
  const saved=await download('#exportBuild','warcraftsim-build.json',name+'-export');
  const expectedWindows=input.encounter.soloScript.addWindows.filter((_,i)=>i!==removedIndex).map((x,i)=>({...x,targetKey:'target-'+(i+1)}));
  assert.deepEqual(saved.encounter.soloScript.addWindows.slice(0,2),expectedWindows,'Surviving add timing, health and identity');
  assert.deepEqual(saved.encounter.customPriority,input.encounter.customPriority.filter(x=>x!==removed.key));
  input.encounter.priorityRows.forEach((row,i)=>{
   const after=saved.encounter.priorityRows[i];assert.deepEqual(after.conditions,row.conditions);assert.deepEqual(after.orGroups,row.orGroups);
   assert.equal(after.targetActorKey,i===removedIndex?'removed:target-'+(i+1):'target-'+(i>removedIndex?i:i+1));assert.equal(after.enabled,i!==removedIndex);
  });
  await importBuild(saved,name+'-roundtrip');
  const roundtrip=await download('#exportBuild','warcraftsim-build.json',name+'-roundtrip-export');
  assert.deepEqual(roundtrip.encounter.priorityRows,saved.encounter.priorityRows);
  assert.deepEqual(roundtrip.encounter.soloScript,saved.encounter.soloScript);
  const report=await simulate(name,'warlock'),typed=report.originalSimulationRequest.warlock;
  assert.equal(typed.additionalTargets,3);assert.deepEqual(typed.customPriority,saved.encounter.customPriority);
  assert.deepEqual(typed.soloEncounter,saved.encounter.soloScript);
  const timeline=report.rawServerResponse.sim.run.timeline;
  assert.ok(!timeline.some(e=>e.sourceActorKey==='warlock'&&e.abilityKey===removed.key&&[4,6].includes(e.type)),'Deleted-target action never casts or damages boss/reused ID');
  for(const row of saved.encounter.priorityRows.filter(x=>x.enabled)){
   const window=saved.encounter.soloScript.addWindows.find(x=>x.targetKey===row.targetActorKey);
   const casts=timeline.filter(e=>e.sourceActorKey==='warlock'&&e.abilityKey===row.key&&e.type===4);
   assert.ok(casts.length>0,'Survivor action executes: '+row.key);
   assert.ok(casts.every(e=>e.targetActorKey===row.targetActorKey),'Completed casts retain the remapped target, including casts that fizzle at despawn');
   const activeReceipts=timeline.filter(e=>e.sourceActorKey==='warlock'&&e.abilityKey===row.key&&[3,6].includes(e.type));
   assert.ok(activeReceipts.some(e=>e.type===3)&&activeReceipts.some(e=>e.type===6&&e.amount>0),'Survivor has actual cast-start and positive damage receipts');
   assert.ok(activeReceipts.every(e=>e.targetActorKey===row.targetActorKey&&e.timeSeconds>=window.spawnTimeSeconds&&e.timeSeconds<window.despawnTimeSeconds),'Cast starts and damage occur only on the remapped active enemy');
  }
  console.log('CP175 real Chrome encounter removal/export/import/simulate/replay passed:',name,report.combatSummary.dps);
 }
 // One remaining targeted action must not turn into automatic boss rotation.
 const single=fixture();single.encounter.customPriority=['immolate'];single.encounter.priorityRows=single.encounter.priorityRows.slice(0,1);single.encounter.soloScript.addWindows=single.encounter.soloScript.addWindows.slice(0,1);
 await importBuild(single,'encounter-remove-only');await encounter();await click('[aria-label="Remove enemy target-1"]');await click('#run');
 await until("document.querySelector('#status').textContent.includes('Removed-target actions are disabled')");
 assert.equal(await evaluate("document.querySelector('#run').disabled"),false);
 const disabled=await download('#exportBuild','warcraftsim-build.json','encounter-remove-only-export');
 await importBuild(disabled,'encounter-remove-only-roundtrip');await click('#run');
 await until("document.querySelector('#status').textContent.includes('Removed-target actions are disabled')");
 // Reusing target-1 does not revive the action. Only an explicit target choice,
 // enable click and Apply may restore it.
 await encounter();await click('#addSoloEnemy');await click('[data-view=rotation]');
 const retarget='[aria-label="Priority target for immolate"]';
 assert.equal(await evaluate("document.querySelector('[aria-label=\"Priority target for immolate\"]').value"),'removed:target-1');
 await evaluate("(()=>{const target=document.querySelector('[aria-label=\"Priority target for immolate\"]');target.closest('details').open=true;target.value='target-1';target.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await click('#priorityList .check-row input');await click('#applyPriority');
 await until("document.querySelector('#priorityStatus').textContent.includes('validated by the engine')");
 const restored=await simulate('encounter-explicit-retarget','warlock');
 assert.deepEqual(restored.originalSimulationRequest.warlock.customPriority,['immolate']);
 assert.ok(restored.rawServerResponse.sim.run.timeline.some(e=>e.type===4&&e.abilityKey==='immolate'&&e.targetActorKey==='target-1'),'Explicit retarget restores execution');
 assert.ok(!restored.rawServerResponse.sim.run.timeline.some(e=>e.type===4&&e.abilityKey==='immolate'&&e.targetActorKey==='target'),'Explicit retarget does not become boss cast');
 console.log('CP175 removed-only build stays fail-closed across import and recovers only after explicit target/enable/Apply.');
 // Legal armor zero must survive imported settings, downloaded build and report.
 const warrior=JSON.parse(fs.readFileSync('docs/acceptance/cp175/cp175-warrior-custom-rotation.json','utf8'));
 warrior.encounter.duration='25';warrior.encounter.warrior.targetArmor=0;
 await importBuild(warrior,'warrior-zero-armor');await encounter();
 assert.equal(await evaluate("document.querySelector('#warriorArmor').value"),'0');
 await until("document.querySelector('#statStatus').textContent.includes('Backend Warrior stats resolved')");
 await evaluate("document.querySelector('#warriorArmor').scrollIntoView({block:'center'})");
 await screenshot('cp175-warrior-zero-armor');
 const saved=await download('#exportBuild','warcraftsim-build.json','warrior-zero-armor-export');
 assert.equal(saved.encounter.warrior.targetArmor,0);await importBuild(saved,'warrior-zero-armor-roundtrip');
 const zero=await simulate('warrior-zero-armor','warrior');assert.equal(zero.originalSimulationRequest.warrior.targetArmor,0);assert.equal(zero.build.encounter.warrior.targetArmor,0);
 assert.equal(zero.rawServerResponse.sim.effectiveBossArmor,0);
 const armor=structuredClone(saved);armor.encounter.warrior.targetArmor=3500;await importBuild(armor,'warrior-3500-armor');
 const armored=await simulate('warrior-3500-armor','warrior');
 assert.ok(zero.combatSummary.damageDone>armored.combatSummary.damageDone,'Zero armor produces greater actual physical damage at identical seed');
 console.log('CP175 real Chrome zero-armor roundtrip/replay passed:',JSON.stringify({zeroDamage:zero.combatSummary.damageDone,armoredDamage:armored.combatSummary.damageDone}));
 assert.deepEqual(events.filter(e=>e.method==='Runtime.exceptionThrown'),[],'No uncaught browser exceptions');
 assert.deepEqual(events.filter(e=>e.method==='Log.entryAdded'&&e.params?.entry?.level==='error').map(e=>e.params.entry.text),[],'No browser/server error receipts');
}
main().catch(error=>{console.error(error);process.exitCode=1;}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(300);
 // Delete only this process's temporary Chrome profile, never a user profile.
 if(path.dirname(path.resolve(profile))===path.resolve(os.tmpdir())&&path.basename(profile).startsWith('cp175-encounter-browser-'))fs.rmSync(profile,{recursive:true,force:true});
});