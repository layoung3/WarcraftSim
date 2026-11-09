"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path"),os=require("node:os");
const {spawn}=require("node:child_process");
const profile=fs.mkdtempSync(path.join(os.tmpdir(),"cp175-hunter-preview-"));
const port=19349;
const proc=spawn(process.env.CP174_CHROME||"/usr/bin/google-chrome",[
 "--headless","--no-sandbox","--disable-gpu","--remote-debugging-port="+port,
 "--user-data-dir="+profile,"about:blank"],{stdio:"ignore"});
const sleep=ms=>new Promise(r=>setTimeout(r,ms));let ws,id=0,pending=new Map();
function command(method,params={}){return new Promise((resolve,reject)=>{
 const key=++id,t=setTimeout(()=>{pending.delete(key);reject(Error(method+" timed out"));},15000);
 pending.set(key,{resolve:value=>{clearTimeout(t);resolve(value);},reject});
 ws.send(JSON.stringify({id:key,method,params}));
});}
async function evaluate(expression){const r=await command("Runtime.evaluate",{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.text+JSON.stringify(r.exceptionDetails));return r.result.value;}
async function until(expression,message){for(let i=0;i<200;i++){try{if(await evaluate(expression))return;}catch{}await sleep(100);}throw Error(message);}
const stats="Object.fromEntries([...document.querySelectorAll('#stats>div')].map(r=>[r.querySelector('span').textContent,Number(r.querySelector('strong').textContent)]))";
const node="document.querySelector('[data-talent-key=\"lightning-reflexes\"]')";
function close(a,b,message){assert.ok(Math.abs(a-b)<1e-8,message+": "+a+" vs "+b);}
async function main(){
 let tabs;for(let i=0;i<100;i++){try{tabs=await(await fetch("http://127.0.0.1:"+port+"/json")).json();if(tabs.some(t=>t.type==="page"))break;}catch{}await sleep(100);}
 ws=new WebSocket(tabs.find(t=>t.type==="page").webSocketDebuggerUrl);await new Promise(r=>ws.addEventListener("open",r,{once:true}));
 ws.addEventListener("message",e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(p)m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}});
 await command("Page.enable");await command("Runtime.enable");
 await command("Emulation.setDeviceMetricsOverride",{width:1440,height:900,deviceScaleFactor:1,mobile:false});
 await command("Page.navigate",{url:(process.env.CP174_BASE_URL||"http://127.0.0.1:5142")+"/solo-dps.html?view=talents"});
 await until("document.querySelector('#status')?.textContent.includes('Class defaults loaded')","Initial UI did not load");
 await evaluate("document.querySelector('#classKey').value='hunter';document.querySelector('#classKey').dispatchEvent(new Event('change',{bubbles:true}))");
 await until("document.querySelector('#classKey').value==='hunter'&&!document.querySelector('#run').disabled&&document.querySelector('#status').textContent.includes('Class defaults loaded')","Hunter did not load");
 await evaluate("document.querySelector('#spec').value='survival';document.querySelector('#spec').dispatchEvent(new Event('change',{bubbles:true}))");
 await until("document.querySelector('#spec').value==='survival'&&!document.querySelector('#run').disabled&&document.querySelector('#status').textContent.includes('Class defaults loaded')","Survival did not load");
 await evaluate("document.querySelector('#provisional').checked=true;document.querySelector('#provisional').dispatchEvent(new Event('change',{bubbles:true}))");
 await until("document.querySelector('#statStatus').textContent.includes('Up to date')&&!document.querySelector('#statScope').hidden&&("+stats+")['Agility']>0","Scoped Hunter preview did not load");
 assert.ok(await evaluate(node+".getAttribute('aria-label').includes('rank 5 of 5')"),"Starting fixture must learn Lightning Reflexes rank 5");
 const ranked=await evaluate(stats);
 for(let rank=4;rank>=0;rank--){
  await evaluate(node+".dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
  await until(node+".getAttribute('aria-label').includes('rank "+rank+" of 5')","Rank removal failed at "+rank);
 }
 await until("document.querySelector('#statStatus').textContent.includes('Up to date')&&Math.abs(("+stats+")['Agility']-"+(ranked.Agility/1.15)+")<1e-8","Rank removal did not update displayed Agility");
 const removed=await evaluate(stats),extra=removed.Agility*.15;
 close(ranked.Agility-removed.Agility,extra,"Agility talent increment");
 close(ranked['Attack Power']-removed['Attack Power'],extra,"Melee AP talent increment");
 close(ranked['Ranged Attack Power']-removed['Ranged Attack Power'],2*extra,"Ranged AP talent increment");
 close(ranked['Attack Critical Chance Percent']-removed['Attack Critical Chance Percent'],extra/52.9,"Crit talent increment");
 close(ranked.Armor-removed.Armor,2*extra,"Armor talent increment");
 for(let rank=1;rank<=5;rank++){
  await evaluate(node+".click()");
  await until(node+".getAttribute('aria-label').includes('rank "+rank+" of 5')","Rank addition failed at "+rank);
 }
 await until("document.querySelector('#statStatus').textContent.includes('Up to date')&&Math.abs(("+stats+")['Agility']-"+ranked.Agility+")<1e-8","Relearning did not restore displayed Agility");
 assert.deepEqual(await evaluate(stats),ranked,"Remove/relearn must restore every displayed stat without compounding");
 const scope=await evaluate("document.querySelector('#statScope').textContent");
 assert.ok(scope.includes('Lightning Reflexes')&&scope.includes('Lethal Attacks')&&scope.includes('Surefooted')&&scope.includes('Other talent effects')&&scope.includes('companion stats'),scope);
 assert.ok(await evaluate("document.querySelector('#contributions').textContent.includes('previewAdjustments')"),"Talent preview adjustments must be attributed");
 if(process.env.CP174_SCREENSHOT_DIR){fs.mkdirSync(process.env.CP174_SCREENSHOT_DIR,{recursive:true});const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(process.env.CP174_SCREENSHOT_DIR,'cp175-hunter-preview-talents.png'),Buffer.from(shot.data,'base64'));}
 console.log("CP175 Hunter sidebar preview passed: real rank5→0→5 interactions update Agility/AP/crit/armor and reverse exactly, with visible scope. "+JSON.stringify({ranked,removed,scope}));
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{try{ws?.close();}catch{}proc.kill();await sleep(300);fs.rmSync(profile,{recursive:true,force:true});});
