'use strict';
// Actual HTTP execution through the production Combat serializers and report
// projection. This exercises parsed JSON values, not arbitrary Decimal tokens.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-combat-contract.test.cjs');
const comparison=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js'),identity=require('../WarcraftSim.Api/wwwroot/solo-dps-combat-identity.js');
(async()=>{
 const base=process.env.CP174_BASE_URL||'http://127.0.0.1:5269',url=new URL(base);assert.ok(url.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(url.hostname));
 const root=process.env.CP174_REPORT_DIR||'/tmp/combat-rendered-report';fs.mkdirSync(root,{recursive:true});const out=fs.mkdtempSync(path.join(root,'rendered-'));const index=[];
 async function call(name,route,body){if(body)fs.writeFileSync(path.join(out,name+'.request.json'),JSON.stringify(body));const r=await fetch(base+route,body?{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}:undefined),text=await r.text();fs.writeFileSync(path.join(out,name+'.response.json'),text);index.push({name,route,status:r.status});fs.writeFileSync(path.join(out,'index.json'),JSON.stringify(index,null,2));assert.equal(r.status,200,name+': '+text.slice(0,500));return JSON.parse(text);}
 const {c,elements}=environment();c.request=await call('defaults','/api/sim/rogue/defaults?spec=combat');const preset=await call('preset','/api/forever/talents/rogue/combat/default-itemized');c.talentCatalog=(await call('catalog','/api/forever/talents/rogue/combat')).talents;
 const capability=await call('capability','/api/forever/shared/capabilities/rogue?specialization=combat');c.capability=capability;vm.runInContext('combatCapabilityNotes=clone(capability.knownRestrictions||[]);',c);
 c.request.talents={assassinationPointsSpent:0,combatPointsSpent:31,subtletyPointsSpent:0,ranks:preset.ranks};vm.runInContext('Object.assign(request,combatRogueFixedSettings);restoreRogueSettings(Object.fromEntries([...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueFixedSettings),"tuning"].map(key=>[key,request[key]])),"combat");',c);
 vm.runInContext(part('function buildCharacter(){','function body(){')+part('const talentTrees=','async function validateTalentSelection()')+part('async function validateGear(){','// The stat snapshot'),c);
 elements.duration.value='12';elements.rogueStartingEnergyPercent.value='25';
 const reports=[];for(const points of [31,30]){
  if(points===30){delete c.request.talents.ranks['adrenaline-rush'];c.recalculateTalentTotals();}
  const build=plain(c.saveConfig()),b=plain(c.body());assert.equal(b.rogue.character.id,c.request.character.id);assert.deepEqual(b.rogue.character.baseStats,{values:{}});
  await call(points+'-strict-ranks','/api/forever/talents/rogue/combat/validate-itemized',c.request.talents);
  const resolved=await call(points+'-preview','/api/forever/character/preview-rogue',b.rogue),metadata=await call(points+'-metadata','/api/forever/character/report-metadata'),response=await call(points+'-run','/api/forever/character/simulate-level60',b);
  assert.deepEqual(response.preparation.request,b.rogue);assert.deepEqual(response.preparation.preview,resolved);
  Object.assign(c,{b,build,response,data:response.sim,metadata,runItemizedMode:true,runTalents:plain(c.request.talents),runCatalog:plain(c.talentCatalog),batch:null,gearValidation:await c.validateGear(),resolved});
  const expression=part(' results={',' const sim=response.sim||{},summary=sim.run?.summary||{};').trim().replace(/^results=/,'').replace(/;$/,'');const report=plain(vm.runInContext('('+expression+')',c));fs.writeFileSync(path.join(out,points+'-report.json'),JSON.stringify(report,null,2));
  assert.deepEqual(report.rawServerResponse,response);assert.equal(comparison.inspectReport(report).integrityWarnings.length,0);
  const replay=await call(points+'-replay','/api/forever/character/simulate-level60',report.originalSimulationRequest);assert.equal(identity.signature(replay),identity.signature(response));
  reports.push(report);
 }
 assert.equal(comparison.compareReports(...reports).comparable,true);
 fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({passed:true,requests:index.length,reports:2,fullParsedEnvelopeReplays:2,productionSerializer:true,productionProjection:true,out},null,2));
 console.log('Actual Combat production request/report HTTP integration passed: 31/30, owned flat preview, complete parsed replay, strict inspector and comparison. '+out);
})().catch(e=>{console.error(e);process.exitCode=1;});
