#!/usr/bin/env node
"use strict";
// Real Chrome controls, downloads, import and seeded replay. API must already run.
const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const BASE=process.env.CP174_BASE_URL||'http://127.0.0.1:5142';
const CHROME=process.env.CP174_CHROME||(process.platform==='win32'?'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe':'/usr/bin/google-chrome');
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),'cp175-protection-companions'));
fs.mkdirSync(reportDir,{recursive:true});
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||reportDir);
fs.mkdirSync(screenshotDir,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'cp175-companions-browser-'));
const port=19359,sleep=ms=>new Promise(r=>setTimeout(r,ms));
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
 const diagnostics=await evalJs("({status:document.querySelector('#status')?.textContent,statStatus:document.querySelector('#statStatus')?.textContent,statError:document.querySelector('#statStatus')?.dataset.lastError,statScope:document.querySelector('#statScope')?.textContent,classKey:document.querySelector('#classKey')?.value,spec:document.querySelector('#spec')?.value})");
 throw Error('Timeout '+expression+'; '+JSON.stringify(diagnostics));
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
 const presets=[];
 for(const [cls,specs] of Object.entries({mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery'],warrior:['arms','fury']})){
  for(const spec of specs){await choose(cls,spec);const report=await runAndReport(cls+'-'+spec);assert.equal(report.build.specialization,spec);presets.push(cls+'/'+spec);}
 }
 await choose('warrior','protection');await click('[data-view=encounter]');
 assert.equal(await evalJs("document.querySelector('#warriorTank').checked"),false);
 assert.equal(await evalJs("document.querySelector('#warriorManualShield').checked"),false);
 await click('#run');await poll("document.querySelector('#status').textContent.includes('Protection setup required')");
 await value('warriorManualShield',true);
 await click('#run');await poll("document.querySelector('#status').textContent.includes('one-handed')");
 await click('#warriorProtectionExample');await value('warriorRage',100);
 await poll("document.querySelector('#statStatus').textContent==='Backend Warrior stats resolved'&&[...document.querySelectorAll('#stats>div')].find(x=>x.querySelector('span')?.textContent==='Block Value')?.querySelector('strong').textContent==='60'");
 assert.equal(await evalJs("getComputedStyle(document.querySelector('#companionRow')).display"),'none','Warrior companion control is actually hidden');
 assert.equal(await evalJs("[...document.querySelectorAll('#stats>div')].find(x=>x.querySelector('span')?.textContent==='Block Value')?.querySelector('strong').textContent"),'60','Manual block value is the actual precombat value');
 assert.match(await evalJs("document.querySelector('#statScope').textContent"),/manual dodge\/parry\/block/);
 await screenshot('protection-explicit-manual-setup');
 await value('warriorIncoming',false);await click('#run');await poll("document.querySelector('#status').textContent.includes('Boss auto-attacks')");
 await value('warriorIncoming',true);
 await click('[data-view=talents]');
 for(const [key,count] of Object.entries({'improved-bloodrage':2,'shield-specialization':5,'anticipation':5,'improved-revenge':3,'improved-thunder-clap':3,'last-stand':1,'master-of-defense':2,'focused-rage':3,'concussion-blow':1,'bastion':5,'shield-slam':1})){
  for(let n=1;n<=count;n++){
   await click(`[data-talent-key="${key}"]`);
   await poll(`document.querySelector('[data-talent-key="${key}"] .talent-rank').textContent.startsWith('${n}/')`);
  }
 }
 const protection=await runAndReport('warrior-protection');presets.push('warrior/protection');
 const w=protection.originalSimulationRequest.warrior;
 assert.equal(w.shieldEquipped,true);assert.equal(w.isTwoHanded,false);assert.equal(w.dualWield,false);
 assert.equal(w.tankMode,true);assert.equal(w.enableBossAutoAttacks,true);assert.equal(w.playerBlockValue,60);
 assert.equal(w.equipmentMode,'gear-bonuses');assert.deepEqual(w.gear,[]);
 assert.ok(protection.combatSummary.damageByAbility['shield-slam']>0,'Shield Slam damage from selected talent and manual block stats');
 assert.ok(protection.rawServerResponse.sim.batch.representativeRun.timeline.some(e=>e.abilityKey==='shield-slam'),'Shield Slam real event');
 assert.ok(protection.warnings.some(x=>x.includes('not sourced catalog equipment')));
 assert.equal(protection.resolvedCharacter.effectiveStats['block-value'],60);
 assert.equal(protection.resolvedCharacter.effectiveStats['block-chance-percent'],10);
 assert.notEqual(protection.resolvedCharacter.profileStats['block-value'],60);
 for(const key of ['block-value','block-chance-percent','defense'])
  assert.equal(protection.resolvedCharacter.effectiveStats[key],protection.rawServerResponse.sim.effectiveCharacterStats[key]);
 const savedProtection=await download('#exportBuild','warcraftsim-build.json','protection-build.json');
 await choose('mage','arcane');await importBuild(savedProtection.file);
 assert.equal(await evalJs("document.querySelector('#warriorBlockValue').value"),'60');
 assert.equal(await evalJs("document.querySelector('#warriorTank').checked"),true);
 const importedProtection=await runAndReport('warrior-protection-imported');
 assert.deepEqual(importedProtection.originalSimulationRequest,protection.originalSimulationRequest);
 assert.equal(importedProtection.combatSummary.dps,protection.combatSummary.dps);
 // Contradictory equipment is never silently rewritten by the opt-in button.
 await click('[data-view=equipment]');await click('[data-slot="main-hand"]');
 await poll("document.querySelectorAll('#pickerItems .picker-item').length>0");
 await click('#pickerItems .picker-item');await poll("document.querySelector('#pickerDetails .simulate')");
 await click('#pickerDetails .simulate');await poll("!document.querySelector('#itemDialog').open");
 await click('[data-view=encounter]');await click('#warriorProtectionExample');
 await poll("document.querySelector('#status').textContent.includes('Catalog equipment will not be overwritten')");
 await click('#run');await poll("document.querySelector('#status').textContent.includes('catalog equipment remains authoritative')");
 await choose('warlock','affliction');await click('[data-view=encounter]');
 await evalJs("document.querySelector('#warlockSettings').open=true");
 assert.equal(await evalJs("document.querySelector('#warlockTemporary').checked"),false);
 assert.equal(await evalJs("document.querySelector('#warlockRitual').checked"),false);
 await value('companionEnabled',true);
 const demons=[];
 for(const demon of ['imp','voidwalker','succubus','incubus','felhunter','infernal','doomguard']){
  await value('demonType',demon);await value('warlockTemporary',false);await value('warlockRitual',false);
  if(['infernal','doomguard'].includes(demon)){
   await click('#run');await poll("document.querySelector('#status').textContent.includes('temporary-demon simulation assumption')");
   await click('[data-view=encounter]');await click('#warlockTemporary');
   if(demon==='doomguard'){
    await click('#run');await poll("document.querySelector('#status').textContent.includes('ritual-prerequisites')");
    await click('#warlockRitual');
   }
  }
  const report=await runAndReport('warlock-demon-'+demon),r=report.originalSimulationRequest.warlock;
  assert.equal(r.petType,demon);assert.equal(r.allowTemporaryDemon,['infernal','doomguard'].includes(demon));
  assert.equal(r.allowDoomguardRitual,demon==='doomguard');assert.ok(report.combatSummary.petDamage>0);
  demons.push({demon,damage:report.combatSummary.petDamage});
 }
 // Include a custom priority to cover import preflight before late settings restoration.
 await click('[data-view=rotation]');await click('#loadPriority');
 await poll("document.querySelectorAll('#priorityList > div').length>0&&document.querySelector('#priorityText').value.length>0");
 await click('#applyPriority');
 await poll("document.querySelector('#priorityStatus').textContent.includes('validated')");
 const doomBuild=await download('#exportBuild','warcraftsim-build.json','doomguard-build.json');
 assert.equal(doomBuild.data.encounter.classSettings.allowTemporaryDemon,true);
 assert.equal(doomBuild.data.encounter.classSettings.allowDoomguardRitual,true);
 await choose('mage','arcane');await importBuild(doomBuild.file);
 assert.equal(await evalJs("document.querySelector('#demonType').value"),'doomguard');
 assert.equal(await evalJs("document.querySelector('#warlockTemporary').checked"),true);
 assert.equal(await evalJs("document.querySelector('#warlockRitual').checked"),true);
 await runAndReport('warlock-doomguard-imported');
 await click('[data-view=encounter]');await evalJs("document.querySelector('#warlockSettings').open=true");await screenshot('doomguard-explicit-assumptions');
 // Old exports without permissions must remain false, never inferred from the demon name.
 const noPermission=structuredClone(doomBuild.data);delete noPermission.encounter.classSettings.allowTemporaryDemon;delete noPermission.encounter.classSettings.allowDoomguardRitual;
 noPermission.encounter.customPriority=[];noPermission.encounter.priorityRows=[];
 const oldPath=path.join(reportDir,'doomguard-without-permissions-build.json');fs.writeFileSync(oldPath,JSON.stringify(noPermission));
 await importBuild(oldPath);
 assert.equal(await evalJs("document.querySelector('#warlockTemporary').checked"),false);
 assert.equal(await evalJs("document.querySelector('#warlockRitual').checked"),false);
 await click('#run');await poll("document.querySelector('#status').textContent.includes('temporary-demon simulation assumption')");
 assert.deepEqual(errors,[]);assert.equal(presets.length,12);assert.equal(demons.length,7);
 fs.writeFileSync(path.join(reportDir,'coverage.json'),JSON.stringify({presets,demons,protectionShieldSlamDamage:protection.combatSummary.damageByAbility['shield-slam'],negativeCases:['unconfigured Protection','shield + two-hand','tank without incoming','manual shield with catalog hand','temporary demon without opt-in','Doomguard without ritual','import without permissions'],browserExceptions:errors.length},null,2));
 console.log('PASS: 12 presets, seven demons, Protection Shield Slam, explicit assumptions, export/import/replay.',reportDir);
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(300);
 const resolved=path.resolve(profile),root=path.resolve(os.tmpdir())+path.sep;
 if(resolved.startsWith(root)&&path.basename(resolved).startsWith('cp175-companions-browser-'))fs.rmSync(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200});
});
