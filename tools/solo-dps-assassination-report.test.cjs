'use strict';
// Pure admission/transaction checks. Originals are byte-identical UI exports;
// every mutation below is derived synthetic input, never a new simulation.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const clone=x=>JSON.parse(JSON.stringify(x));
const directory=path.join(__dirname,'fixtures/assassination-reports');
const original=name=>JSON.parse(fs.readFileSync(path.join(directory,name+'-report.json'),'utf8'));
const maps=r=>[r.build.talents.ranks,r.originalSimulationRequest.itemizedTalentRanks,r.originalSimulationRequest.rogue.talents.ranks];
// The twelve maxima total 38; no valid-key/cap allocation can exceed 51.
// Excess aggregate counts below are declaration failures, not budget coverage.
const caps={malice:5,ruthlessness:3,'improved-slice-and-dice':3,'improved-expose-armor':2,lethality:5,'vile-poisons':5,'cold-blood':1,'improved-poisons':5,vigor:2,mutilate:1,'seal-fate':5,venom:1};
function allocation(r,ranks){
 const total=Object.values(ranks).reduce((sum,rank)=>sum+rank,0),tree={assassinationPointsSpent:total,combatPointsSpent:0,subtletyPointsSpent:0};
 for(const talents of [r.build.talents,r.originalSimulationRequest.rogue.talents])Object.assign(talents,clone(tree),{ranks:clone(ranks)});
 r.originalSimulationRequest.itemizedTalentRanks=clone(ranks);
 Object.assign(r.rawServerResponse,{allocatedTalentPoints:total,unspentTalentPoints:51-total});
 Object.assign(r.talentAllocation,{mode:'strict-rank-derived',totalSelectedRanks:total,unspentPoints:51-total,treePoints:tree,selected:Object.entries(ranks).filter(([,rank])=>rank>0).map(([key,rank])=>({key,rank}))});
 return r;
}
function invalidCases(){
 const cases=[],add=(name,mutate,reason)=>cases.push({name,mutate,reason});
 const unsupported=[['dirty-deeds',0],['dirty-deeds',1],['dirty-deeds',2],['cutthroat',0],['cutthroat',1],['cutthroat',5],['unknown-talent',0],['unknown-talent',1],['precision',0],['precision',1],['Malice',0],[' malice',0],['malice ',0],['__proto__',0],['constructor',1],['toString',0],['<img src=x onerror=alert(1)>',0]];
 for(const [key,rank] of unsupported)for(const where of ['saved','full','typed','all','coherent'])add(`unsupported ${key}=${rank} ${where}`,r=>{
  if(where==='coherent')allocation(r,{...r.build.talents.ranks,[key]:rank});
  else for(const map of where==='all'?maps(r):[maps(r)[['saved','full','typed'].indexOf(where)]])Object.defineProperty(map,key,{value:rank,enumerable:true,writable:true,configurable:true});
 },/unsupported talent key/i);
 for(const source of ['missing','unknown'])add('unsupported key with '+source+' provenance',r=>{if(source==='missing'){delete r.metadata;delete r.rawServerResponse.talentSourceCommit;}else{r.metadata.sourceCommit='unknown';r.rawServerResponse.talentSourceCommit='unknown';}allocation(r,{...r.build.talents.ranks,'dirty-deeds':0});},/unsupported talent key/i);
 for(const [key,cap] of Object.entries(caps))add('cap '+key,r=>allocation(r,{...r.build.talents.ranks,[key]:cap+1}),/invalid rank/i);
 for(const value of [-1,.5,'5',true,false,null,[],{}])for(const index of [0,1,2])add('rank type '+JSON.stringify(value)+' map '+index,r=>maps(r)[index].malice=value,/invalid rank/i);
 for(const index of [0,1,2])for(const value of [null,[],undefined])add('rank map '+index+' '+String(value),r=>{
  const owners=[r.build.talents,r.originalSimulationRequest,r.originalSimulationRequest.rogue.talents],key=index===1?'itemizedTalentRanks':'ranks';
  if(value===undefined)delete owners[index][key];else owners[index][key]=value;
 },/rank maps.*required/i);
 for(const owner of ['build','typed'])for(const value of [undefined,null,[]])add('talents '+owner+' '+String(value),r=>{
  const target=owner==='build'?r.build:r.originalSimulationRequest.rogue;if(value===undefined)delete target.talents;else target.talents=value;
 },/rank maps.*required/i);
 for(const index of [0,1,2])add('zero presence map '+index,r=>{allocation(r,{});maps(r)[index].malice=0;},/rank maps.*match/i);
 add('typed supported disagreement',r=>r.originalSimulationRequest.rogue.talents.ranks.venom=0,/rank maps.*match/i);
 for(const [key,rank] of Object.entries(caps))if(key!=='malice')add('earlier row '+key,r=>allocation(r,{[key]:rank}),/earlier-row/i);
 // Malice is the sole row-1 node: this parent violation also breaks row-2
 // admission. Either precise gate may reject it; do not prescribe gate order.
 add('lethality parent with coupled row violation',r=>allocation(r,{malice:4,ruthlessness:3,'improved-slice-and-dice':3,lethality:1}),/requires malice|earlier-row/i);
 add('venom parent',r=>{const ranks={...r.build.talents.ranks};delete ranks.mutilate;allocation(r,ranks);},/requires mutilate/i);
 const fields=[['build','itemizedMode',true],['originalSimulationRequest','itemizedTalentMode',true],['originalSimulationRequest','classKey','rogue'],['originalSimulationRequest','specialization','assassination'],['originalSimulationRequest.rogue','specialization','assassination'],['originalSimulationRequest.rogue.character','classKey','rogue'],['originalSimulationRequest.rogue.character','specializationKey','assassination'],['originalSimulationRequest.rogue.character','level',60],['rawServerResponse','classKey','rogue'],['rawServerResponse','specialization','assassination'],['rawServerResponse','level',60],['rawServerResponse.sim','specialization','assassination']];
 for(const [location,key,value] of fields)for(const mode of ['missing','wrong'])add('identity '+location+'.'+key+' '+mode,r=>{const o=location.split('.').reduce((v,k)=>v[k],r);if(mode==='missing')delete o[key];else o[key]=typeof value==='boolean'?false:typeof value==='number'?'60':'other';},/identity|typed simulation request/i);
 for(const value of [null,[],undefined])add('character '+String(value),r=>{r.originalSimulationRequest.rogue.character=value;},/identity/i);
 const counts=[['build.talents','assassinationPointsSpent'],['build.talents','combatPointsSpent'],['build.talents','subtletyPointsSpent'],['originalSimulationRequest.rogue.talents','assassinationPointsSpent'],['originalSimulationRequest.rogue.talents','combatPointsSpent'],['originalSimulationRequest.rogue.talents','subtletyPointsSpent'],['rawServerResponse','allocatedTalentPoints'],['rawServerResponse','unspentTalentPoints'],['talentAllocation','totalSelectedRanks'],['talentAllocation','unspentPoints'],['talentAllocation.treePoints','assassinationPointsSpent'],['talentAllocation.treePoints','combatPointsSpent'],['talentAllocation.treePoints','subtletyPointsSpent']];
 for(const [location,key] of counts)for(const value of [undefined,'0',null,false,.5,-1,99])add('allocation '+location+'.'+key+' '+String(value),r=>{const o=location.split('.').reduce((v,k)=>v[k],r);if(value===undefined)delete o[key];else o[key]=value;},/allocation/i);
 for(const value of [undefined,null,[]])add('allocation object '+String(value),r=>r.talentAllocation=value,/allocation/i);
 for(const key of ['mode','treePoints','selected'])for(const value of [undefined,null,{},'bad'])add('allocation '+key+' '+String(value),r=>r.talentAllocation[key]=value,/allocation|selected/i);
 for(const key of [['malice'],[['malice']],null,{},0,false])add('selected non-string key '+JSON.stringify(key),r=>r.talentAllocation.selected.find(entry=>entry.key==='malice').key=key,/selected/i);
 for(const [name,mutate] of [
  ['missing member',r=>r.talentAllocation.selected.pop()],['duplicate member',r=>r.talentAllocation.selected.push(clone(r.talentAllocation.selected[0]))],
  ['unknown member',r=>r.talentAllocation.selected.push({key:'precision',rank:1})],['zero member',r=>r.talentAllocation.selected[0].rank=0],['negative member',r=>r.talentAllocation.selected[0].rank=-1],['fraction member',r=>r.talentAllocation.selected[0].rank=.5],['string member',r=>r.talentAllocation.selected[0].rank='5'],['null member rank',r=>r.talentAllocation.selected[0].rank=null],['wrong member rank',r=>r.talentAllocation.selected[0].rank=1],['null member',r=>r.talentAllocation.selected[0]=null],['array member',r=>r.talentAllocation.selected[0]=[]]
 ])add('selected '+name,mutate,/selected/i);
 for(const [name,mutate] of [['report v2',r=>r.version=2],['report string version',r=>r.version='1'],['build v2',r=>r.build.version=2],['build string version',r=>r.build.version='1'],['raw wrapper',r=>delete r.build],['build wrapper',r=>r.schema='warcraftsim.solo-dps-build']])add('existing outer '+name,mutate,/report|build/i);
 return cases;
}
function positives(){
 const out=[['original 38',original('rogue-manual')],['original legal 36',original('legal-36')],['original 17.25',original('legacy-tuning')]],add=(name,edit)=>{const r=original('rogue-manual');edit(r);out.push([name,r]);};
 for(const [name,ranks] of [['empty',{}],['row1 boundary',{malice:5}],['row2 boundary',{malice:5,ruthlessness:1}],['row3 boundary',{malice:5,ruthlessness:3,'improved-slice-and-dice':2,lethality:1}],['legal removal',{...caps,venom:0}],['explicit zero',{malice:0}],['all zeros',Object.fromEntries(Object.keys(caps).map(key=>[key,0]))]])add(name,r=>allocation(r,ranks));
 add('reordered maps/selected',r=>{r.originalSimulationRequest.itemizedTalentRanks=Object.fromEntries(Object.entries(r.originalSimulationRequest.itemizedTalentRanks).reverse());r.talentAllocation.selected.reverse();});
 for(const value of [0,13,17.25,23.125])add('explicit tuning '+value,r=>{r.build.encounter.rogue.tuning.mutilateBonusDamagePerHand=value;r.originalSimulationRequest.rogue.tuning.mutilateBonusDamagePerHand=value;});
 add('optional absent',r=>{delete r.metadata;delete r.baselineProvenance;delete r.resolvedCharacter;for(const k of ['talentSourceCommit','talentSourceUrl','talentSourceStatus','talentModelingNotes'])delete r.rawServerResponse[k];r.talentAllocation.selected=r.talentAllocation.selected.map(({key,rank})=>({key,rank}));});
 add('optional null',r=>{r.metadata=null;r.baselineProvenance=null;r.resolvedCharacter=null;r.rawServerResponse.talentSourceCommit=null;for(const s of r.talentAllocation.selected)for(const key of Object.keys(s))if(!['key','rank'].includes(key))s[key]=null;});
 add('optional changed',r=>{Object.assign(r.metadata,{sourceCommit:'not-stamped-local-build',runtimeTargetBuild:'other',historicalTalentReferenceCommit:'unknown',version:999});r.rawServerResponse.talentSourceCommit='unknown';r.baselineProvenance={confidenceClassification:'another'};for(const s of r.talentAllocation.selected)Object.assign(s,{name:'Untrusted description',maxRank:0,row:99,prerequisite:'invented',column:99,tree:'combat',historicalValidationSourceCommit:'other'});});
 add('extra unrelated fields',r=>{r.configuredModifierSelections={note:'preserved'};r.talentAllocation.extra=null;r.build.talents.totalPoints='not a required field';r.originalSimulationRequest.rogue.talents.unspentPoints='not a required field';});
 return out;
}
async function transactions(modulePath){
 let scenarios=0;const nodes=new Map(),listeners={};
 const element=()=>({textContent:'',hidden:false,disabled:false,value:'',children:[],listeners:{},addEventListener(k,fn){this.listeners[k]=fn;},replaceChildren(){this.children=[];},append(x){this.children.push(x);},click(){this.listeners.click?.();}});
 const document={getElementById(id){if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);},createElement:element,addEventListener(k,fn){listeners[k]=fn;}};
 const ctx={window:{SoloDpsAssumptions:require('../WarcraftSim.Api/wwwroot/solo-dps-assumptions.js')},document,console};vm.createContext(ctx);vm.runInContext(fs.readFileSync(modulePath,'utf8'),ctx);
 const get=id=>document.getElementById(id),file=get('compareFile'),good=original('rogue-manual'),other=original('legal-36');
 const snap=()=>JSON.stringify(['compareLabelA','compareLabelB','compareDetails','compareRows','compareWarnings','compareChanges'].map(get),(k,v)=>k==='listeners'?undefined:v);
 const imported=async(slot,read)=>{get('compareImport'+slot).click();file.files=[{text:read}];await file.listeners.change();};
 const read=r=>async()=>JSON.stringify(r),bad=clone(good);maps(bad)[2]['dirty-deeds']=0;
 for(const state of ['empty','A','both','incompatible']){
  if(state==='A')await imported('A',read(good));
  if(state==='both')await imported('B',read(other));
  if(state==='incompatible'){const changed=clone(other);changed.build.encounter.seed='123';changed.originalSimulationRequest.rogue.seed=123;await imported('B',read(changed));}
  for(const slot of ['A','B'])for(const failure of [read(bad),async()=>'{',async()=>{throw Error('read failed');}]){
   const before=snap();await imported(slot,failure);assert.equal(snap(),before,state+' '+slot+' preserves labels, values, delta, warnings and changes');assert.match(get('compareStatus').textContent,new RegExp('Slot '+slot+' was not changed'));scenarios++;
  }
 }
 for(let i=0;i<2;i++){await imported('B',read(bad));await imported('B',read(good));assert.match(get('compareStatus').textContent,/Encounter settings match/);scenarios++;}
 for(const [a,b] of [[good,other],[other,good]]){await imported('A',read(a));await imported('B',read(b));assert.match(get('compareStatus').textContent,/Encounter settings match/);scenarios++;}
 const cancel=snap();get('compareImportA').click();file.files=[];await file.listeners.change();assert.equal(snap(),cancel);scenarios++;
 for(const failure of [false,true])for(const newer of ['import','use']){
  let release,reject;const pending=new Promise((resolve,no)=>{release=resolve;reject=no;});const old=imported('A',()=>pending);
  if(newer==='import')await imported('B',read(good));else{listeners['warcraftsim:simulation-complete']({detail:good});get('compareUseB').click();}
  const before=snap(),status=get('compareStatus').textContent;if(failure)reject(Error('late failure'));else release(JSON.stringify(other));await old;assert.equal(snap(),before);assert.equal(get('compareStatus').textContent,status);scenarios++;
 }
 const acceptedLabels=[get('compareLabelA').textContent,get('compareLabelB').textContent];listeners['warcraftsim:simulation-complete']({detail:bad});
 assert.equal(get('compareUseA').disabled,true);assert.equal(get('compareUseB').disabled,true);assert.equal(get('compareDetails').hidden,true);assert.equal(get('compareRows').children.length,0);assert.deepEqual([get('compareLabelA').textContent,get('compareLabelB').textContent],acceptedLabels);scenarios++;
 listeners['warcraftsim:simulation-complete']({detail:good});assert.equal(get('compareDetails').hidden,false);assert.ok(get('compareRows').children.length);scenarios++;
 return scenarios;
}
async function run(){
 const modulePath=process.env.ASSASSINATION_COMPARISON_MODULE?path.resolve(process.env.ASSASSINATION_COMPARISON_MODULE):path.join(__dirname,'../WarcraftSim.Api/wwwroot/solo-dps-comparison.js'),api=require(modulePath),classification=process.env.ASSASSINATION_REPORT_CLASSIFY;
 const receipt={modulePath,moduleSha256:crypto.createHash('sha256').update(fs.readFileSync(modulePath)).digest('hex'),positives:[],comparisonControls:[],negative:[]};let failures=0;
 for(const entry of JSON.parse(fs.readFileSync(path.join(directory,'provenance.json'))).files)assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(directory,entry.file))).digest('hex'),entry.sha256,entry.file+' complete original bytes');
 for(const [name,r] of positives()){
  try{const before=JSON.stringify(r),a=api.inspectReport(r);assert.deepEqual(api.inspectReport(r),a);assert.equal(JSON.stringify(r),before);assert.deepEqual(a.report,r);assert.equal(api.compareReports(r,r).comparable,true);if(name.startsWith('optional ')){const a=original('rogue-manual'),base=api.compareReports(a,a),pair=api.compareReports(a,r);for(const key of ['comparable','warnings','dpsDelta','dpsDeltaPercent','damageDelta'])assert.deepEqual(pair[key],base[key],name+' preserves original-versus-mutated '+key);}receipt.positives.push({name,passed:true});}catch(e){failures++;receipt.positives.push({name,passed:false,error:e.message});}
 }
 // Preserve established encounter-versus-build choices, including poison as
 // a build choice. These derived reports do not assert re-executed damage.
 for(const key of ['targetArmor','isBehindTarget','mainHandPoison','seed','duration','weapon']){
  const a=original('rogue-manual'),b=clone(a),t=b.originalSimulationRequest.rogue,e=b.build.encounter;
  if(key==='seed'){e.seed=String(t.seed+1);t.seed++;}
  else if(key==='duration'){e.duration=String(t.durationSeconds+1);t.durationSeconds++;}
  else if(key==='weapon'){e.rogue.tuning.mainHandWeaponDamage=t.tuning.mainHandWeaponDamage=234;}
  else{const value=key==='targetArmor'?0:key==='isBehindTarget'?!t[key]:t[key]==='instant'?'deadly':'instant';e.rogue[key]=t[key]=value;if(key==='targetArmor')b.combatSummary.targetSemantics[key]=value;}
  const c=api.compareReports(a,b);assert.equal(c.comparable,['mainHandPoison','weapon'].includes(key));assert.equal(c.dpsDelta,c.comparable?0:null);
  receipt.comparisonControls.push({key,comparable:c.comparable,warnings:c.warnings,dpsDelta:c.dpsDelta,changes:c.changes});
 }
 const forward=api.compareReports(original('rogue-manual'),original('legal-36')),backward=api.compareReports(original('legal-36'),original('rogue-manual'));
 assert.equal(forward.comparable,true);assert.equal(backward.comparable,true);assert.equal(forward.dpsDelta+backward.dpsDelta,0);
 for(const {name,mutate,reason} of invalidCases()){
  const r=original('rogue-manual');mutate(r);const before=JSON.stringify(r);let inspected,error;try{inspected=api.inspectReport(r);}catch(e){error=e.message;}
  assert.equal(JSON.stringify(r),before,name+' does not mutate caller');
  const passed=!!error&&reason.test(error);if(!passed)failures++;
  receipt.negative.push({name,passed,accepted:!error,error:error||null,warnings:inspected?.integrityWarnings||[],expectedReason:reason.source});
 }
 if(!classification){
  assert.equal(failures,0,JSON.stringify(receipt.negative.filter(x=>!x.passed).concat(receipt.positives.filter(x=>!x.passed)),null,2));
  const zero=allocation(original('rogue-manual'),{malice:0}),empty=allocation(original('rogue-manual'),{}),c=api.compareReports(empty,zero);
  assert.equal(c.comparable,true);assert.equal(c.dpsDelta,0);assert.ok(c.changes.some(x=>x.includes('zero-key presence')));
  assert.ok(api.inspectReport(original('rogue-manual')).integrityCaveats.some(x=>/internal consistency, not server attestation/.test(x)));
  receipt.transactionScenarios=await transactions(modulePath);
 }else fs.writeFileSync(classification,JSON.stringify(receipt,null,2)+'\n');
 console.log(`Assassination report admission: ${receipt.positives.length} positive controls; ${receipt.negative.length} negative controls; ${failures} unmet expectations${classification?' (old-source classification)':''}.`);
 if(failures)process.exitCode=1;
 return receipt;
}
module.exports={original,maps,allocation,invalidCases,positives,run};
if(require.main===module)run().catch(error=>{console.error(error);process.exitCode=1;});
