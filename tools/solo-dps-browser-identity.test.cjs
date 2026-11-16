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
const plain=value=>JSON.parse(JSON.stringify(value));
// Use the real API inventory and its declared key ordering, not an invented
// single race that can never expose faction-specific selector aliases.
const raceSource=fs.readFileSync('WarcraftSim.Api/Simulation/ForeverPlayableRaceCatalog.cs','utf8');
const raceController=fs.readFileSync('WarcraftSim.Api/Controllers/ForeverCharacterController.cs','utf8');
assert.match(raceController,/races=source\.OrderBy\(x=>x\.Key\)\.ToArray\(\)/);
const races=[...raceSource.matchAll(/new\("([^"]+)","([^"]+)","([^"]+)",\[([^\]]+)\],SourceBuild,SourceUrl\)/g)].map(([,key,name,faction,classes])=>({key,name,faction,classes:JSON.parse('['+classes+']')})).sort((a,b)=>a.key<b.key?-1:a.key>b.key?1:0);
assert.equal(races.length,10,'Every production playable race is parsed');
const selectedRace=ui.match(/function selectedRace\(\)\{[^\n]+/)[0];

// Pin the admitted UI inventory independently, then execute the actual
// initializer and actual browser assertions. A stale expected option list must
// fail this pure gate before the expensive browser stage is reached.
const inventory={rogue:['assassination','combat'],priest:['shadow'],druid:['balance','feral'],shaman:['elemental','enhancement'],mage:['arcane','fire','frost'],warlock:['affliction','demonology','destruction'],hunter:['marksmanship','survival','beast-mastery'],warrior:['arms','fury','protection']};
const initClass=ui.slice(ui.indexOf('async function initClass(){'),ui.indexOf('async function loadPreset(token=generation){'));
assert.ok(initClass.startsWith('async function initClass(){')&&initClass.includes("$('spec').replaceChildren"));
async function renderSelector(classKey){
 const select=()=>({value:'stale-selection',options:[],get selectedOptions(){return this.options.filter(x=>x.value===this.value);},replaceChildren(...options){this.options=options;this.value=options[0]?.value||'';}});
 const elements={classKey:{value:classKey,selectedOptions:[{textContent:labels[classKey]}]},spec:select(),race:select(),run:{disabled:false},characterTitle:{},characterSubtitle:{},status:{dataset:{buildState:'input-required'}}},loaded=[];
 const browser={$:id=>elements[id],document:{querySelector:selector=>elements[selector.slice(1)]},specMap:plain(specMap),generation:0,statsRefreshId:0,baselineCheckId:0,baselineAvailable:true,request:{},loadedClass:'stale',loadedSpec:'stale',pendingClassCatalog:null,
  races:plain(races),option:(value,textContent)=>({value,textContent}),setBuildPreparing:()=>{},note:()=>{},resetSelections:()=>{},loadCatalog:async()=>{},loadPreset:async()=>loaded.push({classKey:elements.classKey.value,specialization:elements.spec.value}),updateIdentity:()=>{}};
 vm.createContext(browser);await vm.runInContext(human+'\n'+selectedRace+'\n'+updater+'\n'+initClass+';initClass();',browser);
 assert.deepEqual(elements.spec.options.map(x=>x.value),inventory[classKey],classKey+' exact production selector inventory');
 assert.deepEqual(loaded,[{classKey,specialization:inventory[classKey][0]}],classKey+' default selected before preset loading');
 return {browser,elements};
}
let checks=0,inventoryChecks=0,raceChecks=0;
(async()=>{
 assert.deepEqual(plain(specMap),inventory,'Every admitted class/spec must be explicitly covered');inventoryChecks++;
 for(const classKey of Object.keys(inventory)){await renderSelector(classKey);inventoryChecks+=2;}
 const classBySuite={elemental:'shaman',balance:'druid',shadow:'priest',rogue:'rogue'};
 for(const [name,classKey] of Object.entries(classBySuite)){
  const source=fs.readFileSync(`tools/solo-dps-${name}-browser.test.cjs`,'utf8');
  const lines=source.split('\n').filter(line=>line.includes("document.querySelector('#spec').options")&&line.includes('assert.deepEqual'));
  assert.equal(lines.length,1,name+' retains one exact browser inventory assertion');
  const statement=lines[0].slice(0,lines[0].indexOf(';')+1),{browser,elements}=await renderSelector(classKey);
  const check=()=>vm.runInNewContext('(async()=>{'+statement+'})()',{assert:{deepEqual:(a,b,...rest)=>assert.deepEqual(plain(a),plain(b),...rest)},evaluate:async expression=>vm.runInContext(expression,browser)});
  await check();inventoryChecks++;
  const original=elements.spec.options;
  const wrong=[original.slice(0,-1),[...original,{value:'unsupported-spec'}],[...original,original[0]],...(original.length>1?[[...original].reverse()]:[])];
  for(const options of wrong){elements.spec.options=options;await assert.rejects(check,undefined,name+' rejects missing, extra, duplicate or reordered inventory');inventoryChecks++;}
  elements.spec.options=original;
  if(classKey==='druid'){
   assert.equal(elements.spec.value,'balance','Class-only Druid remains Balance, not the newly admitted Feral');
   await assert.rejects(()=>vm.runInNewContext('(async()=>{'+statement.replace("['balance','feral']","['balance']")+'})()',{assert:{deepEqual:(a,b)=>assert.deepEqual(plain(a),plain(b))},evaluate:async expression=>vm.runInContext(expression,browser)}),undefined,'Regression oracle rejects the original obsolete Balance-only expectation');inventoryChecks+=2;
  }
 }
 // Execute every actual race option and sidebar renderer, including both
 // Skyborne factions. The unchanged strict shared oracle must agree exactly.
 for(const [classKey,specs] of Object.entries(inventory)){
  const {browser,elements}=await renderSelector(classKey),eligible=races.filter(r=>r.classes.includes(classKey));
  assert.deepEqual(elements.race.options.map(x=>x.value),eligible.map(r=>r.key));raceChecks++;
  assert.equal(elements.race.value,eligible[0].key);raceChecks++;
  for(const race of eligible)for(const specialization of specs){
   elements.spec.value=specialization;elements.race.value=race.key;vm.runInContext('updateIdentity();',browser);
   const identity={classKey,specialization,classLabel:elements.classKey.selectedOptions[0].textContent,race:elements.race.selectedOptions[0].textContent,displayedTitle:elements.characterTitle.textContent,subtitle:elements.characterSubtitle.textContent};
   assertSoloDpsIdentity(identity);assert.equal(identity.subtitle,identity.race+' · Level 60');raceChecks+=2;
   if(race.key.endsWith('skyborne')){
    const label=race.faction==='alliance'?'Skyborne (Alliance)':'Skyborne (Horde)';assert.equal(identity.race,label);
    assert.throws(()=>assertSoloDpsIdentity({...identity,subtitle:race.name+' · Level 60'}),/Sidebar race/,'Old canonical-name subtitle fails unchanged strict identity');
    assert.throws(()=>assertSoloDpsIdentity({...identity,subtitle:(race.faction==='alliance'?'Skyborne (Horde)':'Skyborne (Alliance)')+' · Level 60'}),/Sidebar race/,'Opposite faction cannot satisfy identity');raceChecks+=3;
   }
  }
 }
 // Run the actual Feral fixture setup helper against the actual initializer's
 // DOM-shaped selectors. This pure model neither dispatches UI events nor
 // asserts browser acceptance; the Actions helper still uses native input.
 const feralSource=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');
 const setup=feralSource.slice(feralSource.indexOf('async function selectFeralFixtureRace(){'),feralSource.indexOf('async function assertIdentity(){'));
 assert.ok(setup.startsWith('async function selectFeralFixtureRace(){'));
 const {browser:fixtureBrowser,elements:fixtureElements}=await renderSelector('druid');
 fixtureElements.spec.value='feral';vm.runInContext('updateIdentity();',fixtureBrowser);
 assert.equal(fixtureElements.race.value,'high-order-skyborne');assert.equal(fixtureElements.characterSubtitle.textContent,'Skyborne (Alliance) · Level 60');raceChecks+=2;
 const chosen=[];let identities=0,blocked=0;
 await vm.runInNewContext(setup+';selectFeralFixtureRace();',{
  setInput:async(selector,value)=>{assert.equal(selector,'#race');assert.ok(fixtureElements.race.options.some(x=>x.value===value));chosen.push([selector,value]);fixtureElements.race.value=value;vm.runInContext('updateIdentity();',fixtureBrowser);},
  until:async expression=>assert.equal(vm.runInContext(expression,fixtureBrowser),true,'Fixture readiness uses actual current DOM identity, not assumed preparation'),
  assertIdentity:async()=>{identities++;assertSoloDpsIdentity({classKey:'druid',specialization:'feral',classLabel:labels.druid,race:fixtureElements.race.selectedOptions[0].textContent,displayedTitle:fixtureElements.characterTitle.textContent,subtitle:fixtureElements.characterSubtitle.textContent},{classKey:'druid',specialization:'feral'});},
  assertBlockedEditable:async()=>{blocked++;assert.equal(fixtureElements.status.dataset.buildState,'input-required');}
 });
 assert.deepEqual(chosen,[['#race','night-elf']]);assert.equal(identities,1);assert.equal(blocked,1);assert.equal(fixtureElements.race.value,'night-elf');raceChecks+=4;
 // A class reset still resets race; fixture import must restore its saved key.
 await vm.runInContext('initClass();',fixtureBrowser);assert.equal(fixtureElements.race.value,'high-order-skyborne');raceChecks++;
 for(const name of ['elemental','balance','shadow','enhancement','rogue','feral']){
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
   if((name==='enhancement'&&(classKey!=='shaman'||specialization!=='enhancement'))||(name==='rogue'&&(classKey!=='rogue'||specialization!=='assassination'))||(name==='feral'&&(classKey!=='druid'||specialization!=='feral'))){
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
   elements.spec.value='unsupported-spec';await assert.rejects(()=>test.assertIdentity(),['enhancement','rogue','feral'].includes(name)?/requested specialization/:/supported Solo DPS identity/);checks++;
  }
 }
 console.log(`Actual Elemental/Balance/Shadow/Enhancement/Rogue/Feral browser identity helpers passed: ${checks} positive/negative assertions across every supported class/spec route, using production sidebar rendering and HTML disclosure labels.`);
 console.log(`Actual production race inventory, sidebar and explicit Feral setup passed: ${raceChecks} checks across all eligible class/spec/race combinations. No browser or API executed.`);
 console.log(`Actual production selectors and exact browser inventory assertions passed: ${inventoryChecks} checks across eight classes, including default Balance plus admitted Feral.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
