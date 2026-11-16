'use strict';
const assert=require('node:assert/strict');
// Observation only: delays preserve the actual status, body and native response.
function installObserver(){
 window.__combatHttp=[];window.__combatSequence=0;window.__combatCompleted=[];window.__combatInputs=[];
 const nativeFetch=window.fetch;
 window.fetch=async(...args)=>{
  const sequence=++window.__combatSequence,url=String(args[0]);
  if(url==='/api/forever/character/preview-rogue')window.__latestCombatPreview=sequence;
  const candidate=window.__combatHold;
  const hold=candidate?.armed&&(candidate.prefix?url.startsWith(candidate.url):url===candidate.url)?candidate:null;
  if(hold){hold.armed=false;hold.sequence=sequence;}
  const receipt={sequence,url,request:args[1]?.body?JSON.parse(args[1].body):null,requestedAt:performance.now()};
  const response=await nativeFetch(...args);receipt.status=response.status;
  try{receipt.response=await response.clone().json();}catch{}
  receipt.receivedAt=performance.now();window.__combatHttp.push(receipt);
  if(hold){hold.held=true;await new Promise(resolve=>hold.release=resolve);hold.released=true;}
  receipt.deliveredAt=performance.now();return response;
 };
 document.addEventListener('warcraftsim:simulation-complete',event=>window.__combatCompleted.push(JSON.parse(JSON.stringify(event.detail))));
 for(const type of ['mousedown','mouseup','click','contextmenu','keydown','keyup','input','change'])document.addEventListener(type,event=>{
  window.__combatInputs.push({type,trusted:event.isTrusted,id:event.target?.id,value:event.target?.value,checked:event.target?.checked,key:event.key,at:performance.now()});
 },true);
}
// Independent port of rogue-combat-historical-http.py. JS parsed-number
// comparison is not exact-Decimal acceptance; the Python gate remains required.
function canonical(value){
 const guid=/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i,zero='00000000-0000-0000-0000-000000000000';
 const identities=new Map(),literals=new Set();let next=0;
 const generated=path=>{const p=path[0]==='sim'?path.slice(1):path;return p.length===4&&p[0]==='run'&&p[1]==='timeline'&&Number.isInteger(p[2])&&['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'].includes(p[3]);};
 function collect(x,path=[]){if(Array.isArray(x)){x.forEach((v,i)=>collect(v,[...path,i]));return;}if(x&&typeof x==='object'){for(const [k,v]of Object.entries(x)){if(guid.test(k))literals.add(k);collect(v,[...path,k]);}return;}if(typeof x==='string'&&guid.test(x)&&(!generated(path)||x===zero))literals.add(x);}
 function representative(x){if(!identities.has(x)){let name;do{name='00000000-0000-4000-8000-'+(next++).toString(16).padStart(12,'0');}while(literals.has(name));identities.set(x,name);}return identities.get(x);}
 function visit(x,path=[]){if(Array.isArray(x))return x.map((v,i)=>visit(v,[...path,i]));if(x&&typeof x==='object')return Object.fromEntries(Object.keys(x).sort().map(k=>[k,visit(x[k],[...path,k])]));if(typeof x==='number')assert.ok(Number.isFinite(x),'Non-finite number cannot enter replay');if(typeof x==='string'&&guid.test(x)&&x!==zero&&generated(path)&&!literals.has(x))return representative(x);return x;}
 collect(value);return JSON.stringify(visit(value));
}
function assertRejectedReportState(before,after,status,label){assert.match(status,/^Report import rejected:/,label+' explicit rejection');assert.deepEqual(after,before,label+' rejected candidate must preserve both slots and their rendered analysis');}
const ranks={'improved-eviscerate':3,'lightning-reflexes':5,'puncturing-wounds':3,precision:3,endurance:2,'flawless-execution':1,'dual-wield-specialization':5,'blade-flurry':1,'hack-and-slash':5,'weapon-expertise':2,'adrenaline-rush':1};
const rejectionCases=[
 ['unknown-field',x=>x.unknown=true],['missing-talents',x=>delete x.talents],['unknown-zero-key',x=>x.talents.ranks.malice=0],
 ['precision-four',x=>{x.talents.ranks.precision=4;x.talents.combatPointsSpent=32;}],
 ['precision-five',x=>{x.talents.ranks.precision=5;x.talents.combatPointsSpent=33;}],
 ['declared-total',x=>x.talents.combatPointsSpent=30],['missing-tree-total',x=>delete x.talents.assassinationPointsSpent],
 ['parent-precision',x=>{x.talents.ranks.precision=2;x.talents.combatPointsSpent=30;}],
 ['parent-blade-flurry',x=>{x.talents.ranks['blade-flurry']=0;x.talents.combatPointsSpent=30;}],
 ['tier-gate',x=>{x.talents.ranks={'adrenaline-rush':1};x.talents.combatPointsSpent=1;}],
 ['missing-opt-in',x=>delete x.provisional],['missing-race',x=>delete x.race],
 ['invalid-seed',x=>x.encounter.seed='not-a-seed'],['blank-duration',x=>x.encounter.duration=''],['batch',x=>x.encounter.iterations=2],
 ['dagger-flag',x=>x.encounter.rogue.bothWeaponsDaggers=true],['main-dagger',x=>x.encounter.rogue.mainHandWeaponType='dagger'],['off-axe',x=>x.encounter.rogue.offHandWeaponType='axe'],
 ['adds',x=>x.encounter.rogue.additionalTargets=1],['incoming',x=>x.encounter.rogue.enableBossAttacks=true],['utility',x=>x.encounter.rogue.useUtilityAbilities=true],
 ['custom-priority',x=>x.encounter.customPriority=['sinister-strike']],['script',x=>x.encounter.soloScript.enabled=true],
 ['item',x=>x.gear.items.push({slotKey:'main-hand',itemId:19102})],['enchant',x=>x.gear.items.push({slotKey:'main-hand',enchantId:1})],
 ['modifier',x=>x.modifiers.buffs.push('arcane-intellect')],['inventory',x=>x.preparedCombatConsumables.push({key:'majorManaPotionCharges',charges:1})]
].map(([name,mutate])=>({name,mutate}));
const gateNames=['unavailable-editable','flat-preparation-bound','blank-versus-zero','manual-controls','legal31-30-clear-reset','illegal-native-talents','preview-no-run','real-run-lock','full-report-reconciliation','complete-response-replay','build-roundtrip','strict-import-rejections','same-file-retry','invalid-raw-rollback','cross-identity-rollback','dirty-cancel-escape-confirm','save-load','stale-success','stale-failure','stale-validation','stale-preset','stale-baseline','matched31-30-comparison','tuning-choice-comparison','scenario-mismatch','reject-before-slot-overwrite','timing-source-mismatch','desktop-coverage','mobile-coverage'];
module.exports={installObserver,canonical,ranks,rejectionCases,gateNames,assertRejectedReportState};
// A <pre> has its own scrollbar; document-region screenshots cannot prove that
// every configured entry was visible. Scroll only, never resize/reveal content.
async function frameScrollable(selector,scrollTop=null){
 const element=document.querySelector(selector);if(!element||!element.getClientRects().length)throw Error('Missing/hidden scroll capture: '+selector);
 const header=document.querySelector('.topbar');if(!header)throw Error('Missing sticky header');
 if(scrollTop!==null){element.scrollTop=scrollTop;element.scrollIntoView({block:'center',behavior:'instant'});const top=header.getBoundingClientRect().bottom;const rect=element.getBoundingClientRect();if(rect.top<top+16)window.scrollBy(0,rect.top-top-16);}
 function measure(){const r=element.getBoundingClientRect(),top=header.getBoundingClientRect().bottom;return {left:r.left,right:r.right,top:r.top,bottom:r.bottom,scrollTop:element.scrollTop,scrollLeft:element.scrollLeft,height:element.clientHeight,total:element.scrollHeight,width:element.clientWidth,scrollWidth:element.scrollWidth,viewportWidth:innerWidth,viewportHeight:innerHeight,headerBottom:top,text:element.textContent};}
 let prior='',stable=0;for(let i=0;i<60;i++){await new Promise(resolve=>requestAnimationFrame(resolve));const r=measure(),signature=JSON.stringify(r);stable=signature===prior?stable+1:0;prior=signature;if(stable>=2){if(r.left< -1||r.right>r.viewportWidth+1||r.top<r.headerBottom+15||r.bottom>r.viewportHeight-15||r.height<=0||r.width<=0||r.scrollWidth>r.width+1)throw Error('Scroll region clipped/offscreen: '+selector);return r;}}
 throw Error('Scroll capture did not settle: '+selector);
}
module.exports.frameScrollable=frameScrollable;

const combatCoveragePrefix='Historical provisional partial Combat starting preset (0/31/0; 31 allocated, 20 unspent), eleven reconciled keys only; no automatic 51-point filler. Lightning Reflexes, Puncturing Wounds and Endurance inactive in fixed-sword Combat; Blade Flurry cleave inactive with one target.';
const combatScopeParagraphs=[
 'Combat is partial historical-provisional 0/31/0: 31 allocated, 20 unspent; 0/30/0 without Adrenaline Rush has 21 unspent. No automatic 51-point filler. Precision admits only ranks 0–3; the unchanged legacy direct cap is 5. DWS requires Precision 3; Weapon Expertise requires Blade Flurry 1 under the local full-parent convention.',
 'Lightning Reflexes, Puncturing Wounds and Endurance are inactive here. Endurance source 30/60% differs from legacy 10/20%, inaccessible without utility. Blade Flurry cleave is inactive with one target. Source and mechanical limitations remain historical/provisional.'
];
function expectedCombatTalentCoverage(catalog){
 combatTalentDetailRows(catalog);
 const inactive=['lightning-reflexes','puncturing-wounds','endurance'];
 for(const talent of catalog){
  assert.equal(talent.simulationStatus,inactive.includes(talent.key)?'inactive-in-fixed-sword-combat':'partial','Actual catalog status for '+talent.key);
  assert.equal(talent.historicalValidationSourceStatus,'historical-provisional-not-current-client-certified');
  assert.equal(talent.historicalValidationSourceCommit,'1cc5ae1e9abdbf941b7326d2d90421822dce31ba');
 }
 const admitted=catalog.filter(t=>t.historicalValidationSourceStatus);
 return combatCoveragePrefix+' '+['assassination','combat','subtlety'].map(tree=>tree[0].toUpperCase()+tree.slice(1)+': '+admitted.filter(t=>t.tree===tree).length+' historically validated runner keys, '+catalog.filter(t=>t.tree===tree&&t.modeled===false).length+' historical-only visual keys').join(' | ')+'. Source '+[...new Set(admitted.map(t=>t.historicalValidationSourceCommit).filter(Boolean))].join(', ')+'. Not current-client certification; rank tooltips and complete mechanical coverage remain unverified.';
}
// Observation only, after native navigation to Talents. A hidden node's
// textContent cannot satisfy the visible-copy contract.
function readCombatTalentCoverage(){
 const required=selector=>{const node=document.querySelector(selector);if(!node)throw Error('Missing Combat allocation disclosure: '+selector);return node;};
 const visible=e=>{if(!e.getClientRects().length)return false;for(let n=e;n;n=n.parentElement){const style=getComputedStyle(n);if(n.hidden||style.display==='none'||['hidden','collapse'].includes(style.visibility))return false;}return true;};
 const read=e=>({text:e.textContent,visible:visible(e)});
 return {classKey:required('#classKey').value,specialization:required('#spec').value,
  catalogReceipt:window.__combatHttp.filter(x=>x.url==='/api/forever/talents/rogue/combat'&&x.status===200&&x.deliveredAt!==undefined).at(-1),
  previewReceipt:window.__combatHttp.find(x=>x.sequence===window.__latestCombatPreview),
  summary:read(required('#talentSummary')),caption:read(required('#talentCoverage')),
  scope:[...required('#rogueCombatTalentScope').querySelectorAll(':scope > p')].map(read),
  talents:[...document.querySelectorAll('#talents [data-talent-key]')].map(node=>({key:node.dataset.talentKey,rank:node.querySelector('.talent-rank')?.textContent}))};
}
function assertCombatTalentCoverage(state,expectedAllocated){
 assert.equal(state.classKey,'rogue');assert.equal(state.specialization,'combat','Copy belongs to the natively selected Combat preset');
 const catalog=state.catalogReceipt,preview=state.previewReceipt;
 assert.equal(catalog?.url,'/api/forever/talents/rogue/combat');assert.equal(catalog.status,200);assert.ok(Number.isFinite(catalog.deliveredAt),'Delivered actual catalog required');
 assert.deepEqual([catalog.response.classKey,catalog.response.specialization,catalog.response.level,catalog.response.maxPoints],['rogue','combat',60,51]);
 assert.equal(preview?.url,'/api/forever/character/preview-rogue');assert.equal(preview.status,200);assert.ok(Number.isFinite(preview.deliveredAt),'Delivered actual preparation required');
 assert.equal(preview.request.character.classKey,'rogue');assert.equal(preview.request.character.specializationKey,'combat');
 const selected=preview.request.talents.ranks,allocated=Object.values(selected).reduce((sum,rank)=>sum+rank,0);
 assert.deepEqual(state.talents.map(t=>t.key).sort(),Object.keys(ranks).sort(),'All eleven native talent controls');
 for(const talent of state.talents)assert.equal(talent.rank,(selected[talent.key]??0)+'/'+ranks[talent.key],'Rendered rank is bound to the actual prepared request: '+talent.key);
 if(expectedAllocated!==undefined)assert.equal(allocated,expectedAllocated);
 assert.equal(state.summary.visible,true,'Current allocation summary must be rendered');assert.equal(state.caption.visible,true,'Selected-preset coverage caption must be rendered');
 assert.equal(state.summary.text,allocated+' rank-derived points · '+(51-allocated)+' remaining / 51. Source-backed historical-only ranks count without awarding combat effects.');
 assert.equal(state.caption.text,expectedCombatTalentCoverage(catalog.response.talents),'Visible coverage must describe selected Combat, not a stale Assassination preset');
 assert.equal(state.scope.length,2,'Both complete Combat limitation paragraphs are required');
 for(let index=0;index<2;index++){assert.equal(state.scope[index].visible,true,'Combat limitation paragraph is rendered');assert.equal(state.scope[index].text,combatScopeParagraphs[index],'Complete partial/scope limitations remain visible');}
 return {allocated,unspent:51-allocated,catalogSequence:catalog.sequence,previewSequence:preview.sequence};
}
async function captureCombatAllocation({read,capture,size,coverage}){
 const before=await read(),allocation=assertCombatTalentCoverage(before,31);
 const receipt=await capture('allocation-'+size,'#talentSummary,#talentCoverage,#rogueCombatTalentScope');
 assert.ok(receipt.tiles?.length,'Allocation pixels require complete strict capture receipts');
 for(const tile of receipt.tiles){assert.ok(tile.text.startsWith(before.summary.text+'\n'+before.caption.text+'\n'),'Actual captured allocation includes the verified selected-preset caption');for(const paragraph of before.scope)assert.ok(tile.text.includes(paragraph.text),'Actual allocation capture includes complete Combat limitations');}
 assert.deepEqual(await read(),before,'Visible Combat allocation/caption and owning responses cannot change during capture');
 coverage.push({gate:'visible-combat-allocation',size,...allocation,summary:before.summary.text,caption:before.caption.text,scope:before.scope.map(p=>p.text),captureName:receipt.name,images:receipt.tiles.length});
 return before.catalogReceipt;
}
Object.assign(module.exports,{combatCoveragePrefix,combatScopeParagraphs,expectedCombatTalentCoverage,readCombatTalentCoverage,assertCombatTalentCoverage,captureCombatAllocation});

// Exact text is derived from the current successful catalog response, not a
// reduced tooltip fixture. Every known limitation remains part of the capture.
function combatTalentDetailRows(catalog){
 assert.ok(Array.isArray(catalog),'Current Combat catalog required');
 assert.equal(catalog.length,11,'Capture all eleven Combat nodes');
 assert.deepEqual([...catalog.map(t=>t.key)].sort(),Object.keys(ranks).sort(),'Exact Combat catalog keys');
 return catalog.map(t=>{
  assert.equal(t.tree,'combat');assert.equal(t.maxRank,ranks[t.key]);
  assert.ok(typeof t.simulationStatus==='string'&&t.simulationStatus.trim(),'Explicit status for '+t.key);
  const limits=Array.isArray(t.knownLimitations)?t.knownLimitations.join(' '):t.knownLimitations;
  assert.ok(typeof limits==='string'&&limits.trim(),'Explicit limitations for '+t.key);
  return {key:t.key,text:t.key.replaceAll('-',' ').replace(/\b\w/g,c=>c.toUpperCase())+': '+t.simulationStatus+'. '+limits};
 });
}
// Runs in the page: observation only. Native clicks own the details state.
function readCombatTalentDetails(){
 const details=document.querySelector('#rogueCombatTalentScope > details');
 const summary=details?.querySelector(':scope > summary');
 const list=details?.querySelector('#rogueCombatTalentLimitations');
 if(!details||!summary||!list)throw Error('Missing Combat talent details structure');
 const visible=e=>{if(!e.getClientRects().length)return false;for(let n=e;n;n=n.parentElement){const css=getComputedStyle(n);if(n.hidden||css.display==='none'||['hidden','collapse'].includes(css.visibility))return false;}return true;};
 return {open:details.open,summaryVisible:visible(summary),rows:[...list.children].map(row=>{
  const r=row.getBoundingClientRect();return {tag:row.tagName,text:row.textContent,visible:details.open&&visible(row),left:r.left,right:r.right,top:r.top+window.scrollY,bottom:r.bottom+window.scrollY,width:row.clientWidth,height:row.clientHeight,scrollWidth:row.scrollWidth,scrollHeight:row.scrollHeight};
 }),viewportWidth:window.innerWidth,documentWidth:document.documentElement.scrollWidth};
}
async function captureExpandedCombatTalents({read,click,capture,catalog,size,coverage,catalogSequence}){
 const summary='#rogueCombatTalentScope > details > summary',expected=combatTalentDetailRows(catalog),before=await read(),started=Date.now(),regions=[];let completion=null;
 assert.equal(before.open,false,'Expanded detail proof starts from the prior closed state');
 assert.equal(before.summaryVisible,true,'The actual summary must be rendered');
 try{
  await click(summary);
  const expanded=await read();assert.equal(expanded.open,true,'Native summary click expands details');
  assert.equal(expanded.rows.length,11,'All eleven limitation rows are rendered');
  assert.deepEqual(expanded.rows.map(row=>row.text),expected.map(row=>row.text),'All catalog statuses and complete limitations, in source order');
  assert.ok(expanded.documentWidth<=expanded.viewportWidth+1,'Expanded details cause no horizontal document overflow');
  for(let index=0;index<expected.length;index++){
   const row=expanded.rows[index];assert.equal(row.tag,'LI');assert.equal(row.visible,true,'Expanded row is actually rendered: '+expected[index].key);
   assert.ok(row.width>0&&row.height>0&&row.left>=-1&&row.right<=expanded.viewportWidth+1,'Expanded row has visible horizontal bounds');
   assert.ok(row.scrollWidth<=row.width+1&&row.scrollHeight<=row.height+1,'Expanded row text is not clipped');
   // Each row uses the unchanged sixteen-segment capture bound. The complete
   // 27k-character list cannot fit that bound as one mobile region.
   const region=await capture('talent-detail-'+expected[index].key+'-'+size,'#rogueCombatTalentLimitations > li:nth-child('+(index+1)+')');
   assert.ok(region.tiles.length>0&&region.verticalSegments<=16&&region.horizontalSegments===1,'Bounded complete row capture');
   assert.ok(region.tiles.every(tile=>tile.text===expected[index].text),'Pixels retain the exact catalog-derived row');
   assert.ok(region.tiles[0].from<=row.top+1&&region.tiles.at(-1).to>=row.bottom-1,'Tiled capture covers the full expanded row');regions.push(region);
  }
  const after=await read();assert.equal(after.open,true);assert.deepEqual(after.rows,expanded.rows,'Rows remain unchanged throughout complete capture');
  const images=regions.reduce((n,region)=>n+region.tiles.length,0);assert.ok(images>=11&&images<=176,'Eleven complete rows stay within the existing16-tiles-per-row bound');
  completion={gate:'expanded-combat-talent-details',size,catalogSequence,keys:expected.map(row=>row.key),rows:expanded.rows,regions:regions.map(region=>region.name),images};return completion;
 }finally{
  // Restore through the same native control, including after a failed capture.
  if((await read()).open)await click(summary);
  const restored=await read();assert.equal(restored.open,before.open,'Restore the original closed details state');
  assert.equal(restored.summaryVisible,true);assert.ok(restored.rows.every(row=>!row.visible),'Detail rows are closed again');
  if(completion){completion.restoredClosed=true;completion.elapsedMilliseconds=Date.now()-started;coverage.push(completion);}
 }
}
module.exports.combatTalentDetailRows=combatTalentDetailRows;
module.exports.readCombatTalentDetails=readCombatTalentDetails;
module.exports.captureExpandedCombatTalents=captureExpandedCombatTalents;
