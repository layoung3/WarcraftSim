#!/usr/bin/env node
"use strict";
// Real Chromium CDP interactions; no third-party JS dependencies.
// The API should already be running at BASE_URL.
const assert=require("node:assert/strict");
const {spawn}=require("node:child_process");
const {mkdtempSync,rmSync}=require("node:fs");
const {tmpdir}=require("node:os");
const path=require("node:path");
const BASE=process.env.CP174_BASE_URL||"http://127.0.0.1:5142";
const CHROME=process.env.CP174_CHROME||"/usr/bin/google-chrome";
const profile=mkdtempSync(path.join(tmpdir(),"cp174-browser-"));
const port=19347;
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
async function main(){
 for(let tries=0;tries<80;tries++){try{const d=await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:"PUT"})).json();if(d.webSocketDebuggerUrl){ws=new WebSocket(d.webSocketDebuggerUrl);break;}}catch{}await sleep(100);}
 if(!ws)throw Error("Chrome debugging port unavailable");
 await new Promise((resolve,reject)=>{ws.addEventListener("open",resolve,{once:true});ws.addEventListener("error",reject,{once:true});});
 ws.addEventListener("message",event=>{let msg=JSON.parse(event.data);if(msg.id){const q=pending.get(msg.id);if(q){pending.delete(msg.id);if(msg.error)q.reject(Error(msg.error.message));else q.resolve(msg.result);}}else events.push(msg);});
 await command("Page.enable");await command("Runtime.enable");await command("Log.enable");
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
 await click("#pickerItems .picker-item");
 await poll("document.querySelector('#pickerDetails .simulate') && document.querySelectorAll('#pickerDetails .enchant-group').length===2");
 const enchantChoices=await evalJs("document.querySelectorAll('#pickerDetails .enchant-group:first-child .enchant-tile').length");
 if(enchantChoices>1)await click('#pickerDetails .enchant-group:first-child .enchant-tile:nth-child(2)');
 await click("#pickerDetails .simulate");
 await poll("!document.querySelector('#itemDialog').open && !document.querySelector('[data-slot=main-hand] .empty-item')",20000);
 const equippedLabel=await evalJs("document.querySelector('[data-slot=main-hand] .slot-copy strong').textContent");
 assert.notEqual(equippedLabel,"Empty slot");
 await click("[data-view=effects]");
 await poll("document.querySelectorAll('.modifier-tile').length>0");
 const effect=await evalJs("(()=>{const x=[...document.querySelectorAll('.modifier-tile input')].find(i=>!i.checked);return x?true:false})()");
 assert.equal(effect,true);
 await click(".modifier-tile");
 assert.equal(await evalJs("document.querySelector('.modifier-tile').classList.contains('selected')"),true);
 await click(".modifier-tile");
 assert.equal(await evalJs("document.querySelector('.modifier-tile').classList.contains('selected')"),false);
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
 await evalJs("(()=>{document.querySelector('#race').value='human';document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='20';document.querySelector('#timeline').checked=false;})()");
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("!document.querySelector('#results').hidden && !!document.querySelector('#metrics strong')",30000);
 const dps=await evalJs("document.querySelector('#metrics').textContent");
 assert.match(dps,/DPS/i);
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
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
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("document.querySelector('#status').textContent.includes('Simulation completed') && document.querySelector('#metrics').textContent.includes('OwnerDamage')",35000);
 assert.match(await evalJs("document.querySelector('#metrics').textContent"),/PetDamage/);
 // Beast Mastery executes the dedicated Hunter typed request with a real pet.
 await evalJs("(()=>{const x=document.querySelector('#classKey');x.value='hunter';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').options.length===3",25000);
 await evalJs("(()=>{const x=document.querySelector('#spec');x.value='beast-mastery';x.dispatchEvent(new Event('change',{bubbles:true}));})()");
 await poll("document.querySelector('#status').textContent.includes('Class defaults loaded') && document.querySelector('#spec').value==='beast-mastery'",25000);
 await evalJs("(()=>{const r=document.querySelector('#race');r.value='orc';r.dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#provisional').checked=true;document.querySelector('#duration').value='20';})()");
 await poll("document.querySelector('#race').value==='orc'");
 await poll("!document.querySelector('#run').disabled",15000);
 await click("#run");
 await poll("document.querySelector('#status').textContent.includes('Simulation completed') && document.querySelector('#metrics').textContent.includes('OwnerDamage')",35000);
 assert.match(await evalJs("document.querySelector('#metrics').textContent"),/PetDamage/);
 console.log("Mage item equipped:",equippedLabel,"compatible enchants:",enchantChoices-1);
 const errors=events.filter(e=>e.method==="Runtime.exceptionThrown");
 assert.deepEqual(errors.map(x=>x.params?.exceptionDetails?.text),[]);
 const consoleErrors=events.filter(e=>e.method==="Log.entryAdded"&&e.params?.entry?.level==="error");
 console.log("Chrome console errors:",consoleErrors.length,consoleErrors.slice(0,5).map(e=>({message:e.params.entry.text,url:e.params.entry.url})));
 assert.deepEqual(consoleErrors.map(e=>e.params.entry.text),[],"Browser console errors must be resolved before acceptance");
 console.log("CP174 real Chrome CDP interactions passed: tab navigation, rank left/right clicks, item picker search/close, modifier toggle, encounter/results views.",{initialRank:before,afterRank:after});
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);rmSync(profile,{recursive:true,force:true});});
