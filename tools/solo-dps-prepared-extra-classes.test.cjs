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
async function run(route, label, input, expected = 200, requestPath = "/api/sim/" + route + "/run") {
  const before = JSON.stringify(input);
  const result = await fetch(BASE + requestPath, {
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
  async function prepared(intellect = 100) {
    const request = await defaults("enhancement-shaman");
    request.character.race = "orc";
    request.level60Baseline = {race:"orc", classKey:"shaman", strength:100, agility:100,
      stamina:100, intellect, spirit:100, clientBuild:"synthetic",
      sourceReference:"synthetic-http-test-observation-not-live-game-data",
      recordedAtUtc:"2026-11-09T00:00:00Z"};
    return request;
  }
  for (const observed of [false, true]) {
    const request = await prepared();
    request.level60Baseline.observedUnbuffedMeleeAttackPower = 700;
    if (observed) {
      request.level60Baseline.observedUnbuffedMaximumMana = 2000;
      request.level60Baseline.observedUnbuffedMaximumHealth = 3000;
    }
    authored(request, {strength:10, intellect:2, stamina:3, mana:4, health:5, "spell-power":6});
    request.character.talents = {selections:[{talentKey:"authored-test-budget", stats:{values:{intellect:4, mana:5}}}]};
    request.character.buffs = {selections:[{buffKey:"authored-test-budget", stats:{values:{intellect:6, mana:7}}}]};
    const label = observed ? "observed" : "fallback";
    const result = await run("enhancement-shaman", "enhancement-prepared-" + label, request);
    // AP700 already includes naked primary stats: +20 Strength AP +112 Mental Dexterity.
    assert.equal(result.attackPower, 832);
    assert.equal(result.spellPower, 39.6); // 6 flat +112 Intellect *15% *rank2.
    assert.equal(result.maximumMana, observed ? 2196 : 2916); // Base1520 +92*15 +16 flat.
    assert.equal(result.maximumHealth, observed ? 3035 : 3535);
    assert.equal(result.endingHealth, result.maximumHealth);
    assert.ok(result.modelingNotes.some(note => note.includes(observed ? "Supplied naked Mana" : "client base Mana")));
    assert.ok(result.modelingNotes.some(note => note.includes(observed ? "Supplied naked Health" : "manual scenario cap")));
    assert.ok(result.modelingNotes.some(note => note.includes("synthetic-http-test-observation-not-live-game-data")));
    const replay = await run("enhancement-shaman", "enhancement-prepared-" + label + "-replay", request);
    assert.deepEqual(replay.run.summary, result.run.summary);
    coverage.push({route:"enhancement-shaman", syntheticOnly:true, resources:label,
      attackPower:832, spellPower:39.6, mana:result.maximumMana, health:result.maximumHealth, replay:true});
  }
  // Enhancement uses max(0, Intellect-20)*15, not the Priest first-20 contribution.
  for (const [naked, delta, expected] of [[10,5,2000], [10,15,2075], [30,-15,1850], [30,-40,1850], [100,-10,1850]]) {
    const request = await prepared(naked);
    request.level60Baseline.observedUnbuffedMaximumMana = 2000;
    request.character.buffs = {selections:[{buffKey:"authored-intellect-delta", stats:{values:{intellect:delta}}}]};
    const label = "enhancement-intellect-" + naked + "-" + delta;
    const result = await run("enhancement-shaman", label + "-observed", request);
    assert.equal(result.maximumMana, expected);
    request.level60Baseline.observedUnbuffedMaximumMana = null;
    const withDelta = await run("enhancement-shaman", label + "-fallback-delta", request);
    request.character.buffs.selections = [];
    const nakedFallback = await run("enhancement-shaman", label + "-fallback-naked", request);
    assert.equal(withDelta.maximumMana - nakedFallback.maximumMana, expected - 2000);
  }
  const provisional = await prepared();
  provisional.level60Baseline = null;
  provisional.useProvisionalLevel60Baseline = true;
  const provisionalResult = await run("enhancement-shaman", "enhancement-provisional-disclosure", provisional);
  assert.ok(provisionalResult.modelingNotes.some(note => note.includes("NOT verified for Forever")));
  for (const [label, item] of [
    ["catalog", {itemId:19102}], ["enchant", {enchantSpellId:20034}],
    ["temporary-enchant", {temporaryEnchantSpellId:25122}]
  ]) {
    const request = await prepared();
    request.character.equipment = {items:[{slotKey:"chest", itemKey:"unsupported-test-selection", ...item}]};
    await run("enhancement-shaman", "enhancement-prepared-" + label + "-rejected", request, 400);
  }
  for (const [label, invalidate] of [
    ["null-equipment", request => {request.character.equipment = null;}],
    ["null-items", request => {request.character.equipment = {items:null};}],
    ["null-entry", request => {request.character.equipment = {items:[null]};}],
    ["null-ranks", request => {request.talents.ranks = null;}],
    ["null-calls", request => {request.callOfSpiritsTotems = null;}],
    ["null-disabled-items", request => {request.combatItems.disabledItemKeys = null;}]
  ]) {
    const request = await prepared();
    invalidate(request);
    await run("enhancement-shaman", "enhancement-prepared-" + label + "-rejected", request, 400);
  }
  coverage.push({route:"enhancement-shaman", syntheticOnly:true, clampedIntellectObservedFallbackParity:true,
    provisionalDisclosure:true, catalogAndEnchantsRejected:true, malformedGraphRejected:true});
}
async function sharedCasterThresholds() {
  for (const [route, classKey, race] of [
    ["balance-druid", "druid", "night-elf"], ["elemental-shaman", "shaman", "orc"]
  ]) {
    for (const [naked, delta, expectedDelta] of [[10,5,0], [10,20,150], [30,-20,-150]]) {
      const request = await defaults(route);
      request.character.race = race;
      request.level60Baseline = {race, classKey, strength:100, agility:100, stamina:100,
        intellect:naked, spirit:100, observedUnbuffedMaximumMana:2000,
        clientBuild:"synthetic", sourceReference:"synthetic-http-threshold-not-live-game-data",
        recordedAtUtc:"2026-11-09T00:00:00Z"};
      request.talents = {ranks:{}};
      request.majorManaPotionCharges = 0;
      request.combatItems.enabled = false;
      request.character.equipment = {items:[]};
      request.character.talents = {selections:[]};
      request.character.buffs = {selections:[{buffKey:"authored-threshold-budget",
        stats:{values:{intellect:delta, mana:7}}}]};
      const label = route + "-threshold-" + naked + "-" + delta;
      const result = await run(route, label + "-observed", request);
      assert.equal(result.maximumMana, 2007 + expectedDelta);
      if (naked === 10 && delta === 5) {
        const replay = await run(route, label + "-replay", request);
        assert.deepEqual(replay.run.summary, result.run.summary);
      }
      request.level60Baseline.observedUnbuffedMaximumMana = null;
      const withDelta = await run(route, label + "-fallback-delta", request);
      request.character.buffs.selections = [];
      const nakedFallback = await run(route, label + "-fallback-naked", request);
      assert.equal(withDelta.maximumMana - nakedFallback.maximumMana, expectedDelta + 7);
    }
    coverage.push({route, syntheticOnly:true, clampedObservedFallbackManaParity:true,
      flatManaOnce:true, replay:true, formula:"15*max(0,Intellect-20); no Priest first-20 term"});
  }
}
async function historicalTypedDispatch() {
  const dispatchPath = "/api/forever/character/simulate-level60";
  const historicalCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
  const historicalStatus = "historical-provisional-not-current-client-certified";
  async function get(relative) {
    const result = await fetch(BASE + relative, {redirect:"error", signal:AbortSignal.timeout(10000)});
    assert.equal(result.status, 200);
    return result.json();
  }
  const discovery = await get("/api/forever/character/simulation-specs");
  assert.ok(!discovery.classes.some(row => ["druid", "shaman"].includes(row.classKey)),
    "This backend-only stage must not advertise new unified UI workflows");
  for (const [route, classKey, specialization, property, expectedPoints] of [
    ["balance-druid", "druid", "balance", "balance", 35],
    ["elemental-shaman", "shaman", "elemental", "elemental", 51]
  ]) {
    const typed = await defaults(route);
    typed.character.race = "tauren";
    typed.useProvisionalLevel60Baseline = true;
    typed.majorManaPotionCharges = 0;
    typed.combatItems.enabled = false;
    typed.character.equipment = {items:[]};
    const catalog = await get("/api/forever/talents/" + classKey + "/" + specialization);
    const trees = new Map(catalog.talents.map(row => [row.key, row.tree]));
    const totals = classKey === "druid" ?
      {balancePointsSpent:"balance", feralPointsSpent:"feral", restorationPointsSpent:"restoration"} :
      {elementalPointsSpent:"elemental", enhancementPointsSpent:"enhancement", restorationPointsSpent:"restoration"};
    function reconcile(allocation) {
      const byTree = {};
      for (const [key, rank] of Object.entries(allocation.ranks)) {
        assert.ok(trees.has(key), "Default fixture must use an existing reconciled source key: " + key);
        byTree[trees.get(key)] = (byTree[trees.get(key)] || 0) + rank;
      }
      for (const [name, tree] of Object.entries(totals)) allocation[name] = byTree[tree] || 0;
      return byTree;
    }
    const byTree = reconcile(typed.talents);
    const points = Object.values(typed.talents.ranks).reduce((sum, rank) => sum + rank, 0);
    assert.equal(points, expectedPoints, "Existing default ranks changed; review their sourced allocation");
    const validationPath = "/api/forever/talents/" + classKey + "/" + specialization + "/validate-itemized";
    const validated = await run(route, route + "-strict-default-allocation", typed.talents, 200, validationPath);
    assert.equal(validated.pointsSpent, points);
    assert.equal(validated.sourceStatus, historicalStatus);
    const input = {classKey, specialization, itemizedTalentMode:true,
      itemizedTalentRanks:structuredClone(typed.talents.ranks), [property]:typed};
    const result = await run(route, route + "-strict-typed", input, 200, dispatchPath);
    assert.equal(result.talentSourceStatus, historicalStatus);
    assert.equal(result.talentSourceCommit, historicalCommit);
    assert.match(JSON.stringify(result.baseline), /provisional/i);
    assert.ok(result.sim.damageDone > 0);
    const replay = await run(route, route + "-strict-typed-replay", input, 200, dispatchPath);
    // Correlation GUIDs are not seeded gameplay outcomes.
    for (const key of ["dps", "damageDone", "endingMana", "endingHealth", "damageByAbility", "combatItemUses"]) {
      assert.ok(Object.hasOwn(result.sim, key) && Object.hasOwn(replay.sim, key));
      assert.deepEqual(replay.sim[key], result.sim[key], route + " replay " + key);
    }
    if (classKey === "druid") {
      const sourcePath = "/default-talents/druid-balance.json";
      const asset = await get(sourcePath);
      assert.equal(asset.id, "druid-balance");
      const assetInput = structuredClone(input);
      Object.assign(assetInput.balance, structuredClone(asset.overrides));
      const assetTrees = reconcile(assetInput.balance.talents);
      assetInput.itemizedTalentRanks = structuredClone(assetInput.balance.talents.ranks);
      assert.equal(Object.values(assetInput.itemizedTalentRanks).reduce((sum, rank) => sum + rank, 0), 36);
      const allocation = await run(route, "balance-static-36-allocation", assetInput.balance.talents, 200, validationPath);
      assert.equal(allocation.pointsSpent, 36);
      const accepted = await run(route, "balance-static-36-typed", assetInput, 200, dispatchPath);
      assert.equal(accepted.talentSourceStatus, historicalStatus);
      assert.equal(accepted.talentSourceCommit, historicalCommit);
      const replayed = await run(route, "balance-static-36-replay", assetInput, 200, dispatchPath);
      for (const key of ["dps", "damageDone", "endingMana", "endingHealth", "damageByAbility", "combatItemUses"])
        assert.deepEqual(replayed.sim[key], accepted.sim[key]);
      fs.writeFileSync(path.join(root, "balance-existing-static-talent-asset.json"),
        JSON.stringify({sourceCommit, sourcePath, asset}, null, 2));
      coverage.push({route, historicalOnly:true, allocationSource:sourcePath,
        points:36, treePoints:assetTrees, namedTotalsDerivedWithoutChangingRanks:true, gameplayReplay:true});
    }
    const firstKey = Object.keys(typed.talents.ranks).find(key => typed.talents.ranks[key] > 0);
    for (const [label, invalidate] of [
      ["named-total", request => {request[property].talents[Object.keys(totals)[0]]++;}],
      ["typed-full-map-mismatch", request => {request.itemizedTalentRanks[firstKey]--;}],
      ["unmatched-source-key", request => {
        const key = classKey === "druid" ? "starlight-wrath" : "elemental-precision";
        request[property].talents.ranks[key] = 1;
        request.itemizedTalentRanks[key] = 1;
        if (points === 51) {
          // Keep total51 so the source-key gate, not the point ceiling, is exercised.
          const donor = Object.keys(typed.talents.ranks).find(name =>
            trees.get(name) === specialization && typed.talents.ranks[name] > 0);
          assert.ok(donor);
          request[property].talents.ranks[donor]--;
          request.itemizedTalentRanks[donor]--;
        } else request[property].talents[Object.keys(totals)[0]]++;
      }],
      ["missing-earlier-rows", request => {
        const ranks = {[classKey === "druid" ? "moonkin-form" : "elemental-mastery"]:1};
        request[property].talents.ranks = ranks;
        reconcile(request[property].talents);
        request.itemizedTalentRanks = structuredClone(ranks);
      }],
      ["missing-prerequisite-rank", request => {
        if (classKey === "druid") request[property].talents.ranks =
          {"improved-wrath":5, genesis:5, "natures-majesty":1, "natures-splendor":1};
        else request[property].talents.ranks["elemental-alacrity"] = 2;
        reconcile(request[property].talents);
        request.itemizedTalentRanks = structuredClone(request[property].talents.ranks);
      }],
      ["noncanonical-rank-key", request => {
        const rank = request[property].talents.ranks[firstKey];
        delete request[property].talents.ranks[firstKey];
        delete request.itemizedTalentRanks[firstKey];
        request[property].talents.ranks[firstKey.toUpperCase()] = rank;
        request.itemizedTalentRanks[firstKey.toUpperCase()] = rank;
      }],
      ["conflicting-typed-payloads", request => {request[property === "balance" ? "elemental" : "balance"] = {};}]
    ]) {
      const bad = structuredClone(input);
      invalidate(bad);
      await run(route, route + "-strict-" + label + "-rejected", bad, 400, dispatchPath);
    }
    coverage.push({route, historicalOnly:true, strictTypedDispatch:true, points, treePoints:byTree,
      sourceCommit:historicalCommit, sourceStatus:historicalStatus, gameplayReplay:true,
      namedTotalsAndMapsChecked:true, rowAndPrerequisiteChecks:true, noncanonicalKeysRejected:true,
      conflictingTypedPayloadsRejected:true, unifiedUiDiscoveryUnchanged:true});
  }
}
async function historicalShadowDispatch() {
  const route = "shadow-priest";
  const dispatchPath = "/api/forever/character/simulate-level60";
  const validationPath = "/api/forever/talents/priest/shadow/validate-itemized";
  const typed = await defaults(route);
  typed.character.race = "human";
  typed.useProvisionalLevel60Baseline = true;
  // Existing named ranks only; legacy unnamed 10/0/41 totals are not evidence.
  assert.equal(Object.values(typed.talents.ranks).reduce((sum, rank) => sum + rank, 0), 36);
  typed.talents.disciplinePointsSpent = 0;
  typed.talents.holyPointsSpent = 0;
  typed.talents.shadowPointsSpent = 36;
  const catalogResponse = await fetch(BASE + "/api/forever/talents/priest/shadow",
    {redirect:"error", signal:AbortSignal.timeout(10000)});
  assert.equal(catalogResponse.status, 200);
  const catalog = await catalogResponse.json();
  const penance = catalog.talents.find(row => row.key === "penance");
  assert.equal(penance.simulationStatus, "unreachable-in-restricted-shadow-allocation");
  assert.match(penance.knownLimitations, /not advertised/);
  const validation = await run(route, "shadow-strict-existing-36-allocation", typed.talents, 200, validationPath);
  assert.equal(validation.pointsSpent, 36);
  const input = {classKey:"priest", specialization:"shadow", itemizedTalentMode:true,
    itemizedTalentRanks:structuredClone(typed.talents.ranks), shadow:typed};
  const accepted = await run(route, "shadow-strict-typed", input, 200, dispatchPath);
  assert.equal(accepted.classKey, "priest");
  assert.equal(accepted.specialization, "shadow");
  assert.equal(accepted.level, 60);
  assert.equal(accepted.talentSourceStatus, "historical-provisional-not-current-client-certified");
  assert.equal(validation.sourceStatus, accepted.talentSourceStatus);
  assert.equal(accepted.talentSourceCommit, "1cc5ae1e9abdbf941b7326d2d90421822dce31ba");
  assert.match(JSON.stringify(accepted.baseline), /provisional/i);
  assert.ok(accepted.sim.damageDone > 0);
  const replay = await run(route, "shadow-strict-replay", input, 200, dispatchPath);
  for (const key of ["dps", "damageDone", "healingDone", "endingMana", "endingHealth", "damageByAbility", "priestDied"]) {
    assert.ok(Object.hasOwn(accepted.sim, key) && Object.hasOwn(replay.sim, key));
    assert.deepEqual(replay.sim[key], accepted.sim[key], "Shadow replay " + key);
  }
  for (const [label, invalidate] of [
    ["null-character", r => {r.shadow.character = null;}],
    ["wrong-class", r => {r.shadow.character.classKey = "mage";}],
    ["wrong-spec", r => {r.shadow.character.specializationKey = "holy";}],
    ["wrong-level", r => {r.shadow.character.level = 59;}],
    ["wrong-wrapper-class", r => {r.classKey = "mage";}],
    ["wrong-wrapper-spec", r => {r.specialization = "holy";}],
    ["missing-baseline", r => {r.shadow.useProvisionalLevel60Baseline = false;}],
    ["null-talents", r => {r.shadow.talents = null;}],
    ["null-ranks", r => {r.shadow.talents.ranks = null;}],
    ["null-full-map", r => {r.itemizedTalentRanks = null;}],
    ["map-mismatch", r => {r.itemizedTalentRanks["shadow-focus"] = 4;}],
    ["unexecuted-full-map-rank", r => {r.itemizedTalentRanks["inner-focus"] = 1;}],
    ["unnamed-total", r => {r.shadow.talents.disciplinePointsSpent = 10;}],
    ["null-equipment", r => {r.shadow.character.equipment = null;}],
    ["conflicting-payload", r => {r.balance = {};}],
    ...["mind-flay", "vampiric-embrace"].map(parent => ["missing-parent-" + parent, r => {
      r.shadow.talents.shadowPointsSpent -= r.shadow.talents.ranks[parent];
      delete r.shadow.talents.ranks[parent]; delete r.itemizedTalentRanks[parent];
    }])
  ]) {
    const bad = structuredClone(input);
    invalidate(bad);
    await run(route, "shadow-strict-" + label + "-rejected", bad, 400, dispatchPath);
  }
  for (const kind of ["uppercase", "whitespace", "duplicate-case"])
    for (const mode of ["typed", "full", "both"]) {
      const bad = structuredClone(input);
      const maps = mode === "both" ? [bad.shadow.talents.ranks, bad.itemizedTalentRanks] :
        [mode === "typed" ? bad.shadow.talents.ranks : bad.itemizedTalentRanks];
      for (const ranks of maps) {
        const key = "shadow-focus";
        if (kind !== "duplicate-case") delete ranks[key];
        ranks[kind === "whitespace" ? " " + key + " " : key.toUpperCase()] = 5;
      }
      await run(route, "shadow-strict-" + kind + "-" + mode + "-rejected", bad, 400, dispatchPath);
    }
  for (const [key, allegedTotal] of [["inner-focus",11], ["penance",21], ["power-infusion",31]]) {
    // No fabricated filler: neither exact one-point nor alleged legacy totals unlock these rows.
    for (const total of [1, allegedTotal]) {
      const bad = structuredClone(input);
      bad.shadow.talents = {ranks:{[key]:1}, disciplinePointsSpent:total, holyPointsSpent:0, shadowPointsSpent:0};
      bad.itemizedTalentRanks = {[key]:1};
      await run(route, "shadow-strict-unreachable-" + key + "-" + total, bad, 400, dispatchPath);
    }
  }
  coverage.push({route, strictTypedDispatch:true, historicalOnly:true, points:36,
    treePoints:{discipline:0, holy:0, shadow:36}, gameplayReplay:true,
    identityAndBaselineChecked:true, canonicalAndFullMapGates:true,
    unreachableDisciplineRejected:true, penanceActionNotClaimed:true});
}
async function main() {
  await rogue();
  await shadow();
  await enhancement();
  await sharedCasterThresholds();
  await historicalTypedDispatch();
  await historicalShadowDispatch();
  fs.writeFileSync(path.join(root, "coverage.json"), JSON.stringify({sourceCommit, requests, coverage}, null, 2));
  console.log("Prepared extra-class HTTP acceptance passed: " + requests +
    " requests; Rogue replay and rejection, Shadow synthetic resource/execute oracles, Enhancement prepared inputs and rejection, shared caster threshold parity and historical typed dispatch.");
}
main().catch(error => {console.error(error); process.exitCode = 1;});
