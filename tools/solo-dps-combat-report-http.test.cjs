'use strict';
// Actual HTTP execution through the production Combat serializers and report
// projection. This exercises parsed JSON values, not arbitrary Decimal tokens.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-combat-contract.test.cjs');
const comparison=require('../WarcraftSim.Api/wwwroot/solo-dps-comparison.js'),identity=require('../WarcraftSim.Api/wwwroot/solo-dps-combat-identity.js');
(async()=>{
 const legacyOnly=process.argv.includes('--legacy-only');
 const base=process.env.CP174_BASE_URL||'http://127.0.0.1:5269',url=new URL(base);assert.ok(url.protocol==='http:'&&['localhost','127.0.0.1','[::1]'].includes(url.hostname));
 const root=process.env.CP174_REPORT_DIR||'/tmp/combat-rendered-report';fs.mkdirSync(root,{recursive:true});const out=fs.mkdtempSync(path.join(root,'rendered-'));const index=[];
 async function call(name,route,body){if(body)fs.writeFileSync(path.join(out,name+'.request.json'),JSON.stringify(body));const r=await fetch(base+route,body?{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}:undefined),text=await r.text();fs.writeFileSync(path.join(out,name+'.response.json'),text);index.push({name,route,status:r.status});fs.writeFileSync(path.join(out,'index.json'),JSON.stringify(index,null,2));assert.equal(r.status,200,name+': '+text.slice(0,500));return JSON.parse(text);}
 const {c,elements}=environment();c.request=await call('defaults','/api/sim/rogue/defaults?spec=combat');const preset=await call('preset','/api/forever/talents/rogue/combat/default-itemized');c.talentCatalog=(await call('catalog','/api/forever/talents/rogue/combat')).talents;
 const capability=await call('capability','/api/forever/shared/capabilities/rogue?specialization=combat');c.capability=capability;vm.runInContext('combatCapabilityNotes=clone(capability.knownRestrictions||[]);',c);
 c.request.talents={assassinationPointsSpent:0,combatPointsSpent:31,subtletyPointsSpent:0,ranks:preset.ranks};vm.runInContext('Object.assign(request,combatRogueFixedSettings);restoreRogueSettings(Object.fromEntries([...rogueNumericKeys,...rogueBooleanKeys,...roguePoisonKeys,...Object.keys(rogueFixedSettings),"tuning"].map(key=>[key,request[key]])),"combat");',c);
 vm.runInContext(part('function buildCharacter(){','function body(){')+part('const talentTrees=','async function validateTalentSelection()')+part('async function validateGear(){','// The stat snapshot'),c);
 elements.duration.value='12';elements.rogueStartingEnergyPercent.value='25';
 const reports=[];for(const points of legacyOnly?[]:[31,30]){
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
 if(reports.length===2)assert.equal(comparison.compareReports(...reports).comparable,true);
 // These inputs predate the opt-in class-wide DTO additions. Keep their saved
 // bytes/values intact and execute them under the CURRENT engine; importing an
 // old build is not a claim of cross-source-version report replay equivalence.
 const oldBuilds=[
  {specialization:'combat',file:'fixtures/rogue-legacy-compat/legacy-combat-build.json',build:JSON.parse(fs.readFileSync(path.join(__dirname,'fixtures/rogue-legacy-compat/legacy-combat-build.json'),'utf8'))},
  {specialization:'assassination',file:'fixtures/assassination-reports/legacy-tuning-report.json',build:require('./fixtures/assassination-reports/legacy-tuning-report.json').build}
 ];
 const legacy=[];
 for(const witness of oldBuilds){
  const {environment:loadingEnvironment}=require('./solo-dps-loading-contract.test.cjs');
  const e=loadingEnvironment(),context=e.c,label='legacy-saved-'+witness.specialization;
  const input=plain(witness.build),before=plain(input),inactive='classWideMutilateBonusDamagePerHand';
  assert.equal(Object.hasOwn(input.encounter.rogue.tuning,inactive),false,'Genuine older fixture must omit the new override');
  context.races=(await call(label+'-races','/api/forever/character/races')).races;
  // Retain the existing production loader/import wrapper chain. Only its
  // transport is replaced with real local HTTP, and its DOM is the existing
  // offline control harness. Native file selection is covered separately.
  context.fetch=async(route,options)=>{
   const name=label+'-import-'+index.length,request=options?.body?JSON.parse(options.body):undefined;
   if(request!==undefined)fs.writeFileSync(path.join(out,name+'.request.json'),JSON.stringify(request));
   const result=await fetch(base+route,{...options,signal:AbortSignal.timeout(30000)});
   fs.writeFileSync(path.join(out,name+'.response.json'),await result.clone().text());
   index.push({name,route,status:result.status});fs.writeFileSync(path.join(out,'index.json'),JSON.stringify(index,null,2));
   assert.equal(result.status,200,name);return result;
  };
  context.validateRemovedPriorityTargets=()=>{};
  for(const id of ['classKey','spec','race','rotation','petFamily','demonType','rogueMainHandPoison','rogueOffHandPoison'])e.elements[id].tagName='SELECT';
  for(const id of ['seed','duration','targetHealth','iterations','rogueTargetArmor','rogueMainHandWeaponDamage','rogueMainHandSpeedSeconds'])e.elements[id].tagName='INPUT';
  vm.runInContext([
   part('function buildCharacter(){','function body(){'),part('function body(){','// Strict historical/manual fixed-Cat contract.'),
   part('function readWarriorAssumptions(){','function restoreWarriorAssumptions()'),part('const legacyBody=body;','const legacyLoadPreset='),
   part('const originalSoloBody=body;','const originalSoloSave='),part('const previousConditionalBody=body;','const previousConditionalDraw='),
   part('const baseAddBody=body;','const priorOrGroupDraw='),part('const talentTrees=','async function validateTalentSelection()'),
   part('async function validateGear(){','// The stat snapshot'),part('function replaySignature(result){','async function replayLastResult(){')
  ].join('\n'),context);
  await context.applyConfig(input);
  assert.deepEqual(input,before,'Import cannot rewrite its input');
  assert.deepEqual(plain(context.saveConfig()),before,'Every saved field, including omitted tuning and maintained modifiers, survives import');
  assert.equal(Object.hasOwn(context.request.tuning,inactive),false,'Only the outgoing copy may materialize the wire default');
  const submitted=plain(context.body()),saved=plain(context.saveConfig());
  assert.deepEqual(plain(context.saveConfig()),saved,'Serializing a current request cannot change the saved build');
  const resolved=await call(label+'-preview','/api/forever/character/preview-rogue',submitted.rogue),metadata=await call(label+'-metadata','/api/forever/character/report-metadata');
  const response=await call(label+'-run','/api/forever/character/simulate-level60',submitted);
  if(witness.specialization==='combat'){
   assert.deepEqual(response.preparation.request,submitted.rogue,'Current server preparation retains the exact current outgoing request');
   assert.deepEqual(response.preparation.preview,resolved);
  }else{
   assert.equal(Object.hasOwn(response,'preparation'),false,'Legacy Assassination has no owned preparation receipt');
   for(const key of ['maximumEnergy','maximumHealth'])assert.equal(response.sim[key],resolved[key]);
   assert.equal(response.sim.run.summary.actorSummaries.rogue.startingResources.energy,resolved.startingEnergy);
  }
  assert.deepEqual(submitted.rogue.tuning,{...before.encounter.rogue.tuning,[inactive]:null});
  Object.assign(context,{b:submitted,build:saved,response,data:response.sim,metadata,runItemizedMode:true,runTalents:plain(context.request.talents),runCatalog:plain(context.talentCatalog),batch:null,gearValidation:await context.validateGear(),resolved});
  const expression=part(' results={',' const sim=response.sim||{},summary=sim.run?.summary||{};').trim().replace(/^results=/,'').replace(/;$/,'');
  const report=plain(vm.runInContext('('+expression+')',context)),reportFile=path.join(out,label+'-report.json');
  fs.writeFileSync(reportFile,JSON.stringify(report,null,2));
  const imported=JSON.parse(fs.readFileSync(reportFile,'utf8'));
  assert.deepEqual(imported,report);assert.deepEqual(imported.build,before);assert.deepEqual(imported.rawServerResponse,response);
  assert.equal(comparison.inspectReport(imported).integrityWarnings.length,0);
  assert.equal(comparison.compareReports(report,imported).comparable,true);
  await context.applyConfig(imported.build);
  assert.deepEqual(plain(context.saveConfig()),before,'Report-embedded build reimport preserves the genuine older build');
  const replay=await call(label+'-current-replay','/api/forever/character/simulate-level60',imported.originalSimulationRequest);
  assert.equal(identity.signature(replay),identity.signature(response),'Complete current parsed response replay uses the unchanged strict identity policy');
  if(witness.specialization==='assassination')assert.equal(context.replaySignature(replay),context.replaySignature(response),'Actual legacy Assassination replay projection agrees');
  legacy.push({specialization:witness.specialization,fixture:witness.file,report:path.basename(reportFile),savedBuildPreserved:true,outgoingNullOnly:true,exactOwnedRequest:witness.specialization==='combat'?true:null,previewResourceParity:true,reimported:true,currentEngineCompleteReplay:true,sourceCommit:metadata.sourceCommit});
 }
 fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({passed:true,legacyOnly,requests:index.length,reports:reports.length+legacy.length,fullParsedEnvelopeReplays:reports.length+legacy.length,productionSerializer:true,productionProjection:true,productionImportWrappers:true,legacy,out},null,2));
 console.log('Actual Rogue production request/report HTTP integration passed: '+(legacyOnly?'':'31/30, ')+'genuine old Combat/Assassination imports, unchanged saves, current wire/preparation equality, report reimport and complete current-engine replay. '+out);
})().catch(e=>{console.error(e);process.exitCode=1;});
