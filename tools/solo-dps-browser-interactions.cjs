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
  const expected=filename==="cp175-f3a-arms-mortal-strike.json"?"mortal-strike":
   filename==="cp175-f3a-fury-offtree-spearing.json"?"spearing-strike":
   acceptance.classKey==="warrior"?"bloodthirst":
   acceptance.classKey==="warlock"?"incinerate":
   acceptance.classKey==="mage"?"arcane-blast":"aimed-shot";
  assert.ok(timeline.some(x=>x.abilityKey===expected && x.type===4),
   filename+" must actually execute "+expected+"; no schema-only acceptance");
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

 const errors=events.filter(e=>e.method==="Runtime.exceptionThrown");
 assert.deepEqual(errors.map(x=>x.params?.exceptionDetails?.text),[]);
 const consoleErrors=events.filter(e=>e.method==="Log.entryAdded"&&e.params?.entry?.level==="error");
 console.log("Chrome console errors:",consoleErrors.length,consoleErrors.slice(0,5).map(e=>({message:e.params.entry.text,url:e.params.entry.url})));
 console.log("CP175 stat refresh diagnostics:",await evalJs("({failures:document.querySelector('#statStatus').dataset.failureCount||'0',last:document.querySelector('#statStatus').dataset.lastError||'',status:document.querySelector('#statStatus').textContent})"));
 const unexpected=consoleErrors.filter(e=>!(/\/api\/forever\/talents\/mage\/arcane\/validate(?:-itemized)?$/.test(e.params.entry.url||'') && /400 \(Bad Request\)/.test(e.params.entry.text||'')));
 assert.deepEqual(unexpected.map(e=>e.params.entry.text),[],"Unexpected browser console errors must be resolved. Intentional invalid-rank/prerequisite rejection is a verified strict 400.");
 console.log("CP174 real Chrome CDP interactions passed: tab navigation, rank left/right clicks, item picker search/close, modifier toggle, encounter/results views.",{initialRank:before,afterRank:after});
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);rmSync(profile,{recursive:true,force:true});});
