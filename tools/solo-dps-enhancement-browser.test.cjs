#!/usr/bin/env node
"use strict";
// End-to-end acceptance against an already running local API. Every successful
// report is downloaded from a real UI simulation, never manufactured in a test.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const {compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const {assertSoloDpsIdentity}=require("./solo-dps-browser-identity.cjs");
const {enhancementRejectionCases,serverTalentRejection,rejectionExpression}=require("./solo-dps-enhancement-rejections.cjs");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp174-full-reports"),"enhancement-browser");
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||path.join(os.tmpdir(),"cp174-ui-captures"));
const fixtureDir=path.resolve(__dirname,"../docs/acceptance/cp175");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-enhancement-browser-"));
const port=Number(process.env.CP175_ENHANCEMENT_CDP_PORT||19359);
fs.mkdirSync(reportDir,{recursive:true});fs.mkdirSync(screenshotDir,{recursive:true});
const proc=spawn(process.env.CP174_CHROME||"/usr/bin/google-chrome",[
 "--headless","--no-sandbox","--disable-gpu","--disable-dev-shm-usage",
 `--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,"about:blank"],{stdio:"ignore",windowsHide:true});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
let ws,id=0,pending=new Map(),browserErrors=[],checks=0,reportFiles=[],gateReceipts=[];
function command(method,params={}){return new Promise((resolve,reject)=>{
 const key=++id,timer=setTimeout(()=>{pending.delete(key);reject(Error("CDP timed out: "+method));},20000);
 pending.set(key,{resolve:value=>{clearTimeout(timer);resolve(value);},reject:error=>{clearTimeout(timer);reject(error);}});
 ws.send(JSON.stringify({id:key,method,params}));
});}
async function evaluate(expression){
 const result=await command("Runtime.evaluate",{expression,returnByValue:true,awaitPromise:true});
 if(result.exceptionDetails)throw Error("Browser exception: "+JSON.stringify(result.exceptionDetails));
 return result.result?.value;
}
const text=selector=>evaluate(`document.querySelector(${JSON.stringify(selector)})?.textContent`);
async function until(expression,timeout=40000){
 const start=Date.now();
 while(Date.now()-start<timeout){if(await evaluate(expression))return;await sleep(100);}
 const state=await evaluate("({status:document.querySelector('#status')?.textContent,priority:document.querySelector('#priorityStatus')?.textContent,comparison:document.querySelector('#compareStatus')?.textContent})");
 throw Error("Timed out: "+expression+"; "+JSON.stringify(state));
}
async function click(selector,button="left"){
 const point=await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('Missing click target');e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect(),x=r.x+r.width/2,y=r.y+r.height/2,hit=document.elementFromPoint(x,y);return {x,y,visible:r.width>0&&r.height>0,hit:hit===e||e.contains(hit),disabled:!!e.disabled};})()`);
 assert.ok(point.visible&&point.hit&&!point.disabled,"Clickable "+selector+": "+JSON.stringify(point));
 for(const type of ["mousePressed","mouseReleased"])
  await command("Input.dispatchMouseEvent",{type,x:point.x,y:point.y,button,clickCount:1});
}
async function setInput(selector,value){
 await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
}
async function selectFile(button,input,file){
 assert.ok(fs.existsSync(file),"Import file must exist: "+file);
 // Use the application's import button and the actual file-input DOM path.
 // This intentionally does not call a renderer or inject a simulation event.
 await click(button);
 const doc=await command("DOM.getDocument");
 const {nodeId}=await command("DOM.querySelector",{nodeId:doc.root.nodeId,selector:input});
 await command("DOM.setFileInputFiles",{nodeId,files:[path.resolve(file)]});
}
async function beginImportObservation(input='#file',status='#status'){
 await evaluate(`(()=>{
  window.__statusChanged=false;window.__fileChanged=false;window.__observer?.disconnect();
  window.__observer=new MutationObserver(()=>window.__statusChanged=true);
  window.__observer.observe(document.querySelector(${JSON.stringify(status)}),{childList:true,subtree:true,characterData:true});
  if(window.__observedFile&&window.__importChangeListener)window.__observedFile.removeEventListener('change',window.__importChangeListener);
  window.__observedFile=document.querySelector(${JSON.stringify(input)});
  window.__importChangeListener=()=>{window.__fileChanged=true;};
  window.__observedFile.addEventListener('change',window.__importChangeListener,{once:true});
 })()`);
}
async function importBuild(file){
 await beginImportObservation();
 await selectFile("#importBuild","#file",file);
 await until("window.__fileChanged && window.__statusChanged && document.querySelector('#file').value==='' && document.querySelector('#status').textContent==='Build imported.'");
 await assertIdentity();
}
async function assertIdentity(){
 const identity=await evaluate("({classKey:document.querySelector('#classKey').value,specialization:document.querySelector('#spec').value,classLabel:document.querySelector('#classKey').selectedOptions[0].textContent,race:document.querySelector('#race').selectedOptions[0].textContent,displayedTitle:document.querySelector('#characterTitle').textContent,subtitle:document.querySelector('#characterSubtitle').textContent})");
 assertSoloDpsIdentity(identity,{classKey:'shaman',specialization:'enhancement'});
}
async function downloaded(filename,selector,destination){
 const file=path.join(reportDir,filename);
 if(fs.existsSync(file))fs.unlinkSync(file);
 await click(selector);
 let data;
 for(let i=0;i<160;i++){
  if(fs.existsSync(file)&&!fs.readdirSync(reportDir).some(x=>x.endsWith(".crdownload"))){
   try{data=JSON.parse(fs.readFileSync(file,"utf8"));break;}catch{}
  }
  await sleep(100);
 }
 assert.ok(data,"Complete browser JSON download required: "+filename);
 const target=path.join(reportDir,destination);
 fs.renameSync(file,target);
 return {data,file:target};
}
const exportBuild=name=>downloaded("warcraftsim-build.json","#exportBuild",name+"-build.json");
async function run(name){
 const completed=await evaluate("window.__comparisonCompleted||0"),beforeSequence=await sequence();
 const settings=await evaluate("[...document.querySelectorAll('#classKey,#spec,#race,[id^=enhancement]')].filter(x=>['INPUT','SELECT'].includes(x.tagName)).map(x=>({id:x.id,value:x.value,checked:x.checked,disabled:x.disabled}))");
 await click("#run");
 assert.equal(await evaluate("document.querySelector('#run').disabled"),true,'Run locks before asynchronous preparation');
 // Native repeated clicks on a disabled control must neither start another
 // simulation nor alter settings while the real response remains pending.
 await evaluate("document.querySelector('#run').click();document.querySelector('#run').click()");

 await until(`(window.__comparisonCompleted||0)>${completed} && !document.querySelector('#run').disabled && /Simulation completed|Warrior completed/.test(document.querySelector('#status').textContent)`,60000);
 const simulationRequests=await evaluate(`window.__enhancementHttp.filter(x=>x.sequence>${beforeSequence}&&x.url==='/api/forever/character/simulate-level60')`);
 assert.equal(simulationRequests.length,1,'Pending duplicate clicks must produce one simulation');
 assert.deepEqual(await evaluate("[...document.querySelectorAll('#classKey,#spec,#race,[id^=enhancement]')].filter(x=>['INPUT','SELECT'].includes(x.tagName)).map(x=>({id:x.id,value:x.value,checked:x.checked,disabled:x.disabled}))"),settings,'Pending run preserves selected settings and restores availability');
 const result=await downloaded("warcraftsim-simulation-report.json","#exportResults",name+"-report.json");
 assert.equal(result.data.schema,"warcraftsim.solo-dps-report");
 const inspected=compareReports(result.data,result.data);
 assert.equal(inspected.comparable,true,name+": "+inspected.warnings.join("; "));
 const actualWarnings=await evaluate("[...document.querySelectorAll('#resultWarningList > div')].map(row=>row.textContent)");
 assert.deepEqual(actualWarnings,[...new Set((result.data.warnings||[]).filter(value=>typeof value==='string'&&value.trim()))],"Readable warnings must come exactly from this run, without stale notes or invented warnings");
 await assertReadableLayout('results',name+'-results');
 reportFiles.push(path.basename(result.file));
 return result;
}
async function replay(pattern){
 await click("#replayResult");await until(`new RegExp(${JSON.stringify(pattern)}).test(document.querySelector('#status').textContent)`,60000);
}
async function importReport(slot,file){
 await beginImportObservation("#compareFile","#compareStatus");
 await selectFile("#compareImport"+slot,"#compareFile",file);
 await until("window.__fileChanged && window.__statusChanged && document.querySelector('#compareFile').value===''");
 assert.ok(!(await text("#compareStatus")).includes("rejected"),await text("#compareStatus"));
}
async function assertReadableLayout(view,label){
 await click('[data-view='+view+']');
 const layout=await evaluate(`(()=>{
  const selector=${JSON.stringify(view==='results'?'#resultWarningList > div':'#enhancementSettings p,#enhancementSettings label')};
  return {viewport:document.documentElement.clientWidth,width:document.documentElement.scrollWidth,
   rows:[...document.querySelectorAll(selector)].filter(x=>x.getClientRects().length>0).map(x=>{
    const r=x.getBoundingClientRect();return {text:x.textContent,left:r.left,right:r.right,clientWidth:x.clientWidth,scrollWidth:x.scrollWidth,clientHeight:x.clientHeight,scrollHeight:x.scrollHeight};
   })};
 })()`);
 assert.ok(layout.rows.length>0,label+' requires real rendered scope/assumption text');
 assert.ok(layout.width<=layout.viewport+1,label+' document horizontal overflow: '+JSON.stringify(layout));
 for(const row of layout.rows){
  assert.ok(row.left>=-1&&row.right<=layout.viewport+1,label+' text outside viewport: '+JSON.stringify(row));
  assert.ok(row.scrollWidth<=row.clientWidth+1&&row.scrollHeight<=row.clientHeight+1,label+' clipped scope/assumption text: '+JSON.stringify(row));
 }
 gateReceipts.push({gate:'readable-layout',label,view,...layout});
}
async function screenshot(name,selector){
 if(selector)await evaluate(`(()=>{document.querySelector(${JSON.stringify(selector)}).scrollIntoView({block:'start'});window.scrollBy(0,-document.querySelector('.topbar').getBoundingClientRect().height-16);})()`);
 const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 fs.writeFileSync(path.join(screenshotDir,name+".png"),Buffer.from(shot.data,"base64"));
}
const rows=()=>evaluate("[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent))");
async function ready(after,fields={}){
 // The latest initiated Enhancement preview must have finished, match the
 // currently selected identity and controls, and be reflected in the sidebar.
 await until(`(()=>{
  const receipt=window.__enhancementHttp.find(x=>x.sequence===window.__latestEnhancementPreview);
  if(!receipt||receipt.sequence<=${after}||receipt.status!==200||document.querySelector('#classKey').value!=='shaman'||document.querySelector('#spec').value!=='enhancement'||document.querySelector('#statStatus').textContent!=='Up to date')return false;
  if(receipt.request?.character?.race!==document.querySelector('#race').value)return false;
  if(!Object.entries(${JSON.stringify(fields)}).every(([key,value])=>receipt.request[key]===value))return false;
  for(const input of document.querySelectorAll('[id^=enhancement]')){
   const suffix=input.id.slice('enhancement'.length),key=suffix[0]?.toLowerCase()+suffix.slice(1);
   if(!Object.prototype.hasOwnProperty.call(receipt.request,key)||!['INPUT','SELECT'].includes(input.tagName))continue;
   const value=input.type==='checkbox'?input.checked:input.type==='number'?Number(input.value):input.value;
   if(receipt.request[key]!==value)return false;
  }
  const rendered=[...document.querySelectorAll('#stats > div')].map(x=>[x.querySelector('span')?.textContent.toLowerCase(),x.querySelector('strong')?.textContent]);
  const stats=receipt.response.effectiveStats.values||receipt.response.effectiveStats;
  return Object.entries(stats).filter(([key,value])=>value!==null&&typeof value!=='object').every(([key,value])=>rendered.some(([label,text])=>label===key.replace(/-/g,' ')&&text===Number(value).toLocaleString('en-US',{minimumFractionDigits:key.includes('percent')?1:0,maximumFractionDigits:key.includes('percent')?1:2})));
 })()`);
 gateReceipts.push({gate:'latest-selected-build-preview',after,fields,sequence:await evaluate('window.__latestEnhancementPreview'),at:await evaluate('performance.now()')});
}
const sequence=()=>evaluate('window.__enhancementSequence');
async function parity(report){
 const p=report.resolvedCharacter,s=report.rawServerResponse.sim;
 for(const key of ['attackPower','spellPower','maximumMana','maximumHealth'])assert.equal(p[key],s[key],key+' actual preview/execution');
 assert.ok(p.rotation.entries.length>0,'Runtime-generated preview rotation required');
 assert.equal(s.run.summary.actorSummaries.shaman.startingResources.mana,p.maximumMana);
 assert.match(p.statPreviewScope,/display-only.*not-current-client-certified/);
 assert.equal(report.originalSimulationRequest.specialization,'enhancement');
 assert.equal(report.originalSimulationRequest.itemizedTalentMode,true);
 checks++;
}
async function main(){
 let tabs;for(let i=0;i<100;i++){try{tabs=await(await fetch(`http://127.0.0.1:${port}/json`)).json();if(tabs.some(t=>t.type==='page'))break;}catch{}await sleep(100);}
 assert.ok(tabs?.some(t=>t.type==='page'),'Chromium CDP port unavailable');
 ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
 await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
 ws.addEventListener('message',event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(p)m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}else if(m.method==='Runtime.exceptionThrown')browserErrors.push(m.params.exceptionDetails);});
 for(const method of ['Page.enable','Runtime.enable','DOM.enable','Network.enable'])await command(method);
 await command('Network.emulateNetworkConditions',{offline:false,latency:Number(process.env.CP175_CDP_LATENCY_MS||100),downloadThroughput:-1,uploadThroughput:-1});
 // Observe real fetches without manufacturing responses or publishing completion.
 await command('Page.addScriptToEvaluateOnNewDocument',{source:"window.__enhancementHttp=[];window.__enhancementSequence=0;const nativeFetch=window.fetch;window.fetch=async(...args)=>{const sequence=++window.__enhancementSequence;if(String(args[0])==='/api/forever/character/preview-enhancement')window.__latestEnhancementPreview=sequence;const response=await nativeFetch(...args);if(String(args[0]).startsWith('/api/forever/')){const receipt={sequence,url:String(args[0]),request:args[1]?.body?JSON.parse(args[1].body):null,status:response.status};try{receipt.response=await response.clone().json();}catch{}window.__enhancementHttp.push(receipt);}return response;};window.__comparisonCompleted=0;document.addEventListener('warcraftsim:simulation-complete',()=>window.__comparisonCompleted++);"});
 await command('Page.setDownloadBehavior',{behavior:'allow',downloadPath:reportDir});
 await command('Page.setInterceptFileChooserDialog',{enabled:true});
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command('Page.navigate',{url:BASE+'/solo-dps.html'});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')");
 await setInput('#classKey','shaman');
 await until("document.querySelector('#spec').value==='elemental' && /Class defaults loaded/.test(document.querySelector('#status').textContent)");
 let start=await sequence();await setInput('#spec','enhancement');
 await until("document.querySelector('#spec').value==='enhancement' && /Class defaults loaded/.test(document.querySelector('#status').textContent)");
 if(!await evaluate("document.querySelector('#provisional').checked"))await click('#provisional');
 await ready(start);await assertIdentity();
 const preset=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/shaman-enhancement.json'),'utf8'));
 const baseline=await exportBuild('enhancement-baseline');
 assert.deepEqual(baseline.data.talents.ranks,preset.ranks);assert.equal(Object.values(preset.ranks).reduce((a,b)=>a+b),36);
 assert.deepEqual(Object.fromEntries(Object.entries(baseline.data.talents).filter(([k])=>k.endsWith('PointsSpent'))),{elementalPointsSpent:0,enhancementPointsSpent:36,restorationPointsSpent:0});
 await click('[data-view=talents]');assert.match(await text('#talentCoverage'),/historical|provisional/i);await screenshot('enhancement-talents-desktop','#talentSummary');
 await click('[data-view=encounter]');
 await setInput('#duration',30);start=await sequence();await setInput('#enhancementTargetArmor',2100);await ready(start,{targetArmor:2100});
 start=await sequence();await click('#enhancementDualWield');await ready(start,{dualWield:true});
 for(const [field,value] of Object.entries({maximumHealth:4900,weaponMinDamage:180,weaponMaxDamage:240,weaponSpeedSeconds:2.6,offHandWeaponMinDamage:100,offHandWeaponMaxDamage:145,offHandWeaponSpeedSeconds:1.8,mainHandImbue:'rockbiter',offHandImbue:'flametongue'})){
  start=await sequence();await setInput('#enhancement'+field[0].toUpperCase()+field.slice(1),value);await ready(start,{[field]:value});
 }
 const saved=await exportBuild('enhancement-manual');
 assert.equal(saved.data.encounter.enhancement.targetArmor,2100);assert.equal(saved.data.encounter.enhancement.maximumHealth,4900);assert.equal(saved.data.encounter.enhancement.dualWield,true);
 await assertReadableLayout('encounter','manual-desktop-encounter');
 const a=await run('enhancement-manual');await parity(a.data);
 await screenshot('enhancement-results-desktop','#resultWarnings');
 assert.deepEqual(a.data.combatSummary.targetSemantics,{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:2100,targetCount:1,initialHealthPercent:100});
 const typed=a.data.originalSimulationRequest.enhancement;
 assert.equal(typed.targetArmor,2100);assert.equal(typed.mainHandImbue,'rockbiter');assert.equal(typed.offHandImbue,'flametongue');
 assert.equal(typed.combatItems.majorManaPotionCharges,0);assert.equal(typed.combatItems.majorHealingPotionCharges,0);
 assert.notEqual(a.data.resolvedCharacter.maximumHealth,4900,'Historical supplied Health takes precedence over manual fallback');
 for(const talent of a.data.talentAllocation.selected){assert.equal(talent.historicalValidationSourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');assert.match(talent.historicalValidationSourceStatus,/historical/);}
 await replay('Deterministic replay matched');
 const repeat=await run('enhancement-repeat');assert.deepEqual(repeat.data.combatSummary,a.data.combatSummary);
 await importReport('A',a.file);await importReport('B',repeat.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 await importBuild(saved.file);assert.deepEqual((await exportBuild('enhancement-roundtrip')).data,saved.data);checks++;
 // Preset36 is a starting allocation, not a required minimum or a completed51.
 const lower=structuredClone(saved.data);delete lower.talents.ranks['rage-of-the-farseer'];lower.talents.enhancementPointsSpent=35;
 const lowerFile=path.join(reportDir,'legal-35-build.json');fs.writeFileSync(lowerFile,JSON.stringify(lower));
 await importBuild(lowerFile);const lowerExport=await exportBuild('legal-35-roundtrip');assert.deepEqual(lowerExport.data.talents,lower.talents);
 await click('[data-view=talents]');assert.match(await text('#talentSummary'),/35/);assert.match(await text('#talentSummary'),/16/);
 const lowerRun=await run('legal-35');await parity(lowerRun.data);assert.equal(Object.values(lowerRun.data.originalSimulationRequest.itemizedTalentRanks).reduce((a,b)=>a+b),35);
 assert.deepEqual(lowerRun.data.originalSimulationRequest.itemizedTalentRanks,lowerRun.data.originalSimulationRequest.enhancement.talents.ranks);
 await importBuild(saved.file);
 // Each rejection begins with a complete exported real build and must be atomic.
 for(const fixture of enhancementRejectionCases){
  const {name,mutate}=fixture,invalid=structuredClone(saved.data);mutate(invalid);
  const file=path.join(reportDir,'rejected-'+name+'.json');fs.writeFileSync(file,JSON.stringify(invalid));
  await beginImportObservation();const before=await sequence();
  await selectFile('#importBuild','#file',file);await until(rejectionExpression(fixture,before));
  assert.equal(await text('#status'),fixture.error,name+' exact rejection');
  assert.deepEqual((await exportBuild('after-rejected-'+name)).data,saved.data,name+' atomic rejection');checks++;
 }
 // A known canonical key with an out-of-range rank reaches the server-owned
 // talent gate (unknown keys are rejected locally above). Failed imports
 // preserve full same-class and cross-class builds and their dirty guards.
 const unknown=structuredClone(saved.data);serverTalentRejection.mutate(unknown);
 const unknownFile=path.join(reportDir,'rejected-server-talent.json');fs.writeFileSync(unknownFile,JSON.stringify(unknown));
 async function rejectedTalent(prior,label){
  await beginImportObservation();const before=await sequence();await selectFile('#importBuild','#file',unknownFile);
  await until(rejectionExpression(serverTalentRejection,before));
  assert.equal(await text('#status'),serverTalentRejection.error,label+' exact server rejection');
  assert.deepEqual((await exportBuild(label+'-rollback')).data,prior.data,label+' full rollback');checks++;
 }
 await click('[data-view=encounter]');await setInput('#duration',32);const sameDirty=await exportBuild('same-class-dirty');await rejectedTalent(sameDirty,'same-class');
 await setInput('#spec','elemental');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.deepEqual((await exportBuild('same-dirty-guard')).data,sameDirty.data);
 await setInput('#classKey','mage');await until("document.querySelector('#classKey').value==='mage' && /Class defaults loaded/.test(document.querySelector('#status').textContent)");
 await setInput('#duration',33);const mageDirty=await exportBuild('cross-class-dirty');await rejectedTalent(mageDirty,'cross-class');
 await setInput('#spec','fire');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.deepEqual((await exportBuild('cross-dirty-guard')).data,mageDirty.data);
 await importBuild(saved.file);
 const otherRace=await evaluate("[...document.querySelector('#race').options].find(x=>x.value!==document.querySelector('#race').value).value");
 start=await sequence();await setInput('#race',otherRace);await ready(start);await assertIdentity();
 const raceBuild=await exportBuild('changed-race');assert.equal(raceBuild.data.race,otherRace);
 await importBuild(raceBuild.file);assert.deepEqual((await exportBuild('changed-race-roundtrip')).data,raceBuild.data);
 await importBuild(saved.file);
 await click('[data-view=rotation]');start=await sequence();await click('#enhancementPreviewRotation');
 await until(`window.__enhancementHttp.some(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview'&&x.request?.specialization==='enhancement'&&x.status===200)&&document.querySelector('#enhancementRotationEntries').textContent===window.__enhancementHttp.filter(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview').at(-1).response.entries.map(x=>x.abilityKey.replace(/-/g,' ').replace(/\\b\\w/g,c=>c.toUpperCase())).join(' > ')`);
 const rotation=await evaluate(`window.__enhancementHttp.filter(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview').at(-1)`);
 assert.deepEqual(rotation.response.entries.map(x=>x.abilityKey),a.data.resolvedCharacter.rotation.entries.map(x=>x.abilityKey));
 // A dirty-build switch is interrupted and cancelled twice; neither mutates it.
 await click('[data-view=encounter]');await setInput('#duration',31);const dirty=await exportBuild('dirty');
 for(let i=0;i<2;i++){await setInput('#spec','elemental');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.deepEqual((await exportBuild('cancel-preserved-'+i)).data,dirty.data);}
 await importBuild(saved.file);
 // Rapid newer selections race with old network responses. Final state must be
 // backed by an Enhancement request begun after the final selection, not stale text.
 start=await sequence();await evaluate("(()=>{const s=document.querySelector('#spec');for(const v of ['elemental','enhancement','elemental','enhancement']){s.value=v;s.dispatchEvent(new Event('change',{bubbles:true}));}})()");
 await ready(start);await assertIdentity();
 const switched=await exportBuild('after-fast-switch');assert.equal(switched.data.specialization,'enhancement');assert.deepEqual(switched.data.talents.ranks,preset.ranks);
 await importBuild(saved.file);const restored=await run('enhancement-restored');assert.deepEqual(restored.data.combatSummary,a.data.combatSummary);await parity(restored.data);
 await click('[data-view=encounter]');start=await sequence();await setInput('#enhancementWeaponMaxDamage',280);await ready(start,{weaponMaxDamage:280});
 const weapon=await run('enhancement-weapon-only');await parity(weapon.data);assert.notEqual(weapon.data.combatSummary.damageDone,a.data.combatSummary.damageDone);
 await importReport('A',a.file);await importReport('B',weapon.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 const weaponComparison=compareReports(a.data,weapon.data);assert.equal(weaponComparison.comparable,true);assert.notEqual(weaponComparison.dpsDelta,0);
 assert.match(await text('#compareChanges'),/weapon|hand/i);await screenshot('enhancement-weapon-comparison-desktop','.comparison-workspace');
 await importBuild(saved.file);
 await click('[data-view=encounter]');start=await sequence();await setInput('#enhancementTargetArmor',0);await ready(start,{targetArmor:0});
 const b=await run('enhancement-zero-armor');await parity(b.data);assert.notEqual(b.data.combatSummary.damageDone,a.data.combatSummary.damageDone);
 await importReport('A',a.file);await importReport('B',b.file);assert.match(await text('#compareStatus'),/Not directly comparable/);assert.match(await text('#compareWarnings'),/armor/i);
 await screenshot('enhancement-comparison-desktop','.comparison-workspace');
 // Elemental modifiers are real selected state. Cancellation preserves them;
 // only a clearly explained confirmed Enhancement preset load may clear them.
 await importBuild(saved.file);start=await sequence();await setInput('#spec','elemental');
 await until(`document.querySelector('#spec').value==='elemental' && /Class defaults loaded/.test(document.querySelector('#status').textContent) && document.querySelector('#statStatus').textContent==='Up to date' && window.__enhancementHttp.some(x=>x.sequence>${start}&&x.url==='/api/forever/character/preview-elemental'&&x.status===200)`);
 await click('[data-view=effects]');start=await sequence();await click('[data-modifier-key="arcane-intellect"]');
 await until(`document.querySelector('[data-modifier-key="arcane-intellect"]').getAttribute('aria-pressed')==='true'&&window.__enhancementHttp.some(x=>x.sequence>${start}&&x.url==='/api/forever/character/preview-elemental'&&x.status===200&&x.request?.externalModifiers?.buffs?.includes('arcane-intellect'))&&document.querySelector('#statStatus').textContent==='Up to date'`);
 const elementalBuff=await exportBuild('elemental-buffed-before-switch');assert.ok(elementalBuff.data.modifiers.buffs.includes('arcane-intellect'));
 await setInput('#spec','enhancement');await until("document.querySelector('#presetDialog').open");assert.match(await text('#presetDialog'),/clear.*modifier|modifier.*clear/i);await click('#cancelPreset');
 assert.deepEqual((await exportBuild('elemental-buffed-cancel')).data,elementalBuff.data,'Cancelled Enhancement transition preserves complete Elemental build');
 await setInput('#spec','enhancement');await until("document.querySelector('#presetDialog').open");start=await sequence();await click('#confirmPreset');await ready(start);await assertIdentity();
 const cleanEnhancement=await exportBuild('enhancement-confirmed-cleared');for(const key of ['buffs','consumables','targetDebuffs'])assert.deepEqual(cleanEnhancement.data.modifiers[key],[],'Confirmed preset clears '+key);
 await click('[data-view=encounter]');await setInput('#duration',30);const clearedRun=await run('enhancement-after-buffed-elemental');await parity(clearedRun.data);for(const key of ['buffs','consumables','targetDebuffs'])assert.deepEqual(clearedRun.data.build.modifiers[key],[]);
 await command('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 await assertReadableLayout('results','mobile-results');await screenshot('enhancement-results-mobile','#resultWarnings');
 await assertReadableLayout('encounter','mobile-encounter');await screenshot('enhancement-encounter-mobile','#enhancementSettings');
 assert.deepEqual(browserErrors,[]);
 fs.writeFileSync(path.join(reportDir,'http-observations.json'),JSON.stringify(await evaluate('window.__enhancementHttp'),null,2));
 fs.writeFileSync(path.join(reportDir,'causal-gate-receipts.json'),JSON.stringify(gateReceipts,null,2));
 fs.writeFileSync(path.join(reportDir,'acceptance-summary.json'),JSON.stringify({status:'passed',sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',checks,reports:reportFiles,screenshots:screenshotDir},null,2));
 console.log('Enhancement actual-browser acceptance passed: '+checks+' gates.');
}
main().catch(async error=>{console.error(error);process.exitCode=1;try{await screenshot('enhancement-browser-failure');fs.writeFileSync(path.join(reportDir,'failure-observations.json'),JSON.stringify({error:String(error),dom:await evaluate('document.body.innerText'),http:await evaluate('window.__enhancementHttp')},null,2));}catch{}}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(400);const relative=path.relative(path.resolve(os.tmpdir()),path.resolve(profile));if(relative&&!relative.startsWith('..')&&!path.isAbsolute(relative))fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});});
