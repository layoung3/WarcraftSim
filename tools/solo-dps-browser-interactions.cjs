#!/usr/bin/env node
"use strict";
// Real Chromium CDP interactions; no third-party JS dependencies.
// The API should already be running at BASE_URL.
const assert=require("node:assert/strict");
const {spawn}=require("node:child_process");
const {mkdtempSync,rmSync,mkdirSync,existsSync,readFileSync,renameSync,readdirSync,writeFileSync}=require("node:fs");
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
 if(cls==="mage"){
  assert.equal(report.originalSimulationRequest.mage.useMajorManaPotion,false,"Unselected potion must not execute");
  assert.equal(report.originalSimulationRequest.mage.majorManaPotionCharges,0,"Unselected potion has zero inventory");
  assert.ok(!report.rawServerResponse.sim.run.timeline?.some(e=>e.abilityKey==="major-mana-potion"&&e.type===4),
    "No hidden Major Mana Potion event");
 }
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
 // Itemized Arcane default enables only explicitly supported learned
 // offensive cooldowns, never an invisible consumable inventory.
 await poll("document.querySelector('#useArcanePower')?.checked===true && document.querySelector('#usePresenceOfMind')?.checked===true");
 assert.equal(await evalJs("document.querySelector('[data-combat-item=majorManaPotionCharges]')?.getAttribute('aria-pressed')"),"false");
 assert.equal(await evalJs("document.querySelector('[data-combat-item=manaRubyCharges]')?.getAttribute('aria-pressed')"),"false","Mage cannot start with prepared Mana Ruby");
 assert.equal(await evalJs("document.querySelector('[data-combat-item=mightyRagePotionCharges]')"),null,"Rage potion hidden for Mage");

 assert.equal(await evalJs("document.querySelector('#targetDebuffMode')?.value"),"maintained");
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
 // A historical-build Mage set piece must show two independent indicators:
 // provisional source stats and a separately unimplemented set-bonus mechanic.
 await click('[data-slot=chest]');
 await poll("document.querySelector('#itemDialog').open");
 await evalJs("(()=>{const q=document.querySelector('#itemSearch');q.value='Frostfire Robe';q.dispatchEvent(new Event('input',{bubbles:true}));})()");
 await poll("document.querySelectorAll('#pickerItems .picker-item').length===1");
 await click("#pickerItems .picker-item");
 await poll("document.querySelectorAll('#pickerDetails .source-indicator').length===2");
 assert.ok(await evalJs("!!document.querySelector('#pickerDetails .source-indicator.confidence')"));
 assert.ok(await evalJs("!!document.querySelector('#pickerDetails .source-indicator.mechanics')"));
 await click("#pickerDetails .source-indicator.confidence");
 assert.equal(await evalJs("document.querySelector('#pickerDetails .source-indicator.confidence').getAttribute('aria-expanded')"),"true");
 await click("#pickerDetails .source-indicator.mechanics");
 assert.equal(await evalJs("document.querySelector('#pickerDetails .source-indicator.mechanics').getAttribute('aria-expanded')"),"true");
 const f2bScreens="/tmp/cp174-ui-captures";mkdirSync(f2bScreens,{recursive:true});
 const bothIndicators=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(f2bScreens,"cp175-f2b1-dual-confidence-set-bonus.png"),Buffer.from(bothIndicators.data,"base64"));
 await click("#closePicker");
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
  assert.ok(await evalJs("![...document.querySelectorAll('#pickerEnchantDetails .enchant-tile')].some(x=>x.textContent.includes('Wizard Oil'))"),"Temporary weapon effects must not be selectable permanent enchants");
  assert.ok(await evalJs("![...document.querySelectorAll('#pickerEnchantDetails button')].some(x=>x.textContent.includes('Open weapon consumables'))"),"No temporary management buttons in Enchants tab");
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
 if(equippedLabel==="Crackling Staff"){
  await click("[data-enchant-slot=main-hand]");
  await poll("document.querySelector('#itemDialog').open && document.querySelector('[data-enchant-spell-id=\"20036\"]')");
  await click('[data-enchant-spell-id=\"20036\"]');
  await poll("document.querySelector('.enchant-inspection .source-indicator.confidence')");
  assert.ok(await evalJs("document.querySelector('.enchant-inspection').textContent.includes('Intellect: 9')"));
  await click(".enchant-inspection .source-indicator.confidence");
  assert.equal(await evalJs("document.querySelector('.enchant-inspection .source-indicator.confidence').getAttribute('aria-expanded')"),"true");
  const enchantSource=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
  writeFileSync(path.join(f2bScreens,"cp175-f2b1-provisional-enchant-tooltip.png"),Buffer.from(enchantSource.data,"base64"));
  await click("#pickerEnchantDetails .simulate");
  await poll("!document.querySelector('#itemDialog').open",25000);
  // This Gnome fixture has no approved Forever character baseline. Preview
  // requires explicitly choosing the historical/provisional baseline first.
  await evalJs("(()=>{const b=document.querySelector('#provisional');b.checked=true;b.dispatchEvent(new Event('change',{bubbles:true}));})()");
  await poll("document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
  assert.ok(await evalJs("document.querySelector('#contributions').textContent.includes('enchant-20036')"),
      "Provisional +9 Intellect enchant must reach stat preview as a distinct source");
 }


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

 assert.equal(await evalJs("document.querySelector('#targetDebuffMode')?.value"),"maintained");
 await evalJs("(()=>{const x=document.querySelector('#targetDebuffMode');x.value='initial-only';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 assert.equal(await evalJs("document.querySelector('#targetDebuffMode')?.value"),"initial-only");
 await evalJs("(()=>{const x=document.querySelector('#targetDebuffMode');x.value='maintained';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
  await poll("document.querySelectorAll('.modifier-tile').length>0");
 const effect=await evalJs("document.querySelectorAll('.modifier-tile input[type=checkbox]').length===0");
 assert.equal(effect,true);
 // CP175-F2B2: new published Forever food is enabled in the same modifier
 // catalog, changes the real automatic stats and has a provisional source.
 assert.ok(await evalJs("!!document.querySelector('[data-modifier-key=baked-salmon]')"));
 assert.ok(await evalJs("!!document.querySelector('[data-modifier-key=elixir-of-cunning]')"));
 assert.ok(await evalJs("!!document.querySelector('[data-modifier-key=flask-of-chromatic-resistance]')"));
 assert.ok(await evalJs("!!document.querySelector('[data-modifier-key=mark-of-the-wild]')"));
 assert.ok(await evalJs("!!document.querySelector('[data-modifier-key=curse-of-recklessness]')"));
 const f2b2BaselineStats=await evalJs("document.querySelector('#stats').textContent");
 await click('[data-modifier-key=baked-salmon]');
 await poll("document.querySelector('#statStatus').textContent.includes('Up to date') && document.querySelector('#stats').textContent!=="+JSON.stringify(f2b2BaselineStats),25000);
 assert.ok(await evalJs("document.querySelector('[data-modifier-key=baked-salmon] .source-indicator.confidence')?.getAttribute('aria-label').includes('datamined') || document.querySelector('[data-modifier-key=baked-salmon] .source-indicator.confidence')?.getAttribute('aria-label').includes('published')"));
 const f2b2Screens='/tmp/cp174-ui-captures';mkdirSync(f2b2Screens,{recursive:true});
 const f2b2FoodImage=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(f2b2Screens,"cp175-f2b2-new-provisional-food.png"),Buffer.from(f2b2FoodImage.data,"base64"));
 await click('[data-modifier-key=baked-salmon]');
 await click('[data-modifier-key=curse-of-recklessness]');
 assert.ok(await evalJs("document.querySelector('[data-modifier-key=curse-of-recklessness] .source-indicator.mechanics')?.getAttribute('aria-label').includes('Some effects are not implemented')"));
 const f2b2Partial=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(f2b2Screens,"cp175-f2b2-partial-target-curse.png"),Buffer.from(f2b2Partial.data,"base64"));
 await click('[data-modifier-key=curse-of-recklessness]');
 const superiorSelector='.combat-item-entry input[aria-label="Superior Mana Potion available charges"]';
 assert.ok(await evalJs("!!document.querySelector("+JSON.stringify(superiorSelector)+")"));
 await evalJs("(()=>{const x=document.querySelector("+JSON.stringify(superiorSelector)+");x.value='2';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 assert.equal(await evalJs("document.querySelector('[data-combat-item=superiorManaPotionCharges]').getAttribute('aria-pressed')"),"true");
 await click('[data-combat-item=superiorManaPotionCharges]');
 assert.equal(await evalJs("document.querySelector('[data-combat-item=superiorManaPotionCharges]').getAttribute('aria-pressed')"),"false");
 assert.equal(await evalJs("document.querySelector("+JSON.stringify(superiorSelector)+").value"),"2","Disabling newer potion must preserve finite inventory");
 await evalJs("(()=>{const x=document.querySelector("+JSON.stringify(superiorSelector)+");x.value='0';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
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
 await click('[data-combat-item=denseDynamiteCharges]');
 assert.equal(await evalJs("document.querySelector('[data-combat-item=denseDynamiteCharges]').getAttribute('aria-pressed')"),"false");
 assert.equal(await evalJs("document.querySelector("+JSON.stringify(quantitySelector)+").value"),"3","Disabling must preserve inventory");
 assert.ok(await evalJs("document.querySelector('[data-combat-item=denseDynamiteCharges]').closest('.combat-item-entry').textContent.includes('Disabled')"));
 // Captured directly from real Chrome after selecting three charges but disabling execution.
 const f2aScreenshotDir="/tmp/cp174-ui-captures";
 mkdirSync(f2aScreenshotDir,{recursive:true});
 const disabledInventoryImage=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(f2aScreenshotDir,"cp175-f2a-mage-disabled-stock.png"),
    Buffer.from(disabledInventoryImage.data,"base64"));
 // Export and reimport the actual build file while an item is stocked but disabled.
 const disabledBuildPath=path.join(reportDir,"warcraftsim-build.json");
 await evalJs("document.querySelector('#exportBuild').click()");
 for(let i=0;i<100&&!existsSync(disabledBuildPath);i++)await sleep(100);
 assert.ok(existsSync(disabledBuildPath),"Disabled-stock build must export");
 const disabledBuild=JSON.parse(readFileSync(disabledBuildPath,"utf8"));
 assert.equal(disabledBuild.encounter.combatItems.denseDynamiteCharges,3);
 assert.ok(disabledBuild.encounter.combatItems.disabledItemKeys.includes("dense-dynamite"));
 await evalJs("(()=>{const file=new File(["+JSON.stringify(JSON.stringify(disabledBuild))+
   "], 'warcraftsim-build.json',{type:'application/json'});const transfer=new DataTransfer();transfer.items.add(file);"+
   "const input=document.querySelector('#file');input.files=transfer.files;input.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Build imported')",35000);
 assert.equal(await evalJs("document.querySelector('[data-combat-item=denseDynamiteCharges]')?.getAttribute('aria-pressed')"),"false");
 assert.equal(await evalJs("document.querySelector("+JSON.stringify(quantitySelector)+").value"),"3");
 // The six CP175 builds export to this same basename; remove the temporary
 // Mage file after verifying import to avoid stale contents in later assertions.
 rmSync(disabledBuildPath,{force:true});


 await click('[data-combat-item=denseDynamiteCharges]');
 assert.equal(await evalJs("document.querySelector('[data-combat-item=denseDynamiteCharges]').getAttribute('aria-pressed')"),"true");

 // Restore zero charges before simulating: an explosive requires Engineering 250.
 await evalJs("(()=>{const x=document.querySelector("+JSON.stringify(quantitySelector)+");x.value='0';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await click("[data-view=rotation]");
 await poll("!document.querySelector('#view-rotation').hidden && document.querySelector('#view-encounter').hidden");
 await click("#loadPriority");
 await poll("document.querySelectorAll('#priorityList .priority-entry').length>0",15000);
 await click("#applyPriority");
 await poll("document.querySelector('#priorityStatus').textContent.includes('Custom priority validated')",15000);
 await click("#resetPriority");
 assert.equal(await evalJs("document.querySelector('#priorityText').value"),"");
 assert.equal(await evalJs("!document.querySelector('#view-rotation').hidden"),true);
 await click("[data-view=encounter]");
 assert.equal(await evalJs("!document.querySelector('#view-encounter').hidden"),true);
 await click("[data-view=results]");
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
 // End-to-end browser submission against the real typed Mage endpoint.
 await evalJs("(()=>{document.querySelector('#race').value='human';document.querySelector('#provisional').checked=true;document.querySelector('#provisional').dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#duration').value='20';document.querySelector('#timeline').checked=true;})()");
 assert.equal(await evalJs("document.querySelector('#preview')"),null,"Manual stat Refresh button removed");
 await poll("document.querySelector('#statStatus').textContent.includes('Up to date') && document.querySelectorAll('#stats strong').length>0",25000);
 const originalStats=await evalJs("document.querySelector('#stats').textContent");
 await click("[data-view=effects]");
 await click('[data-modifier-key=arcane-intellect]');
 await poll("document.querySelector('#statStatus').textContent.includes('Up to date') && document.querySelector('#stats').textContent!=="+JSON.stringify(originalStats),25000);
 await click('[data-modifier-key=arcane-intellect]');
 await poll("document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
 await click("[data-view=encounter]");
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("!document.querySelector('#results').hidden && !!document.querySelector('#metrics strong')",30000);
 const dps=await evalJs("document.querySelector('#metrics').textContent");
 assert.match(dps,/DPS/i);
 const displayedDps=await evalJs("document.querySelector('#metrics strong')?.textContent||''");
 assert.ok(/^[0-9,]+[.][0-9]$/.test(displayedDps), "DPS must display one decimal while exported engine precision remains intact");
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
 await captureReport("mage");
 // CP175 multi-iteration stats use actual independent seed runs, not a
 // one-sample percentile. Restore single-run mode afterwards for legacy checks.
 await evalJs("document.querySelector('#iterations').value='3'");
 await click("#run");
 await poll("!document.querySelector('#distributionSummary').hidden && document.querySelector('#distributionSummary').textContent.includes('99th percentile DPS')",60000);
 assert.ok(await evalJs("document.querySelector('#distributionNotice').textContent.includes('3 independent seeded simulations')"));
 await click("#replayResult");
 await poll("document.querySelector('#status').textContent.includes('Repeated seeded distribution matched every sample')",60000);
 await evalJs("document.querySelector('#iterations').value='1'");
 console.log("CP175 actual 3-seed Mage DPS distribution and replay passed");

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
 const enabledInventoryImage=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(f2aScreenshotDir,"cp175-f2a-hunter-enabled-inventory.png"),
    Buffer.from(enabledInventoryImage.data,"base64"));

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

 // CP175 D5: actual file-picker import, custom rotation, combat, export,
 // deterministic replay and screenshots for six committed acceptance builds.
 const acceptanceDir=path.join(__dirname,"..","docs","acceptance","cp175");
 const acceptanceFiles=[
  "cp175-warrior-custom-rotation.json","cp175-f3a-fury-offtree-spearing.json","cp175-f3a-arms-mortal-strike.json","cp175-f3a2-fury-improved-berserker-rage.json","cp175-mage-custom-rotation.json",
  "cp175-f3b0-mage-arcane-stacks-resilience.json",
  "cp175-f3b1-arcane-mana-shield.json","cp175-f3b1-fire-arcane-subtlety.json",
  "cp175-f3b1-mage-missile-barrage.json",
  "cp175-f3b2-fire-combustion-pyroblast.json",
  "cp175-f3b2-fire-scorch-power.json",
  "cp175-f3b2-arcane-start-fire-mixed.json",
  "cp175-f3b3-frost-dps.json",
  "cp175-f3b3-frost-fingers-proc.json",
  "cp175-f3b3-frost-barrier.json",
  "cp175-f3b3-fire-start-frost-hybrid.json",
  "cp175-f3b4-arcane-fire.json",
  "cp175-f3b4-arcane-frost.json",
  "cp175-f3b4-fire-frost.json",
  "cp175-f3b4-three-trees.json",
  "cp175-f3c0-affliction-wrack.json",
  "cp175-f3c0-demonology-felhunter.json",
  "cp175-f3c0-destruction-shadow-flame.json",
  "cp175-f3c0-affliction-start-destruction.json",
  "cp175-f3c2-demonology-imp.json",
  "cp175-f3c2-destruction-start-demonology.json",
  "cp175-f3c2-affliction-start-demonology.json",
  "cp175-f3c3-destruction-bane-conflagrate.json",
  "cp175-f3c3-affliction-start-destruction.json",
  "cp175-f3c3-demonology-start-destruction.json",
  "cp175-f3c4-affliction-demonology.json",
  "cp175-f3c4-affliction-destruction.json",
  "cp175-f3c4-demonology-destruction.json",
  "cp175-f3c4-three-tree.json",
  "cp175-f3d0-marksmanship-with-pet.json",
  "cp175-f3d0-survival-melee.json",
  "cp175-f3d0-beast-mastery-with-pet.json",
  "cp175-f3d0-marksmanship-petless.json",
  "cp175-f3d1-marksmanship-shots.json",
  "cp175-f3d1-scatter-utility.json",
  "cp175-f3d1-beast-mastery-marksmanship.json",
  "cp175-f3d1-marksmanship-lone-wolf.json",
  "cp175-f3d2-melee-survival.json",
  "cp175-f3d2-lacerating-strikes.json",
  "cp175-f3d2-survival-beast-mastery.json",
  "cp175-f3d2-marksmanship-survival.json",
  "cp175-f3d3b-beast-wrath-frenzy.json",
  "cp175-f3d3b-wind-serpent-focus.json",
  "cp175-f3d3b-survival-bm.json",
  "cp175-f3d3b-petless-lone-wolf.json",
  "cp175-f3d4-marksmanship-beast-mastery.json",
  "cp175-f3d4-survival-beast-mastery.json",
  "cp175-f3d4-marksmanship-survival.json",
  "cp175-f3d4-three-trees.json",
  "cp175-f3d4-survival-three-trees.json",
  "cp175-f3d4-beast-mastery-three-trees.json",
  "cp175-f3d4b-survival-sniper-scatter.json",
  "cp175-f3d4b-beast-mastery-sniper-scatter.json",
  "cp175-f3d4b-marksmanship-summon-hawk.json",
  "cp175-f3d4b-marksmanship-survival-melee.json",
  "cp175-f3d4b-beast-mastery-survival-melee.json",
  "cp175-f4a-warrior-arms-fury.json",
  "cp175-f4a-mage-three-tree.json",
  "cp175-f4a-warlock-demonology-three-tree.json",
  "cp175-f4a-hunter-mortal-shots-pet.json",
  "cp175-warlock-custom-rotation.json","cp175-hunter-custom-rotation.json",
  "cp175-warrior-mixed-tree.json","cp175-warlock-mixed-tree.json",
  "cp175-warrior-scripted-execute.json","cp175-warlock-conditional-maintenance.json",
  "cp175-warrior-180s-scripted-add-groups.json",
  "cp175-warlock-180s-or-add-target.json"
 ];
 const screenshotDir="/tmp/cp174-ui-captures";mkdirSync(screenshotDir,{recursive:true});
 for(const filename of acceptanceFiles){
  const acceptance=JSON.parse(readFileSync(path.join(acceptanceDir,filename),"utf8"));
  const raw=JSON.stringify(acceptance);
  await evalJs(`(()=>{const input=document.querySelector('#file');const f=new File([${JSON.stringify(raw)}],
   ${JSON.stringify(filename)},{type:'application/json'});const transfer=new DataTransfer();
   transfer.items.add(f);input.files=transfer.files;
   input.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#classKey').value==="+JSON.stringify(acceptance.classKey),35000);
  assert.equal(await evalJs("document.querySelector('#classKey').value"),acceptance.classKey);
  assert.equal(await evalJs("document.querySelector('#spec').value"),acceptance.specialization);
  assert.equal(await evalJs("document.querySelector('#race').value"),acceptance.race);
  if(filename==="cp175-f3a2-fury-improved-berserker-rage.json"){
   await click("[data-view=encounter]");
   assert.equal(await evalJs("document.querySelector('#warriorBerserkerRage').checked"),true);
   const screenshot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,"cp175-f3a2-fury-berserker-rage-selected.png"),Buffer.from(screenshot.data,"base64"));
  }
  if(filename==="cp175-f3b0-mage-arcane-stacks-resilience.json"){
   await click("[data-view=talents]");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=arcane-resilience] .talent-rank').textContent"),"3/3");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=arcane-concentration] .talent-rank').textContent"),"5/5");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=arcane-resilience] .source-indicator.confidence')"),
    "70707 Arcane Resilience requires an explicit provisional-source indicator");
   await poll("document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
   const armorFromStats=()=>evalJs("(()=>{const e=[...document.querySelectorAll('#stats div')].find(x=>x.querySelector('span')?.textContent.toLowerCase()==='armor');return e?Number(e.querySelector('strong')?.textContent):null})()");
   const originalArmor=await armorFromStats();
   assert.ok(Number.isFinite(originalArmor),"Mage precombat armor must resolve");
   await click("[data-talent-key=arcane-resilience]","right");
   await poll("document.querySelector('[data-talent-key=arcane-resilience] .talent-rank').textContent==='2/3' && document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
   const lessArmor=await armorFromStats();
   assert.ok(originalArmor>lessArmor,"Arcane Resilience rank must change resolved armor preview");
   await click("[data-talent-key=arcane-resilience]");
   await poll("document.querySelector('[data-talent-key=arcane-resilience] .talent-rank').textContent==='3/3' && document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
   const armorRestored=await armorFromStats();
   assert.equal(armorRestored,originalArmor,"Armor preview must not double-apply on refresh");
   const beforeDependency=await evalJs("document.querySelector('[data-talent-key=arcane-concentration] .talent-rank').textContent");
   await click("[data-talent-key=arcane-concentration]","right");
   await sleep(500);
   assert.equal(await evalJs("document.querySelector('[data-talent-key=arcane-concentration] .talent-rank').textContent"),beforeDependency,
    "Arcane Meditation must reject removal of its 5/5 prerequisite");
   const mageCapture=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,"cp175-f3b0-mage-arcane-source-armor.png"),Buffer.from(mageCapture.data,"base64"));
   console.log("CP175 F3B0 Mage stat preview:",{originalArmor,lessArmor,armorRestored});
  }
  if(filename==="cp175-f3b1-arcane-mana-shield.json"){
   await click("[data-view=rotation]");
   assert.ok(await evalJs("document.querySelector('#priorityText').value.includes('mana-shield')"),
      "Mage defensive action must survive build import");
   const selected=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,"cp175-f3b1-arcane-mana-shield-rotation.png"),
      Buffer.from(selected.data,"base64"));
  }
  if(filename==="cp175-f3b1-fire-arcane-subtlety.json"){
   await click("[data-view=talents]");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=arcane-subtlety] .talent-rank').textContent"),"2/2");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=arcane-subtlety] .source-indicator.mechanics')"),
      "Arcane Subtlety remains partial due to unmodeled resist penetration");
  }
  if(filename.startsWith("cp175-f3b2-")){
   await click("[data-view=talents]");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=fire-power] .talent-rank').textContent"),"3/3");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=ignite] .talent-rank').textContent"),"5/5");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=impact] .talent-rank').textContent"),"3/3");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=impact] .source-indicator.mechanics')"),
      "Impact's unmodeled target control must remain visibly partial");
   const fireShot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
      Buffer.from(fireShot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3b3-")){
   await click("[data-view=talents]");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=frostbite] .talent-rank').textContent"),"3/3");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=improved-frost-nova] .talent-rank').textContent"),"2/2");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=ice-lance] .talent-rank').textContent"),"1/1");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=frostbite] .source-indicator.mechanics')"),
      "Frostbite is partial: real freeze proc, but enemy AI is unavailable");
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3b4-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51);
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),
       "selected rank must survive mixed-tree import: "+key+" -> "+shown);
   }
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3c2-")){
   await click("[data-view=talents]");
   const talent=filename.includes("affliction-start")?"improved-sayaad":"improved-imp";
   const rank=await evalJs("document.querySelector('[data-talent-key="+talent+"] .talent-rank')?.textContent||''");
   assert.equal(rank,"3/3","F3C2 learned pet talent must survive build import");
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d2-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51,
     "F3D2 itemized ranks must actually total 51");
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),
       "F3D2 rank "+key+" must survive import: "+shown);
   }
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=lightning-reflexes] .source-indicator.mechanics')") ||
     acceptance.specialization==="marksmanship",
     "Lightning Reflexes defensive dodge gap must remain disclosed");
   const capture=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(capture.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d4b-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51,
     "F3D4B cross-preset action builds must be legal 51-point allocations");
   const actionKeys=filename.includes("summon-hawk")?["summon-hawk"]:
     filename.includes("survival-melee")?["deterrence","strider-kick","counterattack"]:
     ["sniper-shot","scatter-shot"];
   for(const key of actionKeys){
     const rank=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.equal(rank,"1/1",key+" must be learned in the starting preset's talent tree");
   }
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d4-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51,
     "F3D4 must spend exactly 51 real itemized talent points");
   const totals=acceptance.talents;
   assert.equal(totals.marksmanshipPointsSpent+totals.survivalPointsSpent+
     totals.beastMasteryPointsSpent,51,"F3D4 aggregates cannot grant synthetic points");
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),"F3D4 imported rank missing "+key+": "+shown);
   }
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d3b-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51,
     "F3D3B requires 51 itemized Hunter talent points");
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),"F3D3B learned talent rank missing: "+key+" -> "+shown);
   }
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d1-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51,
     "Marksmanship acceptance must use 51 itemized talent points");
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),"F3D1 imported talent rank "+key+" -> "+shown);
   }
   if(filename.includes("scatter-utility"))
     assert.ok(await evalJs("!!document.querySelector('[data-talent-key=scatter-shot] .source-indicator.mechanics')"),
       "Scatter Shot must be marked partial for omitted enemy AI/range");
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3d0-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51);
   const index=Object.entries(acceptance.talents.ranks);
   for(const [key,rank] of index){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),
       "70753 Hunter talent rank must survive import: "+key+" -> "+shown);
   }
   const incomplete=acceptance.specialization==="survival"?
     "lightning-reflexes":acceptance.specialization==="beast-mastery"?
     "pathfinding":"scatter-shot";
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key="+incomplete+"] .source-indicator.mechanics')"),
      "Hunter incomplete "+incomplete+" must display accessible mechanical status");
   const screenshot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(screenshot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3c4-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51);
   for(const [key,rank] of Object.entries(acceptance.talents.ranks)){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.ok(shown.startsWith(rank+"/"),"F3C4 imported talent rank "+key+" -> "+shown);
   }
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3c3-")){
   await click("[data-view=talents]");
   assert.equal(Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0),51);
   for(const [key,rank] of Object.entries({
     bane:5,aftermath:5,intensity:3,pyroclasm:2,
     conflagrate:1,"shadow-and-flame":5
   })){
     const shown=await evalJs("document.querySelector('[data-talent-key="+key+"] .talent-rank')?.textContent||''");
     assert.equal(shown,rank+"/"+(key==="pyroclasm"?2:key==="conflagrate"?1:key==="intensity"?3:5),
       "Selected F3C3 source rank must survive import: "+key);
   }
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=pyroclasm] .source-indicator.mechanics')"),
     "Pyroclasm has source-backed proc markers but enemy control remains partial");
   const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(shot.data,"base64"));
  }
  if(filename.startsWith("cp175-f3c0-")){
   await click("[data-view=talents]");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=curse-of-exhaustion] .source-indicator.mechanics')"),
    "Current 70753 Curse of Exhaustion must warn that its movement slow is NOT modeled");
   const chosen=filename.includes("demonology")?"demonic-knowledge":
      filename.includes("destruction")?"shadow-and-flame":"improved-drains";
   const shownRank=await evalJs("document.querySelector('[data-talent-key="+chosen+"] .talent-rank').textContent");
   assert.ok(shownRank.startsWith(acceptance.talents.ranks[chosen]+"/"),
     "Warlock selected source rank must survive import: "+chosen+" -> "+shownRank);
   const capture=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,filename.replace(".json","-talents.png")),
     Buffer.from(capture.data,"base64"));
  }
  assert.equal(await evalJs("document.querySelectorAll('.priority-entry').length"),acceptance.encounter.priorityRows.length);
  assert.ok(await evalJs("document.querySelector('#priorityText').value.trim().length>0"),"Custom priority UI must be populated");
  if(filename==="cp175-f3a-fury-offtree-spearing.json"){
   await click("[data-view=talents]");
   const talent=await evalJs("document.querySelector('[data-talent-key=spearing-strike]')");
   assert.ok(talent,"Source-backed Arms Spearing Strike talent must be visible in Fury preset");
   assert.equal(await evalJs("document.querySelector('[data-talent-key=spearing-strike] .talent-rank').textContent"),"1/1");
   assert.ok(await evalJs("!!document.querySelector('[data-talent-key=spearing-strike] .source-indicator.confidence')"));
   const sourceCapture=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
   writeFileSync(path.join(screenshotDir,"cp175-f3a-fury-offtree-talents.png"),Buffer.from(sourceCapture.data,"base64"));
  }
  await click("[data-view=rotation]");
  const screenshot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
  writeFileSync(path.join(screenshotDir,filename.replace(".json","-rotation.png")),Buffer.from(screenshot.data,"base64"));
  // The responsive sidebar collapses at the CI viewport. Invoke the
  // real click handler rather than attempting coordinates on a hidden button.
  await evalJs("document.querySelector('#exportBuild').click()");
  const roundtripPath=path.join(reportDir,"warcraftsim-build.json");
  for(let retry=0;retry<120&&!existsSync(roundtripPath);retry++)await sleep(100);
  assert.ok(existsSync(roundtripPath),"Imported build must re-export");
  const roundtrip=JSON.parse(readFileSync(roundtripPath,"utf8"));
  assert.equal(roundtrip.classKey,acceptance.classKey);
  assert.deepEqual(roundtrip.talents.ranks,acceptance.talents.ranks);
  if(filename==="cp175-f3a2-fury-improved-berserker-rage.json")
   assert.deepEqual(roundtrip.encounter.warrior.majorCooldowns,
    acceptance.encounter.warrior.majorCooldowns,
    "Explicit Berserker Rage must survive Warrior build export and import");
  assert.deepEqual(roundtrip.gear,acceptance.gear);
  assert.deepEqual(roundtrip.encounter.priorityRows.map(x=>({key:x.key,enabled:x.enabled})),
   acceptance.encounter.priorityRows.map(x=>({key:x.key,enabled:x.enabled})));
  if(filename.includes("180s")){
   assert.deepEqual(roundtrip.encounter.soloScript,acceptance.encounter.soloScript,
    "180s scripted boss/add milestones must survive file import/export without changed time coordinates");
   assert.deepEqual(roundtrip.encounter.priorityRows.map(x=>({
    key:x.key,conditions:x.conditions||[],orGroups:x.orGroups||[],
    targetActorKey:x.targetActorKey||null})),
    acceptance.encounter.priorityRows.map(x=>({
    key:x.key,conditions:x.conditions||[],orGroups:x.orGroups||[],
    targetActorKey:x.targetActorKey||null})),
    "AND/OR conditions and explicit add targets must roundtrip without substitution");
  }
  renameSync(roundtripPath,path.join(reportDir,filename.replace(".json","-roundtrip.json")));
  // Previous class's Results panel may still be visible. Require this run
  // to repopulate it and finish before exporting any combat report.
  await evalJs("document.querySelector('#results').hidden=true");
  await click("#run");
  await poll("!document.querySelector('#results').hidden && !document.querySelector('#run').disabled && "+
    "(document.querySelector('#status').textContent.includes('Warrior completed') || "+
    "document.querySelector('#status').textContent.includes('Simulation completed'))",60000);
  await evalJs("document.querySelector('#exportResults').click()");
  const reportPath=path.join(reportDir,"warcraftsim-simulation-report.json");
  for(let retry=0;retry<120&&!existsSync(reportPath);retry++)await sleep(100);
  assert.ok(existsSync(reportPath),"Actual simulation report must download");
  const report=JSON.parse(readFileSync(reportPath,"utf8"));
  assert.equal(report.build.classKey,acceptance.classKey);
  const sim=report.rawServerResponse.sim;
  const timeline=acceptance.classKey==="warrior"?
   sim.batch.representativeRun.timeline:sim.run.timeline;
  const expected=filename==="cp175-f4a-mage-three-tree.json"?"frostfire-bolt":
   filename==="cp175-f4a-warlock-demonology-three-tree.json"?"shadow-bolt":
   filename==="cp175-f4a-hunter-mortal-shots-pet.json"?"scatter-shot":
   filename==="cp175-f3a-arms-mortal-strike.json"?"mortal-strike":
   filename==="cp175-f3a-fury-offtree-spearing.json"?"spearing-strike":
   acceptance.classKey==="warrior"?"bloodthirst":
   acceptance.classKey==="warlock"?
     (filename==="cp175-f3c0-affliction-wrack.json"?"wrack":
      filename==="cp175-f3c0-demonology-felhunter.json" ||
      filename.startsWith("cp175-f3c2-") ||
      filename.startsWith("cp175-f3c4-") ||
      filename==="cp175-f3c3-destruction-bane-conflagrate.json"?"shadow-bolt":"incinerate"):
   acceptance.classKey==="mage"?
     (filename==="cp175-f3b1-fire-arcane-subtlety.json"?"arcane-missiles":
      filename==="cp175-f3b2-fire-scorch-power.json"?"scorch":
      filename.startsWith("cp175-f3b2-")?"fire-blast":
      filename.startsWith("cp175-f3b3-")?
         (filename.includes("fingers-proc")||filename.includes("fire-start")?"ice-lance":"frostbolt"):
      filename.startsWith("cp175-f3b4-")?
       (filename.includes("arcane-fire")?"fireball":filename.includes("arcane-frost")?"frostbolt":"frostfire-bolt"):
      "arcane-blast"):((filename.startsWith("cp175-f3d2-")||filename.startsWith("cp175-f3d3b-")||filename.startsWith("cp175-f3d4-")||filename.startsWith("cp175-f3d4b-")||filename.startsWith("cp175-f3d4-"))&&
    acceptance.specialization==="survival"?"raptor-strike":
    filename.includes("survival-melee")?"strider-kick":
    filename.includes("marksmanship-summon-hawk")?"summon-hawk":
    filename.startsWith("cp175-f3d4b-")?"scatter-shot":filename==="cp175-f3d1-scatter-utility.json"?"scatter-shot":"aimed-shot");
  assert.ok(timeline.some(x=>x.abilityKey===expected && x.type===4) ||
     (filename==="cp175-f3d0-survival-melee.json"||
       (filename.startsWith("cp175-f3d2-")||filename.startsWith("cp175-f3d3b-"))&&acceptance.specialization==="survival")&&
       timeline.some(x=>x.abilityKey==="raptor-strike"&&x.type===6&&x.amount>0),
   filename+" must actually execute "+expected+"; no schema-only acceptance");
  if(filename.startsWith("cp175-f4a-")){
   const points=Object.values(acceptance.talents.ranks).reduce((a,b)=>a+b,0);
   assert.equal(points,51,"F4A must preserve 51 actual itemized talent points");
   assert.equal(report.talentAllocation.totalSelectedRanks,51);
   assert.ok(sim.dps>0,"F4A representative combat must deal positive actual damage");
   if(acceptance.classKey==="hunter"){
     assert.equal(report.originalSimulationRequest.survival.talents.ranks["mortal-shots"],5,
       "F4A selected off-tree Mortal Shots must survive typed request serialization");
     assert.ok(sim.petDamage>0,"F4A mixed Hunter must retain active pet");
     assert.ok(timeline.some(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&e.amount>0),
       "F4A Hunter pet damage needs real actor receipts");
     assert.ok(timeline.some(e=>e.type===6&&e.abilityKey==="sniper-shot"&&
       e.sourceActorKey==="hunter"&&e.isCritical),
       "F4A selected ranged critical passive must be tested on real shots");
   }
   if(acceptance.classKey==="warlock"){
     assert.ok(sim.demonDamage>0||sim.petDamage>0,
       "F4A mixed Warlock must retain actual demon damage attribution");
   }
   if(acceptance.classKey==="mage")
     assert.ok(timeline.some(e=>e.type===6&&e.abilityKey==="frostfire-bolt"&&e.amount>0),
       "F4A Mage must execute a mixed Fire/Frost projectile");
   if(acceptance.classKey==="warrior")
     assert.ok(timeline.some(e=>e.abilityKey==="bloodthirst"),
       "F4A Arms preset must execute legally learned Fury Bloodthirst");
   console.log("CP175 F4A real Chrome four-class",filename,{
     dps:sim.dps,owner:sim.hunterDamage??sim.warlockDamage,
     pet:sim.petDamage,iterations:1
   });
  }
  if(filename.startsWith("cp175-f3d2-")){
   const typed=report.originalSimulationRequest[acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3D2 complete itemized rank allocation must reach typed request");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.ok(sim.hunterDamage>0,"F3D2 owner combat must execute");
   const expectedPet=acceptance.encounter.companionEnabled;
   assert.equal(sim.petDamage>0,expectedPet,
     "F3D2 selected pet must actually execute, and petless builds remain petless");
   if(acceptance.specialization==="survival"){
     assert.ok(sim.meleeDamage>0,"Survival must remain melee-oriented");
     assert.ok(timeline.some(e=>e.abilityKey==="raptor-strike"&&
       e.type===6&&e.amount>0),"Survival Raptor Strike must resolve a real swing");
     assert.ok(timeline.some(e=>e.abilityKey==="raptor-strike"&&
       e.type===8&&e.amount<0),"Raptor Strike must spend actual Mana");
     assert.ok(sim.run.customMetrics["survival-effective-agility-at-pull"]>0);
     assert.ok(sim.run.customMetrics["survival-lightning-agility-crit-bonus-percent"]>0);
   }
   if(filename.includes("lacerating-strikes")){
     assert.ok(sim.run.customMetrics["survival-lacerating-bleed-ticks"]>0,
       "Lacerating must emit real 3-second tick damage");
     assert.ok(timeline.some(e=>e.type===6&&e.isPeriodic&&
       e.abilityKey==="lacerating-strikes"&&e.amount>0),
       "Talent bleed must deal owner-attributed periodic damage");
     assert.ok(timeline.some(e=>e.type===9&&
       e.abilityKey==="lacerating-strikes"),"Target-owned bleed aura required");
   }
   if(filename.includes("survival-beast-mastery")){
     assert.equal(acceptance.talents.beastMasteryPointsSpent,7);
     assert.equal(typed.talents.ranks["focused-fire"],2);
     assert.ok(timeline.some(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&e.amount>0),
       "Legally selected BM Focused Fire and pet must run in Survival");
   }
   if(filename.includes("marksmanship-survival")){
     assert.equal(typed.talents.ranks["improved-tracking"],5);
     assert.equal(acceptance.talents.survivalPointsSpent,5);
     assert.ok(timeline.some(e=>e.type===6&&e.abilityKey==="aimed-shot"&&e.amount>0),
       "MM preset must run actual trained Aimed Shot alongside Survival passive");
   }
   console.log("CP175 F3D2 real Chrome Survival receipts",filename,{
     spec:acceptance.specialization,petDamage:sim.petDamage,
     ownerDamage:sim.hunterDamage,meleeDamage:sim.meleeDamage||0,
     bleedTicks:sim.run.customMetrics["survival-lacerating-bleed-ticks"]||0
   });
  }
  if(filename.startsWith("cp175-f3d4b-")){
   const typed=report.originalSimulationRequest[
     acceptance.specialization==="beast-mastery"?"beastMastery":acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3D4B selected off-tree action ranks must reach the actual runner");
   const checkedActions=filename.includes("summon-hawk")?["summon-hawk"]:
     filename.includes("survival-melee")?["deterrence","strider-kick","counterattack"]:
     ["scatter-shot","sniper-shot"];
   for(const key of checkedActions){
     assert.ok(timeline.some(e=>e.type===4&&e.abilityKey===key&&e.sourceActorKey==="hunter"),
       "F3D4B cross-preset "+key+" must complete a real cast");
     if(key!=="deterrence")
       assert.ok(timeline.some(e=>e.type===8&&e.abilityKey===key&&
         e.sourceActorKey==="hunter"&&e.resourceKey==="mana"&&e.amount<0),
         "F3D4B "+key+" must spend real Mana");
     else
       assert.ok(timeline.some(e=>e.type===9&&e.abilityKey==="deterrence"&&
         e.targetActorKey==="hunter"),
         "Learned Deterrence must apply the native defensive aura");
   }
   if(filename.includes("survival-melee")){
     assert.equal(typed.useMeleeAuto,true,
       "Selected Hunter melee engagement must survive actual browser import");
     assert.equal(typed.scriptedParryEverySeconds,8,
       "Scripted parries must remain explicit, not a hidden resource");
     assert.equal(sim.petDamage>0,acceptance.encounter.companionEnabled,
       "Off-tree melee cannot create phantom pets or discard a selected companion");
     assert.ok(timeline.some(e=>e.type===6&&e.abilityKey==="strider-kick"&&e.amount>0),
       "Trained Strider Kick must resolve a real melee weapon strike");
     assert.ok(timeline.some(e=>e.abilityKey==="counterattack"&&
       String(e.description||"").includes("parried")),
       "Counterattack must receive an explicit parry-reactive opportunity");
   }
   if(filename.includes("summon-hawk")){
     assert.ok(sim.hawkDamage>0,"MM off-tree Summon Hawk must execute independent summoned actors");
     assert.ok(timeline.some(e=>e.type===6&&
       ["hawk-1","hawk-2"].includes(e.sourceActorKey)&&e.amount>0),
       "Summoned Hawks must deal separate attributed damage");
   }
   assert.ok(sim.hunterDamage>0,"F3D4B off-tree shots must contribute real owner damage");
   if(!filename.includes("survival-melee") || acceptance.encounter.companionEnabled)
     assert.ok(sim.petDamage>0,"F3D4B must preserve independently active companion");
   console.log("CP175 F3D4B real Chrome off-tree shots",filename,{
     ownerDamage:sim.hunterDamage,petDamage:sim.petDamage,
     sniperCasts:timeline.filter(e=>e.type===4&&e.abilityKey==="sniper-shot").length,
     scatterCasts:timeline.filter(e=>e.type===4&&e.abilityKey==="scatter-shot").length
   });
  }
  if(filename.startsWith("cp175-f3d4-")){
   const typed=report.originalSimulationRequest[
     acceptance.specialization==="beast-mastery"?"beastMastery":acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3D4 complete itemized ranks must reach the actual typed runner");
   assert.ok(sim.hunterDamage>0&&sim.petDamage>0,
     "F3D4 must execute owner and pet damage in each selected mix");
   assert.ok(timeline.some(e=>e.type===8 && e.sourceActorKey==="hunter-pet" &&
     e.resourceKey==="focus" && e.amount<0),
     "F3D4 pet must spend real Focus");
   assert.ok(timeline.some(e=>e.type===6 && e.sourceActorKey==="hunter-pet" &&
     e.amount>0),"F3D4 pet attacks must produce real damage");
   if(acceptance.specialization==="survival"){
     assert.ok(sim.meleeDamage>0,"Survival must stay melee-first");
     assert.ok(timeline.some(e=>e.abilityKey==="raptor-strike"&&e.type===6&&e.amount>0));
     assert.equal(typed.talents.ranks["focused-fire"],2);
   }
   if(filename.includes("three-trees")){
     assert.ok(typed.talents.ranks["improved-tracking"]>0 &&
       typed.talents.ranks["endurance-training"]>0 &&
       typed.talents.ranks["lethal-attacks"]>0,
       "All three trees must retain live modeled allocations");
   }
   if(filename.includes("marksmanship-beast-mastery")){
     assert.equal(typed.talents.ranks["endurance-training"],5);
     assert.ok(timeline.some(e=>e.abilityKey==="aimed-shot"&&e.type===6&&e.amount>0));
   }
   console.log("CP175 F3D4 real Chrome Hunter",filename,{
     ownerDamage:sim.hunterDamage,petDamage:sim.petDamage,points:51
   });
  }
  if(filename.startsWith("cp175-f3d3b-")){
   const typed=report.originalSimulationRequest[
     acceptance.specialization==="beast-mastery"?"beastMastery":acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3D3B selected talents must reach the typed Hunter request");
   assert.ok(sim.hunterDamage>0,"F3D3B owner damage required");
   const petHits=timeline.filter(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&e.amount>0);
   const petless=filename.includes("petless");
   assert.equal(sim.petDamage>0,!petless,
     "Pet damage must correspond to actual pet selection");
   assert.equal(petHits.length>0,!petless,
     "Pet actor attacks must not be phantom or missing");
   if(filename.includes("beast-wrath")){
     assert.equal(typed.talents.ranks["bestial-wrath"],1);
     assert.ok(timeline.some(e=>e.type===4&&e.abilityKey==="bestial-wrath"&&
       e.sourceActorKey==="hunter"),"Bestial Wrath must be cast");
     assert.ok(timeline.some(e=>e.type===9&&e.abilityKey==="bestial-wrath"&&
       e.targetActorKey==="hunter-pet"),"Bestial Wrath must affect active pet");
     assert.ok(timeline.some(e=>e.type===9&&e.abilityKey==="pet-frenzy"&&
       e.targetActorKey==="hunter-pet"),"Frenzy needs real pet critical proc");
   }
   if(filename.includes("wind-serpent")){
     assert.ok(timeline.some(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&
       e.abilityKey==="pet-lightning-breath"&&e.amount>0),
       "Wind Serpent must land family Nature damage");
     assert.ok(timeline.some(e=>e.type===8&&e.sourceActorKey==="hunter-pet"&&
       e.abilityKey==="pet-lightning-breath"&&e.resourceKey==="focus"&&e.amount===-50),
       "Wind Serpent must expend 50 Focus");
     assert.ok(timeline.some(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&
       e.abilityKey==="pet-melee"&&e.amount>0),
       "Pet must continue auto-attacks");
   }
   if(filename.includes("survival-bm")){
     assert.equal(typed.talents.ranks["focused-fire"],2);
     assert.ok(sim.meleeDamage>0,"Survival must remain melee-first");
     assert.ok(petHits.length>0,"BM off-tree pet effects require active pet");
   }
   if(petless){
     assert.equal(sim.petDamage,0);
     assert.equal(sim.run.customMetrics["hunter-no-pet-lone-wolf"],1);
   }
   console.log("CP175 F3D3B real Chrome Hunter",filename,{
     ownerDamage:sim.hunterDamage,petDamage:sim.petDamage,
     petHits:petHits.length,petless
   });
  }
  if(filename.startsWith("cp175-f3d1-")){
   const typed=report.originalSimulationRequest[
     acceptance.specialization==="beast-mastery"?"beastMastery":acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3D1 exact itemized talent ranks must reach engine");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   const noPet=!acceptance.encounter.companionEnabled;
   assert.ok(sim.hunterDamage>0,"Owner must produce ranged damage");
   assert.equal(sim.petDamage>0,!noPet,"Pet damage only when pet is selected");
   const ownedPet=timeline.filter(e=>e.type===6&&e.sourceActorKey==="hunter-pet"&&e.amount>0);
   assert.equal(ownedPet.length>0,!noPet,
     "No phantom Pet combat events in petless Marksmanship");
   if(filename.includes("marksmanship") || filename.includes("scatter")){
     assert.ok(timeline.some(e=>e.type===6&&e.sourceActorKey==="hunter"&&e.amount>0));
   }
   if(filename.includes("scatter-utility")){
     assert.equal(typed.talents.ranks["scatter-shot"],1);
     assert.ok(timeline.some(e=>e.abilityKey==="scatter-shot"&&e.type===6&&e.amount>0),
       "Scatter Shot must deliver physical damage, not only a metadata entry");
     assert.ok(timeline.some(e=>e.abilityKey==="scatter-shot"&&e.type===9),
       "Landed Scatter Shot must produce a source-owned disorient aura");
     assert.ok(sim.run.customMetrics["hunter-scatter-shot-auto-stops"]>0,
       "Using Scatter Shot must stop existing Auto Shot timer");
     assert.ok(timeline.some(e=>e.abilityKey==="scatter-shot"&&e.type===8&&e.amount<0),
       "Scatter Shot must pay real Mana");
   }
   if(filename.includes("marksmanship-shots")){
     assert.ok(timeline.some(e=>e.abilityKey==="aimed-shot"&&e.type===4),
       "Aimed Shot must complete its sourced 2-second cast");
     assert.ok(timeline.some(e=>e.abilityKey==="arcane-shot"&&e.type===6&&e.amount>0),
       "Independent Arcane Shot must hit");
     assert.ok(timeline.some(e=>e.abilityKey==="serpent-sting"&&e.isPeriodic&&e.amount>0),
       "Serpent Sting must tick");
     assert.ok(sim.run.customMetrics["hunter-trueshot-aura-applications"]>0,
       "Rank-5 Trueshot must apply real owner RAP");
   }
   if(filename.includes("beast-mastery-marksmanship")){
     assert.equal(typed.talents.ranks["lethal-attacks"],5,
       "BM preset must retain seven legally allocated MM ranks");
     assert.ok(ownedPet.length>0);
   }
   if(filename.includes("lone-wolf")){
     assert.equal(sim.petDamage,0);
     assert.equal(sim.run.customMetrics["hunter-no-pet-lone-wolf"],1);
   }
   console.log("CP175 F3D1 Chrome MM receipts",filename,{
     spec:acceptance.specialization,petDamage:sim.petDamage,
     ownerDamage:sim.hunterDamage,petHits:ownedPet.length,
     scatterHits:timeline.filter(e=>e.abilityKey==="scatter-shot"&&e.type===6&&e.amount>0).length
   });
  }
  if(filename.startsWith("cp175-f3d0-")){
   const typed=report.originalSimulationRequest[
     acceptance.specialization==="beast-mastery"?"beastMastery":acceptance.specialization];
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks);
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.ok(sim.hunterDamage>0,"Actual Hunter owner combat required");
   const petless=filename.includes("petless");
   assert.equal(sim.petDamage>0,!petless,
     "No phantom pet attacks, but enabled pets must participate in combat");
   const petHits=timeline.filter(x=>x.type===6&&x.sourceActorKey==="hunter-pet"&&x.amount>0);
   assert.equal(petHits.length>0,!petless,"Pet actor must match actual selected pet");
   if(petless){
     assert.equal(sim.run.customMetrics["hunter-no-pet-lone-wolf"],1,
       "Lone Wolf is eligible only without active pet");
     assert.ok(!timeline.some(x=>x.sourceActorKey==="hunter-pet"));
   }
   if(filename.includes("survival-melee")){
     assert.ok(sim.meleeDamage>0,"Survival must actually deal melee damage");
     assert.ok(timeline.some(x=>x.type===6&&x.abilityKey==="raptor-strike"&&x.amount>0),
       "Survival real Raptor Strike must replace next melee swing");
     assert.ok(timeline.some(x=>x.abilityKey==="raptor-strike"&&
       x.type===8&&x.resourceKey==="mana"&&x.amount<0),
       "Survival Raptor Strike must spend actual Mana");
   }
   if(filename.includes("marksmanship")){
     assert.ok(timeline.some(x=>x.type===6&&x.abilityKey==="aimed-shot"&&x.amount>0),
       "Actual Marksmanship Aimed Shot must land");
     assert.ok(timeline.some(x=>x.type===6&&x.abilityKey==="serpent-sting"&&x.isPeriodic),
       "Current source Serpent Sting must deal periodic damage");
   }
   if(filename.includes("beast-mastery")){
     assert.ok(timeline.some(x=>x.type===6&&x.sourceActorKey==="hunter-pet"&&x.amount>0),
       "Beast Mastery must deal real Pet damage");
   }
   console.log("CP175 F3D0 real Chrome Hunter",filename,{
     tree:acceptance.specialization,petEnabled:!petless,
     ownerDamage:sim.hunterDamage,petDamage:sim.petDamage,
     petHits:petHits.length,melee:sim.meleeDamage||0
   });
  }
  if(filename.startsWith("cp175-f3c0-")){
   const typed=report.originalSimulationRequest.warlock;
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "All current Warlock talent ranks must reach typed request (including off-tree)");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.equal(typed.useMajorManaPotion??false,false);
   if(filename.includes("affliction-wrack")){
     const ticks=timeline.filter(e=>e.abilityKey==="wrack"&&e.type===6&&e.isChannelTick);
     assert.ok(ticks.length>=3,"Real Affliction must execute channel Wrack damage ticks");
     assert.ok(timeline.some(e=>e.abilityKey==="corruption"&&e.type===9),
       "Actual Corruption DoT aura must be applied");
     assert.equal(sim.run.customMetrics["warlock-pet-maximum-health-at-pull"],0);
   }
   if(filename.includes("demonology-felhunter")){
     assert.equal(sim.demonType,"felhunter");
     assert.ok(sim.petDamage>0,
       "Actual Demonology damage must include a summoned Felhunter");
     assert.ok(timeline.some(e=>e.sourceActorKey==="warlock-pet"&&e.type===6&&e.amount>0),
       "Actual demon's damage must have its own event source");
     assert.ok(sim.run.customMetrics["warlock-pet-maximum-health-at-pull"]>0,
       "Source-backed owner-Stamina demon health must reach the real demon actor");
   }
   if(filename.includes("destruction-shadow-flame")){
     assert.ok(timeline.some(e=>e.abilityKey==="conflagrate"&&e.type===6&&e.amount>0),
       "Destruction must deal talent-granted Conflagrate damage");
     assert.ok(timeline.some(e=>e.abilityKey==="shadow-and-flame"&&e.type===9),
       "Landed Conflagrate must apply the Shadow and Flame buff");
     assert.ok(sim.run.customMetrics["warlock-shadow-and-flame-shadow-procs"]>0);
   }
   if(filename.includes("affliction-start-destruction")){
     assert.equal(report.originalSimulationRequest.specialization,"affliction");
     assert.ok(timeline.some(e=>e.abilityKey==="incinerate"&&e.type===6&&e.amount>0),
       "Affliction starting preset must execute legally learned Destruction Incinerate");
     assert.ok(sim.petDamage>0,"Off-preset talent use must retain separately executing demon");
   }
   console.log("CP175 F3C0 Warlock real Chrome combat receipts",filename,{
     owner:sim.warlockDamage,pet:sim.petDamage,
     wrack:timeline.filter(e=>e.abilityKey==="wrack"&&e.type===6).length,
     shadowAndFlame:sim.run.customMetrics["warlock-shadow-and-flame-shadow-procs"]
   });
  }
  if(filename.startsWith("cp175-f3c2-")){
   const typed=report.originalSimulationRequest.warlock;
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3C2 selected cross-tree pet talent ranks must reach the typed backend");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   const expectedDemon=filename.includes("affliction-start")?"succubus":"imp";
   const expectedPetSpell=expectedDemon==="imp"?"demon-firebolt":"demon-lash-of-pain";
   assert.equal(sim.demonType,expectedDemon,"selected Demonology demon remains active");
   assert.ok(sim.petDamage>0&&sim.warlockDamage>0,"actual owner and pet damage both execute");
   assert.ok(timeline.some(e=>e.sourceActorKey==="warlock-pet"&&
     e.abilityKey===expectedPetSpell&&e.type===6&&e.amount>0),
     "learned talent must affect a real named pet spell damage stream");
   assert.ok(sim.run.customMetrics["warlock-pet-maximum-health-at-pull"]>0,
     "pet receives actual scaled maximum Health");
   assert.equal(report.originalSimulationRequest.specialization,acceptance.specialization);
   if(filename.includes("destruction-start")){
     assert.equal(sim.sacrificeBuffAtPull,"felhunter",
       "learned Demonic Pact must preserve the separately sacrificed Felhunter buff");
     assert.ok(timeline.some(e=>e.abilityKey==="demonic-sacrifice"&&e.type===9),
       "sacrifice aura must be an actual combat event");
   }
   if(filename.includes("affliction-start"))
     assert.ok(timeline.some(e=>e.abilityKey==="corruption"&&e.type===9),
       "off-preset Affliction Corruption must apply its actual target-owned DoT");
   console.log("CP175 F3C2 real Chrome pet/cross-tree receipts",filename,{
     demon:sim.demonType,owner:sim.warlockDamage,pet:sim.petDamage,
     petSpellHits:timeline.filter(e=>e.sourceActorKey==="warlock-pet"&&
       e.abilityKey===expectedPetSpell&&e.type===6).length,
     sacrifice:sim.sacrificeBuffAtPull
   });
  }
  if(filename.startsWith("cp175-f3c4-")){
   const typed=report.originalSimulationRequest.warlock;
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks);
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.equal(report.originalSimulationRequest.specialization,acceptance.specialization);
   assert.equal(sim.demonType,"imp");
   assert.ok(sim.warlockDamage>0&&sim.petDamage>0,"Real owner and pet combat required");
   const impBolts=timeline.filter(x=>x.type===6&&x.sourceActorKey==="warlock-pet"&&
     x.abilityKey==="demon-firebolt"&&x.amount>0);
   assert.ok(impBolts.length>=2,"Real Imp Firebolt damage required");
   const ownerHit=key=>timeline.some(x=>x.type===6&&x.sourceActorKey==="warlock"&&
     x.abilityKey===key&&x.amount>0);
   if(filename.includes("affliction-")||filename.includes("three-tree")){
     assert.ok(ownerHit("corruption"),"Affliction branch requires real Corruption ticks");
     assert.ok(timeline.some(x=>x.type===9&&x.abilityKey==="corruption"));
   }
   if(filename.includes("affliction-demonology"))
     assert.ok(ownerHit("siphon-life"),"Learned off-tree Siphon Life must tick");
   if(filename.includes("destruction")||filename.includes("three-tree")){
     assert.ok(ownerHit("conflagrate"),"Learned Conflagrate must execute");
     assert.ok(timeline.some(x=>x.type===9&&x.abilityKey==="immolate"));
     assert.ok(timeline.some(x=>x.type===8&&x.abilityKey==="conflagrate"&&
       x.resourceKey==="mana"&&x.amount<0),"Conflagrate must cost real Mana");
   }
   if(filename.includes("affliction-destruction")||
      filename.includes("demonology-destruction"))
     assert.ok(sim.run.customMetrics["warlock-shadow-and-flame-shadow-procs"]>0,
       "Shadow and Flame must activate with legally learned talent");
   if(filename.includes("demonology-destruction")){
     assert.equal(sim.run.customMetrics["equipment-ephemeral-power@trinket1-uptime-seconds"],15,
       "Configured Talisman must provide 15s active owner Spell Power");
     assert.ok(timeline.some(x=>x.type===10&&x.effectKey==="ephemeral-power@trinket1"),
       "Talisman bonus must actually expire");
     const during=impBolts.find(x=>!x.isCritical&&x.timeSeconds<15);
     const after=impBolts.find(x=>!x.isCritical&&x.timeSeconds>15);
     assert.ok(during&&after&&during.amount>after.amount,
       "Live owner spell-power change must raise then restore actual pet Firebolt damage");
   }
   console.log("CP175 F3C4 real Chrome mixed-tree receipts",filename,{
     points:[typed.talents.afflictionPointsSpent,typed.talents.demonologyPointsSpent,
       typed.talents.destructionPointsSpent],
     owner:sim.warlockDamage,pet:sim.petDamage,firebolts:impBolts.length,
     conflagrate:timeline.filter(x=>x.type===6&&x.abilityKey==="conflagrate"&&x.amount>0).length
   });
  }
  if(filename.startsWith("cp175-f3c3-")){
   const typed=report.originalSimulationRequest.warlock;
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "F3C3 selected Destruction talent ranks must reach typed backend");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.equal(report.originalSimulationRequest.specialization,acceptance.specialization);
   const castStart=timeline.find(e=>e.abilityKey==="immolate"&&e.type===3);
   const castComplete=timeline.find(e=>e.abilityKey==="immolate"&&e.type===4&&
     e.abilityExecutionId===castStart?.abilityExecutionId);
   assert.ok(castStart&&castComplete,"Immolate must execute actual hardcasts");
   const seconds=castComplete.timeSeconds-castStart.timeSeconds;
   assert.ok(seconds>0&&seconds<=1.5,
     "Bane 5/5 must shorten actual Immolate hardcast from 2 seconds");
   const conflagrateHits=timeline.filter(e=>e.abilityKey==="conflagrate"&&
     e.type===6&&e.amount>0);
   assert.ok(conflagrateHits.length>0,"F3C3 must land real Conflagrate");
   assert.ok(timeline.some(e=>e.abilityKey==="immolate"&&e.type===9),
     "Conflagrate must be preceded by actual Immolate application");
   assert.ok(sim.run.customMetrics["warlock-shadow-and-flame-shadow-procs"]>0,
     "Conflagrate must grant real Shadow damage buff");
   assert.ok(timeline.some(e=>e.abilityKey==="shadow-and-flame"&&e.type===9),
     "Shadow and Flame buff must apply as a combat aura");
   if(filename.includes("bane-conflagrate")){
     assert.ok(timeline.some(e=>e.abilityKey==="shadowburn"&&e.type===6&&e.amount>0),
       "Shadowburn should provide the Fire school buff");
     assert.ok(sim.run.customMetrics["warlock-shadow-and-flame-fire-procs"]>0,
       "Shadowburn must produce a Fire buff receipt");
     assert.ok(sim.run.customMetrics["warlock-shadow-and-flame-instant-shards"]>0,
       "Rank-five Shadow and Flame must return actually spent Shards");
   } else {
     assert.ok(timeline.some(e=>e.abilityKey==="incinerate"&&e.type===6&&e.amount>0),
       "off-preset learned Incinerate must execute actual Fire damage");
     assert.ok(sim.petDamage>0,"Demon must remain independently active in mixed-tree builds");
   }
   // The browser has no selectable stun-susceptibility encounter fixture.
   // Source-backed Pyroclasm/Aftermath control events are proven via xUnit,
   // not presented as a fake browser simulation of enemy AI.
   assert.equal(sim.run.customMetrics["warlock-pyroclasm-stuns"],0);
   assert.equal(sim.run.customMetrics["warlock-aftermath-dazes"],0);
   console.log("CP175 F3C3 real Chrome Destruction receipts",filename,{
     spec:acceptance.specialization,pet:sim.petDamage,
     immolateCastSeconds:seconds,conflagrateHits:conflagrateHits.length,
     shadowBuffs:sim.run.customMetrics["warlock-shadow-and-flame-shadow-procs"],
     fireBuffs:sim.run.customMetrics["warlock-shadow-and-flame-fire-procs"]
   });
  }
  if(filename.startsWith("cp175-f3b4-")){
   const typed=report.originalSimulationRequest.mage;
   assert.deepEqual(typed.talents.ranks,acceptance.talents.ranks,
     "typed Mage request must preserve every executable mixed-tree rank");
   assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0),51);
   assert.equal(typed.useMajorManaPotion,false);
   assert.equal(typed.majorManaPotionCharges,0);
   const has=(key,type)=>timeline.some(e=>e.abilityKey===key&&e.type===type);
   if(filename.includes("arcane-fire")){
     assert.ok(has("arcane-power",4)&&has("presence-of-mind",4),
      "Arcane cooldowns must execute with actual Fire damage talents");
     assert.ok(has("fireball",6));
   }
   if(filename.includes("arcane-frost")){
     assert.ok(has("arcane-power",4));
     assert.ok(has("ice-lance",6)&&has("frostbolt",6),
      "Arcane-oriented Frost hybrid must cast learned Frost spell");
   }
   if(filename.includes("fire-frost")||filename.includes("three-trees")){
     assert.ok(has("frostfire-bolt",6),
       "Fire/Frost shared-school spell must actually deal real damage");
     assert.ok(has("frostfire-bolt",9),
       "Frostfire Bolt must apply its own periodic damage aura");
   }
   console.log("CP175 F3B4 mixed Mage combat receipts",filename,{
     direct:timeline.filter(e=>e.type===6&&!e.isPeriodic).length,
     ignite:timeline.filter(e=>e.abilityKey==="mage-ignite"&&e.type===6).length,
     barrage:sim.missileBarrageProcs
   });
  }
  if(filename.startsWith("cp175-f3b3-")){
   const typed=report.originalSimulationRequest.mage;
   assert.equal(typed.talents.ranks["frostbite"],3);
   assert.equal(typed.talents.ranks["improved-frost-nova"],2);
   assert.equal(typed.talents.ranks["ice-lance"],1);
   assert.equal(typed.talents.ranks["fingers-of-frost"],2);
   assert.equal(typed.talents.ranks["ice-barrier"],1);
   assert.equal(typed.useMajorManaPotion,false);
   assert.equal(typed.majorManaPotionCharges,0);
   if(filename.includes("fire-start"))
     assert.equal(report.originalSimulationRequest.specialization,"fire");
   assert.ok(timeline.some(e=>e.type===6&&e.schoolKey==="frost"&&e.amount>0),
     "F3B3 Frost must execute real Frost damage");
   if(filename.includes("barrier")||filename.includes("fire-start"))
     assert.ok(timeline.some(e=>e.abilityKey==="ice-barrier"&&e.type===24),
       "Actual 70707 Ice Barrier absorb must execute");
   if(filename.includes("fingers-proc")){
     assert.ok(timeline.some(e=>e.abilityKey==="fingers-of-frost"&&e.type===9),
       "Fingers of Frost proc must produce a real aura");
     assert.ok(sim.run.customMetrics["mage-fingers-charges-granted"]>0);
     assert.ok(timeline.some(e=>e.abilityKey==="ice-lance"&&e.type===6),
       "Ice Lance must execute in a proc-oriented Frost rotation");
   }
   if(filename.includes("frost-dps"))
     assert.ok(timeline.filter(e=>e.abilityKey==="frostbolt"&&e.type===6).length>=2,
       "Frostbolt must be a real repeated DPS spell");
   console.log("CP175 F3B3 real Chrome Frost receipts",filename,{
     fingers:sim.run.customMetrics["mage-fingers-charges-granted"],
     frostbite:sim.run.customMetrics["mage-frostbite-procs"],
     winter:timeline.filter(e=>e.abilityKey==="winters-chill"&&e.type===9).length
   });
  }
  if(filename.startsWith("cp175-f3b2-")){
   const typed=report.originalSimulationRequest.mage;
   assert.equal(typed.talents.ranks["fire-power"],3);
   assert.equal(typed.talents.ranks["ignite"],5);
   assert.equal(typed.talents.ranks["impact"],3);
   assert.equal(typed.talents.ranks["combustion"],1);
   assert.equal(typed.useMajorManaPotion,false);
   assert.equal(typed.majorManaPotionCharges,0);
   assert.ok(timeline.some(e=>e.type===6&&e.schoolKey==="fire"&&e.amount>0),
     "Fire checkpoint must deal actual Fire damage");
   if(filename==="cp175-f3b2-fire-scorch-power.json"){
    assert.ok(timeline.filter(e=>e.abilityKey==="scorch"&&e.type===6&&e.amount>0).length>=2);
    assert.ok(timeline.some(e=>e.abilityKey==="improved-scorch"&&e.type===9),
     "Landed Scorch must apply the real personal damage effect");
   }else{
    assert.ok(timeline.some(e=>e.abilityKey==="combustion"&&e.type===4),
     "Learned Combustion must actually activate");
    assert.ok(timeline.some(e=>e.abilityKey==="fire-blast"&&e.type===4),
     "Fire Blast must execute regardless of starting specialization");
    if(filename==="cp175-f3b2-fire-combustion-pyroblast.json"){
     assert.ok(timeline.some(e=>e.abilityKey==="pyroblast"&&e.type===4),
       "Heating Up-enabled Pyroblast must finish a real cast");
     assert.ok(timeline.some(e=>e.abilityKey==="heating-up"&&e.type===9),
       "Landed Fire Blast must generate Heating Up aura");
     assert.ok(timeline.some(e=>e.abilityKey==="heating-up"&&e.type===10),
       "Next non-PoM Pyroblast must consume Heating Up");
    }
   }
   const igniteTicks=timeline.filter(e=>e.abilityKey==="mage-ignite"&&e.type===6&&
       e.effectDeliveryType===1);
   console.log("CP175 F3B2 real browser Fire receipts",filename,
     {igniteTicks:igniteTicks.length,impactProcs:sim.run.customMetrics["mage-impact-procs"]});
  }
  if(filename==="cp175-f3b1-mage-missile-barrage.json"){
   assert.ok(timeline.some(e=>e.abilityKey==="missile-barrage"&&e.type===9),
     "Provisional 70707 proc must apply a real Missile Barrage aura");
   assert.ok(timeline.some(e=>e.abilityKey==="arcane-missiles"&&e.type===28),
     "The proc-conditioned priority must start a real Arcane Missiles channel");
   assert.ok(timeline.some(e=>e.abilityKey==="missile-barrage"&&e.type===10),
     "First Arcane Missiles must consume the proc aura");
   const missiles=timeline.filter(e=>e.abilityKey==="arcane-missiles"&&e.type===6);
   assert.ok(missiles.length>0,"Missiles must deal actual damage during the proc channel");
   const mana=timeline.filter(e=>e.abilityKey==="arcane-missiles"&&e.type===8);
   assert.ok(mana.some(e=>e.amount===0),
     "Missile Barrage must make its next channeled Arcane Missiles free");
  }
  if(filename==="cp175-f3b1-arcane-mana-shield.json"){
   assert.ok(timeline.some(e=>e.abilityKey==="mana-shield"&&e.type===4),
      "Defensive Arcane must actually complete Mana Shield cast");
   assert.ok(timeline.some(e=>e.abilityKey==="mana-shield"&&e.type===24),
      "Defensive Arcane must apply real shield absorption");
  }
  if(filename==="cp175-f3b1-fire-arcane-subtlety.json"){
   assert.equal(report.originalSimulationRequest.mage.talents.ranks["arcane-subtlety"],2);
   assert.ok(timeline.some(e=>e.abilityKey==="arcane-missiles"&&e.type===28),
      "Fire-starting Mage must execute Arcane Missiles channel");
  }
  if(filename==="cp175-f3b0-mage-arcane-stacks-resilience.json"){
   assert.equal(report.originalSimulationRequest.mage.talents.ranks["arcane-resilience"],3);
   assert.equal(report.originalSimulationRequest.mage.talents.ranks["arcane-meditation"],3);
   assert.equal(report.originalSimulationRequest.mage.talents.ranks["arcane-concentration"],5);
   assert.equal(report.originalSimulationRequest.mage.useArcanePower,false);
   assert.equal(report.originalSimulationRequest.mage.usePresenceOfMind,false);
   const armor=sim.run.customMetrics["mage-arcane-resilience-bonus-armor"];
   assert.ok(armor>0,"Real Mage actor must gain Arcane Resilience armor");
   assert.equal(sim.run.customMetrics["mage-arcane-blast-stacks-at-end"],4,
    "Real Mage must accumulate four Arcane Blast stacks");
   const payments=timeline.filter(e=>e.abilityKey==="arcane-blast" && e.type===8 && e.amount<0);
   assert.ok(payments.length>=3,"Real Mage must pay multiple scaled Blast costs");
   assert.ok(payments[1].amount<payments[0].amount,
    "Second Arcane Blast must cost more mana than the first");
   assert.ok(timeline.some(e=>e.abilityKey==="arcane-blast"&&e.type===4),
    "Actual Arcane Blast must complete casts");
  }
  if(filename==="cp175-f3a2-fury-improved-berserker-rage.json"){
   assert.equal(report.originalSimulationRequest.warrior.talentRanks["improved-berserker-rage"],2);
   assert.ok(report.originalSimulationRequest.warrior.majorCooldowns.some(x=>
    x.abilityKey==="berserker-rage"));
   assert.ok(timeline.some(x=>x.abilityKey==="berserker-rage"&&x.type===4),
    "Explicit Berserker Rage policy must complete an actual cast");
   assert.ok(timeline.some(x=>x.abilityKey==="improved-berserker-rage"&&
    x.type===8&&x.amount===10),
    "Berserker Rage 2/2 must generate an actual +10 Rage resource receipt");
  }
  if(filename==="cp175-f3a-arms-mortal-strike.json"){
   assert.equal(report.originalSimulationRequest.warrior.preset,"arms");
   assert.equal(report.originalSimulationRequest.warrior.talentRanks["mortal-strike"],1);
  }
  if(filename==="cp175-f3a-fury-offtree-spearing.json"){
   assert.equal(report.originalSimulationRequest.warrior.preset,"fury");
   assert.equal(report.originalSimulationRequest.warrior.talentRanks["spearing-strike"],1);
   assert.ok(timeline.some(x=>x.abilityKey==="spearing-strike"&&x.type===4),
    "Learned off-tree Spearing Strike must complete a real Battle Stance combat cast");
  }

  if(filename==="cp175-warrior-180s-scripted-add-groups.json"){
   assert.equal(report.originalSimulationRequest.warrior.durationSeconds,180);
   const milestones=report.originalSimulationRequest.warrior.soloEncounter.bossHealthMilestones;
   assert.deepEqual(milestones.map(x=>x.timeSeconds),[60,120,150,180]);
   const states=timeline.filter(x=>x.type===45);
   for(const [key,kind,time,percent] of [
    ["add-1","solo-dps-add-spawn",30,100],
    ["add-1","solo-dps-health-checkpoint",75,55],
    ["add-1","solo-dps-add-despawn",125,0],
    ["add-2","solo-dps-add-spawn",90,90],
    ["add-2","solo-dps-add-despawn",160,0],
    ["target","solo-dps-health-checkpoint",150,20]
   ])assert.ok(states.some(x=>x.targetActorKey===key&&x.effectKey===kind&&
    x.timeSeconds===time&&x.amount===percent),
    "Missing real scripted state receipt: "+key+" "+kind+" at "+time);
   assert.ok(timeline.some(x=>x.abilityKey==="execute"&&x.type===4&&x.timeSeconds>=150),
    "Warrior Execute must occur after scripted 150s 20% phase");
  }
  if(filename==="cp175-warlock-180s-or-add-target.json"){
   assert.equal(report.originalSimulationRequest.warlock.durationSeconds,180);
   const rule=report.originalSimulationRequest.warlock.priorityConditions;
   assert.ok(rule.some(x=>x.abilityKey==="immolate"&&x.targetActorKey==="target-1"));
   assert.ok(rule.some(x=>x.abilityKey==="incinerate"&&x.anyConditionGroups?.length===2));
   assert.ok(timeline.some(x=>x.abilityKey==="incinerate"&&x.type===4&&x.timeSeconds>=40),
    "Warlock Incinerate must execute when the first OR window becomes eligible");
   assert.ok(!timeline.some(x=>x.abilityKey==="incinerate"&&x.type===4&&x.timeSeconds<40),
    "Warlock Incinerate must not bypass both OR conditions");
   const states=timeline.filter(x=>x.type===45);
   assert.ok(states.some(x=>x.targetActorKey==="target-1"&&
    x.effectKey==="solo-dps-health-checkpoint"&&x.timeSeconds===75&&x.amount===40));
   assert.ok(timeline.some(x=>x.abilityKey==="immolate"&&x.type===4&&
    x.targetActorKey==="target-1"&&x.timeSeconds>=25&&x.timeSeconds<120),
    "Warlock Immolate must target active add and respect its spawn window");
   assert.ok(!timeline.some(x=>x.abilityKey==="immolate"&&x.type===4&&
    x.targetActorKey==="target-1"&&x.timeSeconds>=120),
    "Warlock must stop casting at a despawned add");
  }

  if(filename==="cp175-warrior-scripted-execute.json"){
   const script=report.originalSimulationRequest.warrior.soloEncounter;
   assert.equal(script.enabled,true);
   assert.deepEqual(script.bossHealthMilestones.map(x=>x.timeSeconds),[12,28]);
   assert.ok(timeline.some(x=>x.abilityKey==="execute"&&x.type===4&&x.timeSeconds>=12),
    "Warrior Execute must start after the scripted 20% threshold");
  }
  if(filename==="cp175-warlock-conditional-maintenance.json"){
   const rules=report.originalSimulationRequest.warlock.priorityConditions;
   assert.ok(rules.some(x=>x.abilityKey==="immolate"&&
    x.conditions.some(y=>y.conditionType==="target-aura-missing"&&y.key==="immolate")));
   assert.ok(rules.some(x=>x.abilityKey==="incinerate"&&
    x.conditions.some(y=>y.conditionType==="source-resource-percent"&&y.value===20)));
  }

  renameSync(reportPath,path.join(reportDir,filename.replace(".json","-report.json")));
  await evalJs("document.querySelector('#replayResult').click()");
  await poll("document.querySelector('#status').textContent.includes('replay matched') || document.querySelector('#status').textContent.includes('Replay matched')",60000);
  console.log("CP175 import/simulate/replay verified:",filename,expected);
 }


 // F3B4 negative browser acceptance: an actual deallocation must remove its
 // learned-only custom action; a stale imported action must fail closed at the
 // EXISTING server-side rotation validation (even with a legal 51-point build).
 const source=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3b4-fire-frost.json"),"utf8"));
 const importBuild=async(data,filename)=>{
   const json=JSON.stringify(data);
   await evalJs(`(()=>{const e=document.querySelector('#file');
     const f=new File([${JSON.stringify(json)}],${JSON.stringify(filename)},
       {type:'application/json'}),transfer=new DataTransfer();
     transfer.items.add(f);e.files=transfer.files;
     e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
 };
 await importBuild(source,"cp175-f3b4-positive-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#spec').value==='fire'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=combustion] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=combustion]","right");
 await poll("document.querySelector('[data-talent-key=combustion] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('combustion')"),false,
   "Removing a real learned Mage talent must remove its old custom priority action");
 assert.ok(await evalJs("document.querySelector('#priorityText').value.includes('frostfire-bolt')"),
   "Talent removal must retain the other legal custom-priority spells");
 const removedImage=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3b4-removed-combustion-priority.png"),
   Buffer.from(removedImage.data,"base64"));
 await evalJs("document.querySelector('#exportBuild').click()");
 const removedPath=path.join(reportDir,"warcraftsim-build.json");
 for(let retry=0;retry<120&&!existsSync(removedPath);retry++)await sleep(100);
 assert.ok(existsSync(removedPath),"Deallocated talent build must export");
 const removed=JSON.parse(readFileSync(removedPath,"utf8"));
 assert.equal(removed.talents.ranks.combustion,undefined);
 assert.ok(!removed.encounter.customPriority.includes("combustion"));
 renameSync(removedPath,path.join(reportDir,"cp175-f3b4-removed-talent-roundtrip.json"));

 const stale=JSON.parse(JSON.stringify(source));
 delete stale.talents.ranks.combustion;
 stale.talents.ranks["improved-fire-blast"]=1;
 assert.equal(Object.values(stale.talents.ranks).reduce((a,b)=>a+b,0),51,
    "The negative import must remain a valid 51-point build, not a trivial budget rejection");
 await importBuild(stale,"cp175-f3b4-stale-learned-action.json");
 await poll("document.querySelector('#status').textContent.includes('Mage priority requires a missing talent: combustion')",30000);
 assert.ok(!await evalJs("document.querySelector('#status').textContent.includes('Build imported')"),
   "Stale learned ability must not silently survive import");
 console.log("CP175 F3B4 real Chrome talent removal and stale-priority rejection passed");

 // F3B4 character-stat preview must respond to a real off-tree Arcane
 // Mind rank change, without altering the already selected equipment/food.
 const intellectFixture=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3b4-arcane-fire.json"),"utf8"));
 await importBuild(intellectFixture,"cp175-f3b4-arcane-mind-preview.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported')",35000);
 await click("[data-view=talents]");
 await poll("document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
 const currentIntellect=()=>evalJs("(()=>{const e=[...document.querySelectorAll('#stats div')].find(x=>x.querySelector('span')?.textContent.toLowerCase()==='intellect');return e?Number(e.querySelector('strong')?.textContent):null})()");
 const highIntellect=await currentIntellect();
 assert.ok(Number.isFinite(highIntellect)&&highIntellect>0);
 await click("[data-talent-key=arcane-mind]","right");
 await poll("document.querySelector('[data-talent-key=arcane-mind] .talent-rank').textContent==='4/5'&&document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
 const lowerIntellect=await currentIntellect();
 assert.ok(lowerIntellect<highIntellect,
    "Arcane Mind must change real resolved stat preview when a rank is removed");
 await click("[data-talent-key=arcane-mind]");
 await poll("document.querySelector('[data-talent-key=arcane-mind] .talent-rank').textContent==='5/5'&&document.querySelector('#statStatus').textContent.includes('Up to date')",25000);
 assert.equal(await currentIntellect(),highIntellect,
    "Restoring Arcane Mind cannot double-apply its intellect");
 const intellectShot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3b4-arcane-mind-stat-preview.png"),
   Buffer.from(intellectShot.data,"base64"));
 console.log("CP175 F3B4 Arcane Mind preview", {highIntellect,lowerIntellect});

 // CP175-F3C0 Warlock: use the actual DOM to remove the learned Incinerate
 // from a valid 51-point Destruction build. Its saved custom action must go,
 // while legal Immolate/Conflagrate remain. Then attempt a distinct valid
 // 51-point allocation that still contains an unlearned Incinerate action.
 const destruction=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3c0-destruction-shadow-flame.json"),"utf8"));
 await importBuild(destruction,"cp175-f3c0-warlock-positive-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#classKey').value==='warlock'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=incinerate] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=incinerate]","right");
 await poll("document.querySelector('[data-talent-key=incinerate] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('incinerate')"),false,
   "Removing a learned Warlock capstone must delete its old custom action");
 assert.ok(await evalJs("document.querySelector('#priorityText').value.includes('conflagrate')"),
   "Talent removal must not delete remaining legal learned abilities");
 const removalShot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3c0-warlock-talent-removal.png"),
   Buffer.from(removalShot.data,"base64"));
 await evalJs("document.querySelector('#exportBuild').click()");
 const warlockExport=path.join(reportDir,"warcraftsim-build.json");
 for(let retry=0;retry<120&&!existsSync(warlockExport);retry++)await sleep(100);
 assert.ok(existsSync(warlockExport));
 const warlockRemoval=JSON.parse(readFileSync(warlockExport,"utf8"));
 assert.equal(warlockRemoval.talents.ranks.incinerate,undefined);
 assert.ok(!warlockRemoval.encounter.customPriority.includes("incinerate"));
 renameSync(warlockExport,path.join(reportDir,"cp175-f3c0-warlock-removed-talent.json"));
 const staleWarlock=JSON.parse(JSON.stringify(destruction));
 delete staleWarlock.talents.ranks.incinerate;
 staleWarlock.talents.ranks.aftermath=5;
 assert.equal(Object.values(staleWarlock.talents.ranks).reduce((a,b)=>a+b,0),51);
 await importBuild(staleWarlock,"cp175-f3c0-warlock-stale-incinerate.json");
 await poll("document.querySelector('#status').textContent.includes('Unsupported Warlock priority for destruction: incinerate')",30000);
 assert.ok(!await evalJs("document.querySelector('#status').textContent.includes('Build imported')"),
   "Stale Warlock custom action must fail closed on imported legal build");
 console.log("CP175 F3C0 Warlock real Chrome talent removal and stale capstone rejection passed");

 // F3C4 deallocate learned Conflagrate from a real 17/17/17 mixed build,
 // verifying live UI rank removal and action scrubbing without an invalid import.
 const f3c4Mixed=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3c4-three-tree.json"),"utf8"));
 await importBuild(f3c4Mixed,"cp175-f3c4-three-tree-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#classKey').value==='warlock'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=conflagrate] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=conflagrate]","right");
 await poll("document.querySelector('[data-talent-key=conflagrate] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('conflagrate')"),false,
   "Mixed-tree talent removal must scrub old Conflagrate custom priority");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('corruption')"),true);
 const f3c4Removed=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3c4-three-tree-removed-conflagrate.png"),
   Buffer.from(f3c4Removed.data,"base64"));
 console.log("CP175 F3C4 real Chrome 3-tree talent removal passed");

 // F3D1: removing Scatter Shot from a LEGAL 51-point MM build must remove
 // its custom priority entry, leaving trained Arcane/Aimed abilities intact.
 const mmScatterSource=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3d1-scatter-utility.json"),"utf8"));
 await importBuild(mmScatterSource,"cp175-f3d1-scatter-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#spec').value==='marksmanship'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=scatter-shot] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=scatter-shot]","right");
 await poll("document.querySelector('[data-talent-key=scatter-shot] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('scatter-shot')"),false,
   "Unlearned Scatter Shot must disappear from custom priorities");
 assert.ok(await evalJs("document.querySelector('#priorityText').value.includes('arcane-shot')"),
   "Removing Scatter Shot must retain trained ranged actions");
 const scatterRemoved=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3d1-scatter-talent-removed.png"),
   Buffer.from(scatterRemoved.data,"base64"));
 console.log("CP175 F3D1 real Chrome Scatter talent removal passed");

 // F3D2: a legal Survival build can unlearn the active Counterattack
 // without keeping a stale priority entry that would bypass rank checks.
 const f3d2Removal=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3d2-melee-survival.json"),"utf8"));
 await importBuild(f3d2Removal,"cp175-f3d2-counterattack-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#spec').value==='survival'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=counterattack] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=counterattack]","right");
 await poll("document.querySelector('[data-talent-key=counterattack] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('counterattack')"),false,
   "Removing Counterattack must remove the learned-only action");
 const f3d2RemovedCapture=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3d2-counterattack-removed.png"),
   Buffer.from(f3d2RemovedCapture.data,"base64"));
 console.log("CP175 F3D2 real Chrome Counterattack talent removal passed");

 // F3D4: deallocating a learned Marksmanship action from a real 41/5/5
 // three-tree Hunter build must scrub its custom priority, while learned
 // BM and Survival ranks remain unchanged.
 const f3d4Removal=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3d4-three-trees.json"),"utf8"));
 await importBuild(f3d4Removal,"cp175-f3d4-three-tree-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#spec').value==='marksmanship'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=scatter-shot] .talent-rank').textContent"),"1/1");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=endurance-training] .talent-rank').textContent"),"5/5");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=improved-tracking] .talent-rank').textContent"),"5/5");
 await click("[data-talent-key=scatter-shot]","right");
 await poll("document.querySelector('[data-talent-key=scatter-shot] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('scatter-shot')"),false,
   "F3D4 removed talent must not remain in custom priority");
 assert.ok(await evalJs("document.querySelector('#priorityText').value.includes('aimed-shot')"),
   "F3D4 removal must preserve unrelated legal ranged actions");
 const f3d4Removed=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3d4-three-tree-scatter-removed.png"),
   Buffer.from(f3d4Removed.data,"base64"));
 console.log("CP175 F3D4 three-tree Chrome learned-action removal passed");
 // F3D4B: remove a genuine off-tree learned melee action from a BM-start
 // 29/22 build. Its previously imported custom priority must be reconciled
 // without removing still-learned Deterrence and Counterattack.
 const f3d4bRemoval=JSON.parse(readFileSync(path.join(acceptanceDir,
   "cp175-f3d4b-beast-mastery-survival-melee.json"),"utf8"));
 await importBuild(f3d4bRemoval,"cp175-f3d4b-strider-for-removal.json");
 await poll("document.querySelector('#status').textContent.includes('Build imported') && document.querySelector('#spec').value==='beast-mastery'",35000);
 await click("[data-view=talents]");
 assert.equal(await evalJs("document.querySelector('[data-talent-key=strider-kick] .talent-rank').textContent"),"1/1");
 await click("[data-talent-key=strider-kick]","right");
 await poll("document.querySelector('[data-talent-key=strider-kick] .talent-rank').textContent==='0/1'",30000);
 await click("[data-view=rotation]");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('strider-kick')"),false,
   "Removing off-tree Strider must revoke its old custom action");
 assert.equal(await evalJs("document.querySelector('#priorityText').value.includes('deterrence')"),true);
 const f3d4bRemoved=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});
 writeFileSync(path.join(screenshotDir,"cp175-f3d4b-offtree-strider-removed.png"),
   Buffer.from(f3d4bRemoved.data,"base64"));
 console.log("CP175 F3D4B BM-start Survival talent deallocation passed");
 const errors=events.filter(e=>e.method==="Runtime.exceptionThrown");
 assert.deepEqual(errors.map(x=>x.params?.exceptionDetails?.text),[]);
 const consoleErrors=events.filter(e=>e.method==="Log.entryAdded"&&e.params?.entry?.level==="error");
 console.log("Chrome console errors:",consoleErrors.length,consoleErrors.slice(0,5).map(e=>({message:e.params.entry.text,url:e.params.entry.url})));
 console.log("CP175 stat refresh diagnostics:",await evalJs("({failures:document.querySelector('#statStatus').dataset.failureCount||'0',last:document.querySelector('#statStatus').dataset.lastError||'',status:document.querySelector('#statStatus').textContent})"));
 const expectedStaleImportErrors=consoleErrors.filter(e=>
  /\/api\/forever\/rotation\/preview$/.test(e.params.entry.url||'')&&
  /400 \(Bad Request\)/.test(e.params.entry.text||''));
 assert.equal(expectedStaleImportErrors.length,2,
  "Deliberately invalid Mage and Warlock 51-point imports must each cause one strict rotation 400");
 const unexpected=consoleErrors.filter(e=>
  !(/\/api\/forever\/talents\/mage\/arcane\/validate(?:-itemized)?$/.test(e.params.entry.url||'') &&
     /400 \(Bad Request\)/.test(e.params.entry.text||'')) &&
  !expectedStaleImportErrors.includes(e));
 assert.deepEqual(unexpected.map(e=>e.params.entry.text),[],
  "Unexpected Chrome errors cannot be suppressed by intentional negative talent/priority 400s.");
 console.log("CP174 real Chrome CDP interactions passed: tab navigation, rank left/right clicks, item picker search/close, modifier toggle, encounter/results views.",{initialRank:before,afterRank:after});
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);rmSync(profile,{recursive:true,force:true});});
