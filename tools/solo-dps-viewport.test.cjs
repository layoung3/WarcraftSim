"use strict";
const assert=require("node:assert/strict");
const {spawn}=require("node:child_process");
const fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-viewport-"));
const out=process.env.CP174_SCREENSHOT_DIR||"/tmp/cp174-ui-captures";
fs.mkdirSync(out,{recursive:true});
const proc=spawn(process.env.CP174_CHROME||"/usr/bin/google-chrome",["--headless","--no-sandbox","--disable-gpu","--remote-debugging-port=19348","--user-data-dir="+profile,"about:blank"],{stdio:"ignore"});
const sleep=ms=>new Promise(r=>setTimeout(r,ms));let ws,id=0,pending=new Map();
function command(method,params={}){return new Promise((resolve,reject)=>{const key=++id,t=setTimeout(()=>{pending.delete(key);reject(Error(method+" timed out"));},15000);pending.set(key,{resolve:value=>{clearTimeout(t);resolve(value);},reject});ws.send(JSON.stringify({id:key,method,params}));});}
async function evaluate(expression){const r=await command("Runtime.evaluate",{expression,returnByValue:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.text);return r.result.value;}
async function main(){
 let tabs;for(let i=0;i<60;i++){try{tabs=await(await fetch("http://127.0.0.1:19348/json")).json();if(tabs.some(t=>t.type==="page"))break;}catch{}await sleep(100);}
 ws=new WebSocket(tabs.find(t=>t.type==="page").webSocketDebuggerUrl);await new Promise(r=>ws.addEventListener("open",r,{once:true}));
 ws.addEventListener("message",e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(p)m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}});
 await command("Page.enable");await command("Runtime.enable");
 for(const width of [390,768,1440,1920]){
  await command("Emulation.setDeviceMetricsOverride",{width,height:900,deviceScaleFactor:1,mobile:false});
  for(const view of ["equipment","talents","effects","encounter","rotation","results"]){
   await command("Page.navigate",{url:(process.env.CP174_BASE_URL||"http://127.0.0.1:5142")+"/solo-dps.html?view="+view});
   let ready=false;for(let i=0;i<100;i++){try{ready=await evaluate("!!document.querySelector('#view-"+view+"')&&!document.querySelector('#view-"+view+"').hidden&&document.querySelector('#status').textContent.includes('Class defaults loaded')");}catch{}if(ready)break;await sleep(100);}
   assert.ok(ready,width+" "+view+" initialized");
   assert.ok(await evaluate("['importBuild','exportBuild'].every(id=>document.getElementById(id).getClientRects().length>0)"),"Build import/export must remain available at "+width);
   const size=await evaluate("({viewport:innerWidth,page:document.documentElement.scrollWidth,run:document.querySelector('#run').getBoundingClientRect().toJSON()})");
   assert.ok(size.page<=width+1,JSON.stringify({width,view,...size}));
   assert.ok(size.run.left>=0&&size.run.right<=width+1,"Simulate button must stay reachable: "+JSON.stringify({width,view,...size}));
   if(["equipment","talents","effects"].includes(view)){const shot=await command("Page.captureScreenshot",{format:"png",captureBeyondViewport:false});fs.writeFileSync(path.join(out,"cp175-viewport-"+width+"-"+view+".png"),Buffer.from(shot.data,"base64"));}
  }
 }
 console.log("CP175 real Chrome viewport contracts passed: six views at 390, 768, 1440 and 1920 pixels; no page overflow or clipped Simulate control.");
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);fs.rmSync(profile,{recursive:true,force:true});});
