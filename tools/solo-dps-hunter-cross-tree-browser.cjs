#!/usr/bin/env node
"use strict";
// Real Chrome controls, downloads, import and seeded replay. API must already run.
const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const BASE=process.env.CP174_BASE_URL||'http://127.0.0.1:5142';
const CHROME=process.env.CP174_CHROME||(process.platform==='win32'?'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe':'/usr/bin/google-chrome');
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),'cp175-hunter-cross-tree'));
fs.mkdirSync(reportDir,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'cp175-hunter-cross-tree-browser-'));
const port=19361,sleep=ms=>new Promise(r=>setTimeout(r,ms));
const proc=spawn(CHROME,['--headless','--disable-gpu',`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,'about:blank'],{stdio:'ignore',windowsHide:true});
let ws,id=0,completedDownloads=0;const pending=new Map(),errors=[];
async function command(method,params={}){
 return new Promise((resolve,reject)=>{
  const key=++id,timer=setTimeout(()=>{pending.delete(key);reject(Error('CDP timeout '+method));},30000);
  pending.set(key,{resolve:r=>{clearTimeout(timer);resolve(r);},reject:e=>{clearTimeout(timer);reject(e);}});
  ws.send(JSON.stringify({id:key,method,params}));
 });
}
async function evalJs(expression){
 const r=await command('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});
 if(r.exceptionDetails)throw Error('Browser exception '+JSON.stringify(r.exceptionDetails));
 return r.result?.value;
}
async function poll(expression,timeout=40000){
 const start=Date.now();while(Date.now()-start<timeout){if(await evalJs(expression))return;await sleep(100);}
 throw Error('Timeout '+expression+'; '+await evalJs("document.querySelector('#status')?.textContent"));
}
async function click(selector){
 const pt=await evalJs(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('Missing '+${JSON.stringify(selector)});e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,disabled:e.disabled};})()`);
 assert.ok(!pt.disabled,'Enabled control '+selector);
 await command('Input.dispatchMouseEvent',{type:'mousePressed',x:pt.x,y:pt.y,button:'left',clickCount:1});
 await command('Input.dispatchMouseEvent',{type:'mouseReleased',x:pt.x,y:pt.y,button:'left',clickCount:1});
}
async function value(id,value){
 await evalJs(`(()=>{const e=document.getElementById(${JSON.stringify(id)});e.${typeof value==='boolean'?'checked':'value'}=${JSON.stringify(value)};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
}
async function choose(cls,spec){
 if(await evalJs("document.querySelector('#classKey').value")!==cls){
  await value('classKey',cls);await poll(`document.querySelector('#classKey').value===${JSON.stringify(cls)}&&!document.querySelector('#run').disabled`);
 }
 if(await evalJs("document.querySelector('#spec').value")!==spec){
  await value('spec',spec);await sleep(150);
  if(await evalJs("document.querySelector('#presetDialog').open"))await click('#confirmPreset');
  await poll(`document.querySelector('#spec').value===${JSON.stringify(spec)}&&!document.querySelector('#run').disabled&&!document.querySelector('#presetDialog').open`);
 }
 await value('provisional',true);await value('duration',20);await value('seed',175901);await value('iterations',1);await value('timeline',true);
}
async function download(selector,filename,name){
 const file=path.join(reportDir,filename);
 assert.ok(!fs.existsSync(file),'No stale expected download '+filename);
 const completedBefore=completedDownloads;
 await click(selector);
 let data;for(let i=0;i<200;i++){
  if(completedDownloads>completedBefore&&fs.existsSync(file)&&!fs.readdirSync(reportDir).some(x=>x.endsWith('.crdownload')))
   try{data=JSON.parse(fs.readFileSync(file,'utf8'));break;}catch{}
  await sleep(100);
 }
 assert.ok(data,'Browser downloaded '+filename);const dest=path.join(reportDir,name);fs.renameSync(file,dest);
 return {data,file:dest};
}
async function runAndReport(name){
 await click('#run');await poll("document.querySelector('#status').textContent.includes('completed')||document.querySelector('#status').textContent.includes('Simulation complete')");
 await poll("!document.querySelector('#exportResults').disabled");
 const exported=await download('#exportResults','warcraftsim-simulation-report.json',name+'-report.json');
 assert.equal(exported.data.schema,'warcraftsim.solo-dps-report');
 assert.ok(exported.data.combatSummary.dps>0,name+' positive DPS');
 await click('#replayResult');await poll("/replay matched|distribution matched/.test(document.querySelector('#status').textContent)");
 console.log('Browser simulation/export/replay',name,exported.data.combatSummary.dps);
 return exported.data;
}
async function importBuild(file){
 const doc=await command('DOM.getDocument');
 const {nodeId}=await command('DOM.querySelector',{nodeId:doc.root.nodeId,selector:'#file'});
 await command('DOM.setFileInputFiles',{nodeId,files:[file]});
 await poll("document.querySelector('#status').textContent==='Build imported.'");
}
async function screenshot(name){
 const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
 const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||reportDir);
 fs.mkdirSync(screenshotDir,{recursive:true});
 fs.writeFileSync(path.join(screenshotDir,name+'.png'),Buffer.from(shot.data,'base64'));
}
async function main(){
 for(let i=0;i<100;i++){
  try{const tab=await(await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();if(tab.webSocketDebuggerUrl){ws=new WebSocket(tab.webSocketDebuggerUrl);break;}}catch{}
  await sleep(100);
 }
 assert.ok(ws,'Chrome debugging port');
 await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
 ws.addEventListener('message',event=>{
  const m=JSON.parse(event.data);if(m.id){const q=pending.get(m.id);if(q){pending.delete(m.id);if(m.error)q.reject(Error(m.error.message));else q.resolve(m.result);}}
  else if(m.method==='Runtime.exceptionThrown')errors.push(m.params);
  else if(m.method==='Browser.downloadProgress'&&m.params.state==='completed')completedDownloads++;
 });
 await command('Page.enable');await command('Runtime.enable');await command('DOM.enable');
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:reportDir,eventsEnabled:true});
 await command('Page.navigate',{url:BASE+'/solo-dps.html'});await poll("document.querySelectorAll('.gear-slot').length===17&&!document.querySelector('#run').disabled");
 const coverage=[];
 const ranks={'improved-tracking':5,'deflection':5,'entrapment':5,'survivalist':5,
  'clever-traps':2,'surefooted':3,'predators-edge':5,'expose-prey':2,'lacerating-strikes':1};
 function damage(report,key){return Object.entries(report.combatSummary.damageByAbility).filter(([k])=>k===key||k.endsWith(':'+key)).reduce((total,[,v])=>total+Number(v),0);}
 for(const spec of ['marksmanship','beast-mastery']){
  await choose('hunter',spec);await click('[data-view=rotation]');
  assert.equal(await evalJs("document.getElementById('hunter-applyHuntersMark').checked"),false,'No implicit mark');
  assert.equal(await evalJs("document.getElementById('hunter-useFireTraps').checked"),false,'No implicit trap selection');
  assert.ok(await evalJs("[...document.querySelector('#priorityActionSelect').options].some(x=>x.value==='mongoose-bite')"));
  assert.ok(await evalJs("[...document.querySelector('#priorityActionSelect').options].some(x=>x.value==='explosive-trap')"));
  const base=(await download('#exportBuild','warcraftsim-build.json',spec+'-initial-build.json')).data;
  base.talents.ranks={...ranks};
  for(const key of Object.keys(base.talents))if(key.endsWith('PointsSpent'))base.talents[key]=key==='survivalPointsSpent'?33:0;
  base.encounter.duration='120';base.encounter.companionEnabled=false;base.encounter.customPriority=[];base.encounter.priorityRows=[];
  const input=path.join(reportDir,spec+'-legal-survival-build.json');fs.writeFileSync(input,JSON.stringify(base,null,2));
  await importBuild(input);await click('[data-view=rotation]');
  await click('#hunter-useMeleeAuto');await click('#hunter-applyHuntersMark');await click('#hunter-useFireTraps');
  // Preview must call the real runner, not merely recognize a catalog string.
  await value('priorityText','strider-kick');await click('#applyPriority');
  await poll("/Unsupported Marksmanship priority|Unavailable Beast Mastery owner action/.test(document.querySelector('#status').textContent)");
  await value('priorityText','mongoose-bite\nexplosive-trap');await click('#applyPriority');
  await poll("document.querySelector('#priorityStatus').textContent.includes('validated')");
  await screenshot(spec+'-explicit-melee-mark-trap-controls');
  const selected=await runAndReport(spec+'-cross-tree');
  assert.ok(damage(selected,'mongoose-bite')>0,'Real off-tree Mongoose damage');
  assert.ok(damage(selected,'lacerating-strikes')>0,'Real off-tree bleed events');
  assert.ok(damage(selected,'explosive-trap')>0,'Real off-tree trap damage');
  const saved=await download('#exportBuild','warcraftsim-build.json',spec+'-selected-build.json');
  assert.equal(saved.data.encounter.hunterSurvival.applyHuntersMark,true);
  assert.equal(saved.data.encounter.hunterSurvival.useMeleeAuto,true);
  assert.equal(saved.data.encounter.hunterSurvival.useFireTraps,true);
  assert.equal(saved.data.encounter.hunterSurvival.trapAttackPowerCoefficient,.10);
  await choose('mage','arcane');await importBuild(saved.file);await click('[data-view=rotation]');
  assert.equal(await evalJs("document.getElementById('hunter-applyHuntersMark').checked"),true);
  assert.equal(await evalJs("document.getElementById('hunter-useMeleeAuto').checked"),true);
  assert.equal(await evalJs("document.getElementById('hunter-useFireTraps').checked"),true);
  const restored=await runAndReport(spec+'-restored');
  assert.deepEqual(restored.originalSimulationRequest,selected.originalSimulationRequest);
  assert.equal(restored.combatSummary.dps,selected.combatSummary.dps);
  await click('[data-view=rotation]');await click('#hunter-triggerTraps');
  const noTrigger=await runAndReport(spec+'-no-trigger');
  assert.equal(damage(noTrigger,'explosive-trap'),0,'Placement is not a trigger');
  await click('[data-view=rotation]');await click('#hunter-applyHuntersMark');
  const noMark=await runAndReport(spec+'-no-mark');
  assert.equal(damage(noMark,'mongoose-bite'),0);assert.equal(damage(noMark,'lacerating-strikes'),0);
  await click('[data-view=rotation]');await click('#hunter-applyHuntersMark');
  await click('[data-view=talents]');
  await evalJs("document.querySelector('[data-talent-key=\"lacerating-strikes\"]').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
  await poll("document.querySelector('[data-talent-key=\"lacerating-strikes\"] .talent-rank').textContent.startsWith('0/')");
  const noBleed=await runAndReport(spec+'-unlearned-bleed');
  assert.ok(damage(noBleed,'mongoose-bite')>0);assert.equal(damage(noBleed,'lacerating-strikes'),0);
  coverage.push({spec,mongoose:damage(selected,'mongoose-bite'),bleed:damage(selected,'lacerating-strikes'),trap:damage(selected,'explosive-trap'),replay:true,import:true,negativeCases:['unlearned Strider rejected','trigger disabled','mark disabled','Lacerating removed']});
 }
 assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(reportDir,'hunter-cross-tree-coverage.json'),JSON.stringify({coverage,browserExceptions:errors.length},null,2));
 console.log('PASS: MM/BM real Hunter controls, learned-action preview, proc/bleed/trap damage, removal, export/import and seeded replay.',reportDir);
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(300);
 const resolved=path.resolve(profile),root=path.resolve(os.tmpdir())+path.sep;
 if(resolved.startsWith(root)&&path.basename(resolved).startsWith('cp175-hunter-cross-tree-browser-'))fs.rmSync(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200});
});
