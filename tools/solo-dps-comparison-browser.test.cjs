#!/usr/bin/env node
"use strict";
// End-to-end acceptance against an already running local API. Every successful
// report is downloaded from a real UI simulation, never manufactured in a test.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const {compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp174-full-reports"),"comparison-browser");
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||path.join(os.tmpdir(),"cp174-ui-captures"));
const fixtureDir=path.resolve(__dirname,"../docs/acceptance/cp175");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-comparison-browser-"));
const port=Number(process.env.CP175_COMPARISON_CDP_PORT||19350);
fs.mkdirSync(reportDir,{recursive:true});fs.mkdirSync(screenshotDir,{recursive:true});
const proc=spawn(process.env.CP174_CHROME||"/usr/bin/google-chrome",[
 "--headless","--no-sandbox","--disable-gpu","--disable-dev-shm-usage",
 `--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,"about:blank"],{stdio:"ignore"});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
let ws,id=0,pending=new Map(),browserErrors=[],checks=0,reportFiles=[];
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
async function importBuild(file){
 await selectFile("#importBuild","#file",file);
 await until("document.querySelector('#file').value==='' && document.querySelector('#status').textContent==='Build imported.'");
 await assertIdentity();
}
async function assertIdentity(){
 const identity=await evaluate("({race:document.querySelector('#race').selectedOptions[0].textContent,title:document.querySelector('#spec').selectedOptions[0].textContent+' '+document.querySelector('#classKey').selectedOptions[0].textContent,displayedTitle:document.querySelector('#characterTitle').textContent,subtitle:document.querySelector('#characterSubtitle').textContent})");
 assert.equal(identity.displayedTitle.toLowerCase(),identity.title.replaceAll('-',' ').toLowerCase(),"Sidebar must display the selected class and specialization");
 assert.ok(identity.subtitle.startsWith(identity.race+" "),"Sidebar race must match the selected race: "+JSON.stringify(identity));
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
 const completed=await evaluate("window.__comparisonCompleted||0");
 await click("#run");
 await until(`(window.__comparisonCompleted||0)>${completed} && !document.querySelector('#run').disabled && /Simulation completed|Warrior completed/.test(document.querySelector('#status').textContent)`,60000);
 const result=await downloaded("warcraftsim-simulation-report.json","#exportResults",name+"-report.json");
 assert.equal(result.data.schema,"warcraftsim.solo-dps-report");
 const inspected=compareReports(result.data,result.data);
 assert.equal(inspected.comparable,true,name+": "+inspected.warnings.join("; "));
 const actualWarnings=await evaluate("[...document.querySelectorAll('#resultWarningList > div')].map(row=>row.textContent)");
 assert.deepEqual(actualWarnings,[...new Set((result.data.warnings||[]).filter(value=>typeof value==='string'&&value.trim()))],"Readable warnings must come exactly from this run, without stale notes or invented warnings");
 reportFiles.push(path.basename(result.file));
 return result;
}
async function replay(pattern){
 await click("#replayResult");await until(`new RegExp(${JSON.stringify(pattern)}).test(document.querySelector('#status').textContent)`,60000);
}
async function importReport(slot,file){
 await selectFile("#compareImport"+slot,"#compareFile",file);
 await until("document.querySelector('#compareFile').value===''");
 assert.ok(!(await text("#compareStatus")).includes("rejected"),await text("#compareStatus"));
}
async function screenshot(name,selector){
 if(selector)await evaluate(`document.querySelector(${JSON.stringify(selector)}).scrollIntoView({block:'start'})`);
 const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 fs.writeFileSync(path.join(screenshotDir,name+".png"),Buffer.from(shot.data,"base64"));
}
const rows=()=>evaluate("[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent))");
const fmt=(value,digits=1)=>Number(value).toLocaleString("en-US",{minimumFractionDigits:digits,maximumFractionDigits:digits});
async function assertComparison(left,right){
 const expected=compareReports(left,right),actual=await rows();
 assert.equal(expected.comparable,true,expected.warnings.join("; "));
 assert.match(await text("#compareStatus"),/Encounter settings match/);
 assert.equal(actual[0][1],fmt(expected.a.dps)+" ["+expected.a.dpsLabel+"]");
 assert.equal(actual[0][2],fmt(expected.b.dps)+" ["+expected.b.dpsLabel+"]");
 assert.equal(actual[0][3],(expected.dpsDelta>=0?"+":"")+fmt(expected.dpsDelta)+" ("+fmt(expected.dpsDeltaPercent)+"%)");
 assert.ok((await text("#compareChanges")).includes("Gear main-hand"),"Readable changed gear required");
 assert.ok(actual.some(row=>row[0].includes("top abilities")&&row[1].includes("frostfire-bolt")),"Ability sources must be readable without raw JSON");
 checks++;
}
async function main(){
 let tabs;
 for(let i=0;i<100;i++){
  try{tabs=await(await fetch(`http://127.0.0.1:${port}/json`)).json();if(tabs.some(t=>t.type==="page"))break;}catch{}
  await sleep(100);
 }
 assert.ok(tabs?.some(t=>t.type==="page"),"Chromium CDP port unavailable");
 ws=new WebSocket(tabs.find(t=>t.type==="page").webSocketDebuggerUrl);
 await new Promise((resolve,reject)=>{ws.addEventListener("open",resolve,{once:true});ws.addEventListener("error",reject,{once:true});});
 ws.addEventListener("message",event=>{
  const message=JSON.parse(event.data);
  if(message.id){const p=pending.get(message.id);pending.delete(message.id);if(p)message.error?p.reject(Error(message.error.message)):p.resolve(message.result);}
  else if(message.method==="Runtime.exceptionThrown")browserErrors.push(message.params.exceptionDetails);
 });
 for(const method of ["Page.enable","Runtime.enable","DOM.enable"])await command(method);
 await command("Page.addScriptToEvaluateOnNewDocument",{source:"window.__comparisonCompleted=0;document.addEventListener('warcraftsim:simulation-complete',()=>window.__comparisonCompleted++);"});
 await command("Page.setDownloadBehavior",{behavior:"allow",downloadPath:reportDir});
 await command("Page.setInterceptFileChooserDialog",{enabled:true});
 await command("Emulation.setDeviceMetricsOverride",{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command("Page.navigate",{url:BASE+"/solo-dps.html"});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')");

 await importBuild(path.join(fixtureDir,"cp175-f4a-mage-three-tree.json"));
 const originalBuild=await exportBuild("baseline");
 const a=await run("baseline");await replay("Deterministic replay matched");checks++;
 // Change a real equipped slot, leaving the scenario, seeds, rotation and
 // talents unchanged. Both A and B then execute independently on the server.
 await click("[data-view=equipment]");await click("[data-slot=main-hand]");
 await until("document.querySelector('#itemDialog').open");await click("#unequip");
 await until("!document.querySelector('#itemDialog').open && !!document.querySelector('[data-slot=main-hand] .empty-item')");
 const b=await run("candidate");
 assert.notDeepEqual(a.data.build.gear,b.data.build.gear,"Independent builds must genuinely differ");
 assert.notEqual(a.data.combatSummary.dps,b.data.combatSummary.dps,"The selected equipment change must affect actual DPS");
 await importReport("A",a.file);await importReport("B",b.file);
 await assertComparison(a.data,b.data);
 await screenshot("cp175-comparison-distinct-builds",".comparison-workspace");
 console.log("CP175 comparison imported independent runs:",JSON.stringify({a:a.data.combatSummary.dps,b:b.data.combatSummary.dps,changes:await text('#compareChanges')}));

 // A completed report with a genuinely different simulated duration must
 // display its mismatch and suppress ranking, rather than comparing stale UI.
 await click("[data-view=encounter]");await setInput("#duration",90);
 const mismatch=await run("different-duration");
 await importReport("B",mismatch.file);
 assert.match(await text("#compareStatus"),/Not directly comparable/);
 assert.match(await text("#compareWarnings"),/Duration differs/);
 assert.equal((await rows())[0][3],"Not comparable");
 await screenshot("cp175-comparison-mismatched-duration",".comparison-workspace");checks++;

 // Full normalized build round trip, followed by a new run with the same
 // deterministic result. This restores the equipped item, not just its label.
 await importBuild(originalBuild.file);
 const restored=await exportBuild("restored");
 assert.deepEqual(restored.data,originalBuild.data,"All exported build settings must round-trip through the file input");
 const repeated=await run("restored");
 assert.deepEqual(repeated.data.combatSummary,a.data.combatSummary,"Imported build must reproduce its original combat summary");
 await replay("Deterministic replay matched");checks++;

 // Import both reports into a fresh page with no current result, in reversed
 // order. This exercises standalone report consumption rather than use-run.
 await command("Page.reload",{ignoreCache:true});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')");
 await click("[data-view=results]");
 assert.equal(await evaluate("document.querySelector('#compareUseA').disabled"),true);
 await importReport("B",a.file);await importReport("A",b.file);
 await assertComparison(b.data,a.data);
 const preserved=await rows();
 await selectFile("#compareImportB","#compareFile",originalBuild.file);
 await until("document.querySelector('#compareStatus').textContent.includes('Report import rejected')");
 assert.deepEqual(await rows(),preserved,"Rejected build file must not overwrite the selected report");
 await importReport("B",a.file);await assertComparison(b.data,a.data);checks++;

 // Seeded distribution replay also survives build export/import. The mean is
 // displayed explicitly, and representative total damage is not a mean delta.
 await importBuild(originalBuild.file);
 await click("[data-view=encounter]");await setInput("#iterations",3);
 const multi=await run("three-iterations");
 assert.equal(multi.data.iterationStatistics.iterations,3);
 await replay("Repeated seeded distribution matched every sample");
 const multiBuild=await exportBuild("three-iterations");await importBuild(multiBuild.file);
 const multiAgain=await run("three-iterations-restored");
 assert.deepEqual(multiAgain.data.iterationStatistics.samples,multi.data.iterationStatistics.samples);
 assert.equal(multiAgain.data.iterationStatistics.representativeSeed,multi.data.iterationStatistics.representativeSeed);
 await importReport("A",a.file);await importReport("B",multi.file);
 assert.match(await text("#compareWarnings"),/Iterations differs/);
 assert.equal((await rows())[0][3],"Not comparable");checks++;
 console.log("CP175 build round-trip, single-seed replay, three-seed replay and scenario/iteration rejection passed.");

 await setInput("#race","gnome");
 await until("document.querySelector('#characterSubtitle').textContent.startsWith('Gnome ')");
 await assertIdentity();checks++;

 await importBuild(path.join(fixtureDir,"cp175-f3a-arms-mortal-strike.json"));
 const learned=await run("warrior-learned");
 assert.ok(learned.data.combatSummary.damageByAbility["mortal-strike"]>0,"Learned Mortal Strike must execute before deallocation");
 await click("[data-view=talents]");
 const ms="[data-talent-key=mortal-strike]";
 assert.equal(await text(ms+" .talent-rank"),"1/1");
 await click(ms,"right");
 await until("document.querySelector('[data-talent-key=mortal-strike] .talent-rank').textContent==='0/1' || document.querySelector('#status').textContent.includes('Talent allocation rejected')");
 assert.equal(await text(ms+" .talent-rank"),"0/1","Warrior learned action can be deallocated: "+await text("#status"));
 await click("[data-view=rotation]");
 await click("#applyPriority");
 await until("document.querySelector('#priorityStatus').textContent.includes('Custom Warrior rotation accepted')");
 const acceptedPriorityNotice=await text("#priorityStatus");
 await screenshot("cp175-warrior-accepted-priority-warning","#priorityStatus");
 const removed=await exportBuild("warrior-unlearned");
 // The existing Warrior contract retains same-preset unlearned actions as
 // explicitly non-executable drafts. Off-tree unlearned actions are rejected.
 await setInput("#priorityText","bloodthirst\nwhirlwind");await click("#applyPriority");
 await until("/bloodthirst.*allocated off-tree talent/i.test(document.querySelector('#status').textContent)");
 const rejected=await exportBuild("warrior-rejected-priority");
 assert.deepEqual(rejected.data.encounter.warrior.customRotation,removed.data.encounter.warrior.customRotation,"Rejection preserves accepted Warrior custom rotation");
 await screenshot("cp175-warrior-unlearned-priority","#priorityEditor");checks++;
 const unlearned=await run("warrior-unlearned");
 assert.equal(unlearned.data.originalSimulationRequest.warrior.talentRanks["mortal-strike"]||0,0);
 assert.equal(unlearned.data.combatSummary.damageByAbility["mortal-strike"]||0,0,"Unlearned Mortal Strike must never deal damage");
 assert.ok(!unlearned.data.rawServerResponse.sim.batch.representativeRun.timeline.some(event=>event.abilityKey==="mortal-strike"&&event.type===4),"Unlearned Mortal Strike must never complete a cast");
 const warning=unlearned.data.warnings.find(value=>value.includes("UNAVAILABLE CUSTOM ACTION")&&value.includes("mortal-strike"));
 assert.ok(warning,"Engine must explain the retained, unavailable custom action");
 console.log("CP175 Warrior learned-action evidence:",JSON.stringify({learnedDamage:learned.data.combatSummary.damageByAbility["mortal-strike"],unlearnedDamage:unlearned.data.combatSummary.damageByAbility["mortal-strike"]||0,offTreePriorityRejected:true,warning,acceptedPriorityNotice}));
 assert.match(acceptedPriorityNotice,/mortal-strike.*unallocated talent.*cannot execute/,"Accepted editable priority must preserve the engine's unavailable-action warning");
 assert.ok((await evaluate("document.querySelector('#view-results').innerText")).includes(warning),"Unavailable action warning must be readable without expanding raw JSON");
 await evaluate("[...document.querySelectorAll('#resultWarningList > div')].find(row=>row.textContent.includes('UNAVAILABLE CUSTOM ACTION')).scrollIntoView({block:'center'})");
 await screenshot("cp175-warrior-result-warning");
 await replay("Warrior seeded replay matched");checks++;
 assert.deepEqual(browserErrors,[],"No unhandled browser exceptions");
 fs.writeFileSync(path.join(reportDir,"acceptance-summary.json"),JSON.stringify({checks,status:"passed",reports:reportFiles,screenshots:screenshotDir},null,2));
 console.log(`CP175 real-browser report/build acceptance passed: ${checks} workflow checks. Evidence: ${reportDir}`);
}
main().catch(async error=>{
 console.error(error);process.exitCode=1;
 try{await screenshot("cp175-comparison-browser-failure");}catch{}
}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(400);
 // Delete only this harness's generated profile, never a computed parent/root.
 const relative=path.relative(path.resolve(os.tmpdir()),path.resolve(profile));
 if(relative&&!relative.startsWith("..")&&!path.isAbsolute(relative))
  fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});
});
