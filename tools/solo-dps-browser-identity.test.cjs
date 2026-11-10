'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {assertSoloDpsIdentity}=require('./solo-dps-browser-identity.cjs');
const ui=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
const html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8');
const specMap=vm.runInNewContext('('+ui.match(/const specMap=(\{[^\n]+\});/)[1]+')');
const classSelect=html.match(/<select id="classKey">([\s\S]*?)<\/select>/)[1];
const labels=Object.fromEntries([...classSelect.matchAll(/<option value="([^"]+)">([^<]+)<\/option>/g)].map(([,key,label])=>[key,label]));
const updater=ui.slice(ui.indexOf('function updateIdentity(){'),ui.indexOf('const qualityClass='));
const human=ui.match(/const human=[^\n]+/)[0];
let checks=0;
(async()=>{
 for(const name of ['elemental','balance','shadow','enhancement','rogue']){
  const source=fs.readFileSync(`tools/solo-dps-${name}-browser.test.cjs`,'utf8');
  assert.ok(source.includes('require("./solo-dps-browser-identity.cjs")'),'Browser suite must use the tested shared helper');
  const from=source.indexOf('async function assertIdentity(){'),to=source.indexOf('async function downloaded(',from);
  assert.ok(from>=0&&to>from);const actualBrowserHelper=source.slice(from,to);
  for(const [classKey,specs] of Object.entries(specMap))for(const specialization of specs){
   const race=classKey==='druid'?'Night Elf':classKey==='priest'?'Human':'Orc';
   const elements={classKey:{value:classKey,selectedOptions:[{textContent:labels[classKey]}]},spec:{value:specialization,selectedOptions:[{textContent:specialization.replaceAll('-',' ')}]},race:{selectedOptions:[{textContent:race}]},characterTitle:{},characterSubtitle:{}};
   const browser={$:key=>elements[key],selectedRace:()=>({name:race}),document:{querySelector:selector=>elements[selector.slice(1)]}};
   vm.createContext(browser);vm.runInContext(human+'\n'+updater+'\nupdateIdentity();',browser);
   const test={assertSoloDpsIdentity,evaluate:async expression=>vm.runInContext(expression,browser)};
   vm.createContext(test);vm.runInContext(actualBrowserHelper,test);
   // The Enhancement suite additionally requires Enhancement specifically, as
   // its original helper did; the other suites also import other class fixtures.
   if((name==='enhancement'&&(classKey!=='shaman'||specialization!=='enhancement'))||(name==='rogue'&&(classKey!=='rogue'||specialization!=='assassination'))){
    await assert.rejects(()=>test.assertIdentity(),/requested class|requested specialization/);checks++;continue;
   }
   await test.assertIdentity();checks++;
   // Exercise the actual wrapper and query expression for every negative case.
   for(const mutate of [()=>elements.characterTitle.textContent+=' (provisional)',()=>elements.characterTitle.textContent='Enhancement Shaman trailing text',()=>elements.classKey.selectedOptions[0].textContent+=' unsupported',()=>elements.characterSubtitle.textContent='Wrong race · Level 60']){
    const snapshot=JSON.parse(JSON.stringify(elements));mutate();await assert.rejects(()=>test.assertIdentity());
    for(const key of Object.keys(elements))elements[key]=snapshot[key];checks++;
   }
   if(classKey==='shaman'){
    elements.classKey.selectedOptions[0].textContent='Shaman (Elemental, provisional)';await assert.rejects(()=>test.assertIdentity(),/source disclosure/);checks++;
    elements.classKey.selectedOptions[0].textContent=labels[classKey];
   }
   elements.spec.value='unsupported-spec';await assert.rejects(()=>test.assertIdentity(),['enhancement','rogue'].includes(name)?/requested specialization/:/supported Solo DPS identity/);checks++;
  }
 }
 console.log(`Actual Elemental/Balance/Shadow/Enhancement/Rogue browser identity helpers passed: ${checks} positive/negative assertions across every supported class/spec route, using production sidebar rendering and HTML disclosure labels.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
