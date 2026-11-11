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
