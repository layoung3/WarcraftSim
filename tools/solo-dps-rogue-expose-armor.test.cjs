"use strict";
// Real HTTP serializer/binder and deterministic combat contract. Historical
// source default is 1/2 CP, not current-client certification or strict admission.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const BASE = process.env.CP174_BASE_URL || "http://127.0.0.1:5142";
const url = new URL(BASE);
assert.equal(url.protocol, "http:");
assert.ok(["127.0.0.1", "localhost", "[::1]"].includes(url.hostname));
assert.ok(!url.username && !url.password && !url.search && !url.hash);
const root = path.resolve(process.env.CP174_REPORT_DIR || "acceptance-rogue-expose-armor");
fs.mkdirSync(root, { recursive: true });
let requests = 0;
const receipts = [];
const copy = x => JSON.parse(JSON.stringify(x));
const identityFields = new Set(["abilityExecutionId", "autoAttackInstanceId", "auraInstanceId", "absorbInstanceId", "forcedTargetInstanceId"]);
function stable(value) {
  if (Array.isArray(value)) return value.map(stable);
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value)
    .filter(([key]) => !identityFields.has(key)).map(([key, item]) => [key, stable(item)]));
  return value;
}
async function request(label, method, route, input, expected = 200) {
  const before = input === undefined ? undefined : JSON.stringify(input);
  const result = await fetch(BASE + route, { method, redirect: "error",
    headers: { "content-type": "application/json" }, body: before,
    signal: AbortSignal.timeout(30000) });
  const response = await result.json();
  requests++;
  fs.writeFileSync(path.join(root, label + ".json"), JSON.stringify({
    sourceCommit: process.env.CP174_SOURCE_COMMIT || "not-stamped-local-build",
    input, status: result.status, response }, null, 2));
  assert.equal(result.status, expected, label + ": " + JSON.stringify(response).slice(0, 250));
  if (input !== undefined) assert.equal(JSON.stringify(input), before, label + " caller input mutated");
  receipts.push({ label, status: result.status });
  return response;
}
const post = (label, input, status) => request(label, "POST", "/api/sim/rogue/run", input, status);
const field = "improvedExposeArmorFivePointRefund";
(async () => {
  for (const spec of ["assassination", "combat", "subtlety"]) {
    const d = await request(spec + "-defaults", "GET", "/api/sim/rogue/defaults?spec=" + spec);
    assert.ok(Object.hasOwn(d.tuning, field));
    assert.equal(d.tuning[field], null);
    assert.equal(d.useExposeArmor, false);
    assert.equal(d.talents.ranks["improved-expose-armor"] || 0, 0);
    d.durationSeconds = 20; d.captureTimeline = true;
    const current = await post(spec + "-default-run", d);
    const oldExport = copy(d); oldExport.tuning[field] = 5;
    assert.deepEqual(stable(await post(spec + "-old-export-disabled", oldExport)), stable(current));
  }
  const d = await request("scenario-defaults", "GET", "/api/sim/rogue/defaults");
  Object.assign(d, { inputLatencyMs: 0, captureTimeline: true, useExposeArmor: true,
    startInStealth: false, useInstantPoison: false, targetArmor: 5500, targetMaximumHealth: 10000000 });
  d.character.baseStats = { values: {} };
  Object.assign(d.tuning, { physicalCritPercent: 100, mainHandHitPercent: 100, offHandHitPercent: 100, energyPerSecond: 60 });
  const all = {};
  for (const rank of [0, 1, 2]) {
    const results = {};
    for (const [label, value] of [["missing", undefined], ["null", null], ["zero", 0], ["one", 1], ["two", 2], ["five", 5], ["string-zero", "0"], ["string-five", "5"]]) {
      const r = copy(d);
      r.talents = { assassinationPointsSpent: 21, ranks: { "improved-expose-armor": rank, mutilate: 1, "seal-fate": 5 } };
      if (value === undefined) delete r.tuning[field]; else r.tuning[field] = value;
      const response = await post(`rank${rank}-${label}`, r);
      results[label] = stable(response);
      assert.equal(response.run.customMetrics["rogue-expose-armor-points"], 5);
      assert.ok(response.run.timeline.some(e => e.abilityKey === "expose-armor" && e.resourceKey === "energy" && e.amount === -[25, 20, 15][rank]));
      assert.ok(response.run.timeline.some(e => e.rawAmount > e.amount && e.mitigatedAmount > 0));
      const replay = await post(`rank${rank}-${label}-replay`, r);
      assert.deepEqual(stable(replay), results[label]);
    }
    assert.deepEqual(results.missing, results.null);
    assert.deepEqual(results.missing, results[["zero", "one", "two"][rank]]);
    assert.deepEqual(results["string-zero"], results.zero);
    assert.deepEqual(results["string-five"], results.five);
    if (rank === 0) assert.deepEqual(results.missing, results.five);
    else assert.notDeepEqual(results.missing, results.five);
    all[rank] = results;
  }
  // Mixed simultaneous requests cannot leak a resolved value into other calls.
  const mixed = await Promise.all([null, 0, 1, 2, 5].map(async value => {
    const r = copy(d); r.talents = { assassinationPointsSpent: 21, ranks: { "improved-expose-armor": 2, mutilate: 1, "seal-fate": 5 } };
    r.tuning[field] = value;
    return [value, stable(await post("concurrent-" + value, r))];
  }));
  for (const [value, result] of mixed) assert.deepEqual(result, all[2][value === null ? "null" : ({0:"zero",1:"one",2:"two",5:"five"})[value]]);
  for (const [label, value] of [["negative", -1], ["above-cap", 6], ["fractional", 1.5],
    ["fractional-string", "1.5"], ["text", "wrong"], ["bool", true], ["object", {}], ["array", []]]) {
    const r = copy(d); r.tuning[field] = value;
    await post("invalid-" + label, r, 400);
  }
  const refreshed = await request("defaults-after-concurrent", "GET", "/api/sim/rogue/defaults");
  assert.equal(refreshed.tuning[field], null);
  fs.writeFileSync(path.join(root, "summary.json"), JSON.stringify({ requests, receipts }, null, 2));
  console.log(`Rogue Expose Armor HTTP contract: ${requests} requests passed.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
