#!/usr/bin/env node
"use strict";
// End-to-end acceptance against an already running local API. Every successful
// report is downloaded from a real UI simulation, never manufactured in a test.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const {compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const {assertSoloDpsIdentity}=require("./solo-dps-browser-identity.cjs");
const {createBrowserClick}=require("./solo-dps-browser-click.cjs");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp174-full-reports"),"shadow-browser");
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||path.join(os.tmpdir(),"cp174-ui-captures"));
const fixtureDir=path.resolve(__dirname,"../docs/acceptance/cp175");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-shadow-browser-"));
const port=Number(process.env.CP175_SHADOW_CDP_PORT||19358);
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
 const identity=await evaluate("({classKey:document.querySelector('#classKey').value,specialization:document.querySelector('#spec').value,classLabel:document.querySelector('#classKey').selectedOptions[0].textContent,race:document.querySelector('#race').selectedOptions[0].textContent,displayedTitle:document.querySelector('#characterTitle').textContent,subtitle:document.querySelector('#characterSubtitle').textContent})");
 assertSoloDpsIdentity(identity);
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
 if(selector)await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});e.scrollIntoView({block:'start'});const top=e.getBoundingClientRect().top,header=document.querySelector('.topbar').getBoundingClientRect().height+16;if(top<header)window.scrollBy(0,top-header);})()`);
 const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 fs.writeFileSync(path.join(screenshotDir,name+".png"),Buffer.from(shot.data,"base64"));
}
const rows=()=>evaluate("[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent))");
async function verifyParity(report){
 const p=report.resolvedCharacter,sim=report.rawServerResponse.sim;
 for(const key of ['maximumMana','maximumHealth','spellPower'])assert.equal(p[key],sim[key],key+' preview/execution');
 assert.equal(p.rotationPreviewScope,'fixed-toggle-priority-no-custom-rotation');
 assert.ok(!Object.hasOwn(sim,'resolvedRotation'),'Do not fabricate a Shadow resolvedRotation response field');
 const keys=p.rotation.entries.map(x=>x.abilityKey);
 for(const ability of Object.keys(sim.damageByAbility||{}))assert.ok(keys.includes(ability),'Actual Shadow damage comes from its fixed priority: '+ability);
 assert.equal(sim.run.summary.actorSummaries.priest.startingResources.mana,p.maximumMana,'Actual priest actor starting mana');
 const rendered=await evaluate("[...document.querySelectorAll('#stats > div')].map(x=>[x.querySelector('span')?.textContent,x.querySelector('strong')?.textContent])");
 const stats=p.effectiveStats.values||p.effectiveStats;
 assert.equal(rendered.length,Object.entries(stats).filter(([k,v])=>v!==null&&typeof v!=='object').length,'No displayed scalar stats omitted');
 for(const [key,value] of Object.entries(stats).filter(([k,v])=>v!==null&&typeof v!=='object')){
  const human=key.replace(/-/g,' ').replace(/\b\w/g,c=>c.toUpperCase());
  assert.ok(rendered.some(([label,text])=>label.toLowerCase()===human.toLowerCase()&&text===Number(value).toLocaleString('en-US',{minimumFractionDigits:key.includes('percent')?1:0,maximumFractionDigits:key.includes('percent')?1:2})),'Hero stat '+key+' matches effective precombat value '+value);
 }
 assert.match(p.statPreviewScope,/display-only.*not-current-client-certified/);
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
 if(Number(process.env.CP175_CDP_LATENCY_MS||0)>0){await command('Network.enable');await command('Network.emulateNetworkConditions',{offline:false,latency:Number(process.env.CP175_CDP_LATENCY_MS),downloadThroughput:-1,uploadThroughput:-1});}
 await command("Page.addScriptToEvaluateOnNewDocument",{source:"window.__shadowHttp=[];const originalFetch=window.fetch;window.fetch=async(...args)=>{const response=await originalFetch(...args);if(['/api/forever/character/preview-shadow','/api/forever/rotation/preview','/api/forever/character/simulate-level60'].includes(String(args[0]))){const capture={observedAt:performance.now(),url:String(args[0]),request:args[1]?.body?JSON.parse(args[1].body):null,status:response.status};try{capture.response=await response.clone().json();}catch{}window.__shadowHttp.push(capture);}return response;};window.__comparisonCompleted=0;document.addEventListener('warcraftsim:simulation-complete',()=>window.__comparisonCompleted++);"});
 await command("Page.setDownloadBehavior",{behavior:"allow",downloadPath:reportDir});
 await command("Page.setInterceptFileChooserDialog",{enabled:true});
 await command("Emulation.setDeviceMetricsOverride",{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command("Page.navigate",{url:BASE+"/solo-dps.html"});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')");



 await setInput('#classKey','priest');
 await until("document.querySelector('#spec').value==='shadow' && /Class defaults loaded/.test(document.querySelector('#status').textContent)");
 await setInput('#race','human');await until("document.querySelector('#characterSubtitle').textContent.startsWith('Human ')");
 if(!await evaluate("document.querySelector('#provisional').checked"))await click('#provisional');
 await until("document.querySelector('#statStatus').textContent==='Up to date'");
 assert.deepEqual(await evaluate("[...document.querySelector('#spec').options].map(x=>x.value)"),['shadow']);checks++;
 await click('[data-view=talents]');
 const base=await exportBuild('shadow-baseline');
 const preset=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/priest-shadow.json'),'utf8'));
 assert.deepEqual(base.data.talents.ranks,preset.ranks);assert.equal(preset.totalRankedPoints,36);
 assert.equal(Object.values(base.data.talents.ranks).reduce((a,b)=>a+b),36);
 assert.deepEqual(Object.fromEntries(Object.entries(base.data.talents).filter(([k])=>k.endsWith('PointsSpent'))),{disciplinePointsSpent:0,holyPointsSpent:0,shadowPointsSpent:36});
 assert.match(await text('#talentCoverage'),new RegExp('0/0/36.*15 unspent','i'));assert.match(await text('#talentSummary'),/15 remaining/);
 assert.match(await text('#status'),/36 explicitly ranked points and 15 unspent.*Partial/i);checks++;
 await screenshot('cp175-shadow-talents-desktop','#talentSummary');
 await click('[data-view=encounter]');
 for(const limitation of [/fixed/i,/63/,/unmitigated/i,/racial/i,/manual/i])assert.match(await text('#classCapabilityNotes'),limitation);
 assert.match(await text('#shadowHealthPrecedence'),/observed or historical-proxy naked Health takes precedence/i);
 assert.equal(await evaluate("document.querySelector('#iterations').disabled"),true);
 assert.equal(await evaluate("document.querySelector('#scriptedBossEnabled').getClientRects().length===0"),true);checks++;
 await click('[data-view=effects]');assert.equal(await evaluate("document.querySelector('#racialActive').disabled"),true);
 assert.equal(await evaluate("[...document.querySelectorAll('#modifiers button,#modifiers input')].filter(x=>!x.disabled&&x.getClientRects().length).length"),0,'No active modifiers or items on fixed Shadow workflow');
 await click('[data-view=equipment]');assert.match(await text('#catalogCoverage'),/pending|not enabled|unavailable/i);checks++;
 await setInput('#duration',30);await evaluate("document.querySelector('#timeline').checked=true");
 const timed=await exportBuild('shadow-timed');
 const a=await run('shadow-baseline');await verifyParity(a.data);
 assert.equal(a.data.resolvedCharacter.effectiveStats.values['spell-hit-chance-percent'],88,'Pinned Shadow Focus5 adds5 points to the provisional base83; literal source witness, not only preview/render parity');
 const input=a.data.originalSimulationRequest.shadow;
 assert.equal(a.data.originalSimulationRequest.classKey,'priest');assert.equal(a.data.originalSimulationRequest.specialization,'shadow');
 for(const field of ['externalModifiers','combatItems','customPriority','rotationPolicy','majorManaPotionCharges'])assert.equal(Object.hasOwn(input,field),false,'Unsupported Shadow field absent: '+field);
 assert.equal(a.data.combatSummary.actorKey,'priest');assert.equal(a.data.combatSummary.ownerDamage,a.data.combatSummary.damageDone);
 assert.equal(a.data.combatSummary.maximumMana,a.data.resolvedCharacter.maximumMana);
 assert.deepEqual(a.data.combatSummary.targetSemantics,{level:63,mitigation:'unmitigated',targetCount:1,initialHealthPercent:100});
 assert.match(a.data.resolvedCharacter.statPreviewScope,/display-only.*not-current-client-certified/);
 assert.ok(a.data.resolvedCharacter.modelingNotes.some(x=>/manual|observed|Supplied naked/i.test(x)),'Resource assumptions are disclosed');
 for(const talent of a.data.talentAllocation.selected){assert.equal(talent.historicalValidationSourceCommit,preset.provenance.sourceCommit);assert.equal(talent.historicalValidationSourceUrl,preset.provenance.sources[0]);}
 await replay('Deterministic replay matched');checks++;
 await importReport('A',a.file);await importReport('B',a.file);assert.match(await text('#compareStatus'),/Encounter settings match/);checks++;
 console.log('Shadow partial preset, actual fixed preview/resource parity, report and replay passed.');
 const cases=[
 ['racial',x=>x.racialActive=true],['gear',x=>x.gear.items.push({slotKey:'main-hand',itemId:1})],['iterations',x=>x.encounter.iterations=2],
 ['target-level',x=>x.encounter.targetLevel=62],['target-armor',x=>x.encounter.targetArmor=3500],['script',x=>x.encounter.soloScript.enabled=true],
 ['buff',x=>x.modifiers.buffs.push('arcane-intellect')],['consumable',x=>x.modifiers.consumables.push('greater-arcane-elixir')],['debuff',x=>x.modifiers.targetDebuffs.push('curse-of-elements')],
 ['custom-priority',x=>x.encounter.customPriority=['mind-blast']],['combat-item',x=>x.encounter.combatItems={majorManaPotionCharges:1}],['rotation-policy',x=>x.encounter.rotation='adaptive'],
 ['conditions',x=>x.encounter.priorityRows=[{key:'mind-blast',enabled:true,conditions:[{kind:'mana-percent',value:50}]}]],['rotation-condition',x=>x.encounter.rotationConditions={foo:1}]
 ];
 for(const [name,mutate] of cases){
  const data=structuredClone(timed.data);mutate(data);const file=path.join(reportDir,'rejected-'+name+'.json');fs.writeFileSync(file,JSON.stringify(data));
  await evaluate("window.__importStatusChanges=0;window.__importObserver?.disconnect();window.__importObserver=new MutationObserver(()=>window.__importStatusChanges++);window.__importObserver.observe(document.querySelector('#status'),{childList:true,subtree:true,characterData:true});");
  await selectFile('#importBuild','#file',file);
  await until("window.__importStatusChanges>0 && /unavailable|unsupported|not supported|pending|one deterministic|fixed/i.test(document.querySelector('#status').textContent)");
  assert.deepEqual((await exportBuild('after-rejected-'+name)).data,timed.data,name+' rejected import preserves full Shadow build');checks++;
  gateReceipts.push({gate:name,at:await evaluate('performance.now()'),status:await text('#status'),preserved:true});
 }
 await importBuild(timed.file);assert.deepEqual((await exportBuild('shadow-roundtrip')).data,timed.data);checks++;
 await click('[data-view=rotation]');
 // Read-only fixed rotation inspection must not offer configurable priorities.
 assert.doesNotMatch(await text('#view-rotation'),/custom ordered list overrides/i,'Fixed Shadow UI must not advertise an unavailable custom override');
 await click('#shadowPreviewRotation');
 await until("document.querySelector('#shadowRotationEntries').textContent.includes('Mind Flay')");
 const rotation=await evaluate("window.__shadowHttp.filter(x=>x.url==='/api/forever/rotation/preview'&&x.request?.classKey==='priest').at(-1)");
 assert.equal(rotation.status,200);assert.equal(rotation.response.rotationPreviewScope,'fixed-toggle-priority-no-custom-rotation');
 assert.deepEqual(rotation.response.entries.map(x=>x.abilityKey),a.data.resolvedCharacter.rotation.entries.map(x=>x.abilityKey));
 assert.equal(await evaluate("document.querySelector('#applyPriority').disabled||document.querySelector('#applyPriority').getClientRects().length===0"),true);
 await screenshot('cp175-shadow-fixed-rotation-desktop','#shadowFixedRotation');checks++;
 // Remove a real sourced leaf talent through the UI; do not invent a build or report.
 await click('[data-view=talents]');assert.equal(await text('[data-talent-key=shadowform] .talent-rank'),'1/1');
 await click('[data-talent-key=shadowform]','right');await until("document.querySelector('[data-talent-key=shadowform] .talent-rank').textContent==='0/1'");
 const b=await run('shadow-without-shadowform');await verifyParity(b.data);
 assert.equal(b.data.originalSimulationRequest.itemizedTalentRanks.shadowform||0,0);
 assert.notEqual(b.data.combatSummary.dps,a.data.combatSummary.dps,'Source-backed talent change affects real gameplay');
 await importReport('A',a.file);await importReport('B',b.file);assert.match(await text('#compareStatus'),/Encounter settings match/);checks++;
 await screenshot('cp175-shadow-distinct-builds-desktop','.comparison-workspace');
 await click('[data-view=encounter]');await setInput('#duration',35);const mismatch=await run('shadow-duration-mismatch');await importReport('B',mismatch.file);
 assert.match(await text('#compareStatus'),/Not directly comparable/);assert.match(await text('#compareWarnings'),/Duration differs/);checks++;
 await importBuild(timed.file);const restored=await run('shadow-restored');assert.deepEqual(restored.data.combatSummary,a.data.combatSummary);await replay('Deterministic replay matched');checks++;
 await click('[data-view=encounter]');await setInput('#shadowPriestHealth',4800);
 const manualHealth=await run('shadow-manual-player-health');await verifyParity(manualHealth.data);
 assert.equal(manualHealth.data.originalSimulationRequest.shadow.priestMaximumHealth,4800);
 assert.equal(manualHealth.data.originalSimulationRequest.shadow.targetInitialHealthPercent,100);
 assert.deepEqual(manualHealth.data.combatSummary.targetSemantics,{level:63,mitigation:'unmitigated',targetCount:1,initialHealthPercent:100});
 assert.equal(manualHealth.data.combatSummary.maximumHealth,manualHealth.data.resolvedCharacter.baseline?.nakedMaximumHealth??4800,'Observed/proxy naked health takes precedence; otherwise explicit manual cap applies');
 await click('[data-view=encounter]');await screenshot('cp175-shadow-health-precedence-desktop','#shadowHealthSettings');
 const healthBuild=await exportBuild('shadow-manual-player-health');assert.equal(Number(healthBuild.data.encounter.shadowPriestHealth),4800);checks++;
 await setInput('#shadowTargetInitialHealth',20);const initialHealth=await run('shadow-target-initial-health');
 assert.equal(initialHealth.data.originalSimulationRequest.shadow.targetInitialHealthPercent,20);
 assert.deepEqual(initialHealth.data.combatSummary.targetSemantics,{level:63,mitigation:'unmitigated',targetCount:1,initialHealthPercent:20});
 assert.equal(initialHealth.data.originalSimulationRequest.shadow.priestMaximumHealth,4800);
 assert.equal(initialHealth.data.combatSummary.maximumHealth,manualHealth.data.combatSummary.maximumHealth,'Target initial health is not player maximum health');
 await importReport('A',manualHealth.file);await importReport('B',initialHealth.file);assert.match(await text('#compareStatus'),/Not directly comparable/);checks++;
 await importBuild(timed.file);

 await command('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 await click('[data-view=encounter]');await screenshot('cp175-shadow-encounter-mobile','#classCapabilityNotes');
 await screenshot('cp175-shadow-health-precedence-mobile','#shadowHealthSettings');
 await click('[data-view=talents]');await screenshot('cp175-shadow-talents-mobile','#talentSummary');
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await setInput('#classKey','mage');await until("document.querySelector('#characterTitle').textContent.includes('Mage') && /Class defaults loaded/.test(document.querySelector('#status').textContent) && !document.querySelector('#iterations').disabled && !document.querySelector('#racialActive').disabled");
 await click('[data-view=effects]');await click('[data-modifier-key=\"arcane-intellect\"]');
 const mage=await exportBuild('mage-after-shadow');
 await selectFile('#importBuild','#file',path.join(reportDir,'rejected-custom-priority.json'));await until("/unsupported|unavailable|fixed/i.test(document.querySelector('#status').textContent)");
 assert.deepEqual((await exportBuild('mage-after-rejected-shadow')).data,mage.data,'Cross-class rejection preserves Mage build');
 await setInput('#spec','fire');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.equal(await evaluate("document.querySelector('#presetDialog').open"),false,'Cancel dismisses the dirty-preset dialog');
 assert.deepEqual((await exportBuild('mage-dirty-preserved')).data,mage.data,'Failed Shadow import preserves dirty preset protection');checks++;
 await importBuild(timed.file);assert.equal((await exportBuild('shadow-after-mage')).data.classKey,'priest');
 await importBuild(path.join(fixtureDir,'cp175-f3a-arms-mortal-strike.json'));
 const warrior=await run('warrior-after-shadow');assert.equal(warrior.data.build.classKey,'warrior');
 assert.ok(warrior.data.combatSummary.damageDone>0);assert.ok(warrior.data.originalSimulationRequest.warrior);checks++;
 await importBuild(timed.file);

 assert.deepEqual(browserErrors,[],'No unhandled browser exceptions');
 fs.writeFileSync(path.join(reportDir,'http-observations.json'),JSON.stringify(await evaluate('window.__shadowHttp'),null,2));
 fs.writeFileSync(path.join(reportDir,'causal-gate-receipts.json'),JSON.stringify(gateReceipts,null,2));
 fs.writeFileSync(path.join(reportDir,'acceptance-summary.json'),JSON.stringify({status:'passed',sourceLabel:process.env.CP175_ACCEPTANCE_LABEL||'local-shadow-ui',checks,reports:reportFiles,screenshots:screenshotDir},null,2));
 console.log(`Shadow real-browser acceptance passed: ${checks} gates. Evidence: ${reportDir}`);
}
main().catch(async error=>{
 console.error(error);process.exitCode=1;
 try{await screenshot("cp175-shadow-browser-failure");fs.writeFileSync(path.join(reportDir,'failure-observations.json'),JSON.stringify({error:String(error),dom:await evaluate('document.body.innerText'),input:await evaluate('window.__soloDpsClickHistory||[]'),buildState:await evaluate("({state:document.querySelector('#status').dataset.buildState,classKey:document.querySelector('#classKey').value,spec:document.querySelector('#spec').value,dialogOpen:document.querySelector('#presetDialog').open,exportDisabled:document.querySelector('#exportBuild').disabled})"),downloadFiles:fs.readdirSync(reportDir),http:await evaluate('window.__shadowHttp')},null,2));}catch{}
}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(400);
 // Delete only this harness's generated profile, never a computed parent/root.
 const relative=path.relative(path.resolve(os.tmpdir()),path.resolve(profile));
 if(relative&&!relative.startsWith("..")&&!path.isAbsolute(relative))
  fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});
});
