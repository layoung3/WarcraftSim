#!/usr/bin/env node
"use strict";
// Genuine browser acceptance. Local execution is intentionally deferred to the
// user-selected existing Actions browser job; pure tests are not browser proof.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {spawn}=require('node:child_process');
const {assertSoloDpsIdentity}=require("./solo-dps-browser-identity.cjs");
const {createBrowserClick}=require('./solo-dps-browser-click.cjs');
const {createRegionCapture}=require('./solo-dps-rogue-capture.cjs');
const {installObserver,canonical,rejectionCases,gateNames,assertRejectedReportState}=require('./solo-dps-feral-browser-support.cjs');
const {compareReports}=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const {inspect:inspectEvents}=require('../WarcraftSim.Api/wwwroot/solo-dps-events.js');
const BASE=process.env.CP174_BASE_URL||'http://127.0.0.1:5142',origin=new URL(BASE);
assert.ok(origin.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(origin.hostname));
assert.ok(!origin.username&&!origin.password&&!origin.search&&!origin.hash);
const root=path.resolve(process.env.CP174_REPORT_DIR||path.join(os.tmpdir(),'cp174-full-reports','feral-solo-ui'));
fs.mkdirSync(root,{recursive:true});
// Never remove or overwrite earlier acceptance outputs, including failed runs.
const reportDir=fs.mkdtempSync(path.join(root,'browser-')),stamp=path.basename(reportDir);
const screenshotDir=path.resolve(process.env.CP174_SCREENSHOT_DIR||'/tmp/cp174-ui-captures');fs.mkdirSync(screenshotDir,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'cp175-feral-browser-'));
const port=Number(process.env.CP175_FERAL_CDP_PORT||19371);
const proc=spawn(process.env.CP174_CHROME||'/usr/bin/google-chrome',['--headless','--no-sandbox','--disable-gpu','--disable-dev-shm-usage',`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,'about:blank'],{stdio:'ignore',windowsHide:true});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
let ws,id=0;const pending=new Map(),browserErrors=[],gates=[],coverage=[],reportFiles=[],inputs=[];
function command(method,params={}){return new Promise((resolve,reject)=>{const key=++id,timer=setTimeout(()=>{pending.delete(key);reject(Error('CDP timed out: '+method));},20000);pending.set(key,{resolve:value=>{clearTimeout(timer);resolve(value);},reject:error=>{clearTimeout(timer);reject(error);}});ws.send(JSON.stringify({id:key,method,params}));});}
async function evaluate(expression){const result=await command('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(result.exceptionDetails)throw Error('Browser exception: '+JSON.stringify(result.exceptionDetails));return result.result?.value;}
const text=selector=>evaluate(`document.querySelector(${JSON.stringify(selector)})?.textContent`);
const sequence=()=>evaluate('window.__feralSequence');
function mark(name,details={}){assert.ok(gateNames.includes(name),'Unknown gate '+name);assert.ok(!gates.some(x=>x.name===name),'Duplicate gate '+name);gates.push({name,...details});}
async function until(expression,timeout=40000){const start=Date.now();while(Date.now()-start<timeout){if(await evaluate(expression))return;await sleep(100);}throw Error('Timed out: '+expression+'; '+JSON.stringify(await evaluate("({status:document.querySelector('#status')?.textContent,stats:document.querySelector('#statStatus')?.textContent,compare:document.querySelector('#compareStatus')?.textContent})")));}
const trustedClick=createBrowserClick({evaluate,command});
async function click(selector,button='left'){await trustedClick(selector,button);inputs.push(await evaluate('window.__soloDpsClickCurrent'));}
async function keyPress(key,code,windowsVirtualKeyCode,modifiers=0){for(const type of ['keyDown','keyUp'])await command('Input.dispatchKeyEvent',{type,key,code,windowsVirtualKeyCode,modifiers});}
async function setInput(selector,value,blur=true){
 const target=await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});return {tag:e.tagName,index:e.tagName==='SELECT'?[...e.options].findIndex(x=>x.value===${JSON.stringify(String(value))}):-1};})()`);
 await click(selector);
 if(target.tag==='SELECT'){assert.ok(target.index>=0,'Missing native select option '+selector+' '+value);await keyPress('Home','Home',36);for(let i=0;i<target.index;i++)await keyPress('ArrowDown','ArrowDown',40);await keyPress('Enter','Enter',13);}
 else{await keyPress('a','KeyA',65,2);if(String(value))await command('Input.insertText',{text:String(value)});else await keyPress('Backspace','Backspace',8);if(blur)await keyPress('Tab','Tab',9);}
 const selected=await evaluate(`({value:document.querySelector(${JSON.stringify(selector)}).value,confirmation:document.querySelector('#presetDialog').open})`);
 if(!(selector==='#spec'&&selected.confirmation))assert.equal(selected.value,String(value),'Trusted input must reach '+selector);
}
async function setChecked(selector,value){const current=await evaluate(`document.querySelector(${JSON.stringify(selector)}).checked`);if(current!==value)await click(selector);}
async function chooseSpec(value){await setInput('#spec',value);if(await evaluate("document.querySelector('#presetDialog').open"))await click('#confirmPreset');}
// The playable race inventory is broader than the historical Feral adapter.
// Select the numerical fixture explicitly, never inherit the first Druid race.
async function selectFeralFixtureRace(){
 await setInput('#race','night-elf');
 await until("document.querySelector('#race').value==='night-elf'&&document.querySelector('#characterSubtitle').textContent==='Night Elf · Level 60'&&document.querySelector('#status').dataset.buildState==='input-required'");
 await assertIdentity();await assertBlockedEditable();
}
async function assertIdentity(){
 const identity=await evaluate("({classKey:document.querySelector('#classKey').value,specialization:document.querySelector('#spec').value,classLabel:document.querySelector('#classKey').selectedOptions[0].textContent,race:document.querySelector('#race').selectedOptions[0].textContent,displayedTitle:document.querySelector('#characterTitle').textContent,subtitle:document.querySelector('#characterSubtitle').textContent})");
 assertSoloDpsIdentity(identity,{classKey:'druid',specialization:'feral'});
}
async function downloaded(filename,selector,destination){
 const file=path.join(reportDir,filename);assert.equal(fs.existsSync(file),false,'Previous download must already be preserved');
 await click(selector);let data;
 for(let i=0;i<160;i++){if(fs.existsSync(file)&&!fs.readdirSync(reportDir).some(x=>x.endsWith('.crdownload'))){try{data=JSON.parse(fs.readFileSync(file,'utf8'));break;}catch{}}await sleep(100);}
 assert.ok(data,'Actual complete JSON download required: '+filename);const target=path.join(reportDir,destination);assert.equal(fs.existsSync(target),false,'Do not replace acceptance output');fs.renameSync(file,target);return {data,file:target};
}
const exportBuild=name=>downloaded('warcraftsim-build.json','#exportBuild',name+'-build.json');
function fixture(name,data){const file=path.join(reportDir,name+'.json');assert.equal(fs.existsSync(file),false);fs.writeFileSync(file,JSON.stringify(data,null,2));return file;}
async function observeImport(input='#file',status='#status'){
 await evaluate(`(()=>{window.__importStatus=false;window.__importChanged=false;window.__importObserver?.disconnect();window.__importObserver=new MutationObserver(()=>window.__importStatus=true);window.__importObserver.observe(document.querySelector(${JSON.stringify(status)}),{childList:true,subtree:true,characterData:true});document.querySelector(${JSON.stringify(input)}).addEventListener('change',()=>window.__importChanged=true,{once:true});})()`);
}
async function selectFile(button,input,file){assert.ok(fs.existsSync(file));await click(button);const doc=await command('DOM.getDocument'),node=await command('DOM.querySelector',{nodeId:doc.root.nodeId,selector:input});await command('DOM.setFileInputFiles',{nodeId:node.nodeId,files:[path.resolve(file)]});}
async function importBuild(file){await observeImport();const after=await sequence();await selectFile('#importBuild','#file',file);await until("window.__importChanged&&window.__importStatus&&document.querySelector('#file').value===''&&document.querySelector('#status').textContent==='Build imported.'");await ready(after);await assertIdentity();assert.equal(await evaluate("document.querySelector('#race').value"),JSON.parse(fs.readFileSync(file,'utf8')).race,'Import restores the explicitly saved race, not the class default');}
async function presetReady(classKey,specialization,after){await until(`document.querySelector('#classKey').value===${JSON.stringify(classKey)}&&document.querySelector('#spec').value===${JSON.stringify(specialization)}&&document.querySelector('#status').dataset.buildState===${JSON.stringify(specialization==='feral'?'input-required':'ready')}&&window.__feralHttp.some(x=>x.sequence>${after}&&x.url===${JSON.stringify('/api/forever/talents/'+classKey+'/'+specialization+'/default-itemized')}&&x.status===200)`);}
const draftExpression="[...document.querySelectorAll('#classKey,#spec,#race,#provisional,#duration,#seed,#targetHealth,#timeline,#feralSettings input,#feralSettings select,#feralFurorRank')].map(x=>({id:x.id,value:x.value,checked:x.checked}))";
const paintExpression="({stats:document.querySelector('#stats').innerHTML,scope:document.querySelector('#statScope').textContent,status:document.querySelector('#statStatus').textContent,baseline:document.querySelector('#feralBaseline').textContent,priority:document.querySelector('#feralRotationEntries').textContent,visiblePriority:document.querySelector('#feralPriorityOrder').textContent})";
async function assertBlockedEditable(){
 const state=await evaluate("({blocked:['run','save','exportBuild'].map(id=>document.getElementById(id).disabled),editable:['feralPreTalentCatAttackPower','provisional','feralShapeshiftMana','seed','feralTargetBehind'].map(id=>!document.getElementById(id).disabled&&!document.getElementById(id).closest('[inert]')),stats:document.querySelector('#stats').textContent,priority:document.querySelector('#feralRotationEntries').textContent,visiblePriority:document.querySelector('#feralPriorityOrder').textContent})");
 assert.ok(state.blocked.every(Boolean),'Invalid/unprepared build cannot run/save/export '+JSON.stringify(state));assert.ok(state.editable.every(Boolean),'Unprepared inputs stay editable '+JSON.stringify(state));assert.equal(state.stats.trim(),'','No stale prepared stats');assert.match(state.priority,/waiting|preparation|required/i);assert.equal(state.visiblePriority,'','No stale visible configured priority');
}
async function ready(after,fields={}){
 await until(`(()=>{const r=window.__feralHttp.find(x=>x.sequence===window.__latestFeralPreview);return r&&r.sequence>${after}&&r.status===200&&r.deliveredAt!==undefined&&document.querySelector('#classKey').value==='druid'&&document.querySelector('#spec').value==='feral'&&document.querySelector('#status').dataset.buildState==='ready'&&!document.querySelector('#run').disabled&&!document.querySelector('#exportBuild').disabled&&document.querySelector('#statStatus').textContent==='Up to date'&&r.request.character.race===document.querySelector('#race').value;})()`);
 const receipt=await evaluate('window.__feralHttp.find(x=>x.sequence===window.__latestFeralPreview)');
 await assertIdentity();assert.equal(receipt.response.baseline.race,receipt.request.character.race,'Prepared baseline owns the explicitly selected race');
 assert.equal(canonical(receipt.response.request),canonical(receipt.request),'Typed preparation must retain complete exact submitted request');
 const controls=await evaluate("[...document.querySelectorAll('#feralSettings input,#feralSettings select')].map(x=>({id:x.id,type:x.type,value:x.value,checked:x.checked}))");
 for(const c of controls){const key=c.id.slice(5,6).toLowerCase()+c.id.slice(6),expected=receipt.request.manualInputs?.[key]??receipt.request[key];if(expected===undefined)continue;assert.equal(c.type==='checkbox'?c.checked:c.type==='number'?Number(c.value):c.value,expected,'Current control agrees with exact fresh preview '+c.id);}
 const encounter=await evaluate("({seed:Number(document.querySelector('#seed').value),durationSeconds:Number(document.querySelector('#duration').value),targetMaximumHealth:Number(document.querySelector('#targetHealth').value),captureTimeline:document.querySelector('#timeline').checked})");for(const [key,value] of Object.entries(encounter))assert.equal(receipt.request[key],value,'Current encounter owns preview '+key);
 const ranks=await evaluate("[...document.querySelectorAll('#talents [data-talent-key]')].map(x=>[x.dataset.talentKey,Number(x.querySelector('.talent-rank').textContent.split('/')[0])])");for(const [key,value] of ranks)assert.equal(receipt.request.talents.ranks[key]??0,value,'Current talent owns preview '+key);
 for(const [key,value] of Object.entries(fields))assert.equal(receipt.request.manualInputs?.[key]??receipt.request[key],value,'Changed input owns current preview '+key);
 const p=receipt.response.preview;
 const expected=[['Manual pre-talent Cat AP',p.authoredAttackPower],['HOTW AP increment',p.heartOfTheWildAttackPowerIncrement],['Predatory Strikes AP',p.predatoryStrikesAttackPower],['Prepared spellbook AP',p.spellbookAttackPower],['Maximum Health (racials omitted)',p.maximumHealth],['Maximum Mana',p.maximumMana],['Initial shift Mana cost',p.initialShiftManaCost],['Post-shift Mana',p.manaAfterInitialShift],['Maximum Energy',p.maximumEnergy],['Starting Energy',p.startingEnergy]].map(([key,value])=>[key,String(value)]);
 assert.deepEqual(await evaluate("[...document.querySelectorAll('#stats > div')].map(x=>[x.querySelector('span').textContent,x.querySelector('strong').textContent])"),expected,'Every prepared stat must come from this fresh receipt');
 assert.deepEqual(JSON.parse(await text('#feralRotationEntries')),p.configuredRotation,'Configured priority is this exact preparation, not immediate eligibility');assert.equal(await text('#feralPriorityOrder'),p.configuredRotation.entries.map(entry=>String(entry.priority)+'. '+entry.abilityKey.replaceAll('-',' ').replace(/\b\w/g,c=>c.toUpperCase())+(entry.isEnabled?'':' (disabled)')).join(' > '),'Every visible priority entry matches exact preparation');return receipt;
}
async function change(field,value){const selector='#feral'+field[0].toUpperCase()+field.slice(1),current=await evaluate(`document.querySelector(${JSON.stringify(selector)}).${typeof value==='boolean'?'checked':'value'}`);assert.notEqual(String(current),String(value),'Do not wait for a fresh response after no-op '+field);const after=await sequence();if(typeof value==='boolean')await click(selector);else await setInput(selector,value);return ready(after,{[field]:value});}
async function holdResponse(url,prefix=false){await evaluate(`window.__feralHold={url:${JSON.stringify(url)},prefix:${prefix},armed:true,held:false,released:false}`);}
async function releaseResponse(label,key='__feralHold'){const expression='window['+JSON.stringify(key)+']',held=await evaluate(`({held:${expression}.held,sequence:${expression}.sequence,url:${expression}.url})`);assert.equal(held.held,true);await evaluate(`(async()=>{${expression}.release();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));})()`);assert.equal(await evaluate(expression+'.released'),true);coverage.push({gate:'held-real-response',label,...held});}
const captureRegion=createRegionCapture({evaluate,command,receipts:coverage,save:(name,data)=>fs.writeFileSync(path.join(screenshotDir,name),Buffer.from(data,'base64'),{flag:'wx'})});
const capture=(label,selector)=>captureRegion('feral-'+stamp+'-'+label,selector);
async function viewport(width,height,mobile){await command('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile});}
async function readable(selector){const layout=await evaluate(`(()=>{const elements=[...document.querySelectorAll(${JSON.stringify(selector)})].filter(x=>x.getClientRects().length);return {viewport:document.documentElement.clientWidth,width:document.documentElement.scrollWidth,rows:elements.map(x=>{const r=x.getBoundingClientRect();return {text:x.textContent,left:r.left,right:r.right,width:x.clientWidth,scrollWidth:x.scrollWidth,height:x.clientHeight,scrollHeight:x.scrollHeight};})};})()`);assert.ok(layout.rows.length);assert.ok(layout.width<=layout.viewport+1,'No horizontal document overflow');for(const r of layout.rows){assert.ok(r.left>=-1&&r.right<=layout.viewport+1,'Visible text stays within viewport '+r.text);assert.ok(r.scrollWidth<=r.width+1&&r.scrollHeight<=r.height+1,'Text not clipped '+r.text);}coverage.push({gate:'readable-layout',selector,...layout});}
async function run(name,{lock=false}={}){
 await click('[data-view=encounter]');const before=await sequence(),completed=await evaluate('window.__feralCompleted.length'),draft=await evaluate(draftExpression);
 if(lock)await holdResponse('/api/forever/character/simulate-level60');
 await click('#run');
 if(lock){await until('window.__feralHold.held');const controls=await evaluate("[...document.querySelectorAll('input,select,textarea,button')].map(x=>({id:x.id,disabled:x.disabled}))");assert.ok(controls.every(x=>x.disabled),'Actual in-flight simulation locks every control '+JSON.stringify(controls));
  // A second native mouse dispatch at the settled original button coordinate
  // observes disabled semantics without synthesizing a DOM click or retrying it.
  const point=inputs.at(-1).point;for(let repeat=0;repeat<2;repeat++)for(const type of ['mousePressed','mouseReleased'])await command('Input.dispatchMouseEvent',{type,x:point.x,y:point.y,button:'left',clickCount:1});
  assert.equal(await evaluate(`window.__feralHttp.filter(x=>x.sequence>${before}&&x.url==='/api/forever/character/simulate-level60').length`),1);await releaseResponse('simulation-lock');
 }
 await until(`window.__feralCompleted.length>${completed}&&!document.querySelector('#run').disabled&&/Simulation completed/.test(document.querySelector('#status').textContent)`,60000);
 assert.deepEqual(await evaluate(draftExpression),draft,'Simulation preserves settings');
 const http=await evaluate(`window.__feralHttp.filter(x=>x.sequence>${before}&&x.url==='/api/forever/character/simulate-level60')`);assert.equal(http.length,1);assert.equal(http[0].status,200);
 const result=await downloaded('warcraftsim-simulation-report.json','#exportResults',name+'-report.json');reportFiles.push(path.basename(result.file));
 assert.deepEqual(result.data,await evaluate('window.__feralCompleted.at(-1)'),'Export equals actual completion document');assert.deepEqual(result.data.rawServerResponse,http[0].response);assert.deepEqual(result.data.originalSimulationRequest,http[0].request);
 const report=result.data,raw=report.rawServerResponse,p=raw.preparation.preview,s=raw.sim,actor=s.run.summary.actorSummaries.druid;
 await assertIdentity();const race=await evaluate("document.querySelector('#race').value");for(const actual of [report.build.race,report.originalSimulationRequest.feral.character.race,raw.preparation.request.character.race,raw.preparation.baseline.race,raw.baseline.race])assert.equal(actual,race,'Every complete report identity/receipt retains the explicitly selected race');
 assert.deepEqual(report.feralPreparation,raw.preparation);assert.deepEqual(report.resolvedCharacter,raw.preparation);assert.deepEqual(report.baselineProvenance,raw.baseline);assert.deepEqual(report.inputTiming,raw.inputTiming);
 assert.equal(report.combatSummary.actorKey,'druid');assert.equal(report.combatSummary.damageDone,s.damageDone);assert.equal(report.combatSummary.dps,s.dps);
 for(const key of ['maximumHealth','maximumMana','maximumEnergy','startingEnergy','manaAfterInitialShift'])assert.equal(report.combatSummary[key],p[key]);
 assert.equal(report.combatSummary.startingMana,actor.startingResources.mana);assert.equal(report.combatSummary.endingMana,s.endingMana);assert.equal(report.combatSummary.endingEnergy,s.endingEnergy);assert.equal(report.combatSummary.endingHealth,null);
 assert.deepEqual(report.combatSummary.configuredRotation,p.configuredRotation);assert.equal(raw.inputTiming.additionalInputLatencyMilliseconds,0);
 const inspected=compareReports(report,report);assert.equal(inspected.comparable,true,inspected.warnings.join('; '));
 const warningRows=await evaluate("[...document.querySelectorAll('#resultWarningList > div')].map(x=>x.textContent)");assert.deepEqual(warningRows,[...new Set(report.warnings.filter(x=>typeof x==='string'&&x.trim()))]);
 for(const pattern of [/manual.*AP/i,/Troll/i,/racial/i,/Omen/i,/Natural Instinct/i,/80/])assert.match(warningRows.join('\n'),pattern);
 const abilityRows=await evaluate("[...document.querySelectorAll('#abilities tr')].map(x=>[...x.children].map((c,index)=>index===0?[...c.childNodes].filter(n=>n.nodeType===Node.TEXT_NODE).map(n=>n.textContent).join(''):c.textContent))");
 const expectedRows=Object.entries(s.damageByAbility).sort((a,b)=>b[1]-a[1]).map(([key,damage])=>{const a=s.run.summary.abilities?.[key]||{};return [key,...[a.castCompletedCount??'—',a.damageOccurrenceCount??'—',a.criticalDamageCount??'—',damage].map(value=>typeof value==='number'?value.toLocaleString('en-US',{maximumFractionDigits:0}):String(value))];});assert.deepEqual(abilityRows,expectedRows);
 assert.ok(Math.abs(Object.values(s.damageByAbility).reduce((a,b)=>a+b,0)-s.damageDone)<1e-8,'Ability damage reconciles without substituting a mean');
 const cards=await evaluate("[...document.querySelectorAll('#metrics > div')].map(x=>[(x.querySelector('span')?.textContent||x.firstChild?.textContent||'').replace(/:\\s*$/,''),x.querySelector('strong')?.textContent])");
 for(const [key,value,digits] of [['DPS',s.dps,1],['Total damage',s.damageDone,0],['Manual pre-talent Cat AP',p.authoredAttackPower,2],['HOTW AP increment',p.heartOfTheWildAttackPowerIncrement,2],['Predatory Strikes AP',p.predatoryStrikesAttackPower,2],['Prepared spellbook AP',p.spellbookAttackPower,2],['Maximum Mana',p.maximumMana,2],['Actor starting Mana (before shift)',actor.startingResources.mana,2],['Initial shift Mana cost',p.initialShiftManaCost,2],['Post-shift Mana',p.manaAfterInitialShift,2],['Ending Mana',s.endingMana,2],['Maximum Energy',p.maximumEnergy,2],['Starting Energy',p.startingEnergy,2],['Ending Energy',s.endingEnergy,2],['Maximum Health (racials omitted)',p.maximumHealth,2]]){const card=cards.find(([name])=>name===key);assert.ok(card,'Missing actual numeric resource card '+key);assert.equal(card[1],value.toLocaleString('en-US',{minimumFractionDigits:key==='DPS'?1:0,maximumFractionDigits:digits}),key+' actual numeric display; exact value retained in raw report');}
 await setInput('#reportEventFilter','resources');const events=inspectEvents(report).rows.filter(x=>x.category==='resources');assert.ok(events.length>0&&events.length<=200,'Bounded complete resource evidence required');
 assert.deepEqual(await evaluate("[...document.querySelectorAll('#reportEvents tr')].map(x=>[...x.children].map(c=>c.textContent))"),events.map(e=>[e.time,e.event,e.actor,e.target,e.ability,e.detail].map(x=>String(x??'-'))));
 return result;
}
async function importReport(slot,file){await observeImport('#compareFile','#compareStatus');await selectFile('#compareImport'+slot,'#compareFile',file);await until("window.__importChanged&&window.__importStatus&&document.querySelector('#compareFile').value===''");}
async function compare(a,b,label,compatible){await importReport('A',a.file);await importReport('B',b.file);const actual=compareReports(a.data,b.data);assert.equal(actual.comparable,compatible,actual.warnings.join('; '));assert.deepEqual(await evaluate('window.__feralComparisons.at(-1)'),actual,'Actual comparison renders complete captured report analysis');assert.match(await text('#compareStatus'),compatible?/Encounter settings match/:/Not directly comparable/);fixture(label+'-comparison',actual);return actual;}
const comparisonState=()=>evaluate("({labelA:document.querySelector('#compareLabelA').textContent,labelB:document.querySelector('#compareLabelB').textContent,hidden:document.querySelector('#compareDetails').hidden,rows:document.querySelector('#compareRows').innerHTML,warnings:document.querySelector('#compareWarnings').innerHTML,changes:document.querySelector('#compareChanges').innerHTML,useA:document.querySelector('#compareUseA').disabled,useB:document.querySelector('#compareUseB').disabled,lastComparison:window.__feralComparisons.at(-1)??null})");
async function rejectedReport(slot,file,label){
 const before=await comparisonState();await importReport(slot,file);
 const status=await text('#compareStatus'),after=await comparisonState();
 assertRejectedReportState(before,after,status,label);
 coverage.push({gate:'rejected-report-transaction',label,slot,status,before,after});
}
async function rejectedImport(file,label,{server=false}={}){const prior=await evaluate(draftExpression),identity=await evaluate("[document.querySelector('#characterTitle').textContent,document.querySelector('#characterSubtitle').textContent]"),before=await sequence();await observeImport();await selectFile('#importBuild','#file',file);await until(`window.__importChanged&&window.__importStatus&&document.querySelector('#file').value===''&&!/Build imported/.test(document.querySelector('#status').textContent)&&['ready','input-required'].includes(document.querySelector('#status').dataset.buildState)${server?`&&window.__feralHttp.some(x=>x.sequence>${before}&&x.url==='/api/forever/character/preview-feral'&&x.status===400&&x.request.manualInputs.shapeshiftMana===999999)`:''}`);assert.deepEqual(await evaluate(draftExpression),prior,label+' exact raw draft preserved');assert.deepEqual(await evaluate("[document.querySelector('#characterTitle').textContent,document.querySelector('#characterSubtitle').textContent]"),identity,label+' exact visible identity preserved');const message=await text('#status');assert.ok(message&&!/Class defaults loaded/.test(message),label+' explicit import failure');return message;}
async function captureAll(size){
 await capture('identity-stats-'+size,'.character-card,#stats,#statScope');
 await click('[data-view=encounter]');await readable('#feralSettings p,#feralSettings label');
 await capture('baseline-source-'+size,'#feralSettings > h2,#feralSettings > p,#feralBaseline');
 await capture('manual-controls-'+size,'#feralSettings .settings-grid');
 await capture('encounter-controls-'+size,'#view-encounter > .settings-group:not([id])');
 await click('[data-view=talents]');await capture('talent-allocation-'+size,'#talentSummary,#talentCoverage,#feralTalentScope');await capture('talent-trees-'+size,'#talents');
 await click('[data-view=rotation]');await capture('configured-priority-'+size,'#feralFixedRotation');
 await click('[data-view=results]');await readable('#resultWarningList > div');await capture('metrics-'+size,'#metrics');await capture('abilities-'+size,'#abilities');await capture('warnings-'+size,'#resultWarnings');
 const report=await evaluate('window.__feralCompleted.at(-1)');for(const category of ['actions','resources']){await setInput('#reportEventFilter',category);const events=inspectEvents(report).rows.filter(e=>e.category===category);assert.ok(events.length>0&&events.length<=200,'Complete bounded '+category+' evidence');assert.deepEqual(await evaluate("[...document.querySelectorAll('#reportEvents tr')].map(x=>[...x.children].map(c=>c.textContent))"),events.map(e=>[e.time,e.event,e.actor,e.target,e.ability,e.detail].map(x=>String(x??'-'))));await capture(category+'-events-'+size,'table:has(#reportEvents)');}
}
async function main(){
 let tabs;for(let i=0;i<100;i++){try{tabs=await(await fetch(`http://127.0.0.1:${port}/json`)).json();if(tabs.some(x=>x.type==='page'))break;}catch{}await sleep(100);}assert.ok(tabs?.some(x=>x.type==='page'),'Chromium CDP unavailable');
 ws=new WebSocket(tabs.find(x=>x.type==='page').webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
 ws.addEventListener('message',event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(p)m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}else if(m.method==='Runtime.exceptionThrown')browserErrors.push(m.params.exceptionDetails);});
 for(const method of ['Page.enable','Runtime.enable','DOM.enable','Network.enable'])await command(method);
 await command('Network.emulateNetworkConditions',{offline:false,latency:Number(process.env.CP175_CDP_LATENCY_MS||100),downloadThroughput:-1,uploadThroughput:-1});
 await command('Page.addScriptToEvaluateOnNewDocument',{source:'('+installObserver.toString()+')()'});
 await command('Page.setDownloadBehavior',{behavior:'allow',downloadPath:reportDir});await command('Page.setInterceptFileChooserDialog',{enabled:true});await viewport(1440,1000,false);
 await command('Page.navigate',{url:BASE+'/solo-dps.html'});await until("document.querySelector('#status')?.dataset.buildState==='ready'&&/Class defaults loaded/.test(document.querySelector('#status').textContent)");
 await evaluate("(()=>{const original=window.WarcraftSimComparison.compareReports;window.__feralComparisons=[];window.WarcraftSimComparison.compareReports=(...args)=>{const result=original(...args);window.__feralComparisons.push(JSON.parse(JSON.stringify(result)));return result;};})()");
 let start=await sequence();await setInput('#classKey','druid');await presetReady('druid','balance',start);
 start=await sequence();await chooseSpec('feral');await presetReady('druid','feral',start);await assertIdentity();
 assert.equal(await evaluate("document.querySelector('#feralPreTalentCatAttackPower').value"),'');assert.equal(await evaluate("document.querySelector('#provisional').checked"),false);
 await click('[data-view=encounter]');await assertBlockedEditable();assert.equal(await evaluate("document.querySelector('#race').value"),'high-order-skyborne','Actual sorted playable inventory starts with an unavailable historical race');await selectFeralFixtureRace();assert.equal(await evaluate("window.__feralHttp.filter(x=>x.url==='/api/forever/character/preview-feral'||x.url==='/api/forever/character/simulate-level60').length"),0,'Race selection with blank AP and no historical opt-in does not prepare or simulate');mark('blank-manual-readiness');
 await setInput('#feralPreTalentCatAttackPower',800);await assertBlockedEditable();start=await sequence();await click('#provisional');const prepared=await ready(start,{preTalentCatAttackPower:800});mark('explicit-opt-in-preview');
 const p=prepared.response.preview,b=prepared.response.baseline;assert.equal(prepared.request.character.race,'night-elf');assert.equal(b.race,'night-elf');
 assert.deepEqual([p.authoredAttackPower,p.heartOfTheWildAttackPowerIncrement,p.predatoryStrikesAttackPower,p.spellbookAttackPower,p.maximumHealth,p.maximumMana,p.manaAfterInitialShift,p.startingEnergy],[800,12.4,90,902.4,2173,2594,2344,0]);
 assert.deepEqual([b.strength,b.intellect,b.observedUnbuffedMaximumHealth,b.observedUnbuffedMaximumMana,b.includesRacialPassives,b.observedUnbuffedMeleeAttackPower],[62,100,2173,2444,false,null]);
 const preset=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../WarcraftSim.Api/wwwroot/itemized-presets/druid-feral.json'),'utf8'));
 const initial=await exportBuild('initial');assert.deepEqual(initial.data.talents.ranks,preset.ranks);assert.deepEqual([initial.data.talents.balancePointsSpent,initial.data.talents.feralPointsSpent,initial.data.talents.restorationPointsSpent,initial.data.talents.totalPoints,initial.data.talents.unspentPoints],[0,31,5,36,15]);mark('independent-numeric-preview');
 for(const [value,expectedAp] of [[0,102.4],[800.125,902.525]]){const r=await change('preTalentCatAttackPower',value);assert.equal(r.response.preview.spellbookAttackPower,expectedAp);}await change('preTalentCatAttackPower',800);mark('zero-and-fractional-ap');
 await setInput('#feralPreTalentCatAttackPower','',false);assert.equal(await evaluate('document.activeElement.id'),'feralPreTalentCatAttackPower');await assertBlockedEditable();
 await setInput('#feralPreTalentCatAttackPower',-1,false);assert.equal(await evaluate('document.activeElement.id'),'feralPreTalentCatAttackPower');await assertBlockedEditable();start=await sequence();await setInput('#feralPreTalentCatAttackPower',800,false);await ready(start);mark('focused-invalid-input');
 for(const [key,value] of Object.entries({weaponDps:76.25,meleeHitPercent:87.5,meleeCritPercent:21.25,shapeshiftMana:251.5,shiftingPowerBaseMana:1245.5,shiftingPowerCostReductionPercent:10.5,targetArmor:2100,useFaerieFire:true,useFeralCooldowns:true}))await change(key,value);
 for(const [key,value] of Object.entries({weaponDps:75,meleeHitPercent:88,meleeCritPercent:20,shapeshiftMana:250,shiftingPowerBaseMana:1244,shiftingPowerCostReductionPercent:0}))await change(key,value);
 for(const [selector,value] of [['#duration',20],['#seed',144002],['#targetHealth',999999]]){start=await sequence();await setInput(selector,value);await ready(start);}
 start=await sequence();await click('#timeline');await ready(start);mark('manual-controls');
 const saved=await exportBuild('illustrative-ap800');assert.equal(saved.data.race,'night-elf','The roundtrip/replay baseline owns the explicitly chosen fixture race');
 await click('[data-view=talents]');assert.match(await text('#talentSummary'),/36/);assert.match(await text('#talentSummary'),/15/);assert.match(await text('#feralTalentScope'),/inactive.*initial-zero|initial-zero.*inactive/i);
 for(const rank of [0,5]){start=await sequence();await setInput('#feralFurorRank',rank);const r=await ready(start);assert.equal(r.request.talents.ranks.furor,rank,'Explicit zero-key identity retained');assert.equal(r.response.preview.startingEnergy,0);assert.equal(r.request.talents.totalPoints,31+rank);}mark('furor-zero-five');
 const lower=structuredClone(saved.data);lower.talents.ranks=Object.fromEntries(Object.entries(lower.talents.ranks).filter(([key])=>['ferocity','heart-of-the-wild','feral-swiftness','shredding-attacks','savage-fury','sharpened-claws','predatory-strikes','furor'].includes(key)));Object.assign(lower.talents,{feralPointsSpent:22,totalPoints:27,unspentPoints:24});await importBuild(fixture('legal-lower-talent-subset',lower));await click('[data-view=talents]');
 start=await sequence();await click('[data-talent-key="heart-of-the-wild"]','right');let changed=await ready(start);assert.equal(changed.request.talents.ranks['heart-of-the-wild'],4);assert.equal(changed.response.preview.spellbookAttackPower,899.92);
 start=await sequence();await click('[data-talent-key="predatory-strikes"]','right');changed=await ready(start);assert.equal(changed.request.talents.ranks['predatory-strikes'],2);assert.equal(changed.response.preview.spellbookAttackPower,869.92);mark('talent-change-preview');await importBuild(saved.file);
 await click('[data-view=encounter]');await change('startInProwl',true);await click('#feralTargetBehind');await assertBlockedEditable();await change('preferredCatOpener','pounce');await change('startInProwl',false);await change('targetBehind',true);await change('preferredCatOpener','ravage');mark('position-opener-recovery');
 start=await sequence();await setInput('#feralShapeshiftMana',999999);await until(`window.__feralHttp.some(x=>x.sequence>${start}&&x.url==='/api/forever/character/preview-feral'&&x.status===400)`);await assertBlockedEditable();await change('shapeshiftMana',250);mark('shift-affordability-recovery');
 assert.equal(await evaluate("window.__feralHttp.filter(x=>x.url==='/api/forever/character/simulate-level60').length"),0,'All preparation and validation happen without simulation');
 await click('[data-view=rotation]');start=await sequence();await click('#feralPreviewRotation');await ready(start);assert.match(await text('#feralFixedRotation'),/not immediate eligibility/);assert.equal(await evaluate("window.__feralHttp.filter(x=>x.url==='/api/forever/rotation/preview'&&x.request?.specialization==='feral').length"),0);assert.equal(await evaluate("window.__feralHttp.filter(x=>x.url==='/api/forever/character/simulate-level60').length"),0,'Explicit priority refresh must also remain preparation-only');mark('preview-only-no-simulation');
 const a=await run('baseline',{lock:true});mark('real-simulation-lock');mark('full-report-reconciliation');
 start=await sequence();await click('#replayResult');await until("/Deterministic Feral replay matched/.test(document.querySelector('#status').textContent)",60000);
 const replay=await evaluate(`window.__feralHttp.filter(x=>x.sequence>${start}&&x.url==='/api/forever/character/simulate-level60').at(-1)`);assert.equal(replay.status,200);assert.equal(canonical(replay.response),canonical(a.data.rawServerResponse),'Complete envelope including all events/preparation/timing, only allowlisted generated engine ID renaming');mark('complete-envelope-replay',{normalization:'Sorted object keys; ordered arrays; Only configuredRotation.id and generated timeline execution/aura/autoattack/absorb/forced-target IDs renamed by first occurrence preserving identity. Provenance untouched; no fields omitted. JS parsed numbers; exact decimals independently checked by Python HTTP suite.'});
 await importBuild(saved.file);assert.deepEqual((await exportBuild('roundtrip')).data,saved.data);mark('build-roundtrip');
 const rejections=[];for(const {name,mutate} of rejectionCases){const invalid=structuredClone(saved.data);mutate(invalid);rejections.push({name,message:await rejectedImport(fixture('rejected-'+name,invalid),name)});assert.deepEqual((await exportBuild('rollback-'+name)).data,saved.data);}mark('strict-import-rejections',{count:rejections.length,rejections});
 await click('[data-view=encounter]');await setInput('#feralPreTalentCatAttackPower','');await setInput('#seed',.5);await setInput('#feralShapeshiftMana',-1);await click('#feralStartInProwl');await click('#feralTargetBehind');
 await assertBlockedEditable();const bad=structuredClone(saved.data);bad.manualInputs.preTalentCatAttackPower='800';await rejectedImport(fixture('invalid-draft-rejection',bad),'invalid-raw-draft');await assertBlockedEditable();const serverBad=structuredClone(saved.data);serverBad.manualInputs.shapeshiftMana=999999;const serverBadFile=fixture('server-rejected-invalid-draft',serverBad);await rejectedImport(serverBadFile,'invalid-raw-draft-async-rollback',{server:true});await assertBlockedEditable();mark('invalid-raw-draft-rollback');await importBuild(saved.file);assert.deepEqual((await exportBuild('recovered-invalid-draft')).data,saved.data);mark('valid-import-recovers');
 await click('[data-view=encounter]');await change('preTalentCatAttackPower',801);const dirty=await exportBuild('dirty');
 for(let n=0;n<2;n++){await setInput('#spec','balance');await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.equal(await evaluate("document.querySelector('#presetDialog').open"),false);assert.deepEqual((await exportBuild('dirty-cancel-'+n)).data,dirty.data);}
 await click('#save');await change('preTalentCatAttackPower',802);await click('#load');await until("!document.querySelector('#run').disabled&&document.querySelector('#feralPreTalentCatAttackPower').value==='801'");assert.deepEqual((await exportBuild('dirty-restored')).data,dirty.data);mark('dirty-cancel-and-restore');
 const foreignInvalid=fixture('foreign-rejected-feral',serverBad);
 for(const [classKey,spec] of [['druid','balance'],['mage','arcane']]){start=await sequence();if(classKey==='druid')await chooseSpec(spec);else await setInput('#classKey',classKey);await presetReady(classKey,spec,start);await click('[data-view=encounter]');await setInput('#duration',23);const control=await exportBuild(classKey+'-dirty-control');await rejectedImport(foreignInvalid,classKey+'-rollback',{server:true});assert.deepEqual((await exportBuild(classKey+'-preserved')).data,control.data);
  const other=classKey==='druid'?'feral':'fire';for(let n=0;n<2;n++){await setInput('#spec',other);await until("document.querySelector('#presetDialog').open");await click('#cancelPreset');assert.deepEqual((await exportBuild(classKey+'-cancel-'+n)).data,control.data);}}
 mark('balance-and-mage-rollback');await importBuild(saved.file);
 // Native repeated race/spec changes. Every final preview must own both identity
 // and exact controls, never inherit AP from another Druid specialization.
 await click('[data-view=encounter]');for(const race of ['tauren','troll','night-elf']){start=await sequence();await setInput('#race',race);const r=await ready(start);assert.equal(r.response.baseline.race,race);assert.equal(r.response.baseline.includesRacialPassives,false);if(race==='troll')assert.match(await text('#feralSettings'),/Troll.*extrapolat/i);}
 start=await sequence();await chooseSpec('balance');await presetReady('druid','balance',start);start=await sequence();await chooseSpec('feral');await presetReady('druid','feral',start);assert.equal(await evaluate("document.querySelector('#feralPreTalentCatAttackPower').value"),'');assert.equal(await evaluate("document.querySelector('#race').value"),'night-elf','Same-class preset switch retains the explicit race');await assertIdentity();await assertBlockedEditable();await importBuild(saved.file);mark('rapid-class-spec-race-preset');
 await click('[data-view=encounter]');await holdResponse('/api/forever/character/preview-feral');await setInput('#feralPreTalentCatAttackPower',810);await until('window.__feralHold.held');start=await sequence();await setInput('#feralPreTalentCatAttackPower',820);await ready(start);let paint=await evaluate(paintExpression);await releaseResponse('older-manual-ap');assert.deepEqual(await evaluate(paintExpression),paint);mark('late-manual-preview');
 await importBuild(saved.file);await click('[data-view=encounter]');await holdResponse('/api/forever/character/preview-feral');await setInput('#feralPreTalentCatAttackPower',811);await until('window.__feralHold.held');await click('[data-view=talents]');start=await sequence();await click('[data-talent-key="berserk"]','right');await ready(start);paint=await evaluate(paintExpression);await releaseResponse('older-preview-after-talent');assert.deepEqual(await evaluate(paintExpression),paint);mark('late-talent-preview');
 await importBuild(saved.file);await click('[data-view=talents]');await holdResponse('/api/forever/talents/druid/feral/validate-itemized');await click('[data-talent-key="berserk"]','right');await until('window.__feralHold.held');start=await sequence();await chooseSpec('balance');await presetReady('druid','balance',start);const balance=await exportBuild('balance-before-late-validation'),beforeValidationRelease=await sequence();await releaseResponse('older-feral-validation-after-balance');await until(`window.__feralHttp.some(x=>x.sequence>${beforeValidationRelease}&&x.url==='/api/forever/talents/druid/feral/validate'&&x.status===200&&x.deliveredAt!==undefined&&x.request.ranks.berserk===0)`);await evaluate('new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))');assert.deepEqual((await exportBuild('balance-after-late-validation')).data,balance.data);mark('late-validation-identity');
 await holdResponse('/api/forever/talents/druid/feral/default-itemized');await chooseSpec('feral');await until('window.__feralHold.held');start=await sequence();await setInput('#classKey','mage');await presetReady('mage','arcane',start);const mage=await exportBuild('mage-before-late-feral-preset');await releaseResponse('older-feral-preset-after-mage');assert.deepEqual((await exportBuild('mage-after-late-feral-preset')).data,mage.data);mark('late-identity-preset');
 await importBuild(saved.file);await click('[data-view=encounter]');await holdResponse('/api/forever/character/preview-feral');await setInput('#feralPreTalentCatAttackPower',812);await until('window.__feralHold.held');await click('#provisional');await until("!document.querySelector('#provisional').checked&&document.querySelector('#run').disabled");await assertBlockedEditable();paint=await evaluate(paintExpression);await releaseResponse('older-historical-preview-after-opt-out');assert.deepEqual(await evaluate(paintExpression),paint);mark('late-baseline-preview');await importBuild(saved.file);
 await click('[data-view=encounter]');await change('preTalentCatAttackPower',900);const ap=await run('ap-only');
 // The first report pair has not been selected yet. A hard-invalid candidate
 // must leave the empty B slot empty, without creating any comparison delta.
 await importReport('A',a.file);const emptyTarget=await comparisonState();
 assert.equal(emptyTarget.labelB,'No report selected');assert.equal(emptyTarget.hidden,true);assert.equal(emptyTarget.rows,'');assert.equal(emptyTarget.lastComparison,null);
 const emptyCandidate=structuredClone(a.data);delete emptyCandidate.feralPreparation;
 await rejectedReport('B',fixture('intentionally-corrupted-empty-slot',emptyCandidate),'empty-B-slot');
 const apComparison=await compare(a,ap,'ap-only',true);assert.notEqual(apComparison.dpsDelta,0);assert.match(await text('#compareChanges'),/attack|AP|manual/i);mark('compatible-ap-comparison');
 await importBuild(saved.file);await click('[data-view=encounter]');await change('weaponDps',100);const weapon=await run('weapon-only');const weaponComparison=await compare(a,weapon,'weapon-only',true);assert.notEqual(weaponComparison.dpsDelta,0);assert.match(await text('#compareChanges'),/weapon/i);mark('compatible-weapon-comparison');
 await capture('compatible-values-desktop','.comparison-workspace');await viewport(390,844,true);await capture('compatible-values-mobile','.comparison-workspace');await viewport(1440,1000,false);
 await importBuild(saved.file);await click('[data-view=encounter]');await change('targetArmor',0);const armor=await run('zero-armor');await compare(a,armor,'zero-armor',false);assert.match(await text('#compareWarnings'),/armor/i);mark('incompatible-armor-comparison');
 await capture('incompatible-values-desktop','.comparison-workspace');await viewport(390,844,true);await capture('incompatible-values-mobile','.comparison-workspace');await viewport(1440,1000,false);
 await importBuild(saved.file);await click('[data-view=encounter]');await change('targetBehind',false);const front=await run('front-position');await compare(a,front,'front-position',false);assert.match(await text('#compareWarnings'),/position|behind/i);mark('incompatible-position-comparison');
 // These are explicitly corrupted imports derived from actual downloads, not
 // invented successful executions. Integrity warnings remain visible without
 // ranking. A hard schema rejection never commits the candidate or erases the
 // previously validated A/B reports and their rendered comparison.
 for(const [name,mutate] of [
  ['timing',x=>x.inputTiming.additionalInputLatencyMilliseconds=80],
  ['source',x=>x.baselineProvenance.sourceKind='unverified-source'],
  ['conflicting-ap',x=>x.combatSummary.spellbookAttackPower+=1]
 ]){
  const corrupted=structuredClone(a.data);mutate(corrupted);const file=fixture('intentionally-corrupted-'+name,corrupted);
  await compare(a,{data:corrupted,file},'integrity-warning-'+name,false);
  const rows=await evaluate("[...document.querySelectorAll('#compareRows tr')].map(row=>[...row.children].map(cell=>cell.textContent))");
  assert.equal(rows[0][3],'Not comparable');assert.equal(rows[1][3],'Not compared','No numeric damage delta for conflicting '+name+' receipt');
 }
 for(const [name,mutate] of [
  ['missing-preparation',x=>delete x.feralPreparation],
  ['missing-resources',x=>delete x.combatSummary.actorResources]
 ]){
  const corrupted=structuredClone(a.data);mutate(corrupted);const file=fixture('intentionally-corrupted-'+name,corrupted);
  for(const slot of ['A','B']){
   const valid=await compare(a,weapon,'before-rejected-'+name+'-'+slot,true);assert.notEqual(valid.dpsDelta,0,'Preservation case starts with a genuine nonzero valid delta');
   await rejectedReport(slot,file,name+'-'+slot);
   // Reimport only the OTHER slot first. If the rejected candidate silently
   // changed its target slot, this repaint cannot reproduce the validated pair.
   const other=slot==='A'?'B':'A';await importReport(other,other==='A'?a.file:weapon.file);
   assert.deepEqual(await evaluate('window.__feralComparisons.at(-1)'),valid,'Rejected target slot remains the validated report after later repaint');
   assert.match(await text('#compareStatus'),/Encounter settings match/);
   await importReport(slot,slot==='A'?a.file:weapon.file);
   assert.deepEqual(await evaluate('window.__feralComparisons.at(-1)'),valid,'A valid later import into the rejected target recovers normally');
   assert.match(await text('#compareStatus'),/Encounter settings match/);fixture('recovered-'+name+'-'+slot+'-comparison',valid);
  }
 }
 mark('incompatible-timing-and-source');mark('missing-conflicting-comparison-receipts');
 await importBuild(saved.file);await run('capture-baseline');await captureAll('desktop');mark('desktop-complete-coverage');await viewport(390,844,true);await captureAll('mobile');mark('mobile-complete-coverage');
 assert.deepEqual(browserErrors,[]);assert.deepEqual(gates.map(x=>x.name).sort(),[...gateNames].sort());
 fs.writeFileSync(path.join(reportDir,'acceptance-summary.json'),JSON.stringify({status:'passed',sourceCommit:process.env.CP174_SOURCE_COMMIT||'unstamped-local',gates:gates.length,expectedGates:gateNames.length,rejectedImports:rejectionCases.length,reports:reportFiles,screenshotDir,stamp,normalization:'Complete envelopes; only GUID identity-preserving normalization. No successful result injection.'},null,2));
 console.log('Feral actual-browser acceptance passed: '+gates.length+' named gates and '+rejectionCases.length+' rejected imports. '+reportDir);
}
async function persist(error){
 const output={error:error?String(error):null,gates,coverage,inputs};
 try{Object.assign(output,{dom:await evaluate('document.body.innerText'),http:await evaluate('window.__feralHttp'),nativeInputs:await evaluate('window.__feralInputs'),completedReports:await evaluate('window.__feralCompleted'),comparisons:await evaluate('window.__feralComparisons'),buildState:await evaluate("({state:document.querySelector('#status').dataset.buildState,classKey:document.querySelector('#classKey').value,spec:document.querySelector('#spec').value,dialog:document.querySelector('#presetDialog').open})")});if(error){const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(screenshotDir,'feral-'+stamp+'-failure.png'),Buffer.from(shot.data,'base64'),{flag:'wx'});}}catch(failure){output.captureFailure=String(failure);}
 fs.writeFileSync(path.join(reportDir,error?'failure-receipts.json':'causal-gate-receipts.json'),JSON.stringify(output,null,2));
}
main().then(()=>persist()).catch(async error=>{console.error(error);process.exitCode=1;await persist(error);}).finally(()=>{try{ws?.close();}catch{}proc.kill();});
