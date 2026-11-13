"use strict";
// HTTP acceptance of source-labelled, partial Enhancement allocations.
// Controlled observations below are synthetic test inputs, never live-game evidence.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const BASE = process.env.CP174_BASE_URL || "http://127.0.0.1:5142";
const url = new URL(BASE);
assert.ok(url.protocol === "http:" && ["127.0.0.1", "localhost", "[::1]"].includes(url.hostname));
assert.ok(!url.username && !url.password && !url.search && !url.hash);
const root = path.resolve(process.env.CP174_REPORT_DIR || "acceptance-enhancement-strict");
fs.mkdirSync(root, {recursive:true});
const sourceCommit = process.env.CP174_SOURCE_COMMIT || "not-stamped-local-build";
const historicalCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
const sourceStatus = "historical-provisional-not-current-client-certified";
const ranks = {"thundering-strikes":5, "ancestral-knowledge":5,
  "mental-dexterity":3, "improved-lightning-shield":3, "elemental-weapons":3,
  "shamanistic-focus":1, flurry:5, stormstrike:1, "mental-quickness":2,
  "improved-stormstrike":2, "maelstrom-weapon":5, "rage-of-the-farseer":1};
const crossTreeKeys = ["convection", "concussion", "reverberation", "elemental-devastation"];
let requests = 0;
async function call(route, label, input, expected = 200) {
  const before = input === undefined ? undefined : JSON.stringify(input);
  const response = await fetch(BASE + route, {method:before === undefined ? "GET" : "POST",
    headers:before === undefined ? undefined : {"content-type":"application/json"},
    body:before, redirect:"error", signal:AbortSignal.timeout(30000)});
  const result = await response.json();
  requests++;
  fs.writeFileSync(path.join(root, label + ".json"), JSON.stringify({
    schema:"warcraftsim.enhancement-strict-http-v1", sourceCommit,
    scope:"software acceptance only; historical talent source and synthetic observations do not certify the current client",
    request:input, status:response.status, response:result
  }, null, 2));
  assert.equal(response.status, expected, label + ": " + JSON.stringify(result));
  if(input !== undefined)assert.equal(JSON.stringify(input), before, label + " mutated input");
  return result;
}
function envelope(typed) {
  return {classKey:"shaman", specialization:"enhancement", itemizedTalentMode:true,
    itemizedTalentRanks:structuredClone(typed.talents.ranks), enhancement:typed};
}
function reallocate(input, next) {
  input.enhancement.talents.ranks = structuredClone(next);
  input.enhancement.talents.elementalPointsSpent = Object.entries(next).filter(([key])=>crossTreeKeys.includes(key)).reduce((sum,[,rank])=>sum+rank,0);
  input.enhancement.talents.enhancementPointsSpent = Object.entries(next).filter(([key])=>!crossTreeKeys.includes(key)).reduce((sum,[,rank])=>sum+rank,0);
  input.itemizedTalentRanks = structuredClone(next);
  return input;
}
function gameplay(sim) {
  return Object.fromEntries(["dps", "damageDone", "endingMana", "maximumMana", "maximumHealth",
    "endingHealth", "attackPower", "spellPower", "damageByAbility", "combatItemUses",
    "windfuryWeaponProcs", "maelstromInstantBolts"].map(key=>[key, sim[key]]));
}
// CombatEvent exposes exactly five per-run GUID identity fields. Their owners
// allocate fresh GUIDs (AbilityExecutionState, AutoAttackState, AuraInstance,
// AbsorbManager and ForcedTargetState). Canonicalize first-seen identities,
// retaining nulls, repeated references, every other field and original ordering.
// Reuse the established anchor-aware oracle: complete parsed simulation response,
// only actual generated nonzero event GUIDs renamed; literals and joins stay exact.
const {canonical:canonicalSimulation}=require('./solo-dps-combat-browser-support.cjs');
function verifyShield(sim, rank) {
  const triggers = sim.run.customMetrics["shaman-lightning-shield-triggers"];
  assert.ok(triggers > 0);
  // Source law: a landed Stormstrike grants its owner 20% Nature damage on
  // that target for a half-open 12s window. It does not consume on the first hit.
  const strikes = sim.run.timeline.filter(e=>e.abilityKey === "stormstrike" &&
    e.sourceActorKey === "shaman" && e.targetActorKey === "target" && e.amount > 0 && e.schoolKey === "physical");
  const orbs = sim.run.timeline.filter(e=>e.abilityKey === "lightning-shield" &&
    e.sourceActorKey === "shaman" && e.targetActorKey === "target" && e.amount > 0 && e.schoolKey === "nature");
  assert.equal(orbs.length, triggers);
  let expectedTotal = 0;
  for(const orb of orbs) {
    const active = strikes.some(strike=>strike.timeSeconds <= orb.timeSeconds && orb.timeSeconds < strike.timeSeconds + 12);
    const expected = 178 * (1 + .05 * rank) * (active ? 1.2 : 1);
    assert.ok(Math.abs(orb.amount - expected) < 1e-8, "Shield damage must apply ILS and source-owned Stormstrike once");
    expectedTotal += expected;
  }
  assert.ok(Math.abs(sim.damageByAbility["lightning-shield"] - expectedTotal) < 1e-8);
}
(async()=>{
  const catalog = await call("/api/forever/talents/shaman/enhancement", "catalog");
  assert.deepEqual(catalog.talents.map(x=>x.key).sort(), [...Object.keys(ranks), ...crossTreeKeys].sort());
  assert.equal(catalog.talents.length, 16);
  for(const key of crossTreeKeys){const talent=catalog.talents.find(x=>x.key===key);assert.equal(talent.tree,"elemental");assert.equal(talent.maxRank,key==="elemental-devastation"?3:5);}
  assert.ok(!catalog.talents.some(x=>x.key==="call-of-flame"));
  const elemental = await call("/api/forever/talents/shaman/elemental", "elemental-unchanged");
  assert.equal(elemental.talents.length, 26);
  assert.ok(!elemental.talents.some(x=>x.key === "improved-lightning-shield"));
  const typed = await call("/api/sim/enhancement-shaman/defaults", "legacy-defaults");
  assert.equal(typed.talents.enhancementPointsSpent, 41);
  assert.equal(Object.values(typed.talents.ranks).reduce((a,b)=>a+b,0), 28);
  typed.durationSeconds = 20;
  // Legacy /api/sim middleware defaults to 80 ms; strict dispatch has no extra delay.
  // Explicit zero makes the compared scheduling assumptions identical.
  typed.inputLatencyMs = 0;
  typed.character.race = "orc";
  typed.character.equipment = {items:[]};
  typed.character.talents = {selections:[]};
  typed.character.buffs = {selections:[]};
  typed.level60Baseline = {race:"orc", classKey:"shaman", strength:100, agility:100,
    stamina:100, intellect:100, spirit:100, observedUnbuffedMeleeAttackPower:700,
    observedUnbuffedMaximumMana:2000, observedUnbuffedMaximumHealth:3000,
    clientBuild:"synthetic", sourceReference:"synthetic-http-test-observation-not-live-game-data",
    recordedAtUtc:"2026-11-10T00:00:00Z"};
  typed.talents = {elementalPointsSpent:0, enhancementPointsSpent:36, restorationPointsSpent:0,
    ranks:structuredClone(ranks)};
  typed.mainHandImbue = "none";
  typed.useWindfuryWeapon = false;
  typed.useWindfuryTotem = false;
  typed.useLightningShield = true;
  typed.incomingBossHitDamage = 10;
  for (const key of Object.keys(typed.combatItems))
    if (key.endsWith("Charges")) typed.combatItems[key] = 0; // Actual inventory, not an ignored Enabled field.
  const input = envelope(typed);
  const allocation = await call("/api/forever/talents/shaman/enhancement/validate-itemized", "allocation", typed.talents);
  assert.equal(allocation.pointsSpent, 36);
  const receipt = await call("/api/forever/character/simulate-level60", "strict-full", input);
  assert.equal(receipt.talentSourceStatus, sourceStatus);
  assert.equal(receipt.talentSourceCommit, historicalCommit);
  assert.ok(receipt.sim.damageDone > 0);
  assert.equal(receipt.sim.attackPower, 810);
  assert.equal(receipt.sim.spellPower, 33);
  assert.equal(receipt.sim.maximumMana, 2150);
  assert.equal(receipt.sim.maximumHealth, 3000);
  verifyShield(receipt.sim, 3);
  const replay = await call("/api/forever/character/simulate-level60", "strict-replay", input);
  assert.deepEqual(gameplay(replay.sim), gameplay(receipt.sim));
  const direct = await call("/api/sim/enhancement-shaman/run", "direct-parity", typed);
  assert.deepEqual(gameplay(direct), gameplay(receipt.sim));
  // Selected Elemental ranks travel through the exact full/typed maps; equal
  // explicit latency prevents legacy middleware defaults from skewing parity.
  for(const [label,added,total] of [["46",{convection:5,concussion:5},46],["51",{convection:5,concussion:5,reverberation:5},51]]){
    const selected=reallocate(structuredClone(input),{...ranks,...added});
    const validated=await call("/api/forever/talents/shaman/enhancement/validate-itemized","cross-tree-"+label+"-allocation",selected.enhancement.talents);
    assert.equal(validated.pointsSpent,total);assert.equal(validated.remaining,51-total);
    const first=await call("/api/forever/character/simulate-level60","cross-tree-"+label+"-run",selected);
    const repeat=await call("/api/forever/character/simulate-level60","cross-tree-"+label+"-replay",selected);
    const legacy=await call("/api/sim/enhancement-shaman/run","cross-tree-"+label+"-direct",selected.enhancement);
    assert.deepEqual(gameplay(repeat.sim),gameplay(first.sim));assert.deepEqual(gameplay(legacy),gameplay(first.sim));
    assert.deepEqual(canonicalSimulation(repeat.sim),canonicalSimulation(first.sim));
    assert.deepEqual(canonicalSimulation(legacy),canonicalSimulation(first.sim));
    for(const key of ["attackPower","spellPower","maximumMana","maximumHealth"])assert.equal(first.sim[key],receipt.sim[key],key+" unaffected by spell passives");
    verifyShield(first.sim,3);
    assert.notDeepEqual(gameplay(first.sim),gameplay(receipt.sim),"Selected passives affect execution, not only metadata");
  }
  // Removing the cross-tree ranks restores the untouched baseline timeline.
  const restored=reallocate(structuredClone(input),ranks);
  const restoredRun=await call("/api/forever/character/simulate-level60","cross-tree-removed",restored);
  assert.deepEqual(gameplay(restoredRun.sim),gameplay(receipt.sim));assert.deepEqual(canonicalSimulation(restoredRun.sim),canonicalSimulation(receipt.sim));
  for(const [label,mutate] of [
    ["cross-tree-cap",x=>reallocate(x,{...ranks,convection:6})],
    ["cross-tree-row",x=>reallocate(x,{...ranks,reverberation:1})],
    ["cross-tree-budget",x=>{reallocate(x,{...ranks,convection:5,concussion:5,reverberation:5});x.enhancement.talents.elementalPointsSpent=16;}],
    ["cross-tree-wrong-owner",x=>{reallocate(x,{...ranks,convection:5});x.enhancement.talents.elementalPointsSpent=0;x.enhancement.talents.enhancementPointsSpent=41;}],
    ["cross-tree-full-map",x=>{reallocate(x,{...ranks,convection:5});x.itemizedTalentRanks.convection=4;}],
    ["unsupported-call-of-flame",x=>reallocate(x,{...ranks,"call-of-flame":1})]
  ]){const bad=structuredClone(input);mutate(bad);await call("/api/forever/character/simulate-level60",label+"-rejected",bad,400);}
  const noKnowledge = reallocate(structuredClone(input), {...ranks, "ancestral-knowledge":0});
  const withoutKnowledge = await call("/api/forever/character/simulate-level60", "without-knowledge", noKnowledge);
  assert.equal(withoutKnowledge.sim.attackPower, 800);
  assert.equal(withoutKnowledge.sim.spellPower, 30);
  assert.equal(withoutKnowledge.sim.maximumMana, 2000);
  const noShield = reallocate(structuredClone(input), {...ranks, "improved-lightning-shield":0});
  const withoutShield = await call("/api/forever/character/simulate-level60", "without-shield-ranks", noShield);
  verifyShield(withoutShield.sim, 0);
  for (const kind of ["mismatch", "unknown", "alias", "duplicate-alias", "null-ranks", "null-full-map",
      "missing-baseline", "synthetic-aggregate", "wrong-spec", "missing-child", "multiple-children",
      "catalog-gear", "early-row", "parent-rank"]) {
    let bad = structuredClone(input);
    if(kind === "mismatch")bad.itemizedTalentRanks["ancestral-knowledge"] = 4;
    if(kind === "unknown")bad.itemizedTalentRanks["invented-filler"] = 1;
    if(kind === "alias" || kind === "duplicate-alias") {
      for(const map of [bad.itemizedTalentRanks, bad.enhancement.talents.ranks]) {
        if(kind === "alias")delete map["ancestral-knowledge"];
        map["ANCESTRAL-KNOWLEDGE"] = 5;
      }
    }
    if(kind === "null-ranks")bad.enhancement.talents.ranks = null;
    if(kind === "null-full-map")bad.itemizedTalentRanks = null;
    if(kind === "missing-baseline")bad.enhancement.level60Baseline = null;
    if(kind === "synthetic-aggregate")bad.enhancement.talents.enhancementPointsSpent = 41;
    if(kind === "wrong-spec")bad.specialization = "elemental";
    if(kind === "missing-child")delete bad.enhancement;
    if(kind === "multiple-children")bad.elemental = {};
    if(kind === "catalog-gear")bad.enhancement.character.equipment.items = [{itemId:19102,slotKey:"main-hand"}];
    if(kind === "early-row")bad = reallocate(bad, {"improved-lightning-shield":3});
    if(kind === "parent-rank")bad = reallocate(bad, {...ranks, "mental-dexterity":2});
    await call("/api/forever/character/simulate-level60", kind + "-rejected", bad, 400);
  }
  fs.writeFileSync(path.join(root, "summary.json"), JSON.stringify({
    schema:"warcraftsim.enhancement-strict-http-summary-v1", sourceCommit, requests,
    historicalCommit, sourceStatus, namedRanks:36, unspentPoints:15, optionalAllocations:[46,51], modeledCatalogKeys:16,
    exactResourceOracles:true, shieldDamageOracle:true, replay:true, directParity:true,
    existingElementalCatalogUnchanged:true, rejectedMalformedAndUnsupportedInputs:true
  }, null, 2));
  console.log(`Enhancement strict HTTP acceptance passed: ${requests} requests; 36/46/51 named ranks, exact resources/shield damage, replay, direct parity and rejection gates.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
