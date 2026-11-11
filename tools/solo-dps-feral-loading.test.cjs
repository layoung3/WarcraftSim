'use strict';
// Execute current loading/import/readiness and talent-edit wrappers, with held
// response boundaries. Browser behavior is separately gated in Actions.
const assert=require('node:assert/strict'),vm=require('node:vm');
const {environment,part,plain}=require('./solo-dps-loading-contract.test.cjs');
const {build}=require('./solo-dps-feral-fixture.cjs');const {preparation}=require('./solo-dps-feral-contract.test.cjs');
function env(){
 const e=environment();
 vm.runInContext(part('const talentTrees=','async function validateTalentSelection()')+part('async function preview(refreshToken){','async function simulate(){'),e.c);
 e.responses.set('/api/forever/character/preview-feral',request=>preparation(request));
 vm.runInContext(part('// Native input invalidates immediately,','const beforePreparedImport=applyConfig;'),e.c);
 e.elements.classKey.value='druid';e.elements.spec.value='feral';e.elements.race.value='night-elf';
 return e;
}
(async()=>{
 const example=JSON.parse(require('node:fs').readFileSync('docs/acceptance/cp175/cp175-feral-historical-manual-cat.json','utf8'));const imported=env();await imported.c.loadPreset();await imported.c.applyConfig(example);assert.deepEqual(plain(imported.c.saveConfig()),example,'Checked-in illustrative fixture passes actual import wrapper path');
 const e=env();e.c.cfg.effects.buffs=['stale-balance-buff'];await e.c.loadPreset();assert.deepEqual(plain(e.c.cfg.effects.buffs),[]);
 assert.equal(e.c.loadedSpec,'feral');assert.equal(e.elements.feralPreTalentCatAttackPower.value,'');assert.equal(e.elements.provisional.checked,false);assert.equal(e.elements.status.dataset.buildState,'input-required');
 for(const id of ['run','save','exportBuild'])assert.equal(e.elements[id].disabled,true);assert.equal(e.elements.provisional.disabled,false);assert.equal(e.panels['.workspace'].inert,false);
 assert.equal(e.calls.some(x=>x.url==='/api/sim/feral-druid/defaults'),false);assert.equal(e.calls.some(x=>x.url.includes('simulate')),false);
 e.elements.feralPreTalentCatAttackPower.value='0';e.elements.provisional.checked=true;await e.c.checkBaseline();await e.c.preview();
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.run.disabled,false);assert.equal(e.elements.status.dataset.buildState,'ready');
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.feralPreTalentCatAttackPower.listeners.input();assert.throws(()=>e.c.requirePreparedBuild(),/manual/);e.c.invalidateFeralPreview();assert.equal(e.elements.run.disabled,true);assert.equal(e.elements.provisional.disabled,false);
 e.elements.feralPreTalentCatAttackPower.value='800';await e.c.preview();const valid=plain(e.c.saveConfig());
 // A fresh request changes state while an older result is delayed.
 const held=e.hold('/api/forever/character/preview-feral');const old=e.c.preview();await held.started;e.elements.feralPreTalentCatAttackPower.value='900';e.c.invalidateFeralPreview();await e.c.preview();held.release();await old;
 assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.stats.children.find(row=>row.children[0].textContent==='Manual pre-talent Cat AP').children[1].textContent,'900');
 // Exact raw invalid AP/shift/seed/checkbox draft and dirty state survive a
 // backend-only rank rejection after class/preset loading has started.
 e.elements.feralPreTalentCatAttackPower.value='';e.elements.feralShapeshiftMana.value='-3';e.elements.seed.value='1.5';e.elements.feralUseFaerieFire.checked=true;e.elements.provisional.checked=false;e.c.buildDirty=true;
 const raw=Object.fromEntries(['feralPreTalentCatAttackPower','feralShapeshiftMana','seed','feralUseFaerieFire','provisional'].map(id=>[id,{value:e.elements[id].value,checked:e.elements[id].checked}]));
 const bad=plain(valid);bad.talents.ranks.ferocity=6;bad.talents.feralPointsSpent++;bad.talents.totalPoints++;bad.talents.unspentPoints--;

 await assert.rejects(()=>e.c.applyConfig(bad),/Controlled Feral rejected rank/);
 for(const [id,value] of Object.entries(raw)){assert.equal(e.elements[id].value,value.value,id);assert.equal(e.elements[id].checked,value.checked,id);}
 assert.equal(e.c.buildDirty,true);assert.equal(e.c.loadedSpec,'feral');assert.equal(e.elements.run.disabled,true);assert.equal(e.panels['.workspace'].inert,false);
 await e.c.applyConfig(valid);assert.deepEqual(plain(e.c.saveConfig()),valid);assert.equal(e.c.feralPreviewMatches(),true);assert.equal(e.elements.run.disabled,false);
 // A rejected imported declaration is not silently recomputed.
 const mismatch=plain(valid);mismatch.talents.feralPointsSpent--;mismatch.talents.balancePointsSpent++;
 await assert.rejects(()=>e.c.applyConfig(mismatch),/catalog-derived/);assert.deepEqual(plain(e.c.saveConfig()),valid);
 // Old Feral source-coverage responses cannot publish over same-class Balance.
 const delayed=e.hold('/api/forever/talents/source-coverage');const first=e.c.loadPreset();await delayed.started;e.elements.spec.value='balance';await e.c.loadPreset();delayed.release();await first;
 assert.equal(e.c.loadedSpec,'balance');assert.equal(e.elements.feralSettings.hidden,true);assert.equal(e.elements.priorityEditor.hidden,false);
 // Cross-class rollback restores not only values but availability and labels.
 e.elements.classKey.value='mage';e.elements.spec.value='arcane';await e.c.loadPreset();e.c.buildDirty=true;
 const labels=['classCapabilityNotes','rotationSubtitle','catalogCoverage','talentCoverage'].map(id=>[id,e.elements[id].textContent]);const availability=['mageSettings','priorityEditor','iterations','racialActive'].map(id=>[id,e.elements[id].hidden,e.elements[id].disabled]);
 e.failures.set('/api/forever/talents/druid/feral/validate-itemized','Controlled imported Feral failure');await assert.rejects(()=>e.c.applyConfig(bad));
 assert.equal(e.c.loadedClass,'mage');assert.equal(e.c.buildDirty,true);for(const [id,hidden,disabled] of availability){assert.equal(e.elements[id].hidden,hidden,id);assert.equal(e.elements[id].disabled,disabled,id);}for(const [id,text] of labels)assert.equal(e.elements[id].textContent,text,id);
 // Hold preview inside the real run lock, and exercise its unlock callback.
 const locked=env();await locked.c.loadPreset();locked.elements.provisional.checked=true;locked.elements.feralPreTalentCatAttackPower.value='800';await locked.c.preview();
 let release;const pending=new Promise(r=>release=r);locked.c.beforeSimulationLock=async()=>{}; // lexical wrapper is exercised below in an isolated actual-source closure.
 vm.runInContext('{simulate=async function(){await preview();await holdRun;};'+part('const beforeSimulationLock=simulate;','function safe(task)')+'}',Object.assign(locked.c,{holdRun:pending}));
 locked.elements.statStatus.textContent='Awaiting preflight';const run=locked.c.simulate();for(let n=0;n<30&&locked.elements.statStatus.textContent!=='Up to date';n++)await Promise.resolve();assert.equal(locked.elements.statStatus.textContent,'Up to date');assert.equal(locked.elements.run.disabled,true);assert.equal(locked.elements.classKey.disabled,true);assert.equal(locked.elements.provisional.disabled,true);release();await run;assert.equal(locked.elements.run.disabled,false);
 // Actual rank closure: stale failure after reset or identity switch cannot
 // restore old totals; failed increments preserve an explicitly present zero.
 const talent=env();await talent.c.loadPreset();
 vm.runInContext('function renderTalents(){const talents=request.talents;'+part(' let talentEditPending=false;',' function eligibility(t){')+'lastRenderedRankHandler=setRank;}',talent.c);
 talent.c.document.querySelectorAll=()=>[];talent.c.renderTalents();talent.c.validateTalentSelection=async()=>{};await talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},-5);assert.equal(Object.hasOwn(talent.c.request.talents.ranks,'furor'),true);assert.equal(talent.c.request.talents.ranks.furor,0);assert.equal(talent.c.request.talents.unspentPoints,20);
 talent.c.request.talents.ranks.furor=0;talent.c.recalculateTalentTotals();
 let releaseTalent;talent.c.validateTalentSelection=()=>new Promise((_,reject)=>releaseTalent=()=>reject(Error('controlled talent rejection')));
 const failed=talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},5);talent.c.renderTalents();await talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},-5);assert.equal(talent.c.request.talents.ranks.furor,5,'Re-render cannot bypass pending talent edit');releaseTalent();await failed;assert.equal(Object.hasOwn(talent.c.request.talents.ranks,'furor'),true);assert.equal(talent.c.request.talents.ranks.furor,0);
 const resetting=talent.c.lastRenderedRankHandler({key:'furor',maxRank:5},5);talent.c.request.talents.ranks={};talent.c.recalculateTalentTotals();talent.c.renderTalents();releaseTalent();await resetting;assert.equal(talent.c.request.talents.totalPoints,0);assert.deepEqual(plain(talent.c.request.talents.ranks),{});
 const switching=talent.c.lastRenderedRankHandler({key:'ferocity',maxRank:5},1);talent.c.generation++;talent.c.request={talents:{ranks:{'natures-grace':1}}};releaseTalent();await switching;assert.deepEqual(plain(talent.c.request.talents.ranks),{'natures-grace':1});
 console.log('Feral actual loading/import readiness passed: blank-to-zero recovery, stale exact-request preview, invalid raw-draft rollback, valid import recovery, declared-total rejection, Balance/Feral race, cross-class panel availability and simulation lock after preview.');
})().catch(error=>{console.error(error);process.exitCode=1;});
