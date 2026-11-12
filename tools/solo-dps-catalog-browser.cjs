'use strict';
// Focused extension of the existing real-Chromium suite. No new browser,
// dependency, server, workflow, or synthetic simulation response.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const groupSource='buff:cp173-shared-elixir-critical-chance';
const critKeys=['attack-critical-chance-percent','spell-critical-chance-percent'];
const elixirs=['elixir-of-the-mongoose','elixir-of-the-grizzly','elixir-of-the-owl'];
function assertContributions(rows,food=false){
 assert.ok(Array.isArray(rows),'Actual contribution rows required');
 const shared=rows.filter(x=>x.sourceKey===groupSource);
 assert.equal(shared.length,2,'Exactly one positive shared contribution per crit channel');
 for(const key of critKeys){
  assert.equal(shared.filter(x=>x.statKey===key).length,1);
  assert.equal(shared.find(x=>x.statKey===key).amount,2);
  const itemCrit=rows.filter(x=>elixirs.some(k=>x.sourceKey==='buff:cp173-'+k)&&x.statKey===key);
  assert.equal(itemCrit.length,0,'No duplicated per-elixir crit or negative compensation');
  const squid=rows.filter(x=>x.sourceKey==='buff:cp173-grilled-squid'&&x.statKey===key);
  assert.equal(squid.length,food?1:0);
  if(food)assert.equal(squid[0].amount,1,'Food remains independently additive');
 }
 for(const [key,stat] of [['elixir-of-the-mongoose','agility'],['elixir-of-the-grizzly','strength'],['elixir-of-the-owl','intellect']]){
  const own=rows.filter(x=>x.sourceKey==='buff:cp173-'+key);
  assert.equal(own.length,1,'Per-item contribution contains only the independent attribute');
  assert.equal(own[0].statKey,stat);assert.equal(own[0].amount,25);
 }
}
async function run({evalJs,poll,click,command,importBuild,readDownloadedJson,reportDir,screenshotDir,fixture}){
 oracleSelfTest();
 const selector=key=>'[data-modifier-key="'+key+'"]';
 const selected=key=>evalJs(`document.querySelector(${JSON.stringify(selector(key))})?.getAttribute('aria-pressed')`);
 // Observe real requests/responses without replacing data or application logic.
 // Status text alone can still describe a previous, now stale preview.
 await evalJs(`(()=>{const original=window.fetch;window.__catalogPreview={started:0,receipts:[]};
  window.fetch=async function(input,init){const isPreview=String(input).endsWith('/api/forever/character/generate-level60');
   const sequence=isPreview?++window.__catalogPreview.started:0;
   const request=isPreview?JSON.parse(init.body):null;
   const response=await original.apply(this,arguments);
   if(isPreview&&response.ok){const data=await response.clone().json();window.__catalogPreview.receipts.push({sequence,request,data});}
   return response;
  };})()`);
 const mark=()=>evalJs('window.__catalogPreview.started');
 const ready=(after=0)=>poll(`(()=>{
  if(!document.querySelector('#statStatus').textContent.includes('Up to date'))return false;
  const selected=[...document.querySelectorAll('[data-modifier-key][aria-pressed="true"]')].map(e=>e.dataset.modifierKey).sort();
  const receipt=window.__catalogPreview.receipts.findLast(x=>x.sequence>${after}&&
   JSON.stringify(['buffs','consumables','targetDebuffs'].flatMap(k=>x.request.modifiers[k]||[]).sort())===JSON.stringify(selected));
  try{return !!receipt&&JSON.stringify(receipt.data.contributions)===JSON.stringify(JSON.parse(document.querySelector('#contributions').textContent));}catch{return false;}
 })()`,30000);
 async function toggle(key,value){const before=await mark();await click(selector(key));assert.equal(await selected(key),String(value));await ready(before);}
 async function rows(){return evalJs("JSON.parse(document.querySelector('#contributions').textContent)");}
 async function exportJson(button,basename,name){
  const source=path.join(reportDir,basename),destination=path.join(reportDir,name);
  assert.ok(!fs.existsSync(source),'No stale download may satisfy catalog gate');
  assert.ok(!fs.existsSync(destination),'Preserve earlier catalog evidence');
  await click(button);const data=await readDownloadedJson(source);fs.renameSync(source,destination);return data;
 }
 const build=JSON.parse(JSON.stringify(fixture));
 build.modifiers={buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'initial-only'};
 const baseMark=await mark();
 await importBuild(build,'cp175-catalog-base.json');
 await poll("document.querySelector('#status').textContent==='Build imported.' && document.querySelector('#classKey').value==='mage'",35000);
 await click('[data-view=effects]');await ready(baseMark);
 await toggle(elixirs[0],true);await toggle('elixir-of-ferocity',true);
 await click(selector(elixirs[1]));
 assert.equal(await selected(elixirs[1]),'false');
 assert.match(await evalJs("document.querySelector('#status').textContent"),/Cannot stack.*elixir-strength/);
 assert.equal(await selected('elixir-of-ferocity'),'true','Rejected selection preserves Ferocity');
 await toggle('elixir-of-ferocity',false);await toggle(elixirs[1],true);
 await click(selector('elixir-of-ferocity'));
 assert.equal(await selected('elixir-of-ferocity'),'false');
 assert.match(await evalJs("document.querySelector('#status').textContent"),/Cannot stack.*elixir-strength/);
 assert.equal(await selected(elixirs[1]),'true','Reverse rejection preserves Grizzly');
 assert.equal(await selected(elixirs[0]),'true','Family rejection preserves independent Mongoose');
 await toggle(elixirs[2],true);
 assertContributions(await rows());
 for(const key of elixirs){
  const tooltip=await evalJs(`document.querySelector(${JSON.stringify(selector(key))}).title`);
  assert.match(tooltip,/independent/i);assert.match(tooltip,/shared/i);assert.match(tooltip,/once/i);
 }
 await toggle('grilled-squid',true);assertContributions(await rows(),true);
 // Verify the actual new modifier JPEGs decoded in the rendered browser.
 const modifierArt=[['elixir-of-the-grizzly','inv_potion_160'],['elixir-of-the-owl','inv_potion_164'],['elixir-of-ferocity','inv_potion_161'],['elixir-of-greater-intellect','inv_potion_10']];
 for(const [key,icon] of modifierArt){
  await poll(`(()=>{const e=document.querySelector(${JSON.stringify(selector(key)+' img.effect-art')});return e?.complete&&e.naturalWidth>0&&e.getAttribute('src')===${JSON.stringify('/forever-icons/'+icon+'.jpg')};})()`);
 }
 assert.equal(await evalJs("document.querySelector('[data-modifier-key=elixir-of-the-phalanx] img.effect-art')===null"),true,'Unresolved artwork retains neutral symbol');
 const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
 fs.writeFileSync(path.join(screenshotDir,'cp175-catalog-shared-elixirs.png'),Buffer.from(shot.data,'base64'));
 await command('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 try{
  await evalJs("(()=>{document.querySelector('#contributions').closest('details').open=true;document.querySelector('#contributions').scrollIntoView({block:'center'});})()");
  await evalJs("new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))",true);
  assert.equal(await evalJs('document.documentElement.scrollWidth<=window.innerWidth'),true,'Shared contribution labels must not widen the mobile page');
  const mobileShot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  fs.writeFileSync(path.join(screenshotDir,'cp175-catalog-shared-elixirs-mobile.png'),Buffer.from(mobileShot.data,'base64'));
 }finally{await command('Emulation.clearDeviceMetricsOverride');await evalJs("document.querySelector('#contributions').closest('details').open=false");}

 const exported=await exportJson('#exportBuild','warcraftsim-build.json','cp175-catalog-shared-build.json');
 assert.deepEqual([...exported.modifiers.consumables].sort(),[...elixirs,'grilled-squid'].sort());
 await toggle(elixirs[2],false);
 const roundtripMark=await mark();
 await importBuild(exported,'cp175-catalog-roundtrip.json');
 await poll("document.querySelector('#status').textContent==='Build imported.'",35000);await ready(roundtripMark);
 assertContributions(await rows(),true);
 const invalid={...exported,version:999};
 await importBuild(invalid,'cp175-catalog-rejected-version.json');
 await poll("document.querySelector('#file').value==='' && document.querySelector('#status').textContent!=='Build imported.'");
 for(const key of [...elixirs,'grilled-squid'])assert.equal(await selected(key),'true','Failed import preserves selection '+key);
 assertContributions(await rows(),true);
 const afterFailure=await exportJson('#exportBuild','warcraftsim-build.json','cp175-catalog-after-rejection-build.json');
 assert.deepEqual(afterFailure,exported,'Failed import leaves the entire exported build unchanged');
 const duplicate=JSON.parse(JSON.stringify(exported));
 duplicate.modifiers.consumables.push(' ELIXIR-OF-THE-MONGOOSE ');
 await importBuild(duplicate,'cp175-catalog-rejected-duplicate.json');
 await poll("document.querySelector('#file').value==='' && document.querySelector('#status').textContent.includes('Duplicate modifier')");
 await ready();assertContributions(await rows(),true);
 for(const key of [...elixirs,'grilled-squid'])assert.equal(await selected(key),'true','Duplicate import preserves selection '+key);
 const afterDuplicate=await exportJson('#exportBuild','warcraftsim-build.json','cp175-catalog-after-duplicate-build.json');
 assert.deepEqual(afterDuplicate,exported,'Duplicate import leaves the entire exported build unchanged');

 await click('#run');
 await poll("!document.querySelector('#exportResults').disabled && document.querySelector('#status').textContent.includes('Simulation completed')",45000);
 const report=await exportJson('#exportResults','warcraftsim-simulation-report.json','cp175-catalog-shared-mage-report.json');
 assert.equal(report.originalSimulationRequest.classKey,'mage');
 assert.deepEqual([...report.build.modifiers.consumables].sort(),[...elixirs,'grilled-squid'].sort());
 assertContributions(report.resolvedCharacter.contributions,true);
 await click('#replayResult');
 await poll("document.querySelector('#status').textContent.includes('Deterministic replay matched')",45000);
 // Inspect only: artwork never equips an item or promotes its eligibility.
 await click('[data-view=equipment]');
 const equipmentArt=[['ranged',11287,'inv_staff_02'],['chest',14152,'inv_chest_cloth_38'],['chest',19682,'inv_chest_cloth_07'],['legs',19683,'inv_pants_cloth_14'],['feet',19684,'inv_boots_cloth_02']];
 for(const [slot,id,icon] of equipmentArt){
  await click('[data-slot="'+slot+'"]');
  if(!await evalJs("document.querySelector('#showCatalogAll').checked"))await click('#showCatalogAll');
  await evalJs(`(()=>{const e=document.querySelector('#itemSearch');e.value=${JSON.stringify(String(id))};e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  await poll(`(()=>{const e=document.querySelector('[data-item-id="${id}"] img.item-art');return e?.complete&&e.naturalWidth>0&&e.getAttribute('src')===${JSON.stringify('/forever-icons/'+icon+'.jpg')};})()`);
  const equipmentShot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  fs.writeFileSync(path.join(screenshotDir,'cp175-catalog-item-'+id+'.png'),Buffer.from(equipmentShot.data,'base64'));
  await click('#closePicker');
 }
 fs.writeFileSync(path.join(reportDir,'cp175-catalog-browser-acceptance.json'),JSON.stringify({status:'passed',sharedSource:groupSource,elixirs,food:'grilled-squid',modifierArt,equipmentArt,report:'cp175-catalog-shared-mage-report.json',replay:'matched',failedImport:'full-build-unchanged',duplicateImport:'full-build-unchanged',mobileWidth:390},null,2)+'\n');
 console.log('CP175 catalog browser acceptance passed: shared contribution, additive food, family selection, roundtrip, rollback, report/replay and nine decoded icons.');
}
function oracleSelfTest(){
 const rows=[...critKeys.map(statKey=>({sourceKey:groupSource,statKey,amount:2})),
  ...[['elixir-of-the-mongoose','agility'],['elixir-of-the-grizzly','strength'],['elixir-of-the-owl','intellect']].map(([key,statKey])=>({sourceKey:'buff:cp173-'+key,statKey,amount:25}))];
 assertContributions(rows);
 const food=critKeys.map(statKey=>({sourceKey:'buff:cp173-grilled-squid',statKey,amount:1}));
 assertContributions([...rows,...food],true);
 for(const bad of [rows.slice(1),[...rows,rows[0]],rows.map(x=>x.statKey===critKeys[0]?{...x,amount:4}:x),
  [...rows,{sourceKey:'buff:cp173-elixir-of-the-mongoose',statKey:critKeys[0],amount:2}],
  rows.map(x=>x.statKey==='agility'?{...x,amount:24}:x)])assert.throws(()=>assertContributions(bad));
 assert.throws(()=>assertContributions([...rows,food[0]],true));
 assert.throws(()=>assertContributions([...rows,...food.map(x=>({...x,amount:2}))],true));
 // Execute the actual production formatter, not a copied implementation.
 const ui=fs.readFileSync(path.join(__dirname,'../WarcraftSim.Api/wwwroot/solo-dps.js'),'utf8');
 const start=ui.indexOf('function modifierTooltip(m){'),end=ui.indexOf('function modifierTile(',start);
 assert.ok(start>=0&&end>start);
 const tooltip=require('node:vm').runInNewContext(ui.slice(start,end)+';modifierTooltip;', {human:value=>value});
 const text=tooltip({name:'Elixir of the Owl',sharedStatGroup:'forever-elixir-critical-chance',
  independentStats:{intellect:25},sharedStats:Object.fromEntries(critKeys.map(k=>[k,2])),
  stats:{intellect:25,...Object.fromEntries(critKeys.map(k=>[k,2]))}});
 assert.match(text,/Independent: intellect: 25/);assert.match(text,/Shared.*counted once/);
 assert.equal(text.split('spell-critical-chance-percent: 2').length-1,1,'No misleading duplicate standalone total');
 assert.match(tooltip({name:'Squid',stats:Object.fromEntries(critKeys.map(k=>[k,1]))}),/spell-critical-chance-percent: 1/);
 const duplicateStart=ui.indexOf('function validateImportedModifierDuplicates(data){'),duplicateEnd=ui.indexOf('const beforePreparedImport=',duplicateStart);
 assert.ok(duplicateStart>=0&&duplicateEnd>duplicateStart);
 const guard=require('node:vm').runInNewContext(ui.slice(duplicateStart,duplicateEnd)+';validateImportedModifierDuplicates;');
 guard({modifiers:{buffs:['arcane-intellect'],consumables:elixirs,targetDebuffs:[]}});
 guard({modifiers:{consumables:[]}});guard({});
 for(const modifiers of [{consumables:['elixir-of-the-mongoose','elixir-of-the-mongoose']},
  {consumables:[' ELIXIR-OF-THE-MONGOOSE ','elixir-of-the-mongoose']},
  {buffs:['elixir-of-the-mongoose'],consumables:['elixir-of-the-mongoose']}])assert.throws(()=>guard({modifiers}),/Duplicate modifier/);
 for(const consumables of [null,'elixir-of-the-mongoose',{},[null],[1],[''],['  ']])assert.throws(()=>guard({modifiers:{consumables}}));
 return 26;
}
module.exports={assertContributions,run,oracleSelfTest};
if(require.main===module)console.log('Catalog browser contribution oracle: '+oracleSelfTest()+' positive/mutation checks passed.');
