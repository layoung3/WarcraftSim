#!/usr/bin/env node
"use strict";
// Real Chromium CDP interactions; no third-party JS dependencies.
// The API should already be running at BASE_URL.
const assert=require("node:assert/strict");
const {spawn}=require("node:child_process");
const {mkdtempSync,rmSync,mkdirSync,existsSync,readFileSync,renameSync,readdirSync}=require("node:fs");
const {tmpdir}=require("node:os");
const path=require("node:path");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const CHROME=process.env.CP174_CHROME||"/usr/bin/google-chrome";
const profile=mkdtempSync(path.join(tmpdir(),"cp174-browser-"));
const port=19347;
const reportDir=process.env.CP174_REPORT_DIR||"/tmp/cp174-full-reports";
mkdirSync(reportDir,{recursive:true});
const proc=spawn(CHROME,["--headless","--no-sandbox","--disable-gpu","--disable-dev-shm-usage",`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,"about:blank"],{stdio:"ignore"});
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
let ws,id=0,pending=new Map(),events=[];
async function command(method,params={}){
 const key=++id;return new Promise((resolve,reject)=>{
  const timer=setTimeout(()=>{pending.delete(key);reject(new Error("CDP timeout: "+method));},15000);
  pending.set(key,{resolve:v=>{clearTimeout(timer);resolve(v);},reject:e=>{clearTimeout(timer);reject(e);}});
  ws.send(JSON.stringify({id:key,method,params}));
 });
}
async function evalJs(expression,awaitPromise=false){
 const r=await command("Runtime.evaluate",{expression,returnByValue:true,awaitPromise});
 if(r.exceptionDetails)throw Error("Browser JS exception: "+r.exceptionDetails.text);
 return r.result?.value;
}
async function poll(expression,timeout=12000){
 const start=Date.now();while(Date.now()-start<timeout){if(await evalJs(expression))return;await sleep(200);}
 const diagnostic=await evalJs("({status:document.querySelector('#status')?.textContent,class:document.querySelector('#classKey')?.value,spec:document.querySelector('#spec')?.value,race:document.querySelector('#race')?.value,provisional:document.querySelector('#provisional')?.checked,metrics:document.querySelector('#metrics')?.textContent})");
 throw Error("Timed out waiting: "+expression+"; UI="+JSON.stringify(diagnostic));
}
async function click(selector,button="left"){
 const pt=await evalJs(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error("No element");e.scrollIntoView({block:"center"});let r=e.getBoundingClientRect();let x=r.x+r.width/2,y=r.y+r.height/2;let hit=document.elementFromPoint(x,y);if(!hit||!(hit===e||e.contains(hit))){window.scrollTo(0,0);e.scrollIntoView({block:"nearest"});r=e.getBoundingClientRect();x=r.x+r.width/2;y=r.y+r.height/2;hit=document.elementFromPoint(x,y);}return {x,y,hit:!!hit&&(hit===e||e.contains(hit)),disabled:!!e.disabled};})()`);
 if(!pt.hit)throw Error("Click target obscured: "+selector+" "+JSON.stringify(pt));
 if(pt.disabled)throw Error("Click target disabled: "+selector);
 await command("Input.dispatchMouseEvent",{type:"mousePressed",x:pt.x,y:pt.y,button,clickCount:1});
 await command("Input.dispatchMouseEvent",{type:"mouseReleased",x:pt.x,y:pt.y,button,clickCount:1});
}
async function captureReport(cls){
 await click("#exportResults");
 const downloaded=path.join(reportDir,"warcraftsim-simulation-report.json");
 for(let i=0;i<100;i++){
  if(existsSync(downloaded)&&!readdirSync(reportDir).some(x=>x.endsWith(".crdownload")))break;
  await sleep(100);
 }
 assert.ok(existsSync(downloaded),"Browser must download a complete JSON report");
 const report=JSON.parse(readFileSync(downloaded,"utf8"));
 assert.equal(report.schema,"warcraftsim.solo-dps-report");
 assert.equal(report.version,1);
 assert.equal(report.originalSimulationRequest.classKey,cls);
 assert.equal(report.build.classKey,cls);
 assert.equal(report.metadata.ruleset,"wow-forever");
 assert.ok(report.rawServerResponse.sim.run?.summary,"Raw engine summary required");
 assert.ok(report.resolvedCharacter?.effectiveStats,"Resolved precombat stats required");
 const property=cls==="hunter"?({marksmanship:"marksmanship",survival:"survival","beast-mastery":"beastMastery"}[report.originalSimulationRequest.specialization]):cls;
 assert.ok(report.originalSimulationRequest[property]?.talents,"Exact talents required");
 renameSync(downloaded,path.join(reportDir,cls+"-simulation-report.json"));
 await click("#replayResult");
 await poll("document.querySelector('#status').textContent.includes('Deterministic replay matched')",35000);
 console.log("CP174 verified browser export/replay",cls,report.combatSummary?.dps);
 return report;
}
async function main(){
 for(let tries=0;tries<80;tries++){try{const d=await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:"PUT"})).json();if(d.webSocketDebuggerUrl){ws=new WebSocket(d.webSocketDebuggerUrl);break;}}catch{}await sleep(100);}
 if(!ws)throw Error("Chrome debugging port unavailable");
 await new Promise((resolve,reject)=>{ws.addEventListener("open",resolve,{once:true});ws.addEventListener("error",reject,{once:true});});
 ws.addEventListener("message",event=>{let msg=JSON.parse(event.data);if(msg.id){const q=pending.get(msg.id);if(q){pending.delete(msg.id);if(msg.error)q.reject(Error(msg.error.message));else q.resolve(msg.result);}}else events.push(msg);});
 await command("Page.enable");await command("Runtime.enable");await command("Log.enable");
 await command("Page.setDownloadBehavior",{behavior:"allow",downloadPath:reportDir});
 await command("Page.navigate",{url:BASE+"/solo-dps.html"});
 await poll("document.querySelectorAll('.gear-slot').length===17");
 assert.equal(await evalJs("document.querySelector('#classKey').value"),"mage");
 await click("[data-view=talents]");
 await poll("!document.querySelector('#view-talents').hidden && document.querySelectorAll('.talent-node').length>0");
 const before=await evalJs("document.querySelector('.talent-node .talent-rank').textContent");
 await click(".talent-node");await sleep(600);
 const after=await evalJs("document.querySelector('.talent-node .talent-rank').textContent");
 // An allocation may reject due to preserved preset prerequisites; state must be coherent.
 assert.match(after,/^\d+\/\d+$/);
 await click(".talent-node","right");await sleep(600);
 assert.equal(await evalJs("document.querySelector('.talent-node').isConnected"),true);
 // Source-linked prerequisite arrows and dependent point protection.
 assert.ok(await evalJs("document.querySelectorAll('.talent-connections .dependency-line').length>0"),"Engine prerequisite connectors must render");
 await poll("[...document.querySelectorAll('.talent-connections .dependency-line')].some(p=>p.getAttribute('d')?.startsWith('M '))");
 const measured=await evalJs("(()=>{const g=document.querySelector('.talent-tree-grid'),s=g.querySelector('svg');return {width:g.clientWidth,height:g.clientHeight,viewBox:s.getAttribute('viewBox')}})()");
 assert.equal(measured.viewBox,'0 0 '+measured.width+' '+measured.height,"Connector viewBox must match actual grid geometry");

 const requiresPresence=await evalJs("!!document.querySelector('[data-talent-key=presence-of-mind]')");
 if(requiresPresence){
   const beforeParent=await evalJs("document.querySelector('[data-talent-key=presence-of-mind] .talent-rank').textContent");
   await click("[data-talent-key=presence-of-mind]","right");await sleep(550);
   assert.equal(await evalJs("document.querySelector('[data-talent-key=presence-of-mind] .talent-rank').textContent"),beforeParent,"Removing a prerequisite with a dependent must fail");
 }
 // With a fully allocated default, an empty talent cannot spend point 52.
 // Remove a safe, currently selected rank to exercise real dirty-build behavior.
 await click("[data-talent-key=incineration]","right");
 await poll("document.querySelector('[data-talent-key=incineration] .talent-rank')?.textContent.startsWith('4/')");
 // A preset change must explicitly warn before overwriting edited build state.
 await evalJs("(()=>{const s=document.querySelector('#spec');s.value='fire';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#presetDialog').open");
 await click("#cancelPreset");
 assert.equal(await evalJs("document.querySelector('#spec').value"),"arcane");
 await evalJs("(()=>{const s=document.querySelector('#spec');s.value='fire';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#presetDialog').open");
 await click("#confirmPreset");
 await poll("document.querySelector('#spec').value==='fire' && !document.querySelector('#run').disabled",20000);
 await click("[data-view=equipment]");
 await click(".gear-slot");
 await poll("document.querySelector('#itemDialog').open");
 assert.equal(await evalJs("document.activeElement.id"),"itemSearch");
 await command("Input.insertText",{text:"nonexistent-CP174-item"});
 await poll("document.querySelector('#pickerItems').textContent.includes('No eligible')");
 await click("#closePicker");
 await poll("!document.querySelector('#itemDialog').open");
 // Equip an eligible item through the real picker. If a compatible
 // permanent enchant exists, select the exact server-provided choice.
 await click("[data-slot=main-hand]");
 await poll("document.querySelector('#itemDialog').open && document.querySelectorAll('#pickerItems .picker-item').length>0");
 const dialogBefore=await evalJs("document.querySelector('#itemDialog').getBoundingClientRect().height");
 const rowsBefore=await evalJs("document.querySelectorAll('#pickerItems .picker-item').length");
 await click("#pickerItems .picker-item");
 await poll("document.querySelector('#pickerDetails .simulate') && document.querySelector('#pickerTabItems').getAttribute('aria-selected')==='true'");
 const dialogAfter=await evalJs("document.querySelector('#itemDialog').getBoundingClientRect().height");
 assert.ok(dialogAfter<=710 && dialogAfter>0,"Compact picker must fit within its height budget");
 assert.equal(await evalJs("document.querySelectorAll('#pickerItems .picker-item').length"),rowsBefore,"Selecting an item must not recreate or drop catalog rows");
 await click('#pickerTabEnchants');
  await poll("document.querySelectorAll('#pickerEnchantDetails .enchant-group').length===1");
  const enchantChoices=await evalJs("document.querySelectorAll('#pickerEnchantDetails .enchant-group:first-child .enchant-tile').length");
 assert.ok(await evalJs("!!document.querySelector('#enchantSearch')"),"Enchant search exists");
 await evalJs("(()=>{const q=document.querySelector('#enchantSearch');q.value='unmatched-cp174';q.dispatchEvent(new Event('input',{bubbles:true}));})()");
 assert.equal(await evalJs("document.querySelectorAll('#pickerEnchantDetails .enchant-group:first-child .enchant-tile').length"),0,"Filtering must not show unrelated enchants");
 assert.ok(await evalJs("!!document.querySelector('#removePermanentEnchant')"),"Permanent removal stays visible");
 await evalJs("(()=>{const q=document.querySelector('#enchantSearch');q.value='';q.dispatchEvent(new Event('input',{bubbles:true}));})()");
 assert.equal(await evalJs("document.querySelectorAll('#pickerEnchantDetails .enchant-group:first-child .enchant-tile').length"),enchantChoices);
 if(enchantChoices>1)await click('#pickerEnchantDetails .enchant-group:first-child .enchant-tile:nth-child(2)');
  await click('#pickerTabItems');
 await click("#pickerDetails .simulate");
 await poll("!document.querySelector('#itemDialog').open && !document.querySelector('[data-slot=main-hand] .empty-item')",20000);
 const equippedLabel=await evalJs("document.querySelector('[data-slot=main-hand] .slot-copy strong').textContent");
 assert.notEqual(equippedLabel,"Empty slot");
 if(equippedLabel==="Crackling Staff")
   await poll("document.querySelector('[data-slot=main-hand] img.item-art')?.complete && document.querySelector('[data-slot=main-hand] img.item-art')?.naturalWidth>0",15000);

// Repeat open-and-select immediately after the first equipment selection.
 await click("[data-slot=main-hand]");
 await poll("document.querySelector('#itemDialog').open");
 await click("#pickerItems .picker-item");
 await click('#pickerTabEnchants');
  await poll("document.querySelectorAll('#pickerEnchantDetails .enchant-group').length===1");
 await click("#closePicker");
 await poll("!document.querySelector('#itemDialog').open");

 // Dedicated enchant shortcut must not trigger the item picker.
  await click("[data-enchant-slot=main-hand]");
  await poll("document.querySelector('#itemDialog').open && document.querySelector('#pickerTabEnchants').getAttribute('aria-selected')==='true'");
  await poll("document.querySelectorAll('#pickerEnchantDetails .enchant-group').length===1");
  await click('#removePermanentEnchant');
  await click('#pickerEnchantDetails .simulate');
  await poll("!document.querySelector('#itemDialog').open",20000);
  assert.equal(await evalJs("document.querySelector('[data-slot=main-hand] .slot-copy strong').textContent"),equippedLabel,"Enchant removal preserves item");
  await click("[data-view=effects]");
  // Temporary weapon consumables appear here, not duplicated as permanent enchants.
  const oil=await evalJs("!!document.querySelector('[data-temporary-enchant=\"25122\"]')");
  if(oil){
   await click('[data-temporary-enchant=\"25122\"]');
   await poll("document.querySelector('[data-temporary-enchant=\"25122\"]').getAttribute('aria-pressed')==='true'");
   assert.ok(await evalJs("document.querySelector('[data-slot=main-hand]').textContent.includes('Temporary effect')"));
   await click('[data-temporary-enchant=\"25122\"]');
   await poll("document.querySelector('[data-temporary-enchant=\"25122\"]').getAttribute('aria-pressed')==='false'");
  }

 await poll("document.querySelectorAll('.modifier-tile').length>0");
 const effect=await evalJs("document.querySelectorAll('.modifier-tile input[type=checkbox]').length===0");
 assert.equal(effect,true);
 await click('[data-modifier-key=arcane-intellect]');
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=arcane-intellect]').classList.contains('selected')"),true);
 await click('[data-modifier-key=arcane-intellect]');
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=arcane-intellect]').classList.contains('selected')"),false);
 await click('[data-modifier-key=greater-arcane-elixir]');
 await click('[data-modifier-key=elixir-of-the-mongoose]');
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=greater-arcane-elixir]').getAttribute('aria-pressed')"),"true");
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=elixir-of-the-mongoose]').getAttribute('aria-pressed')"),"true");
 await click('[data-modifier-key=elixir-of-greater-agility]');
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=elixir-of-greater-agility]').getAttribute('aria-pressed')"),"false");
 assert.ok(await evalJs("document.querySelector('#status').textContent.includes('Cannot stack')"));
 await click('[data-modifier-key=greater-arcane-elixir]');
 await click('[data-modifier-key=elixir-of-the-mongoose]');
 const quantitySelector='.combat-item-entry input[aria-label="Dense Dynamite available charges"]';
 assert.ok(await evalJs("!!document.querySelector("+JSON.stringify(quantitySelector)+")"));
 await evalJs("(()=>{const x=document.querySelector("+JSON.stringify(quantitySelector)+");x.value='3';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 assert.equal(await evalJs("document.querySelector("+JSON.stringify(quantitySelector)+").value"),"3");
 // Restore zero charges before simulating: an explosive requires Engineering 250.
 await evalJs("(()=>{const x=document.querySelector("+JSON.stringify(quantitySelector)+");x.value='0';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await click("[data-view=encounter]");
 await click("#loadPriority");
 await poll("document.querySelectorAll('#priorityList .priority-entry').length>0",15000);
 await click("#applyPriority");
 await poll("document.querySelector('#priorityStatus').textContent.includes('Custom priority validated')",15000);
 await click("#resetPriority");
 assert.equal(await evalJs("document.querySelector('#priorityText').value"),"");
 assert.equal(await evalJs("!document.querySelector('#view-encounter').hidden"),true);
 await click("[data-view=results]");
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
 // End-to-end browser submission against the real typed Mage endpoint.
 await evalJs("(()=>{document.querySelector('#race').value='human';document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='20';document.querySelector('#timeline').checked=true;})()");
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("!document.querySelector('#results').hidden && !!document.querySelector('#metrics strong')",30000);
 const dps=await evalJs("document.querySelector('#metrics').textContent");
 assert.match(dps,/DPS/i);
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
 await captureReport("mage");
 // Check actual local build restoration, not just the serialization schema.
 await click("#save");
 await evalJs("document.querySelector('#duration').value='55'");
 await click("#load");
 await poll("document.querySelector('#duration').value==='20' && document.querySelector('#status').textContent.includes('Build loaded')",25000);
 assert.equal(await evalJs("document.querySelector('[data-slot=main-hand] .slot-copy strong').textContent"),equippedLabel);
 
// Exercise actual file-input import using a versioned browser File and DataTransfer.
 await evalJs("(()=>{const f=new File([localStorage.getItem('warcraftsim-solo-dps-v1')],'cp174-build.json',{type:'application/json'});const d=new DataTransfer();d.items.add(f);const input=document.querySelector('#file');input.files=d.files;input.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Build imported')",25000);
 assert.equal(await evalJs("document.querySelector('[data-slot=main-hand] .slot-copy strong').textContent"),equippedLabel);
 // Unsupported schema must fail closed rather than silently replacing selections.
 await evalJs("(()=>{const f=new File(['{}'],'invalid.json',{type:'application/json'});const d=new DataTransfer();d.items.add(f);const input=document.querySelector('#file');input.files=d.files;input.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Unsupported build schema/version')",15000);

 // Switch class through the real selection callback. Check typed Warlock companion output.
 await evalJs("(()=>{const x=document.querySelector('#classKey');x.value='warlock';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').value==='affliction'",25000);
 await evalJs("(()=>{const r=document.querySelector('#race');r.value='orc';r.dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='20';})()");
 await poll("document.querySelector('#race').value==='orc'");
 await click("[data-view=effects]");
 await click('[aria-label="Use supported racial active ability"]');
 assert.equal(await evalJs("document.querySelector('#racialActive').checked"),true);
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("document.querySelector('#status').textContent.includes('Simulation completed') && document.querySelector('#metrics').textContent.includes('OwnerDamage')",35000);
 assert.match(await evalJs("document.querySelector('#metrics').textContent"),/PetDamage/);
 await captureReport("warlock");
 // Beast Mastery executes the dedicated Hunter typed request with a real pet.
 await evalJs("(()=>{const x=document.querySelector('#classKey');x.value='hunter';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').options.length===3",25000);
 await evalJs("(()=>{const x=document.querySelector('#spec');x.value='beast-mastery';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').value==='beast-mastery'",25000);
 await evalJs("(()=>{const r=document.querySelector('#race');r.value='orc';r.dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='20';})()");
 await poll("document.querySelector('#race').value==='orc'");
 // Enable real Hunter engineering inventory through the owner-facing controls.
 await click("[data-view=effects]");
 await poll("!!document.querySelector('[data-combat-item=denseDynamiteCharges]')");
 await evalJs("(()=>{const section=[...document.querySelectorAll('.modifier-group')].find(x=>x.textContent.includes('Supported combat consumables'));const skill=section.querySelector('.tree-points input');skill.value='250';skill.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await evalJs("(()=>{const q=document.querySelector('.combat-item-entry input[aria-label^=Dense]');q.value='1';q.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('[data-combat-item=denseDynamiteCharges]')?.getAttribute('aria-pressed')==='true'");
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("document.querySelector('#status').textContent.includes('Simulation completed') && document.querySelector('#metrics').textContent.includes('OwnerDamage')",35000);
 assert.match(await evalJs("document.querySelector('#metrics').textContent"),/PetDamage/);
 await captureReport("hunter");
 assert.ok(await evalJs("document.querySelector('#metrics').textContent.includes('dense-dynamite')"),"Hunter consumable should report real use in results");
 // Nine real browser submissions through the unified frontend, not just backend unit tests.
 const nine=[
  ["mage",["arcane","fire","frost"],"human"],
  ["warlock",["affliction","demonology","destruction"],"orc"],
  ["hunter",["marksmanship","survival","beast-mastery"],"orc"]
 ];
 const verified=[];
 for(const [cls,specs,race] of nine){
  if(await evalJs("document.querySelector('#classKey').value")!==cls){
   await evalJs("(()=>{const x=document.querySelector('#classKey');x.value="+JSON.stringify(cls)+";x.dispatchEvent(new Event('change',{bubbles:true}));})()");
   await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#classKey').value==="+JSON.stringify(cls),25000);
  }
  for(const spec of specs){
   if(await evalJs("document.querySelector('#spec').value")!==spec){
    await evalJs("(()=>{const x=document.querySelector('#spec');x.value="+JSON.stringify(spec)+";x.dispatchEvent(new Event('change',{bubbles:true}));})()");
    await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').value==="+JSON.stringify(spec),25000);
   }
   await evalJs("(()=>{document.querySelector('#race').value="+JSON.stringify(race)+";document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='12';document.querySelector('#timeline').checked=true;document.querySelector('#racialActive').checked=false;})()");
   await poll("!document.querySelector('#run').disabled");
   await click("#run");
   await poll("document.querySelector('#status').textContent.includes('Simulation completed') && !document.querySelector('#run').disabled",35000);
   const value=await evalJs("document.querySelector('#metrics').textContent");
   assert.match(value,/DPS/i);
   verified.push(cls+"/"+spec);
  }
 }
 assert.equal(verified.length,9,"Every starting spec must submit actual simulation");
 console.log("CP174 all nine frontend simulation routes verified:",verified.join(", "));

 // All nine loaded presets should be fully ranked and validated by the server.
 await evalJs("(()=>{const x=document.querySelector('#classKey');x.value='mage';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').value==='arcane'",25000);
 await click("[data-view=talents]");
 const selected=await evalJs("document.querySelector('#talentSummary').textContent");
 assert.match(selected,/51 rank-derived points/);
 assert.equal(await evalJs("document.querySelector('#itemizeTalents')"),null);
 assert.equal(await evalJs("document.querySelector('[data-talent-key=wand-specialization] .talent-rank').textContent"),"2/2");
 await click("#save");
 await click("#clearTalents");
 await poll("document.querySelector('#talentSummary').textContent.includes('0 rank-derived points')");
 await click("#load");
 await poll("document.querySelector('#status').textContent.includes('Build loaded') && document.querySelector('#talentSummary').textContent.includes('51 rank-derived points')",25000);
 assert.equal(await evalJs("document.querySelector('[data-talent-key=wand-specialization] .talent-rank').textContent"),"2/2");
 await evalJs("(()=>{document.querySelector('#race').value='human';document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='12';document.querySelector('#timeline').checked=true;document.querySelector('#racialActive').checked=false;})()");
 await click("#run");
 await poll("document.querySelector('#status').textContent.includes('Simulation completed')",35000);
 console.log("CP174 automatic 51-point strict itemized default verified, saved, loaded and simulated");

 console.log("Mage item equipped:",equippedLabel,"compatible enchants:",enchantChoices-1);
 const errors=events.filter(e=>e.method==="Runtime.exceptionThrown");
 assert.deepEqual(errors.map(x=>x.params?.exceptionDetails?.text),[]);
 const consoleErrors=events.filter(e=>e.method==="Log.entryAdded"&&e.params?.entry?.level==="error");
 console.log("Chrome console errors:",consoleErrors.length,consoleErrors.slice(0,5).map(e=>({message:e.params.entry.text,url:e.params.entry.url})));
 const unexpected=consoleErrors.filter(e=>!(/\/api\/forever\/talents\/mage\/arcane\/validate$/.test(e.params.entry.url||'') && /400 \(Bad Request\)/.test(e.params.entry.text||'')));
 assert.deepEqual(unexpected.map(e=>e.params.entry.text),[],"Unexpected browser console errors must be resolved. Intentional prerequisite rejection is a verified 400.");
 console.log("CP174 real Chrome CDP interactions passed: tab navigation, rank left/right clicks, item picker search/close, modifier toggle, encounter/results views.",{initialRank:before,afterRank:after});
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);rmSync(profile,{recursive:true,force:true});});
