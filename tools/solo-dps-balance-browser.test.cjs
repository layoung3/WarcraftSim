#!/usr/bin/env node
"use strict";
// End-to-end acceptance against an already running local API. Every successful
// report is downloaded from a real UI simulation, never manufactured in a test.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const {compareReports}=require("../WarcraftSim.Api/wwwroot/solo-dps-comparison.js");
const {assertSoloDpsIdentity}=require("./solo-dps-browser-identity.cjs");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const reportDir=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),"cp174-full-reports"),"balance-browser");
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||path.join(os.tmpdir(),"cp174-ui-captures"));
const fixtureDir=path.resolve(__dirname,"../docs/acceptance/cp175");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-balance-browser-"));
const port=Number(process.env.CP175_BALANCE_CDP_PORT||19357);
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
 if(selector)await evaluate(`(()=>{document.querySelector(${JSON.stringify(selector)}).scrollIntoView({block:'start'});window.scrollBy(0,-document.querySelector('.topbar').getBoundingClientRect().height-16);})()`);
 const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 fs.writeFileSync(path.join(screenshotDir,name+".png"),Buffer.from(shot.data,"base64"));
}
const rows=()=>evaluate("[...document.querySelectorAll('#compareRows tr')].map(r=>[...r.children].map(c=>c.textContent))");
async function verifyParity(report){
 const p=report.resolvedCharacter,sim=report.rawServerResponse.sim;
 for(const key of ['maximumMana','maximumHealth','spellPower'])assert.equal(p[key],sim[key],key+' preview/execution');
 assert.equal(p.rotationPreviewScope,'initial-priority-adaptive-order-changes-during-combat');
 assert.deepEqual(sim.resolvedRotation.split(' > ').sort(),p.rotation.entries.map(x=>x.abilityKey).sort(),'Initial preview and ending runtime priority use same supported action set, not necessarily same adaptive order');
 if(report.originalSimulationRequest.balance.customPriority.length)assert.equal(sim.resolvedRotation,p.rotation.entries.map(x=>x.abilityKey).join(' > '),'Explicit custom order does not adapt');
 assert.equal(sim.run.summary.actorSummaries.druid.startingResources.mana,p.maximumMana,'Actual druid actor starting mana');
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
 await command("Page.addScriptToEvaluateOnNewDocument",{source:"window.__balanceHttp=[];const originalFetch=window.fetch;window.fetch=async(...args)=>{const response=await originalFetch(...args);if(['/api/forever/character/preview-balance','/api/forever/rotation/preview','/api/forever/character/simulate-level60'].includes(String(args[0]))){const capture={observedAt:performance.now(),url:String(args[0]),request:args[1]?.body?JSON.parse(args[1].body):null,status:response.status};try{capture.response=await response.clone().json();}catch{}window.__balanceHttp.push(capture);}return response;};window.__comparisonCompleted=0;document.addEventListener('warcraftsim:simulation-complete',()=>window.__comparisonCompleted++);"});
 await command("Page.setDownloadBehavior",{behavior:"allow",downloadPath:reportDir});
 await command("Page.setInterceptFileChooserDialog",{enabled:true});
 await command("Emulation.setDeviceMetricsOverride",{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await command("Page.navigate",{url:BASE+"/solo-dps.html"});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')");


 await setInput('#classKey','druid');
 await until("document.querySelector('#spec').value==='balance' && /Class defaults loaded/.test(document.querySelector('#status').textContent)");
 await setInput('#race','tauren');
 await until("document.querySelector('#characterSubtitle').textContent.startsWith('Tauren ')");
 if(!await evaluate("document.querySelector('#provisional').checked"))await click('#provisional');
 await until("document.querySelector('#statStatus').textContent==='Up to date'");
 assert.deepEqual(await evaluate("[...document.querySelector('#spec').options].map(x=>x.value)"),['balance']);checks++;
 await click('[data-view=talents]');
 assert.match(await text('#talentCoverage'),/historical|provisional/i);
 console.log('Balance selector, explicit historical baseline and source disclosure passed.');
 const base=await exportBuild('balance-baseline');
 const preset=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/druid-balance.json'),'utf8'));
 assert.deepEqual(base.data.talents.ranks,preset.ranks);
 assert.equal(preset.totalRankedPoints,35);assert.equal(base.data.talents.ranks['natures-grace']||0,0);
 assert.match(await text('#talentCoverage'),/35\/0\/0.*16 unspent/i);assert.match(await text('#talentSummary'),/16 remaining/);
 assert.match(await text('#status'),/35 explicitly ranked points and 16 unspent.*Partial/i);
 assert.equal(Object.values(base.data.talents.ranks).reduce((a,b)=>a+b),35);
 assert.deepEqual(Object.fromEntries(Object.entries(base.data.talents).filter(([k])=>k.endsWith('PointsSpent'))),{balancePointsSpent:35,feralPointsSpent:0,restorationPointsSpent:0});checks++;
 await screenshot('cp175-balance-talents-desktop','#talentSummary');
 await click('[data-view=encounter]');
 assert.match(await text('#classCapabilityNotes'),/63/);
 assert.match(await text('#classCapabilityNotes'),/unmitigated/i);
 assert.match(await text('#classCapabilityNotes'),/racial/i);
 assert.match(await text('#classCapabilityNotes'),/initial.*rotation.*reordered dynamically/i);
 for(const limitation of [/Omen/i,/Eclipse/i,/periodic/i,/coefficients/i])assert.match(await text('#classCapabilityNotes'),limitation);
 assert.equal(await evaluate("document.querySelector('#iterations').disabled"),true);
 assert.equal(await evaluate("document.querySelector('#scriptedBossEnabled').getClientRects().length===0"),true);checks++;
 await click('[data-view=effects]');assert.equal(await evaluate("document.querySelector('#racialActive').disabled"),true);
 await click('[data-view=equipment]');
 assert.match(await text('#catalogCoverage'),/pending.*eligibility evidence/i);
 await setInput('#duration',30);
 await evaluate("document.querySelector('#timeline').checked=true");
 const timed=await exportBuild('balance-timed');
 const a=await run('balance-baseline');await verifyParity(a.data);assert.doesNotMatch(await text('#distributionNotice'),/Select 2-100/,'Balance results must not suggest unsupported batch statistics');
 assert.equal(a.data.originalSimulationRequest.classKey,'druid');
 assert.ok(a.data.talentAllocation.selected.length>0);
 for(const talent of a.data.talentAllocation.selected){assert.equal(talent.historicalValidationSourceCommit,preset.provenance.sourceCommit);assert.equal(talent.historicalValidationSourceUrl,preset.provenance.sources[0]);assert.match(talent.historicalValidationSourceStatus,/historical.*provisional|historical.*validated/);}
 assert.equal(a.data.originalSimulationRequest.specialization,'balance');
 assert.equal(a.data.originalSimulationRequest.balance.majorManaPotionCharges,0);
 assert.equal(a.data.originalSimulationRequest.balance.combatItems.majorManaPotionCharges,0);
 assert.equal(a.data.combatSummary.actorKey,'druid');
 assert.equal(a.data.combatSummary.ownerDamage,a.data.combatSummary.damageDone);
 assert.ok(a.data.combatSummary.maximumMana>0);
 assert.deepEqual(a.data.combatSummary.targetSemantics,{level:63,mitigation:'unmitigated',targetCount:1,initialHealthPercent:100});
 assert.ok(!Object.entries(a.data.combatSummary.combatItemUses).some(([k,v])=>/potion/i.test(k)&&v>0));
 assert.equal(a.data.combatSummary.maximumMana,a.data.resolvedCharacter.maximumMana);
 await replay('Deterministic replay matched');checks++;console.log('Balance actual run, preview/resource parity and replay passed.');
 await importReport('A',a.file);await importReport('B',a.file);
 assert.match(await text('#compareStatus'),/Encounter settings match/);
 assert.equal(compareReports(a.data,a.data).dpsDelta,0);checks++;
 // Every malformed fixture starts with a real exported build. Rejection must
 // leave that build intact, not merely preserve the selected dropdown text.
 const cases=[['racial',x=>x.racialActive=true],['gear',x=>x.gear.items.push({slotKey:'main-hand',itemId:1})],['iterations',x=>x.encounter.iterations=2],['target-level',x=>x.encounter.targetLevel=62],['target-armor',x=>x.encounter.targetArmor=3500],['script',x=>x.encounter.soloScript.enabled=true],['debuff',x=>x.modifiers.targetDebuffs.push('curse-of-elements')],['conditions',x=>x.encounter.priorityRows=[{key:'wrath',enabled:true,conditions:[{kind:'mana-percent',value:50}]}]],['target-override',x=>x.encounter.priorityRows=[{key:'wrath',enabled:true,targetActorKey:'target'}]],['rotation-condition',x=>x.encounter.rotationConditions.foo=1]];
 const rejectionPatterns={racial:'racial actives',gear:'eligibility evidence',iterations:'one deterministic', 'target-level':'targetLevel','target-armor':'targetArmor',script:'generic encounter scripts',debuff:'target debuffs',conditions:'conditional priorities','target-override':'conditional priorities','rotation-condition':'custom rotation conditions'};
 for(const [name,mutate] of cases){
  const data=structuredClone(timed.data);mutate(data);const file=path.join(reportDir,'rejected-'+name+'.json');fs.writeFileSync(file,JSON.stringify(data));
  await evaluate("window.__importStatusChanges=0;window.__importObserver?.disconnect();window.__importObserver=new MutationObserver(()=>window.__importStatusChanges++);window.__importObserver.observe(document.querySelector('#status'),{childList:true,subtree:true,characterData:true});");
  await selectFile('#importBuild','#file',file);
  await until(`window.__importStatusChanges>0 && document.querySelector('#status').textContent.includes(${JSON.stringify(rejectionPatterns[name])})`);
  const preserved=await exportBuild('after-rejected-'+name);assert.deepEqual(preserved.data,timed.data,name+' rejection preserves full build');checks++;console.log('Rejected import preserved build:',name);
 }
 await importBuild(timed.file);const roundtrip=await exportBuild('balance-roundtrip');assert.deepEqual(roundtrip.data,timed.data);checks++;
 // Uppercase Insect Swarm still must pass engine learning gates with valid zero-rank allocation.
 for(const [label,key,unlearned] of [['uppercase-unlearned','INSECT-SWARM',true]]){
  const invalid=structuredClone(timed.data);
  invalid.encounter.customPriority=[key];invalid.encounter.priorityRows=[{key,enabled:true}];
  if(unlearned){invalid.talents.ranks={};for(const field of Object.keys(invalid.talents))if(field.endsWith('PointsSpent'))invalid.talents[field]=0;}
  const file=path.join(reportDir,'rejected-'+label+'.json');fs.writeFileSync(file,JSON.stringify(invalid));
  await selectFile('#importBuild','#file',file);
  await until(`document.querySelector('#status').textContent.includes(${JSON.stringify('Unsupported Balance custom priority: '+key.toLowerCase())})`);
  const preserved=await exportBuild('after-rejected-'+label);
  assert.deepEqual(preserved.data,timed.data,label+' import rejection preserves full build');checks++;
  gateReceipts.push({gate:label,completedAt:await evaluate('performance.now()'),status:await text('#status'),fullPriorBuildPreserved:true});
 }

 await click('[data-view=effects]');
 const available=await evaluate("[...document.querySelectorAll('[data-modifier-key]')].map(x=>({key:x.dataset.modifierKey,disabled:x.disabled,tag:x.tagName,text:x.textContent})).filter(x=>!x.disabled)");
 fs.writeFileSync(path.join(reportDir,'available-modifiers.json'),JSON.stringify(available,null,2));
 assert.ok(available.some(x=>x.key==='arcane-intellect'),'Static Intellect buff is selectable');
 gateReceipts.push({gate:'before-static-buff-click',at:await evaluate('performance.now()'),status:await text('#status')});
 const buffPreviewStart=await evaluate("window.__balanceHttp.length");
 await click('[data-modifier-key="arcane-intellect"]');
 await until(`document.querySelector('[data-modifier-key=\"arcane-intellect\"]').getAttribute('aria-pressed')==='true' && window.__balanceHttp.slice(${buffPreviewStart}).some(x=>x.url==='/api/forever/character/preview-balance'&&x.status===200&&x.request?.externalModifiers?.buffs?.includes('arcane-intellect')) && document.querySelector('#statStatus').textContent==='Up to date'`);
 gateReceipts.push({gate:'fresh-selected-buff-preview',at:await evaluate('performance.now()'),selected:await evaluate("document.querySelector('[data-modifier-key=\"arcane-intellect\"]').getAttribute('aria-pressed')")});
 const b=await run('balance-static-intellect');await verifyParity(b.data);
 assert.ok(b.data.originalSimulationRequest.balance.externalModifiers.buffs.includes('arcane-intellect'));
 assert.ok(b.data.combatSummary.maximumMana>a.data.combatSummary.maximumMana,'Static Intellect must affect real mana');
 assert.notEqual(b.data.combatSummary.dps,a.data.combatSummary.dps,'Actual changed build must affect seeded gameplay');
 await importReport('A',a.file);await importReport('B',b.file);assert.match(await text('#compareStatus'),/Not directly comparable/);assert.match(await text('#compareWarnings'),/Buffs differs/);checks++;
 await screenshot('cp175-balance-comparison-desktop','.comparison-workspace');
 await click('[data-view=rotation]');await click('#loadPriority');
 await until("document.querySelector('#priorityStatus').textContent.includes('Showing the current engine-generated priority')");
 const originalPriority=await evaluate("document.querySelector('#priorityText').value");assert.ok(originalPriority.includes('wrath'));
 const rotationReceipt=await evaluate("window.__balanceHttp.filter(x=>x.url==='/api/forever/rotation/preview'&&x.request?.classKey==='druid').at(-1)");
 assert.equal(rotationReceipt.status,200);assert.equal(rotationReceipt.response.spellPower,b.data.resolvedCharacter.spellPower);
 assert.deepEqual(rotationReceipt.response.entries.map(x=>x.abilityKey),b.data.resolvedCharacter.rotation.entries.map(x=>x.abilityKey));
 await evaluate("document.querySelector('#priorityText').closest('details').open=true");
 await setInput('#priorityText','wrath');await click('#applyPriority');
 await until("/Custom priority validated by the engine/.test(document.querySelector('#priorityStatus').textContent)");
 const custom=await run('balance-custom-priority');await verifyParity(custom.data);
 assert.deepEqual(custom.data.originalSimulationRequest.balance.customPriority,['wrath']);
 assert.ok(custom.data.combatSummary.damageByAbility['wrath']>0);checks++;
 await importReport('A',b.file);await importReport('B',custom.file);assert.match(await text('#compareStatus'),/Encounter settings match/);
 assert.notEqual(custom.data.combatSummary.dps,b.data.combatSummary.dps);assert.equal(compareReports(b.data,custom.data).comparable,true);checks++;
 await screenshot('cp175-balance-distinct-builds-desktop','.comparison-workspace');
 await click('[data-view=encounter]');await setInput('#duration',35);
 const mismatch=await run('balance-duration-mismatch');await importReport('B',mismatch.file);
 assert.match(await text('#compareStatus'),/Not directly comparable/);assert.match(await text('#compareWarnings'),/Duration differs/);checks++;
 await importBuild(timed.file);const restored=await run('balance-restored');
 assert.deepEqual(restored.data.combatSummary,a.data.combatSummary,'Full build re-import restores actual seeded gameplay');
 await replay('Deterministic replay matched');checks++;console.log('Balance actual run, preview/resource parity and replay passed.');
 await command('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 await click('[data-view=encounter]');await screenshot('cp175-balance-encounter-mobile','#classCapabilityNotes');
 await click('[data-view=talents]');await screenshot('cp175-balance-talents-mobile','#talentSummary');
 await command('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await setInput('#classKey','mage');await until("document.querySelector('#spec').value!=='balance' && /Class defaults loaded/.test(document.querySelector('#status').textContent) && document.querySelector('#characterTitle').textContent.includes('Mage') && !document.querySelector('#iterations').disabled && !document.querySelector('#racialActive').disabled");
 assert.equal(await evaluate("document.querySelector('#iterations').disabled"),false);
 assert.equal(await evaluate("document.querySelector('#racialActive').disabled"),false);
 await click('[data-view=effects]');await click('[data-modifier-key="arcane-intellect"]');
 const mage=await exportBuild('mage-after-balance');assert.equal(mage.data.classKey,'mage');checks++;
 await selectFile('#importBuild','#file',path.join(reportDir,'rejected-uppercase-unlearned.json'));
 await until("/Unsupported Balance custom priority/i.test(document.querySelector('#status').textContent)");
 const crossRestored=await exportBuild('mage-after-rejected-balance');assert.deepEqual(crossRestored.data,mage.data,'Cross-class rejected Balance import preserves complete Mage build');checks++;
 await setInput('#spec','fire');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');
 assert.deepEqual((await exportBuild('mage-dirty-preserved')).data,mage.data,'Failed import preserves dirty-build preset protection');checks++;
 await importBuild(timed.file);assert.equal((await exportBuild('balance-after-mage')).data.classKey,'druid');
 assert.deepEqual(browserErrors,[],'No unhandled browser exceptions');
 fs.writeFileSync(path.join(reportDir,'http-observations.json'),JSON.stringify(await evaluate('window.__balanceHttp'),null,2));
 fs.writeFileSync(path.join(reportDir,'causal-gate-receipts.json'),JSON.stringify(gateReceipts,null,2));
 fs.writeFileSync(path.join(reportDir,'acceptance-summary.json'),JSON.stringify({status:'passed',sourceLabel:process.env.CP175_ACCEPTANCE_LABEL||'local-balance-ui',checks,reports:reportFiles,screenshots:screenshotDir},null,2));
 console.log(`Balance real-browser acceptance passed: ${checks} gates. Evidence: ${reportDir}`);
}
main().catch(async error=>{
 console.error(error);process.exitCode=1;
 try{await screenshot("cp175-balance-browser-failure");fs.writeFileSync(path.join(reportDir,'failure-observations.json'),JSON.stringify({error:String(error),dom:await evaluate('document.body.innerText'),http:await evaluate('window.__balanceHttp')},null,2));}catch{}
}).finally(async()=>{
 try{ws?.close();}catch{}proc.kill();await sleep(400);
 // Delete only this harness's generated profile, never a computed parent/root.
 const relative=path.relative(path.resolve(os.tmpdir()),path.resolve(profile));
 if(relative&&!relative.startsWith("..")&&!path.isAbsolute(relative))
  fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});
});
