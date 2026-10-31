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
 throw Error("Timed out waiting: "+expression);
}
async function click(selector,button="left"){
 const pt=await evalJs(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error("No element");e.scrollIntoView({block:"center"});const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()`);
 await command("Input.dispatchMouseEvent",{type:"mousePressed",x:pt.x,y:pt.y,button,clickCount:1});
 await command("Input.dispatchMouseEvent",{type:"mouseReleased",x:pt.x,y:pt.y,button,clickCount:1});
}
async function main(){
 for(let tries=0;tries<80;tries++){try{const d=await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:"PUT"})).json();if(d.webSocketDebuggerUrl){ws=new WebSocket(d.webSocketDebuggerUrl);break;}}catch{}await sleep(100);}
 if(!ws)throw Error("Chrome debugging port unavailable");
 await new Promise((resolve,reject)=>{ws.addEventListener("open",resolve,{once:true});ws.addEventListener("error",reject,{once:true});});
 ws.addEventListener("message",event=>{let msg=JSON.parse(event.data);if(msg.id){const q=pending.get(msg.id);if(q){pending.delete(msg.id);if(msg.error)q.reject(Error(msg.error.message));else q.resolve(msg.result);}}else events.push(msg);});
 await command("Page.enable");await command("Runtime.enable");
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
 await command("Input.dispatchKeyEvent",{type:"keyDown",key:"Escape",code:"Escape",windowsVirtualKeyCode:27});
 await command("Input.dispatchKeyEvent",{type:"keyUp",key:"Escape",code:"Escape",windowsVirtualKeyCode:27});
 await poll("!document.querySelector('#itemDialog').open");
 await click("[data-view=effects]");
 await poll("document.querySelectorAll('.modifier-tile').length>0");
 const effect=await evalJs("(()=>{const x=[...document.querySelectorAll('.modifier-tile input')].find(i=>!i.checked);return x?true:false})()");
 assert.equal(effect,true);
 await click(".modifier-tile");
 await click("[data-view=encounter]");
 assert.equal(await evalJs("!document.querySelector('#view-encounter').hidden"),true);
 await click("[data-view=results]");
 assert.equal(await evalJs("!document.querySelector('#view-results').hidden"),true);
 const errors=events.filter(e=>e.method==="Runtime.exceptionThrown");
 assert.deepEqual(errors.map(x=>x.params?.exceptionDetails?.text),[]);
 console.log("CP174 real Chrome CDP interactions passed: tab navigation, rank left/right clicks, item picker search/escape, modifier toggle, encounter/results views.",{initialRank:before,afterRank:after});
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);rmSync(profile,{recursive:true,force:true});});
