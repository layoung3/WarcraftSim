'use strict';
// Pure observation/fixture contracts only. This does not launch a browser,
// start an API, or establish real-browser acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {installObserver,canonical,rejectionCases,gateNames,assertRejectedReportState}=require('./solo-dps-feral-browser-support.cjs');
const {build,environment}=require('./solo-dps-feral-fixture.cjs');
let checks=0;const test=(name,body)=>Promise.resolve().then(body).then(()=>{checks++;});
(async()=>{
 await test('complete-envelope-guid-identity',()=>{
  const a='11111111-1111-1111-1111-111111111111',b='22222222-2222-2222-2222-222222222222',c='33333333-3333-3333-3333-333333333333',d='44444444-4444-4444-4444-444444444444';
  const first={preparation:{preview:{configuredRotation:{id:a},spellbookAttackPower:902.4},baseline:{source:a}},sim:{run:{timeline:[{abilityExecutionId:b,auraInstanceId:a,at:0},{abilityExecutionId:b,auraInstanceId:a,at:1}]}},timing:0};
  const equivalent={timing:0,sim:{run:{timeline:[{at:0,auraInstanceId:c,abilityExecutionId:d},{at:1,auraInstanceId:c,abilityExecutionId:d}]}},preparation:{baseline:{source:a},preview:{spellbookAttackPower:902.4,configuredRotation:{id:c}}}};
  assert.equal(canonical(first),canonical(equivalent));
  for(const mutate of [x=>x.sim.run.timeline.reverse(),x=>x.sim.run.timeline[1].abilityExecutionId=a,x=>x.preparation.preview.spellbookAttackPower=902.41,x=>x.timing=80,x=>delete x.preparation,x=>x.sim.run.timeline.push({abilityExecutionId:b,auraInstanceId:a,at:2}),x=>x.preparation.baseline.source=c,x=>x.preparation.preview.configuredRotation.name=c]){const changed=structuredClone(first);mutate(changed);assert.notEqual(canonical(changed),canonical(first));}
 });
 await test('structural-allowlist-decoys-and-marker-collisions',()=>{
  const a='11111111-1111-4111-8111-111111111111',b='22222222-2222-4222-8222-222222222222';
  const {c}=environment();
  const same=(left,right)=>{assert.equal(canonical(left),canonical(right));for(const value of [left,right])assert.equal(canonical(value),c.feralReplaySignature(value),'Browser and production replay policies agree');};
  const different=(left,right)=>{assert.notEqual(canonical(left),canonical(right));for(const value of [left,right])assert.equal(canonical(value),c.feralReplaySignature(value),'Browser and production replay policies agree');};
  for(const field of ['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'])for(const nested of [false,true]){
   const shape=id=>{const run={run:{timeline:[{[field]:id},{[field]:id}]}};return nested?{sim:run}:run;};same(shape(a),shape(b));
   for(const literal of ['ENGINE-ID-0','ENGINE-ID-1','not-a-guid',null,0])different(shape(a),shape(literal));
  }
  for(const nested of [false,true]){const shape=id=>{const preview={preview:{configuredRotation:{id}}};return nested?{preparation:preview}:preview;};same(shape(a),shape(b));different(shape(a),shape('ENGINE-ID-0'));}
  for(const shape of [
   id=>({'preparation.preview.configuredRotation.id':id}),
   id=>({preview:{'configuredRotation.id':id}}),
   id=>({'sim.run.timeline.0.auraInstanceId':id}),
   id=>({sim:{'run.timeline':[{auraInstanceId:id}]}}),
   id=>({run:{timeline:{'0':{auraInstanceId:id}}}}),
   id=>({run:{timeline:[{nested:{auraInstanceId:id}}]}}),
   id=>({run:{timeline:[{periodicEffectInstanceId:id}]}}),
   id=>({run:{timeline:[{id}]}}),
   id=>({configuredRotation:{id}}),
   id=>({provenance:{preview:{configuredRotation:{id}}}}),
   id=>({source:{abilityExecutionId:id}})
  ])different(shape(a),shape(b));
 });
 await test('hold-actual-response-not-newer-request',async()=>{
  const listeners={},requests=[],responses=[];let now=0;
  const document={addEventListener:(type,fn)=>listeners[type]=fn};
  const window={fetch:(...args)=>new Promise(resolve=>requests.push({args,resolve}))};
  vm.runInNewContext('('+installObserver.toString()+')()',{window,document,performance:{now:()=>++now},JSON,Promise});
  const hold={url:'/api/forever/character/preview-feral',armed:true,prefix:false};window.__feralHold=hold;
  const old=window.fetch(hold.url,{body:'{"manualInputs":{"preTalentCatAttackPower":800}}'});
  assert.equal(hold.armed,false);assert.equal(hold.held,undefined);
  const newer=window.fetch(hold.url,{body:'{"manualInputs":{"preTalentCatAttackPower":900}}'});
  const response=value=>({status:200,clone:()=>({json:async()=>value})});
  responses[1]=response({manual:900});requests[1].resolve(responses[1]);assert.equal(await newer,responses[1]);
  responses[0]=response({manual:800});requests[0].resolve(responses[0]);await new Promise(setImmediate);assert.equal(hold.held,true);assert.equal(hold.released,undefined);
  assert.equal(window.__feralHttp[0].request.manualInputs.preTalentCatAttackPower,900);assert.equal(window.__feralHttp[1].request.manualInputs.preTalentCatAttackPower,800);
  assert.deepEqual(JSON.parse(JSON.stringify(window.__feralHttp.map(x=>x.response))),[{manual:900},{manual:800}]);
  assert.equal(window.__feralHttp[1].deliveredAt,undefined);hold.release();assert.equal(await old,responses[0]);assert.equal(hold.released,true);assert.ok(window.__feralHttp[1].deliveredAt>window.__feralHttp[0].deliveredAt);
 });
 await test('trusted-input-observation-does-not-dispatch',()=>{
  const listeners={},window={fetch:async()=>{}},document={addEventListener:(type,fn)=>listeners[type]=fn};
  vm.runInNewContext('('+installObserver.toString()+')()',{window,document,performance:{now:()=>1},JSON,Promise});
  listeners.input({isTrusted:true,target:{id:'feralPreTalentCatAttackPower',value:'',checked:false}});assert.equal(window.__feralInputs[0].trusted,true);assert.equal(window.__feralInputs[0].value,'');
  const detail={originalSimulationRequest:{feral:{manualInputs:{preTalentCatAttackPower:800}}}};listeners['warcraftsim:simulation-complete']({detail});detail.originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower=0;assert.equal(window.__feralCompleted[0].originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower,800);
 });
 await test('strict-rejection-fixtures-are-real-mutations',()=>{
  assert.equal(rejectionCases.length,40);assert.equal(new Set(rejectionCases.map(x=>x.name)).size,40);const original=build();const {c}=environment();
  for(const {name,mutate} of rejectionCases){const changed=structuredClone(original);mutate(changed);assert.notEqual(canonical(changed),canonical(original),name);if(!['unknown-rank','unaffordable-shift'].includes(name))assert.throws(()=>c.validateFeralImport(changed),undefined,name);}
 });
 await test('rejected-report-transaction-preserves-valid-or-empty-state',()=>{
  const empty={labelA:'valid A',labelB:'No report selected',hidden:true,rows:'',warnings:'',changes:'',useA:false,useB:false,lastComparison:null};
  const valid={...empty,labelB:'valid B',hidden:false,rows:'<tr><td>+15.2</td></tr>',warnings:'<div>Historical source</div>',changes:'<div>Weapon DPS changed</div>',lastComparison:{comparable:true,dpsDelta:15.2,a:{report:{id:'valid-A'}},b:{report:{id:'valid-B'}}}};
  for(const before of [empty,valid]){
   assertRejectedReportState(before,structuredClone(before),'Report import rejected: candidate invalid','valid/empty preservation');
   assert.throws(()=>assertRejectedReportState(before,structuredClone(before),'Encounter settings match','missing rejection'));
   for(const mutate of [x=>x.labelB='invalid candidate',x=>x.rows=before.rows?'':'<tr><td>+999</td></tr>',x=>x.hidden=!before.hidden,x=>x.warnings='changed',x=>x.changes='changed',x=>x.lastComparison={comparable:true,dpsDelta:999}]){
    const after=structuredClone(before);mutate(after);assert.throws(()=>assertRejectedReportState(before,after,'Report import rejected: candidate invalid','rejected mutation'));
   }
  }
 });
 await test('actual-rejected-import-settles-prior-readiness',async()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');
  const helper=source.slice(source.indexOf('async function rejectedImport('),source.indexOf('async function captureAll('));
  const readyHead=source.slice(source.indexOf('async function ready('),source.indexOf(" const receipt=await evaluate('window.__feralHttp.find",source.indexOf('async function ready(')))+'}';
  const plain=x=>JSON.parse(JSON.stringify(x)),actual=environment(),request=plain(actual.c.feralRequest(build()));
  const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return {promise,resolve};};
  function scenario({kind='queued',classKey='druid',spec='feral',invalid=false,invalidState='ready',server=false}={}){
   const feral=classKey==='druid'&&spec==='feral';
   const previous={sequence:41,url:'/api/forever/character/preview-feral',status:200,deliveredAt:1,request:plain(request)},draft=[{id:'race',value:'night-elf'},{id:'feralPreTalentCatAttackPower',value:invalid?'':'800'},{id:'seed',value:invalid?'0.5':'144001'}];
   const elements={classKey:{value:classKey},spec:{value:spec},race:{value:'night-elf'},status:{dataset:{buildState:invalid?invalidState:'ready'},textContent:'Previous status'},statStatus:{textContent:'Up to date'},file:{value:''},characterTitle:{textContent:spec+' '+classKey},characterSubtitle:{textContent:'Night Elf · Level 60'}};
   for(const id of ['run','save','exportBuild'])elements[id]={disabled:invalid};
   const browser={window:{__feralHttp:[previous],__latestFeralPreview:41},document:{querySelector:selector=>elements[selector.slice(1)],getElementById:id=>elements[id]}};vm.createContext(browser);
   const began=deferred(),released=deferred();let readyCalls=0,blockedCalls=0,finished=false;
   const evaluate=async expression=>expression==='rawDraft'?plain(draft):plain(vm.runInContext(expression,browser));
   const until=async expression=>assert.equal(vm.runInContext(expression,browser),true,'Actual import completion predicate');
   const readyGate=vm.runInNewContext(readyHead+';ready;',{until});
   const context={assert,canonical,draftExpression:'rawDraft',evaluate,sequence:async()=>41,observeImport:async()=>{},text:async selector=>elements[selector.slice(1)].textContent,
    selectFile:async()=>{browser.window.__importChanged=true;browser.window.__importStatus=true;elements.status.textContent='Feral talent is outside the current catalog';if(invalid)elements.status.dataset.buildState='input-required';if(feral&&!invalid&&kind==='queued'){elements.status.dataset.buildState='input-required';elements.statStatus.textContent='Updating…';for(const id of ['run','save','exportBuild'])elements[id].disabled=true;}
     if(server)browser.window.__feralHttp.push({sequence:42,url:'/api/forever/character/preview-feral',status:400,request:{manualInputs:{shapeshiftMana:999999}}});},until,
    ready:async after=>{readyCalls++;assert.equal(after,40,'A retained current receipt is allowed; a newer request is not forced');began.resolve();if(kind==='queued')await released.promise;await readyGate(after);return plain(browser.window.__feralHttp.find(x=>x.sequence===browser.window.__latestFeralPreview));},
    assertBlockedEditable:async()=>{blockedCalls++;for(const id of ['run','save','exportBuild'])assert.equal(elements[id].disabled,true);}
   };
   const result=vm.runInNewContext(helper+';rejectedImport("candidate.json","model",{server:'+server+'});',context).then(x=>{finished=true;return x;});
   function recover(mutate=()=>{}){const current=plain(previous);current.sequence=43;current.deliveredAt=2;mutate(current.request);browser.window.__feralHttp.push(current);browser.window.__latestFeralPreview=43;elements.status.dataset.buildState='ready';elements.statStatus.textContent='Up to date';for(const id of ['run','save','exportBuild'])elements[id].disabled=false;released.resolve();}
   return {result,began,readyGate,browser,elements,previous,draft,recover,get finished(){return finished;},get readyCalls(){return readyCalls;},get blockedCalls(){return blockedCalls;}};
  }
  // Reproduce the real failure boundary: file cleared and raw identity restored,
  // but the queued preparation has not completed. Export must remain blocked.
  const queued=scenario();await new Promise(setImmediate);assert.equal(queued.readyCalls,1,'Rejected valid draft must wait for preparation before returning');assert.equal(queued.finished,false);assert.equal(queued.elements.exportBuild.disabled,true);await assert.rejects(()=>queued.readyGate(40));
  queued.recover();assert.match(await queued.result,/outside the current catalog/);assert.equal(queued.readyCalls,1);
  // Local validation never invalidated the old ready receipt. No new response
  // is needed and requiring sequence>the import start would deadlock this case.
  const local=scenario({kind:'local'});await local.result;assert.equal(local.browser.window.__feralHttp.length,1);assert.equal(local.readyCalls,1);
  for(const invalidState of ['ready','input-required'])for(const server of [false,true]){const invalid=scenario({invalid:true,invalidState,server});await invalid.result;assert.equal(invalid.readyCalls,0);assert.equal(invalid.blockedCalls,1);assert.equal(invalid.elements.exportBuild.disabled,true);assert.equal(invalid.draft[1].value,'');assert.equal(invalid.draft[2].value,'0.5');}
  for(const [classKey,spec] of [['druid','balance'],['mage','arcane']]){const foreign=scenario({classKey,spec,server:true});await foreign.result;assert.equal(foreign.readyCalls,0);assert.equal(foreign.elements.status.dataset.buildState,'ready');}
  // Full exact request, not just race/AP, owns restored readiness.
  const wrong=scenario();await new Promise(setImmediate);assert.equal(wrong.readyCalls,1);const rejected=assert.rejects(()=>wrong.result,/exact prior request/);wrong.recover(r=>r.seed++);await rejected;
  // A ready-looking DOM cannot substitute a missing/failed/undelivered/stale
  // receipt or one whose race no longer owns the current selector.
  const gate=scenario({kind:'local'});await gate.result;
  for(const mutate of [r=>r.status=400,r=>delete r.deliveredAt,r=>r.sequence=39,r=>r.request.character.race='troll']){const original=plain(gate.previous);mutate(gate.previous);await assert.rejects(()=>gate.readyGate(40));Object.keys(gate.previous).forEach(k=>delete gate.previous[k]);Object.assign(gate.previous,original);}
 });
 await test('every-planned-gate-is-asserted-once',()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');const marks=[...source.matchAll(/mark\('([^']+)'/g)].map(x=>x[1]);assert.equal(gateNames.length,34);assert.deepEqual(marks.sort(),[...gateNames].sort());assert.equal(new Set(marks).size,marks.length);
  assert.ok(source.includes('createBrowserClick'));assert.ok(source.includes('createRegionCapture'));assert.ok(source.includes('DOM.setFileInputFiles'));assert.ok(source.includes("viewport(390,844,true)"));assert.ok(source.includes("setInput('#feralPreTalentCatAttackPower','',false)"));
  assert.equal(/dispatchEvent\s*\(|\.click\s*\(|Fetch\.fulfillRequest|new Response\s*\(/.test(source),false,'No synthetic successful response/input or renderer injection');
  assert.ok(source.includes("holdResponse('/api/forever/talents/druid/feral/validate-itemized')"));
  const flow=source.slice(source.indexOf('async function main(){')),fixtureSelection=flow.indexOf('await selectFeralFixtureRace();'),manualEntry=flow.indexOf("await setInput('#feralPreTalentCatAttackPower',800)");
  assert.ok(fixtureSelection>=0&&fixtureSelection<manualEntry,'The executed workflow explicitly establishes Night Elf before its first AP800 preparation');
  assert.equal((flow.match(/await selectFeralFixtureRace\(\);/g)||[]).length,1,'Exactly one explicit initial numerical-fixture race selection');
 });
 console.log('Feral browser support pure contracts passed: '+checks+' groups; 34 planned browser gates and 40 rejection fixtures. No browser or API executed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
