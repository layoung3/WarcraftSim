#!/usr/bin/env node
"use strict";
// End-to-end acceptance against an already running local API. Every successful
// executed report is downloaded from a real UI simulation. Report-admission
// mutations below are explicitly synthetic and are never treated as executions.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const {compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const {assertSoloDpsIdentity}=require("./solo-dps-browser-identity.cjs");
const {createBrowserClick}=require("./solo-dps-browser-click.cjs");
const {createRegionCapture}=require("./solo-dps-rogue-capture.cjs");

const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp174-full-reports"),"rogue-browser");
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||path.join(os.tmpdir(),"cp174-ui-captures"));
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-rogue-browser-"));
const origin=new URL(BASE);assert.ok(origin.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(origin.hostname));
assert.ok(!origin.username&&!origin.password&&!origin.search&&!origin.hash);
const port=Number(process.env.CP175_ROGUE_CDP_PORT||19361);
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
const click=createBrowserClick({evaluate,command});
async function keyPress(key,code,windowsVirtualKeyCode,modifiers=0){
 for(const type of ['keyDown','keyUp'])await command('Input.dispatchKeyEvent',{type,key,code,windowsVirtualKeyCode,modifiers});
}
async function setInput(selector,value){
 const target=await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});return {tag:e.tagName,index:e.tagName==='SELECT'?[...e.options].findIndex(x=>x.value===${JSON.stringify(String(value))}):-1};})()`);
 await click(selector);
 if(target.tag==='SELECT'){
  assert.ok(target.index>=0,'Requested native select option must exist: '+selector+' '+value);
  await keyPress('Home','Home',36);
  for(let i=0;i<target.index;i++)await keyPress('ArrowDown','ArrowDown',40);
  await keyPress('Enter','Enter',13);
 }else{
  await keyPress('a','KeyA',65,2);await command('Input.insertText',{text:String(value)});
  await keyPress('Tab','Tab',9);
 }
 const selected=await evaluate(`({value:document.querySelector(${JSON.stringify(selector)}).value,confirmation:document.querySelector('#presetDialog').open})`);
 // A dirty-preset guard restores the old selection while asking for confirmation.
 if(!(selector==='#spec'&&selected.confirmation))assert.equal(selected.value,String(value),'Trusted native input must reach '+selector);
}
async function setRogueSetting(field,value){
 const selector='#rogue'+field[0].toUpperCase()+field.slice(1);
 const current=await evaluate(`document.querySelector(${JSON.stringify(selector)}).value`);
 assert.notEqual(current,String(value),'Fresh Rogue preview requires a changed value: '+field);
 const after=await sequence();await setInput(selector,value);await ready(after,{[field]:value});
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
async function presetReady(classKey,specialization,after){
 await until(`document.querySelector('#classKey').value===${JSON.stringify(classKey)} && document.querySelector('#spec').value===${JSON.stringify(specialization)} && document.querySelector('#status').dataset.buildState==='ready' && !document.querySelector('#run').disabled && !document.querySelector('#exportBuild').disabled && /Class defaults loaded/.test(document.querySelector('#status').textContent) && window.__rogueHttp.some(x=>x.sequence>${after}&&x.url===${JSON.stringify('/api/forever/talents/'+classKey+'/'+specialization+'/default-itemized')}&&x.status===200)`);
}
async function importBuild(file){
 await beginImportObservation();
 await selectFile("#importBuild","#file",file);
 await until("window.__fileChanged && window.__statusChanged && document.querySelector('#file').value==='' && document.querySelector('#status').textContent==='Build imported.' && document.querySelector('#status').dataset.buildState==='ready'");
 await assertIdentity();
}
async function assertIdentity(){
 const identity=await evaluate("({classKey:document.querySelector('#classKey').value,specialization:document.querySelector('#spec').value,classLabel:document.querySelector('#classKey').selectedOptions[0].textContent,race:document.querySelector('#race').selectedOptions[0].textContent,displayedTitle:document.querySelector('#characterTitle').textContent,subtitle:document.querySelector('#characterSubtitle').textContent})");
 assertSoloDpsIdentity(identity,{classKey:'rogue',specialization:'assassination'});
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
 assert.equal(await evaluate("document.querySelector('#status').dataset.buildState"),'ready','Run requires the complete prepared build');
 const completed=await evaluate("window.__comparisonCompleted||0"),beforeSequence=await sequence();
 const settings=await evaluate("[...document.querySelectorAll('#classKey,#spec,#race,[id^=rogue]')].filter(x=>['INPUT','SELECT'].includes(x.tagName)).map(x=>({id:x.id,value:x.value,checked:x.checked,disabled:x.disabled}))");
 await click("#run");
 assert.equal(await evaluate("document.querySelector('#run').disabled"),true,'Run locks before asynchronous preparation');
 // All editable settings are locked while the actual simulation is pending.
 const locks=await evaluate("[...document.querySelectorAll('#classKey,#spec,#race,#rogueSettings input,#rogueSettings select')].map(x=>({id:x.id,disabled:x.disabled}))");
 assert.ok(locks.every(x=>x.disabled),'Pending run locks all Rogue controls: '+JSON.stringify(locks));

 await until(`(window.__comparisonCompleted||0)>${completed} && !document.querySelector('#run').disabled && /Simulation completed|Warrior completed/.test(document.querySelector('#status').textContent)`,60000);
 const simulationRequests=await evaluate(`window.__rogueHttp.filter(x=>x.sequence>${beforeSequence}&&x.url==='/api/forever/character/simulate-level60')`);
 assert.equal(simulationRequests.length,1,'One native run click must produce exactly one simulation');
 assert.deepEqual(await evaluate("[...document.querySelectorAll('#classKey,#spec,#race,[id^=rogue]')].filter(x=>['INPUT','SELECT'].includes(x.tagName)).map(x=>({id:x.id,value:x.value,checked:x.checked,disabled:x.disabled}))"),settings,'Pending run preserves selected settings and restores availability');
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
// Reuse completed reports. Mutations are explicitly synthetic admission probes,
// not simulations; native file selection still drives the real import handler.
async function reportAdmissionCases(current){
 const helpers=require('./solo-dps-assassination-report.test.cjs');
 const beforeBuild=await exportBuild('report-admission-before');
 const beforeReport=await downloaded('warcraftsim-simulation-report.json','#exportResults','report-admission-current-before.json');
 const start=await sequence(),receipts=[];
 const state=()=>evaluate("({labels:['A','B'].map(s=>document.querySelector('#compareLabel'+s).textContent),hidden:document.querySelector('#compareDetails').hidden,rows:[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent)),warnings:document.querySelector('#compareWarnings').textContent,changes:document.querySelector('#compareChanges').textContent})");
 const results=()=>evaluate("({metrics:document.querySelector('#metrics').textContent,abilities:document.querySelector('#abilities').textContent,warnings:document.querySelector('#resultWarningList').textContent,replayDisabled:document.querySelector('#replayResult').disabled,exportDisabled:document.querySelector('#exportResults').disabled})");
 const currentResults=await results();
 const candidate=(name,r)=>{const file=path.join(reportDir,'report-admission-'+name+'-candidate.json');fs.writeFileSync(file,typeof r==='string'?r:JSON.stringify(r));return file;};
 const invalid=structuredClone(current.data);helpers.maps(invalid)[2]['dirty-deeds']=0;
 const invalidFile=candidate('typed-unsupported-zero',invalid);
 async function reject(slot,file,reason,label){
  const before=await state();await beginImportObservation('#compareFile','#compareStatus');await selectFile('#compareImport'+slot,'#compareFile',file);
  await until("window.__fileChanged&&window.__statusChanged&&document.querySelector('#compareFile').value===''");
  const status=await text('#compareStatus');assert.match(status,/Report import rejected:/);assert.match(status,new RegExp('Slot '+slot+' was not changed'));if(reason)assert.match(status,reason);
  assert.deepEqual(await state(),before,label+' preserves prior labels/rows/deltas/warnings/changes');assert.deepEqual(await results(),currentResults,label+' preserves current results/replay');
  receipts.push({label,slot,status,before,after:await state()});checks++;
 }
 assert.deepEqual((await state()).labels,['No report selected','No report selected']);
 await reject('A',invalidFile,/unsupported talent key: dirty-deeds/i,'empty-slots');
 const fixture=name=>path.join(__dirname,'fixtures/assassination-reports',name+'-report.json');
 await importReport('A',fixture('rogue-manual'));await reject('B',invalidFile,/unsupported talent key/i,'one-slot');
 await importReport('B',fixture('legal-36'));assert.match(await text('#compareStatus'),/Encounter settings match/);
 await importReport('A',fixture('legacy-tuning'));await importReport('A',fixture('rogue-manual'));
 await importReport('B',fixture('rogue-manual'));await importReport('A',fixture('legal-36'));
 await importReport('A',fixture('rogue-manual'));await importReport('B',fixture('legal-36'));
 for(const [key,rank] of [['dirty-deeds',0],['dirty-deeds',1],['dirty-deeds',2],['cutthroat',0],['cutthroat',1],['cutthroat',5]]){
  const r=helpers.allocation(structuredClone(current.data),{...current.data.build.talents.ranks,[key]:rank});
  await reject(rank%2?'B':'A',candidate(key+'-'+rank,r),new RegExp('unsupported talent key: '+key),'coherent-'+key+'-'+rank);
 }
 for(const slot of ['A','B'])for(const [name,edit,reason] of [
  ['saved-null',r=>r.build.talents.ranks=null,/rank maps.*required/i],['full-array',r=>r.originalSimulationRequest.itemizedTalentRanks=[],/rank maps.*required/i],['typed-string',r=>r.originalSimulationRequest.rogue.talents.ranks.malice='5',/invalid rank/i],['full-zero',r=>r.originalSimulationRequest.itemizedTalentRanks.precision=0,/unsupported talent key/i],['zero-presence',r=>{helpers.allocation(r,{});r.build.talents.ranks.malice=0;},/rank maps.*match/i]
 ]){const r=structuredClone(current.data);edit(r);await reject(slot,candidate(slot+'-'+name,r),reason,slot+'-'+name);}
 const unsafe=structuredClone(current.data);helpers.maps(unsafe)[0]['<img src=x onerror=alert(1)>']=0;
 await reject('A',candidate('plain-text',unsafe),/unsupported talent key.*<img/,'plain-text-error');
 assert.equal(await evaluate("document.querySelector('#compareStatus').querySelectorAll('img,script').length"),0,'Imported error key is text, not HTML');
 await reject('A',invalidFile,/unsupported talent key/i,'repeated-rejection');await reject('A',invalidFile,/unsupported talent key/i,'same-file-rejection');
 await importReport('A',current.file);await importReport('B',current.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 const zero=helpers.allocation(structuredClone(current.data),{malice:0}),empty=helpers.allocation(structuredClone(current.data),{});
 await importReport('A',candidate('empty-positive',empty));await importReport('B',candidate('supported-zero-positive',zero));
 assert.match(await text('#compareStatus'),/Encounter settings match/);assert.match(await text('#compareChanges'),/zero-key presence/);
 const incompatible=structuredClone(current.data);incompatible.build.encounter.seed='123';incompatible.originalSimulationRequest.rogue.seed=123;
 await importReport('A',current.file);await importReport('B',candidate('different-seed-positive',incompatible));assert.match(await text('#compareStatus'),/Not directly comparable/);
 await reject('B',invalidFile,/unsupported talent key/i,'incompatible-pair');
 await reject('A',candidate('parse-failure','{'),null,'parse-failure');
 // Inject only the File.text completion boundary. The native file-selection,
 // application change listener, inspection and slot transaction remain real.
 async function holdRead(fail){
  await evaluate(`(()=>{window.__reportRead={armed:true,held:false,released:false,fail:${JSON.stringify(fail)}};window.__reportOriginalText??=File.prototype.text;File.prototype.text=async function(){const h=window.__reportRead;if(!h.armed)return window.__reportOriginalText.call(this);h.armed=false;const value=await window.__reportOriginalText.call(this);h.held=true;return new Promise((resolve,reject)=>{h.release=()=>{h.released=true;h.fail?reject(Error('Injected file read failure')):resolve(value);};});};})()`);
 }
 async function beginHeld(slot,file,fail){await holdRead(fail);await beginImportObservation('#compareFile','#compareStatus');await selectFile('#compareImport'+slot,'#compareFile',file);await until('window.__reportRead.held');}
 async function releaseHeld(){await evaluate('(async()=>{window.__reportRead.release();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));})()');assert.equal(await evaluate('window.__reportRead.released'),true);}
 try{
  const beforeReadFailure=await state();await beginHeld('B',current.file,true);await releaseHeld();await until("document.querySelector('#compareFile').value===''");assert.match(await text('#compareStatus'),/Injected file read failure.*Slot B was not changed/);assert.deepEqual(await state(),beforeReadFailure);
  const beforeCancel=await state();await click('#compareImportA');
  const doc=await command('DOM.getDocument'),{nodeId}=await command('DOM.querySelector',{nodeId:doc.root.nodeId,selector:'#compareFile'});await command('DOM.setFileInputFiles',{nodeId,files:[]});
  // Chromium may not emit change for an already-empty chooser. Exercise the
  // no-file branch explicitly, without claiming it is an OS-dialog cancel.
  await evaluate("document.querySelector('#compareFile').dispatchEvent(new Event('change',{bubbles:true}))");assert.deepEqual(await state(),beforeCancel);
  for(const fail of [false,true])for(const newer of ['import','use']){
   await beginHeld('A',fixture('legal-36'),fail);
   if(newer==='import')await importReport('B',current.file);else await click('#compareUseB');
   const before=await state(),status=await text('#compareStatus');await releaseHeld();assert.deepEqual(await state(),before);assert.equal(await text('#compareStatus'),status);receipts.push({label:'stale-'+(fail?'failure':'success')+'-after-'+newer,before,after:await state()});checks++;
  }
  // Merely leaving Results does not retarget or cancel the pending A read.
  await beginHeld('A',candidate('view-navigation-positive',incompatible),false);const before=await state();
  for(let i=0;i<2;i++){await click('[data-view=talents]');await click('[data-view=encounter]');await click('[data-view=results]');}
  await releaseHeld();await until("document.querySelector('#compareFile').value===''");const after=await state();assert.notEqual(after.labels[0],before.labels[0]);assert.equal(after.labels[1],before.labels[1]);assert.equal(await evaluate("document.querySelector('[data-view=results]').classList.contains('active')"),true);
  receipts.push({label:'pending-A-import-survives-view-switches',before,after});checks++;
 }finally{await evaluate('if(window.__reportOriginalText)File.prototype.text=window.__reportOriginalText');}
 await importReport('A',current.file);await importReport('B',current.file);await reject('A',invalidFile,/unsupported talent key/i,'desktop-readable-error');
 async function readableError(label){
  const layout=await evaluate("(()=>{const e=document.querySelector('#compareStatus'),r=e.getBoundingClientRect();return {text:e.textContent,left:r.left,right:r.right,clientWidth:e.clientWidth,scrollWidth:e.scrollWidth,clientHeight:e.clientHeight,scrollHeight:e.scrollHeight,viewport:document.documentElement.clientWidth,width:document.documentElement.scrollWidth};})()");
  assert.ok(layout.text.includes('Slot A was not changed'));assert.ok(layout.width<=layout.viewport+1&&layout.left>=-1&&layout.right<=layout.viewport+1&&layout.scrollWidth<=layout.clientWidth+1&&layout.scrollHeight<=layout.clientHeight+1,label+' error text must remain readable: '+JSON.stringify(layout));
  gateReceipts.push({gate:'report-import-readable-error',label,...layout});await captureRegion('rogue-report-admission-'+label,'#compareStatus');
 }
 await readableError('desktop');await captureRegion('rogue-report-admission-preserved-results-desktop','.comparison-workspace');
 await captureViewport(390,844,true);await readableError('mobile');await captureRegion('rogue-report-admission-preserved-results-mobile','.comparison-workspace');await captureViewport(1440,1000,false);
 await importReport('A',current.file);await importReport('B',current.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 assert.deepEqual((await exportBuild('report-admission-after')).data,beforeBuild.data,'Report-only imports never change build controls/export');
 assert.deepEqual((await downloaded('warcraftsim-simulation-report.json','#exportResults','report-admission-current-after.json')).data,beforeReport.data,'Report imports never rewrite the current exported result');
 assert.deepEqual(await results(),currentResults);
 const requests=await evaluate(`window.__rogueHttp.filter(x=>x.sequence>${start})`);assert.deepEqual(requests,[],'Report-only import tests cause no API/preview/simulation request');
 gateReceipts.push({gate:'historical-assassination-report-admission',cases:receipts.length,receipts,noNetworkRequests:true,unchangedBuildAndCurrentReport:true});
}

async function assertReadableLayout(view,label){
 await click('[data-view='+view+']');
 const layout=await evaluate(`(()=>{
  const selector=${JSON.stringify(view==='results'?'#resultWarningList > div':'#classCapabilityNotes,#rogueSettings p,#rogueSettings label')};
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
const captureRegion=createRegionCapture({evaluate,command,
 save:(filename,data)=>fs.writeFileSync(path.join(screenshotDir,filename),Buffer.from(data,'base64')),receipts:gateReceipts});
async function captureViewport(width,height,mobile){
 await command('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile});
}
const rows=()=>evaluate("[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent))");
async function ready(after,fields={}){
 // The latest initiated Rogue preview must have finished, match the
 // currently selected identity and controls, and be reflected in the sidebar.
 await until(`(()=>{
  const receipt=window.__rogueHttp.find(x=>x.sequence===window.__latestRoguePreview);
  if(document.querySelector('#status').dataset.buildState!=='ready'||document.querySelector('#run').disabled||document.querySelector('#exportBuild').disabled||!receipt||receipt.sequence<=${after}||receipt.status!==200||document.querySelector('#classKey').value!=='rogue'||document.querySelector('#spec').value!=='assassination'||document.querySelector('#statStatus').textContent!=='Up to date')return false;
  if(receipt.request?.character?.race!==document.querySelector('#race').value)return false;
  if(!Object.entries(${JSON.stringify(fields)}).every(([key,value])=>(receipt.request[key]??receipt.request.tuning?.[key])===value))return false;
  for(const input of document.querySelectorAll('[id^=rogue]')){
   const suffix=input.id.slice('rogue'.length),key=suffix[0]?.toLowerCase()+suffix.slice(1);
   if(!['INPUT','SELECT'].includes(input.tagName))continue;
   if(!Object.hasOwn(receipt.request,key)&&!Object.hasOwn(receipt.request.tuning,key))throw Error('Unbound Rogue setting '+key);
   const value=input.type==='checkbox'?input.checked:input.type==='number'?Number(input.value):input.value;
   if((receipt.request[key]??receipt.request.tuning?.[key])!==value)return false;
  }
  const rendered=[...document.querySelectorAll('#stats > div')].map(x=>[x.querySelector('span')?.textContent.toLowerCase(),x.querySelector('strong')?.textContent]);
  const stats=receipt.response.effectiveStats.values||receipt.response.effectiveStats;
  return rendered.some(([label,text])=>label==='starting energy'&&text===Number(receipt.response.startingEnergy).toLocaleString('en-US',{maximumFractionDigits:2}))&&Object.entries(stats).filter(([key,value])=>value!==null&&typeof value!=='object').every(([key,value])=>rendered.some(([label,text])=>label===(key==='energy'?'maximum energy':key==='health'?'maximum player health':key.replace(/-/g,' '))&&text===Number(value).toLocaleString('en-US',{minimumFractionDigits:key.includes('percent')?1:0,maximumFractionDigits:key.includes('percent')?1:2})));
 })()`);
 gateReceipts.push({gate:'latest-selected-build-preview',after,fields,sequence:await evaluate('window.__latestRoguePreview'),at:await evaluate('performance.now()')});
}
const sequence=()=>evaluate('window.__rogueSequence');
async function holdResponse(url,prefix=false){
 await evaluate(`window.__rogueHold={url:${JSON.stringify(url)},prefix:${JSON.stringify(prefix)},armed:true,held:false,released:false}`);
}
async function releaseResponse(label,key='__rogueHold'){
 const expression='window['+JSON.stringify(key)+']';
 const held=await evaluate(`({held:${expression}.held,sequence:${expression}.sequence,url:${expression}.url})`);assert.equal(held.held,true);
 await evaluate(`(async()=>{${expression}.release();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));})()`);
 assert.equal(await evaluate(expression+'.released'),true);gateReceipts.push({gate:label,...held});
}
async function parity(report){
 const p=report.resolvedCharacter,s=report.rawServerResponse.sim;
 for(const key of ['maximumEnergy','maximumHealth'])assert.equal(p[key],s[key],key+' actual preview/execution');
 assert.equal(p.startingEnergy,s.run.summary.actorSummaries.rogue.startingResources.energy);
 for(const key of ['maximumEnergy','endingEnergy','maximumHealth','endingHealth'])assert.equal(report.combatSummary[key],s[key],key+' export summary');
 assert.equal(report.combatSummary.startingEnergy,p.startingEnergy);assert.equal(report.combatSummary.ownerDamage,s.damageDone);
 assert.equal(p.attackPower,(p.effectiveStats.values||p.effectiveStats)['attack-power']);
 assert.ok(p.rotation.entries.length>0);
 assert.match(p.statPreviewScope,/display-only.*not-current-client-certified/);
 assert.equal(report.originalSimulationRequest.specialization,'assassination');
 assert.equal(report.originalSimulationRequest.itemizedTalentMode,true);
 const actual=await evaluate('window.__rogueCompletedReports.at(-1)');
 assert.deepEqual(report,actual,'Complete export equals the actual captured completion document');
 const response=await evaluate("window.__rogueHttp.filter(x=>x.url==='/api/forever/character/simulate-level60').at(-1).response");
 assert.deepEqual(report.rawServerResponse,response,'Export preserves complete real server response');
 checks++;
}
async function rejectImport(file,prior,label,server=false,error){
 await beginImportObservation();const before=await sequence();await selectFile('#importBuild','#file',file);
 await until(`window.__fileChanged&&window.__statusChanged&&document.querySelector('#file').value===''&&document.querySelector('#status').textContent!=='Build imported.'&&document.querySelector('#status').dataset.buildState==='ready'${server?`&&window.__rogueHttp.some(x=>x.sequence>${before}&&x.url==='/api/forever/talents/rogue/assassination/validate-itemized'&&x.status===400&&x.request?.ranks?.malice===6)`:''}`);
 if(error)assert.equal(await text('#status'),error,label+' exact rejection');
 if(server)assert.equal(await text('#status'),'Unrecognized talent or invalid rank: malice.',label+' exact server rejection');
 assert.deepEqual((await exportBuild(label+'-rollback')).data,prior.data,label+' full atomic rollback');checks++;
}
async function assertEnergyCards(report,label){
 const receipt=await evaluate("window.__rogueHttp.filter(x=>x.url==='/api/forever/character/simulate-level60').at(-1)");
 assert.equal(receipt.status,200);assert.deepEqual(report.rawServerResponse,receipt.response,'Energy cards must use this completed server response');
 const sim=receipt.response.sim;
 const resources={MaximumEnergy:sim.maximumEnergy,StartingEnergy:sim.run.summary.actorSummaries.rogue.startingResources.energy,EndingEnergy:sim.endingEnergy};
 for(const [name,value] of Object.entries(resources))assert.ok(typeof value==='number'&&Number.isFinite(value),name+' actual server resource is numeric');
 const rendered=await evaluate("[...document.querySelectorAll('#metrics > div')].map(metric=>[(metric.querySelector('span')?.textContent||metric.firstChild?.textContent||'').replace(/:\\s*$/,''),metric.querySelector('strong')?.textContent]).filter(([name])=>['MaximumEnergy','StartingEnergy','EndingEnergy'].includes(name))");
 const format=value=>value.toLocaleString('en-US',{maximumFractionDigits:2});
 assert.deepEqual(rendered,Object.entries(resources).map(([name,value])=>[name,format(value)]),'Energy cards match actual server resources at sidebar precision, including zero');
 const sidebar=await evaluate("[...document.querySelectorAll('#stats > div')].map(row=>[row.querySelector('span')?.textContent,row.querySelector('strong')?.textContent]).filter(([name])=>['Maximum Energy','Starting Energy'].includes(name))");
 for(const [name,value] of [['Maximum Energy',resources.MaximumEnergy],['Starting Energy',resources.StartingEnergy]])assert.deepEqual(sidebar.filter(([key])=>key===name),[[name,format(value)]],name+' sidebar/card parity');
 gateReceipts.push({gate:'rendered-energy-parity',label,sequence:receipt.sequence,resources,rendered,sidebar});checks++;
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
 await command('Page.addScriptToEvaluateOnNewDocument',{source:"window.__rogueHttp=[];window.__rogueSequence=0;const nativeFetch=window.fetch;window.fetch=async(...args)=>{const sequence=++window.__rogueSequence;if(String(args[0])==='/api/forever/character/preview-rogue')window.__latestRoguePreview=sequence;const response=await nativeFetch(...args);if(String(args[0]).startsWith('/api/forever/')){const receipt={sequence,url:String(args[0]),request:args[1]?.body?JSON.parse(args[1].body):null,status:response.status};try{receipt.response=await response.clone().json();}catch{}window.__rogueHttp.push(receipt);}const hold=window.__rogueHold;if(hold?.armed&&(hold.prefix?String(args[0]).startsWith(hold.url):String(args[0])===hold.url)){hold.armed=false;hold.held=true;hold.sequence=sequence;await new Promise(resolve=>hold.release=resolve);hold.released=true;}return response;};window.__comparisonCompleted=0;window.__rogueCompletedReports=[];document.addEventListener('warcraftsim:simulation-complete',event=>{window.__comparisonCompleted++;window.__rogueCompletedReports.push(JSON.parse(JSON.stringify(event.detail)));});"});
 await command('Page.setDownloadBehavior',{behavior:'allow',downloadPath:reportDir});
 await command('Page.setInterceptFileChooserDialog',{enabled:true});
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command('Page.navigate',{url:BASE+'/solo-dps.html'});
 await until("document.querySelector('#status')?.dataset.buildState==='ready' && document.querySelector('#status').textContent.includes('Class defaults loaded')");
 await evaluate("(()=>{const original=window.WarcraftSimComparison.compareReports;window.__rogueComparisons=[];window.WarcraftSimComparison.compareReports=(...args)=>{const value=original(...args);window.__rogueComparisons.push(JSON.parse(JSON.stringify(value)));return value;};})()");
 let start=await sequence();await setInput('#classKey','rogue');await presetReady('rogue','assassination',start);
 if(!await evaluate("document.querySelector('#provisional').checked"))await click('#provisional');
 await ready(start);await assertIdentity();
 assert.deepEqual(await evaluate("[...document.querySelector('#spec').options].map(x=>x.value)"),['assassination','combat']);
 const rawCapability=await evaluate("window.__rogueHttp.filter(x=>x.url==='/api/forever/shared/capabilities/rogue?specialization=assassination').at(-1).response");
 assert.equal(rawCapability.knownRestrictions.length,6);assert.ok(rawCapability.knownRestrictions[0].endsWith('Combat and Subtlety are unavailable.'),'Legacy raw capability contract is preserved');
 const displayedRestrictions=rawCapability.knownRestrictions.map((note,index)=>index===0?note.replace('Assassination only:','Selected Assassination preset:').replace('Combat and Subtlety are unavailable.','Combat is available as a separate partial historical-provisional preset; Subtlety remains unavailable.'):note);
 assert.ok((await text('#classCapabilityNotes')).startsWith(displayedRestrictions.join(' · ')),'Selected preset display retains all raw limitations and correct separate Combat/Subtlety availability');
 const preset=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json'),'utf8'));
 const baseline=await exportBuild('rogue-baseline');
 assert.deepEqual(baseline.data.talents.ranks,preset.ranks);assert.equal(Object.values(preset.ranks).reduce((a,b)=>a+b),38);assert.equal(Object.keys(preset.ranks).length,12);
 assert.deepEqual(Object.fromEntries(Object.entries(baseline.data.talents).filter(([k])=>k.endsWith('PointsSpent'))),{assassinationPointsSpent:38,combatPointsSpent:0,subtletyPointsSpent:0});
 await click('[data-view=talents]');assert.match(await text('#talentCoverage'),/historical|provisional/i);await screenshot('rogue-talents-desktop','#talentSummary');
 await click('[data-view=encounter]');await setInput('#duration',20);
 for(const [field,value] of Object.entries({targetArmor:2100,targetDodgePercent:3,targetParryPercent:7,rogueMaximumHealth:4900,startingEnergyPercent:25,mainHandPoison:'deadly',offHandPoison:'none',poisonChargesPerWeapon:80,mainHandWeaponDamage:180,offHandWeaponDamage:130,mainHandSpeedSeconds:1.8,offHandSpeedSeconds:1.4})){
  await setRogueSetting(field,value);
 }
 await setRogueSetting('offHandPoison','instant');
 start=await sequence();await click('#rogueUseExposeArmor');await ready(start,{useExposeArmor:true});
 const saved=await exportBuild('rogue-manual');
 assert.equal(saved.data.encounter.rogue.targetArmor,2100);assert.equal(saved.data.encounter.rogue.rogueMaximumHealth,4900);
 assert.equal(saved.data.encounter.rogue.tuning.mainHandWeaponDamage,180);
 for(const [key,value] of Object.entries({bothWeaponsDaggers:true,mainHandWeaponType:'dagger',offHandWeaponType:'dagger',useInstantPoison:true,useUtilityAbilities:false,additionalTargets:0,enableBossAttacks:false}))assert.equal(saved.data.encounter.rogue[key],value);
 await assertReadableLayout('encounter','manual-desktop-encounter');
 await captureRegion('rogue-controls-desktop','#rogueSettings .settings-grid');
 await captureRegion('rogue-capability-notes-desktop','#classCapabilityNotes');
 const a=await run('rogue-manual');await parity(a.data);await screenshot('rogue-results-desktop','#resultWarnings');
 await assertEnergyCards(a.data,'rogue-manual-desktop');
 await captureRegion('rogue-metrics-desktop','#metrics');
 await captureRegion('rogue-abilities-desktop','#abilities');
 assert.deepEqual(a.data.combatSummary.targetSemantics,{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:2100,targetDodgePercent:3,targetParryPercent:7,targetCount:1,initialHealthPercent:100});
 assert.equal(a.data.resolvedCharacter.maximumEnergy,110);assert.equal(a.data.resolvedCharacter.startingEnergy,27.5);
 assert.notEqual(a.data.resolvedCharacter.maximumHealth,4900,'Supplied baseline Health takes precedence over manual fallback');
 await replay('Deterministic replay matched');
 const repeat=await run('rogue-repeat');await parity(repeat.data);assert.deepEqual(repeat.data.combatSummary,a.data.combatSummary);
 await reportAdmissionCases(repeat);
 await importReport('A',a.file);await importReport('B',repeat.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 await importBuild(saved.file);assert.deepEqual((await exportBuild('rogue-roundtrip')).data,saved.data);
 const lower=structuredClone(saved.data);delete lower.talents.ranks['improved-expose-armor'];lower.talents.assassinationPointsSpent=36;
 const lowerFile=path.join(reportDir,'legal-36-build.json');fs.writeFileSync(lowerFile,JSON.stringify(lower));
 await importBuild(lowerFile);assert.deepEqual((await exportBuild('legal-36-roundtrip')).data.talents,lower.talents);
 await click('[data-view=talents]');assert.match(await text('#talentSummary'),/36/);assert.match(await text('#talentSummary'),/15/);
 const lowerRun=await run('legal-36');await parity(lowerRun.data);assert.equal(Object.values(lowerRun.data.originalSimulationRequest.itemizedTalentRanks).reduce((a,b)=>a+b),36);
 assert.deepEqual(lowerRun.data.originalSimulationRequest.itemizedTalentRanks,lowerRun.data.originalSimulationRequest.rogue.talents.ranks);
 await importBuild(saved.file);
 // Explicit legacy tuning is preserved, not silently reset to newer defaults.
 const legacy=structuredClone(saved.data);legacy.encounter.rogue.tuning.improvedExposeArmorFivePointRefund=5;legacy.encounter.rogue.tuning.mutilateBonusDamagePerHand=17.25;
 const legacyFile=path.join(reportDir,'legacy-tuning-build.json');fs.writeFileSync(legacyFile,JSON.stringify(legacy));
 await importBuild(legacyFile);assert.deepEqual((await exportBuild('legacy-tuning-roundtrip')).data,legacy);
 const legacyRun=await run('legacy-tuning');await parity(legacyRun.data);assert.deepEqual(legacyRun.data.originalSimulationRequest.rogue.tuning,legacy.encounter.rogue.tuning);
 await importBuild(saved.file);
 for(const [name,mutate,error] of [
  ['missing-talents',x=>delete x.talents,'Assassination requires explicit ranked talents and three declared tree totals.'],
  ['null-talents',x=>x.talents=null,'Assassination requires explicit ranked talents and three declared tree totals.'],
  ['missing-ranks',x=>delete x.talents.ranks,'Assassination requires explicit ranked talents and three declared tree totals.'],
  ['array-ranks',x=>x.talents.ranks=[],'Assassination requires explicit ranked talents and three declared tree totals.'],
  ['missing-total',x=>delete x.talents.combatPointsSpent,'Assassination requires explicit ranked talents and three declared tree totals.'],
  ['missing-baseline-mode',x=>delete x.provisional,'Assassination baseline mode and race must be explicit.'],
  ['missing-race',x=>delete x.race,'Assassination baseline mode and race must be explicit.'],
  ...['duration','seed','iterations','targetHealth'].map(key=>['missing-'+key,x=>delete x.encounter[key],'Assassination '+key+' must be explicit and finite.']),
  ['missing-timeline',x=>delete x.encounter.timeline,'Invalid Assassination duration, seed, target Health or timeline.'],
  ['null-duration',x=>x.encounter.duration=null,'Assassination duration must be explicit and finite.'],
  ['malformed-seed',x=>x.encounter.seed='invalid','Assassination seed must be explicit and finite.'],
  ['gear',x=>x.gear.items.push({slotKey:'main-hand',itemId:19102}),'Assassination catalog equipment and enchants are unavailable.'],
  ['enchant',x=>x.gear.items.push({slotKey:'main-hand',itemId:19102,enchantId:1}),'Assassination catalog equipment and enchants are unavailable.'],
  ['buff',x=>x.modifiers.buffs.push('arcane-intellect'),'Assassination external modifiers are unavailable.'],['racial',x=>x.racialActive=true,'Assassination racial actives and combat inventory are unavailable.'],
  ['combat-items',x=>x.preparedCombatConsumables.push({key:'majorManaPotionCharges',charges:1}),'Assassination racial actives and combat inventory are unavailable.'],
  ['companions',x=>x.companion={enabled:true},'Unsupported Assassination build field: companion'],
  ['batch',x=>x.encounter.iterations=2,'Assassination supports one deterministic run only.'],['script',x=>x.encounter.soloScript.enabled=true,'Assassination generic scripts and adds are unavailable.'],
  ['adds',x=>x.encounter.rogue.additionalTargets=1,'Restricted Assassination requires additionalTargets = 0.'],['utility',x=>x.encounter.rogue.useUtilityAbilities=true,'Restricted Assassination requires useUtilityAbilities = false.'],
  ['boss-attacks',x=>x.encounter.rogue.enableBossAttacks=true,'Restricted Assassination requires enableBossAttacks = false.'],['non-dagger',x=>x.encounter.rogue.mainHandWeaponType='sword','Restricted Assassination requires mainHandWeaponType = dagger.'],
  ['priority',x=>x.encounter.customPriority=['mutilate'],'Unsupported Assassination encounter setting: customPriority'],['unknown-rank',x=>x.talents.ranks['unknown-canonical-rank']=1,'Imported build contains a talent outside the current class catalog.']
 ]){
  const invalid=structuredClone(saved.data);mutate(invalid);const file=path.join(reportDir,'rejected-'+name+'.json');fs.writeFileSync(file,JSON.stringify(invalid));
  await rejectImport(file,saved,name,false,error);
 }
 const invalid=structuredClone(saved.data);invalid.talents.ranks.malice=6;invalid.talents.assassinationPointsSpent=39;
 const invalidFile=path.join(reportDir,'server-rejected-build.json');fs.writeFileSync(invalidFile,JSON.stringify(invalid));
 await click('[data-view=encounter]');await setInput('#duration',22);const rogueDirty=await exportBuild('rogue-dirty');await rejectImport(invalidFile,rogueDirty,'rogue-dirty',true);
 // Recovered talent tiles must mutate the restored live request, not detached closures.
 await click('[data-view=talents]');
 for(const points of [37,36]){
  start=await sequence();await click('[data-talent-key="improved-expose-armor"]','right');await ready(start);
  const edited=await exportBuild('post-rollback-talent-'+points);
  assert.equal(edited.data.talents.assassinationPointsSpent,points);
  assert.equal(edited.data.talents.ranks['improved-expose-armor']||0,points-36);
  assert.equal(Object.values(edited.data.talents.ranks).reduce((a,b)=>a+b),points);
 }
 await importBuild(saved.file);
 // Invalid local drafts cannot export, but must not prevent a valid recovery import.
 await click('[data-view=encounter]');await setInput('#rogueMainHandSpeedSeconds',0);
 const draftExpression="[...document.querySelectorAll('#classKey,#spec,#race,#duration,#rogueSettings input,#rogueSettings select')].map(x=>({id:x.id,value:x.value,checked:x.checked}))";
 const invalidDraft=await evaluate(draftExpression);
 await beginImportObservation();const invalidBefore=await sequence();await selectFile('#importBuild','#file',invalidFile);
 await until(`window.__fileChanged&&window.__statusChanged&&document.querySelector('#file').value===''&&document.querySelector('#status').textContent==='Unrecognized talent or invalid rank: malice.'&&window.__rogueHttp.some(x=>x.sequence>${invalidBefore}&&x.url==='/api/forever/talents/rogue/assassination/validate-itemized'&&x.status===400&&x.request?.ranks?.malice===6)`);
 assert.deepEqual(await evaluate(draftExpression),invalidDraft,'Rejected import restores the invalid raw draft without losing controls');
 await importBuild(saved.file);assert.deepEqual((await exportBuild('valid-import-recovers-invalid-draft')).data,saved.data);

 start=await sequence();await setInput('#classKey','mage');await presetReady('mage','arcane',start);
 await setInput('#duration',23);const mageDirty=await exportBuild('mage-dirty');await rejectImport(invalidFile,mageDirty,'mage-dirty',true);
 for(let i=0;i<2;i++){await setInput('#spec','fire');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.equal(await evaluate("document.querySelector('#presetDialog').open"),false);assert.deepEqual((await exportBuild('cancel-preserved-'+i)).data,mageDirty.data);}
 await importBuild(saved.file);
 for(const key of ['shaman','rogue','mage','rogue']){start=await sequence();await setInput('#classKey',key);await presetReady(key,key==='rogue'?'assassination':key==='shaman'?'elemental':'arcane',start);}
 await ready(start);await assertIdentity();assert.deepEqual((await exportBuild('after-class-switches')).data.talents.ranks,preset.ranks);
 // Withhold one actual older response so the newer navigation finishes first.
 start=await sequence();await setInput('#classKey','mage');await presetReady('mage','arcane',start);
 await holdResponse('/api/forever/talents/rogue/assassination/default-itemized');
 await setInput('#classKey','rogue');await until('window.__rogueHold.held');
 start=await sequence();await setInput('#classKey','mage');await presetReady('mage','arcane',start);
 const newerMage=await exportBuild('newer-mage-before-release');await releaseResponse('stale-rogue-preset-after-newer-mage');
 assert.deepEqual((await exportBuild('newer-mage-after-release')).data,newerMage.data,'Older Rogue preset cannot overwrite newer Mage navigation');
 // Hold both real responses: old Rogue preview must not repaint during the new
 // Mage catalog await, before Mage has even initiated its own stats request.
 await importBuild(saved.file);await click('[data-view=encounter]');
 await holdResponse('/api/forever/character/preview-rogue');await setInput('#rogueStartingEnergyPercent',50);await until('window.__rogueHold.held');
 await evaluate('window.__heldRoguePreview=window.__rogueHold');
 await holdResponse('/api/forever/equipment/items?',true);
 start=await sequence();await setInput('#classKey','mage');await until('window.__rogueHold.held');
 const pendingStats=await evaluate("({stats:document.querySelector('#stats').innerHTML,status:document.querySelector('#statStatus').textContent,scope:document.querySelector('#statScope').textContent})");
 await releaseResponse('old-rogue-preview-during-mage-catalog','__heldRoguePreview');
 assert.equal(await evaluate('window.__rogueHold.released'),false,'Mage catalog must still be pending at the old-preview assertion');
 assert.deepEqual(await evaluate("({stats:document.querySelector('#stats').innerHTML,status:document.querySelector('#statStatus').textContent,scope:document.querySelector('#statScope').textContent})"),pendingStats,'Old Rogue response cannot paint during new Mage catalog await');
 await releaseResponse('new-mage-catalog-after-old-preview');await presetReady('mage','arcane',start);
 assert.equal((await exportBuild('mage-after-stale-preview')).data.classKey,'mage');
 // A real older historical preview cannot repaint after reviewed-baseline
 // eligibility has completed and rejected the newly selected baseline mode.
 await importBuild(saved.file);await click('[data-view=encounter]');
 await holdResponse('/api/forever/character/preview-rogue');
 await setInput('#rogueStartingEnergyPercent',75);await until('window.__rogueHold.held');
 const baselineBefore=await sequence();await click('#provisional');
 await until(`!document.querySelector('#provisional').checked&&document.querySelector('#baselineStatus').textContent==='No reviewed Forever baseline for this race/class'&&document.querySelector('#statStatus').textContent==='Select Historical baseline to preview provisional stats'&&window.__rogueHttp.some(x=>x.sequence>${baselineBefore}&&x.url==='/api/forever/character/level60-baseline-coverage'&&x.status===200&&!(x.response.combinations||[]).some(c=>c.race===document.querySelector('#race').value&&c.classKey==='rogue'&&c.status==='reviewed-forever-in-game'))`);
 const unavailableExpression="({stats:document.querySelector('#stats').innerHTML,status:document.querySelector('#statStatus').textContent,scope:document.querySelector('#statScope').textContent,baseline:document.querySelector('#baselineStatus').textContent,provisional:document.querySelector('#provisional').checked})";
 const unavailableBaseline=await evaluate(unavailableExpression);
 await releaseResponse('old-historical-preview-after-reviewed-baseline-unavailable');
 assert.deepEqual(await evaluate(unavailableExpression),unavailableBaseline,'Old historical response must preserve unavailable reviewed-baseline state');
 start=await sequence();await click('#provisional');await ready(start,{startingEnergyPercent:75});



 await importBuild(saved.file);
 await click('[data-view=rotation]');start=await sequence();await click('#roguePreviewRotation');
 await until(`window.__rogueHttp.some(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview'&&x.request?.specialization==='assassination'&&x.status===200)&&document.querySelector('#rogueRotationEntries').textContent===window.__rogueHttp.filter(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview').at(-1).response.entries.map(x=>x.abilityKey.replace(/-/g,' ').replace(/\\b\\w/g,c=>c.toUpperCase())).join(' > ')`);
 const rotation=await evaluate(`window.__rogueHttp.filter(x=>x.sequence>${start}&&x.url==='/api/forever/rotation/preview').at(-1)`);
 assert.deepEqual(rotation.response.entries,a.data.resolvedCharacter.rotation.entries.map(({abilityKey,isEnabled,priority,conditions})=>({abilityKey,isEnabled,priority,conditions})));
 // Same-class settings change must invalidate a late fixed-priority response.
 await importBuild(saved.file);await click('[data-view=rotation]');
 const beforeRotation=await text('#rogueRotationEntries');await holdResponse('/api/forever/rotation/preview');
 await click('#roguePreviewRotation');await until('window.__rogueHold.held');
 await click('[data-view=encounter]');start=await sequence();await click('#rogueUseExposeArmor');await ready(start,{useExposeArmor:false});
 await releaseResponse('stale-priority-after-rogue-setting-change');
 assert.equal(await text('#rogueRotationEntries'),beforeRotation,'Late priority for old settings must not replace selected-state preview');
 await importBuild(saved.file);

 await click('[data-view=encounter]');await setRogueSetting('mainHandWeaponDamage',280);
 const weapon=await run('rogue-weapon-only');await parity(weapon.data);assert.notEqual(weapon.data.combatSummary.damageDone,a.data.combatSummary.damageDone);
 await importReport('A',a.file);await importReport('B',weapon.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 const weaponComparison=compareReports(a.data,weapon.data);
 assert.deepEqual(await evaluate('window.__rogueComparisons.at(-1)'),weaponComparison,'Actual rendered comparison document equals complete captured report comparison');
 assert.deepEqual(weaponComparison.a.report,a.data);assert.deepEqual(weaponComparison.b.report,weapon.data);
 assert.equal(weaponComparison.comparable,true);assert.notEqual(weaponComparison.dpsDelta,0);assert.match(await text('#compareChanges'),/weapon|hand/i);
 fs.writeFileSync(path.join(reportDir,'weapon-comparison.json'),JSON.stringify(weaponComparison,null,2));await screenshot('rogue-weapon-comparison-desktop','.comparison-workspace');
 await captureRegion('rogue-weapon-comparison-values-desktop','.comparison-workspace');
 await captureViewport(390,844,true);
 await captureRegion('rogue-weapon-comparison-values-mobile','.comparison-workspace');
 await captureViewport(1440,1000,false);
 await importBuild(saved.file);await click('[data-view=encounter]');await setRogueSetting('targetArmor',0);
 const b=await run('rogue-zero-armor');await parity(b.data);assert.notEqual(b.data.combatSummary.damageDone,a.data.combatSummary.damageDone);
 await importReport('A',a.file);await importReport('B',b.file);assert.match(await text('#compareStatus'),/Not directly comparable/);assert.match(await text('#compareWarnings'),/armor/i);await screenshot('rogue-comparison-desktop','.comparison-workspace');
 await captureRegion('rogue-armor-comparison-values-desktop','.comparison-workspace');
 await captureViewport(390,844,true);
 await captureRegion('rogue-armor-comparison-values-mobile','.comparison-workspace');
 await assertReadableLayout('results','mobile-results');await screenshot('rogue-results-mobile','#resultWarnings');
 await captureRegion('rogue-metrics-mobile','#metrics');
 await captureRegion('rogue-abilities-mobile','#abilities');
 await captureRegion('rogue-results-notes-mobile','#resultWarnings');
 await assertReadableLayout('encounter','mobile-encounter');await screenshot('rogue-encounter-mobile','#rogueSettings');
 await captureRegion('rogue-controls-mobile','#rogueSettings .settings-grid');
 assert.ok((await text('#classCapabilityNotes')).startsWith(displayedRestrictions.join(' · ')));
 await captureRegion('rogue-capability-notes-mobile','#classCapabilityNotes');
 await captureRegion('rogue-source-notes-mobile','#rogueAssassinationScope > p');
 await click('[data-view=talents]');
 await captureRegion('rogue-talents-mobile','#talentSummary,#talentCoverage,#talents');
 assert.deepEqual(browserErrors,[]);
 fs.writeFileSync(path.join(reportDir,'comparison-documents.json'),JSON.stringify(await evaluate('window.__rogueComparisons'),null,2));
 fs.writeFileSync(path.join(reportDir,'http-observations.json'),JSON.stringify(await evaluate('window.__rogueHttp'),null,2));
 fs.writeFileSync(path.join(reportDir,'causal-gate-receipts.json'),JSON.stringify(gateReceipts,null,2));
 fs.writeFileSync(path.join(reportDir,'acceptance-summary.json'),JSON.stringify({status:'passed',sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',checks,reports:reportFiles,screenshots:screenshotDir},null,2));
 console.log('Rogue actual-browser acceptance passed: '+checks+' gates.');
}
main().catch(async error=>{console.error(error);process.exitCode=1;try{await screenshot('rogue-browser-failure');fs.writeFileSync(path.join(reportDir,'failure-observations.json'),JSON.stringify({error:String(error),dom:await evaluate('document.body.innerText'),input:await evaluate('window.__soloDpsClickHistory||[]'),buildState:await evaluate("({state:document.querySelector('#status').dataset.buildState,classKey:document.querySelector('#classKey').value,spec:document.querySelector('#spec').value,dialogOpen:document.querySelector('#presetDialog').open,exportDisabled:document.querySelector('#exportBuild').disabled})"),downloadFiles:fs.readdirSync(reportDir),http:await evaluate('window.__rogueHttp')},null,2));}catch{}}).finally(async()=>{try{ws?.close();}catch{}proc.kill();const relative=path.relative(path.resolve(os.tmpdir()),path.resolve(profile));if(relative&&!relative.startsWith('..')&&!path.isAbsolute(relative))fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});});
