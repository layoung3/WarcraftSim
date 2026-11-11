'use strict';
// Browser observation and adversarial import fixtures shared with pure tests.
// No response is manufactured: the hold delays delivery of the actual fetch.
function installObserver(){
 window.__feralHttp=[];window.__feralSequence=0;window.__feralCompleted=[];window.__feralInputs=[];
 const nativeFetch=window.fetch;
 window.fetch=async(...args)=>{
  const sequence=++window.__feralSequence,url=String(args[0]);
  if(url==='/api/forever/character/preview-feral')window.__latestFeralPreview=sequence;
  const candidate=window.__feralHold;
  const hold=candidate?.armed&&(candidate.prefix?url.startsWith(candidate.url):url===candidate.url)?candidate:null;
  if(hold){hold.armed=false;hold.sequence=sequence;}
  const receipt={sequence,url,request:args[1]?.body?JSON.parse(args[1].body):null,requestedAt:performance.now()};
  const response=await nativeFetch(...args);receipt.status=response.status;
  try{receipt.response=await response.clone().json();}catch{}
  receipt.receivedAt=performance.now();window.__feralHttp.push(receipt);
  if(hold){hold.held=true;await new Promise(resolve=>hold.release=resolve);hold.released=true;}
  receipt.deliveredAt=performance.now();return response;
 };
 document.addEventListener('warcraftsim:simulation-complete',event=>window.__feralCompleted.push(JSON.parse(JSON.stringify(event.detail))));
 for(const type of ['mousedown','mouseup','click','contextmenu','keydown','keyup','input','change'])document.addEventListener(type,event=>{
  window.__feralInputs.push({type,trusted:event.isTrusted,id:event.target?.id,value:event.target?.value,checked:event.target?.checked,key:event.key,at:performance.now()});
 },true);
}
// Sorted object keys, ordered arrays, and only known generated engine-ID paths.
// Provenance strings are never normalized just because they resemble a GUID.
// JS uses parsed JSON numbers; exact-decimal checking remains a Python gate.
function canonical(value){
 const identities=new Map(),guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
 const generated=path=>{
  const p=path[0]==='preparation'?path.slice(1):path;
  if(p.length===3&&p[0]==='preview'&&p[1]==='configuredRotation'&&p[2]==='id')return true;
  const event=path[0]==='sim'?path.slice(1):path;
  return event.length===4&&event[0]==='run'&&event[1]==='timeline'&&typeof event[2]==='number'&&['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'].includes(event[3]);
 };
 const visit=(x,path=[])=>Array.isArray(x)?x.map((item,index)=>visit(item,[...path,index])):x&&typeof x==='object'?Object.fromEntries(Object.keys(x).sort().map(key=>[key,visit(x[key],[...path,key])])):typeof x==='string'&&guid.test(x)&&generated(path)?(identities.has(x)?identities.get(x):(identities.set(x,'00000000-0000-4000-8000-'+String(identities.size).padStart(12,'0')),identities.get(x))):x;
 return JSON.stringify(visit(value));
}
const rejectionCases=[
 ['missing-contract',x=>delete x.inputMode],['legacy-contract',x=>x.inputMode='legacy-authored-inclusive'],
 ['unknown-build',x=>x.unknown=true],['legacy-tuning',x=>x.tuning={attackPower:800}],
 ['legacy-encounter',x=>x.encounter={duration:30}],['health-override',x=>x.maximumHealth=5500],
 ['mana-override',x=>x.maximumMana=4000],['attributes',x=>x.baseIntellect=180],
 ['inline-observation',x=>x.level60Baseline={observedUnbuffedMeleeAttackPower:800}],
 ['observed-ap',x=>x.manualInputs.observedAttackPower=800],['reviewed-baseline',x=>x.provisional=false],
 ['missing-race',x=>delete x.race],['unknown-manual',x=>x.manualInputs.customCoefficient=1],
 ['missing-ap',x=>delete x.manualInputs.preTalentCatAttackPower],['null-ap',x=>x.manualInputs.preTalentCatAttackPower=null],
 ['string-ap',x=>x.manualInputs.preTalentCatAttackPower='800'],['negative-ap',x=>x.manualInputs.preTalentCatAttackPower=-1],
 ['numeric-string-seed',x=>x.scenario.seed='144001'],['fractional-seed',x=>x.scenario.seed=.5],
 ['null-total',x=>x.talents.totalPoints=null],['fractional-total',x=>x.talents.totalPoints=36.5],
 ['string-total',x=>x.talents.totalPoints='36'],['mismatched-total',x=>x.talents.totalPoints=35],
 ['unknown-rank',x=>{x.talents.ranks['unknown-rank']=0;}],
 ['furor-lower-rank',x=>{x.talents.ranks.furor=4;x.talents.restorationPointsSpent=4;x.talents.totalPoints=35;x.talents.unspentPoints=16;}],
 ['gear',x=>x.gear={items:[{slotKey:'main-hand',itemId:19102}]}],['enchant',x=>x.equipment={items:[{enchantId:1}]}],
 ['modifiers',x=>x.modifiers={buffs:['arcane-intellect']}],['combat-items',x=>x.combatItems={majorManaPotionCharges:1}],
 ['racial',x=>x.racialActive=true],['companion',x=>x.companion={enabled:true}],
 ['custom-priority',x=>x.scenario.customPriority=['claw']],['script',x=>x.scenario.soloScript={enabled:true}],
 ['transition',x=>x.scenario.formTransitions=[{timeSeconds:1,form:'dire-bear'}]],
 ['incoming',x=>x.scenario.enableBossAttacks=true],['seeded-combos',x=>x.scenario.initialComboPoints=5],
 ['batch',x=>x.scenario.iterations=2],['latency',x=>x.scenario.inputLatencyMs=80],
 ['front-ravage',x=>{x.scenario.startInProwl=true;x.scenario.targetBehind=false;x.scenario.preferredCatOpener='ravage';}],
 ['unaffordable-shift',x=>x.manualInputs.shapeshiftMana=999999]
].map(([name,mutate])=>({name,mutate}));
const gateNames=[
 'blank-manual-readiness','explicit-opt-in-preview','independent-numeric-preview','zero-and-fractional-ap',
 'focused-invalid-input','manual-controls','furor-zero-five','talent-change-preview','position-opener-recovery',
 'shift-affordability-recovery','preview-only-no-simulation','real-simulation-lock','full-report-reconciliation',
 'complete-envelope-replay','build-roundtrip','strict-import-rejections','invalid-raw-draft-rollback',
 'valid-import-recovers','dirty-cancel-and-restore','balance-and-mage-rollback',
 'rapid-class-spec-race-preset','late-manual-preview','late-talent-preview','late-validation-identity',
 'late-identity-preset','late-baseline-preview','compatible-ap-comparison','compatible-weapon-comparison',
 'incompatible-armor-comparison','incompatible-position-comparison','incompatible-timing-and-source',
 'missing-conflicting-comparison-receipts','desktop-complete-coverage','mobile-complete-coverage'
];
module.exports={installObserver,canonical,rejectionCases,gateNames};
