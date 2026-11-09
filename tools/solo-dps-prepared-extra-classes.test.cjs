"use strict";
// HTTP evidence for existing class inputs. Synthetic budgets are not live-game calibration.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const BASE = process.env.CP174_BASE_URL || "http://127.0.0.1:5142";
const baseUrl = new URL(BASE);
assert.ok(baseUrl.protocol === "http:" && ["127.0.0.1", "localhost", "[::1]"].includes(baseUrl.hostname),
  "Acceptance API must be HTTP loopback-only");
assert.ok(!baseUrl.username && !baseUrl.password && !baseUrl.search && !baseUrl.hash);
const root = path.resolve(process.env.CP174_REPORT_DIR || "acceptance-prepared-extra-classes");
const sourceCommit = process.env.CP174_SOURCE_COMMIT || "not-stamped-local-build";
fs.mkdirSync(root, {recursive:true});
const coverage = [];
let requests = 0;
async function defaults(route, query = "") {
  const result = await fetch(BASE + "/api/sim/" + route + "/defaults" + query,
    {redirect:"error", signal:AbortSignal.timeout(10000)});
  assert.equal(result.status, 200);
  const input = await result.json();
  input.durationSeconds = 20;
  return input;
}
async function run(route, label, input, expected = 200) {
  const before = JSON.stringify(input);
  const result = await fetch(BASE + "/api/sim/" + route + "/run", {
    method:"POST", headers:{"content-type":"application/json"}, body:before,
    redirect:"error", signal:AbortSignal.timeout(30000)
  });
  const response = await result.json();
  requests++;
  fs.writeFileSync(path.join(root, label + ".json"), JSON.stringify({
    schema:"warcraftsim.prepared-extra-class-http-acceptance-v1", sourceCommit,
    scope:"software acceptance; authored and synthetic inputs are not source certification",
    request:input, status:result.status, response
  }, null, 2));
  assert.equal(result.status, expected, label + ": " + JSON.stringify(response));
  assert.equal(JSON.stringify(input), before);
  return response;
}
function syntheticBaseline(intellect = 100) {
  return {race:"human", classKey:"priest", strength:40, agility:40, stamina:100,
    intellect, spirit:100, clientBuild:"controlled-test-build",
    sourceReference:"synthetic-http-test-observation-not-live-game-data",
    recordedAtUtc:"2026-11-09T00:00:00Z"};
}
function authored(input, values) {
  input.character.equipment = {items:[{slotKey:"chest", itemKey:"authored-test-budget",
    stats:{values}}]};
}
async function shadowInput(intellect = 100) {
  const input = await defaults("shadow-priest");
  input.captureTimeline = true;
  input.baseSpellHitPercent = 100;
  input.spellCritOverridePercent = 0;
  input.character.race = "human";
  input.level60Baseline = syntheticBaseline(intellect);
  return input;
}
async function rogue() {
  for (const spec of ["assassination", "combat", "subtlety"]) {
    const input = await defaults("rogue", "?spec=" + spec);
    const legacy = await run("rogue", "rogue-" + spec + "-legacy", input);
    assert.ok(legacy.damageDone > 0);
    input.character.race = "human";
    input.useProvisionalLevel60Baseline = true;
    const result = await run("rogue", "rogue-" + spec + "-prepared", input);
    assert.ok(result.maximumHealth > 0 && result.damageDone > 0);
    assert.equal(result.maximumEnergy, legacy.maximumEnergy);
    assert.ok(result.modelingNotes.some(note => /NOT Forever-verified|provisional/i.test(note)));
    const replay = await run("rogue", "rogue-" + spec + "-replay", input);
    assert.deepEqual(replay.run.summary, result.run.summary);
    for (const [label, equipment] of [
      ["null-equipment", null], ["null-items", {items:null}], ["null-entry", {items:[null]}],
      ["catalog", {items:[{itemId:19102, slotKey:"main-hand"}]}]
    ]) {
      const bad = structuredClone(input);
      bad.character.equipment = equipment;
      await run("rogue", "rogue-" + spec + "-" + label + "-rejected", bad, 400);
    }
    coverage.push({route:"rogue", spec, replay:true, legacyEnergyPreserved:true,
      rejected:["null-equipment", "null-items", "null-entry", "catalog"]});
  }
}
async function shadow() {
  const legacy = await run("shadow-priest", "shadow-legacy", await defaults("shadow-priest"));
  assert.equal(legacy.maximumMana, 3946);
  for (const observed of [false, true]) {
    const input = await shadowInput();
    if (observed) {
      input.level60Baseline.observedUnbuffedMaximumMana = 4000;
      input.level60Baseline.observedUnbuffedMaximumHealth = 3000;
    }
    authored(input, {intellect:10, mana:100, stamina:10, health:50, "spell-power":40});
    const label = observed ? "observed" : "manual";
    const result = await run("shadow-priest", "shadow-" + label, input);
    assert.equal(result.maximumMana, observed ? 4250 : 2846);
    assert.equal(result.maximumHealth, observed ? 3150 : 3650);
    assert.equal(result.spellPower, 40);
    assert.equal(result.run.summary.actorSummaries.priest.startingResources.mana, result.maximumMana);
    assert.ok(result.modelingNotes.some(note => note.includes("not server attestation")));
    const replay = await run("shadow-priest", "shadow-" + label + "-replay", input);
    assert.deepEqual(replay.run.summary, result.run.summary);
    coverage.push({route:"shadow-priest", syntheticOnly:true, resources:label,
      mana:result.maximumMana, health:result.maximumHealth, replay:true});
  }
  for (const [naked, delta, expected] of [[10,15,2055], [10,5,2005], [30,-15,1905]]) {
    const input = await shadowInput(naked);
    input.statAssumptions.manaPerIntellectAboveTwenty = 9;
    input.level60Baseline.observedUnbuffedMaximumMana = 2000;
    authored(input, {intellect:delta});
    const result = await run("shadow-priest", "shadow-first-twenty-" + naked + "-" + delta, input);
    assert.equal(result.maximumMana, expected);
  }
  const edge = await shadowInput(10);
  edge.level60Baseline.observedUnbuffedMaximumMana = 4000;
  edge.statAssumptions.baseMana = 0;
  authored(edge, {mana:-100});
  const observed = await run("shadow-priest", "shadow-valid-observed-unused-negative-fallback", edge);
  assert.equal(observed.maximumMana, 3900);
  edge.level60Baseline.observedUnbuffedMaximumMana = null;
  await run("shadow-priest", "shadow-invalid-fallback-rejected", edge, 400);
  const execute = await shadowInput();
  execute.targetInitialHealthPercent = 19;
  const result = await run("shadow-priest", "shadow-initial-execute-health", execute);
  assert.ok(result.run.timeline.some(event => event.abilityKey === "shadow-word-death"));
  assert.ok(result.bossEndingHealthPercent < 19);
  const replay = await run("shadow-priest", "shadow-initial-execute-replay", execute);
  assert.deepEqual(replay.run.summary, result.run.summary);
  const malformed = await shadowInput();
  malformed.talents.ranks = null;
  await run("shadow-priest", "shadow-null-talents-rejected", malformed, 400);
  const catalog = await shadowInput();
  catalog.character.equipment = {items:[{itemId:19102, slotKey:"main-hand"}]};
  await run("shadow-priest", "shadow-catalog-rejected", catalog, 400);
  coverage.push({route:"shadow-priest", syntheticOnly:true, legacyMana:3946,
    firstTwentyIntellect:true, observedManaBeforeValidation:3900,
    initialExecuteHealthPreserved:true, malformedTalentsRejected:true, catalogRejected:true});
}
async function enhancement() {
  const input = await defaults("enhancement-shaman");
  const baseline = await run("enhancement-shaman", "enhancement-valid-legacy", input);
  assert.ok(baseline.damageDone > 0);
  for (const cost of ["earthShockMana", "lightningBoltMana"]) {
    for (const enabled of [false, true]) {
      const bad = structuredClone(input);
      bad.tuning[cost] = -1;
      bad.useEarthShock = enabled;
      bad.useMaelstromLightningBolt = enabled;
      bad.useLightningBoltFiller = enabled;
      await run("enhancement-shaman", "enhancement-" + cost + "-" + enabled + "-rejected", bad, 400);
    }
  }
  input.talents.ranks = null;
  await run("enhancement-shaman", "enhancement-null-ranks-rejected", input, 400);
  coverage.push({route:"enhancement-shaman", legacy:true, negativeCostsRejectedWithDisabledToggles:true,
    nullRanksRejected:true});
}
async function main() {
  await rogue();
  await shadow();
  await enhancement();
  fs.writeFileSync(path.join(root, "coverage.json"), JSON.stringify({sourceCommit, requests, coverage}, null, 2));
  console.log("Prepared extra-class HTTP acceptance passed: " + requests +
    " requests; Rogue replay and rejection, Shadow synthetic resource/execute oracles, Enhancement invalid-input rejection.");
}
main().catch(error => {console.error(error); process.exitCode = 1;});
