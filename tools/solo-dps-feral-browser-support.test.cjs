'use strict';
// Pure observation/fixture contracts only. This does not launch a browser,
// start an API, or establish real-browser acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {installObserver,canonical,rejectionCases,gateNames}=require('./solo-dps-feral-browser-support.cjs');
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
 await test('every-planned-gate-is-asserted-once',()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');const marks=[...source.matchAll(/mark\('([^']+)'/g)].map(x=>x[1]);assert.equal(gateNames.length,34);assert.deepEqual(marks.sort(),[...gateNames].sort());assert.equal(new Set(marks).size,marks.length);
  assert.ok(source.includes('createBrowserClick'));assert.ok(source.includes('createRegionCapture'));assert.ok(source.includes('DOM.setFileInputFiles'));assert.ok(source.includes("viewport(390,844,true)"));assert.ok(source.includes("setInput('#feralPreTalentCatAttackPower','',false)"));
  assert.equal(/dispatchEvent\s*\(|\.click\s*\(|Fetch\.fulfillRequest|new Response\s*\(/.test(source),false,'No synthetic successful response/input or renderer injection');
  assert.ok(source.includes("holdResponse('/api/forever/talents/druid/feral/validate-itemized')"));
 });
 console.log('Feral browser support pure contracts passed: '+checks+' groups; 34 planned browser gates and 40 rejection fixtures. No browser or API executed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
