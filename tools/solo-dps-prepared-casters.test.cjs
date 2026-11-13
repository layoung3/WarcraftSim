"use strict";
// Actual HTTP acceptance. Synthetic overlay receipts test plumbing, never real eligibility.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const {spawn} = require("node:child_process");
const BASE = process.env.CP174_BASE_URL || "http://127.0.0.1:5142";
assert.ok(["127.0.0.1", "localhost", "[::1]"].includes(new URL(BASE).hostname), "Acceptance API must be loopback-only");
const root = path.resolve(process.env.CP174_REPORT_DIR || "acceptance-prepared-casters");
const sourceCommit = process.env.CP174_SOURCE_COMMIT || "not-stamped-local-build";
fs.mkdirSync(root, {recursive:true});
const coverage = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
async function request(route, input, base = BASE) {
  const response = await fetch(base + "/api/sim/" + route + "/run", {
    method:"POST", headers:{"content-type":"application/json"}, body:JSON.stringify(input),
    signal:AbortSignal.timeout(30000)
  });
  return {status:response.status, response:await response.json()};
}
function save(name, input, result) {
  fs.writeFileSync(path.join(root, name + ".json"), JSON.stringify({
    schema:"warcraftsim.prepared-caster-api-acceptance-v2", sourceCommit, request:input, ...result
  }, null, 2));
}
async function run(route, input, label, base = BASE) {
  const result = await request(route, input, base);
  assert.equal(result.status, 200, JSON.stringify(result.response));
  save(label, input, result);
  return result.response;
}
async function defaults(route, shaman, base = BASE) {
  const response = await fetch(base + "/api/sim/" + route + "/defaults", {signal:AbortSignal.timeout(10000)});
  assert.equal(response.status, 200);
  const input = await response.json();
  input.character.race = shaman ? "orc" : "night-elf";
  input.character.equipment = {items:[]};
  input.character.buffs = {selections:[]};
  input.character.talents = {selections:[]};
  input.character.baseStats = {values:{}};
  input.talents = {ranks:{}};
  input.useProvisionalLevel60Baseline = true;
  input.durationSeconds = 20;
  input.startingManaPercent = 100;
  input.majorManaPotionCharges = 0;
  for (const key of Object.keys(input.combatItems))
    if (key.endsWith("Charges")) input.combatItems[key] = 0; // Actual inventory, not an ignored Enabled field.
  input.baseSpellHitPercent = 100;
  input.spellCritPercent = 0;
  return input;
}
async function reject(route, input, label, base = BASE, expected = null) {
  const result = await request(route, input, base);
  assert.equal(result.status, 400, JSON.stringify(result.response));
  if (expected) assert.match(JSON.stringify(result.response), expected);
  save(label + "-rejected", input, result);
}
function syntheticManifest() {
  const build = "1.60.1.70707";
  const sparse = "ID,AllowableClass\n900001,1088\n900002,0\n";
  const item = "ID,ClassID,SubclassID,InventoryType\n900001,4,2,5\n900002,4,2,5\n";
  const values = JSON.stringify([{id:900001, name:"Synthetic armor TEST ONLY", intellect:25, stamina:10, spellPower:40, manaPer5:8}]);
  fs.writeFileSync(path.join(root,"synthetic-ItemSparse.csv"),sparse);
  fs.writeFileSync(path.join(root,"synthetic-Item.csv"),item);
  fs.writeFileSync(path.join(root,"synthetic-reviewed-values.json"),values);
  const tableSha256 = {Item:hash(item), ItemSparse:hash(sparse)};
  const evidence = {itemId:900001, sourceKind:"client-item-sparse", rawMask:1088,
    status:"restricted", dataConfidence:"provisional", sourceAllowedClasses:["shaman","druid"],
    sourceBuild:build, sourceUrl:"https://example.test/synthetic/ItemSparse.csv", sourceSha256:tableSha256.ItemSparse};
  const manifest = {
    catalogVersion:"synthetic-prepared-overlay-test-only", contentVersion:"synthetic-test-only",
    clientBuild:build, sourceName:"synthetic-test-only-not-real-eligibility", sourceUrl:"https://example.test/synthetic/source",
    importedAt:"2026-11-09T00:00:00Z", tableSha256, reviewedValuesSha256:hash(values),
    reviewApproval:{clientBuild:build,status:"reviewed",reviewer:"synthetic-test-only",
      sourceUrl:"https://example.test/synthetic/review",resolvedValuesSha256:hash(values),sourceTableSha256:tableSha256},
    items:[{id:900001,name:"Synthetic armor TEST ONLY",requiredLevel:60,allowedSlots:["chest"],
      allowedClasses:["shaman","druid"],itemClass:"armor",itemSubclass:"leather",
      sourceStatus:"forever-client-db2-reviewed",verificationStatus:"client-db2-reviewed",sourceBuild:build,
      sourceUrl:"https://example.test/synthetic/item/900001",classRestrictionEvidence:evidence,
      stats:{intellect:25,stamina:10,"spell-power":40,"mana-per-5":8}}],
    metadataOnly:[{id:900002,name:"Synthetic unknown TEST ONLY",sourceUrl:"https://example.test/synthetic/item/900002",
      verificationStatus:"metadata-only",sourceBuild:build,
      classRestrictionEvidence:{...evidence,itemId:900002,rawMask:0,status:"unknown",dataConfidence:"unknown",sourceAllowedClasses:[]}}],
    overrides:[],sets:[]
  };
  const filename = path.join(root,"synthetic-approved-manifest.json");
  const bytes = JSON.stringify(manifest,null,2);
  fs.writeFileSync(filename,bytes);
  return {filename,sha256:hash(bytes)};
}
async function syntheticOverlay() {
  const fixture = syntheticManifest();
  const dll = path.resolve("WarcraftSim.Api/bin/Release/net10.0/WarcraftSim.Api.dll");
  assert.ok(fs.existsSync(dll), "Build the Release API before acceptance");
  let output = "";
  let spawnError = null;
  const child = spawn("dotnet",[dll,"--urls","http://127.0.0.1:0"],{
    cwd:path.resolve("WarcraftSim.Api"),windowsHide:true,stdio:["ignore","pipe","pipe"],
    env:{...process.env,ASPNETCORE_ENVIRONMENT:"Production",ASPNETCORE_URLS:"http://127.0.0.1:0",
      WARCRAFTSIM_FOREVER_APPROVED_MANIFEST:fixture.filename,WARCRAFTSIM_FOREVER_APPROVED_SHA256:fixture.sha256}
  });
  child.on("error",error=>{spawnError=error;});
  child.stdout.on("data",bytes=>{output+=bytes.toString();});
  child.stderr.on("data",bytes=>{output+=bytes.toString();});
  try {
    let base;
    const deadline=Date.now()+30000;
    while(Date.now()<deadline) {
      if(spawnError)throw spawnError;
      if(child.exitCode!==null)throw new Error("Synthetic API exited during startup: "+output);
      const match=output.match(/Now listening on:\s+(http:\/\/127\.0\.0\.1:\d+)/);
      if(match){base=match[1];break;}
      await delay(100);
    }
    assert.ok(base,"Synthetic loopback API did not start: "+output);
    assert.notEqual(new URL(base).port,new URL(BASE).port,"Synthetic API must use its own port");
    for(const shaman of [false,true]) {
      const route=shaman?"elemental-shaman":"balance-druid";
      const input=await defaults(route,shaman,base);
      const naked=await run(route,input,route+"-synthetic-naked",base);
      input.character.equipment.items=[{itemId:900001,slotKey:"chest",enchantSpellId:20025}];
      input.externalModifiers.buffs=["arcane-intellect"];
      input.externalModifiers.consumables=["greater-arcane-elixir"];
      const result=await run(route,input,route+"-synthetic-armor",base);
      assert.equal(result.maximumMana-naked.maximumMana,900);
      assert.equal(result.maximumHealth-naked.maximumHealth,140);
      assert.equal(result.spellPower-naked.spellPower,75);
      assert.ok(result.modelingNotes.some(note=>note.includes("Prepared equipment 900001")&&
        note.includes("confidence provisional")&&note.includes("raw mask 1088")));
      const replay=await run(route,input,route+"-synthetic-replay",base);
      assert.deepEqual(replay.run.summary,result.run.summary);
      const unknown=structuredClone(input);
      unknown.character.equipment.items=[{itemId:900002,slotKey:"chest"}];
      await reject(route,unknown,route+"-synthetic-unknown-metadata",base,/Unknown Forever catalog item/);
      const legacy=structuredClone(input);
      legacy.character.equipment.items=[{itemId:22476,slotKey:"chest"}];
      await reject(route,legacy,route+"-synthetic-overlay-legacy-pending",base,/unknown\/pending/);
      coverage.push({route,syntheticOnly:true,strictStartupImport:true,manaDelta:900,healthDelta:140,powerDelta:75,
        unknownMetadataExcluded:true,legacyStillPending:true,replay:true});
    }
  } finally {
    // dotnet executes the already-built DLL directly, with no build/server child.
    // Terminate only the process this harness created; never enumerate unrelated processes.
    if(child.pid&&child.exitCode===null) {
      child.kill();
      const deadline=Date.now()+5000;
      while(child.exitCode===null&&child.signalCode===null&&Date.now()<deadline)await delay(50);
      if(child.exitCode===null&&child.signalCode===null)child.kill("SIGKILL");
    }
    fs.writeFileSync(path.join(root,"synthetic-api.log"),output);
  }
}
async function main() {
  for (const shaman of [false, true]) {
    const route = shaman ? "elemental-shaman" : "balance-druid";
    const input = await defaults(route,shaman);
    const naked = await run(route, input, route + "-naked");
    input.character.equipment.items = [{slotKey:"chest",itemKey:"authored-test-budget",
      name:"Authored test budget, not verified equipment",stats:{values:{intellect:25,stamina:10,"spell-power":40,"mana-per-5":8}}}];
    input.externalModifiers.buffs = ["arcane-intellect"];
    input.externalModifiers.consumables = ["greater-arcane-elixir"];
    const authored = await run(route, input, route + "-authored-static");
    const manaDelta = authored.maximumMana - naked.maximumMana;
    const powerDelta = authored.spellPower - naked.spellPower;
    assert.equal(manaDelta,840);assert.equal(powerDelta,75);
    assert.equal(authored.maximumHealth-naked.maximumHealth,100);
    assert.ok(authored.modelingNotes.some(note=>note.includes("Prepared modifier arcane-intellect")&&note.includes("provisional")));
    assert.ok(!authored.modelingNotes.some(note=>note.startsWith("Prepared equipment")));
    const replay = await run(route, input, route + "-replay");
    assert.deepEqual(replay.run.summary, authored.run.summary);
    const rejected = [];
    for (const [label,item] of [
      ["staff-pending",{itemId:19102,slotKey:"main-hand"}],
      ["mail-pending",{itemId:22436,slotKey:"chest"}],
      ["leather-pending",{itemId:22476,slotKey:"chest"}],
      ["enchant-without-item",{slotKey:"main-hand",itemKey:"authored",enchantSpellId:20034}],
      ["request-receipt-cannot-authorize",{itemId:22476,slotKey:"chest",classRestrictionEvidence:{
        itemId:22476,sourceKind:"client-item-sparse",rawMask:1088,status:"restricted",dataConfidence:"provisional",
        sourceAllowedClasses:["shaman","druid"],sourceBuild:"1.60.1.70450",sourceUrl:"https://example.test/forged",sourceSha256:"a".repeat(64)}}]
    ]) {
      const invalid=structuredClone(input);invalid.character.equipment.items=[item];
      await reject(route,invalid,route+"-"+label,BASE,label==="enchant-without-item"?null:/unknown\/pending/);
      rejected.push(label);
    }
    coverage.push({route,authoredBudget:true,manaDelta,powerDelta,replay:true,rejected});
  }
  await syntheticOverlay();
  fs.writeFileSync(path.join(root,"coverage.json"),JSON.stringify({sourceCommit,coverage},null,2));
  console.log("Prepared caster HTTP acceptance passed: authored/static deltas, pending real eligibility, forged-receipt rejection and isolated synthetic import-to-simulation coverage.");
}
main().catch(error=>{console.error(error);process.exitCode=1;});
