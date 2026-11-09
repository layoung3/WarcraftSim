"use strict";
(function(root){
  // Report-only comparison: never reruns, rewrites, or ranks mismatched simulations.
  const own=(object,key)=>Object.prototype.hasOwnProperty.call(object||{},key);
  const number=value=>value===null||value===undefined||value===""?null:
    Number.isFinite(Number(value))?Number(value):null;
  const keyFor=spec=>({"beast-mastery":"beastMastery",marksmanship:"marksmanship",
    survival:"survival"})[spec]||spec;
  function stable(value){
    if(Array.isArray(value))return value.map(stable);
    if(value&&typeof value==="object"){
      const result={};
      for(const key of Object.keys(value).sort())if(value[key]!==undefined)
        result[key]=stable(value[key]);
      return result;
    }
    return value;
  }
  const equal=(a,b)=>JSON.stringify(stable(a))===JSON.stringify(stable(b));
  const chosen=values=>Array.isArray(values)?[...values].sort():[];
  function inspectReport(input){
    if(!input||input.schema!=="warcraftsim.solo-dps-report"||input.version!==1||
       !input.build||!input.combatSummary||!input.originalSimulationRequest||
       !["mage","warlock","hunter","warrior"].includes(input.build.classKey)||
       typeof input.build.specialization!=="string")
      throw Error("Select a complete Solo DPS report (schema v1), not a build or raw combat response.");
    const report=JSON.parse(JSON.stringify(input)),build=report.build,summary=report.combatSummary,
      request=report.originalSimulationRequest,enc=build.encounter||{},
      typed=request.warrior||request[build.classKey==="hunter"?
        keyFor(build.specialization):build.classKey]||{},
      mods=build.modifiers||{},batch=report.iterationStatistics||null,
      iterations=number(batch?.iterations??enc.iterations??request.iterations)??1,
      statistics=iterations>1,sim=report.rawServerResponse?.sim||{},
      actor=sim.batch?.representativeRun?.summary?.actorSummaries?.warrior||{},
      dps=statistics?number(batch?.dps?.mean):
        number(summary.dps??sim.dps??sim.meanDps),
      damage=number(summary.damageDone??sim.damageDone??actor.damageDone),
      ownerDamage=number(summary.ownerDamage),
      petDamage=number(summary.petDamage),
      abilities=summary.damageByAbility||sim.damageByAbility||actor.damageDoneByAbility||{};
    const encounter={
      duration:number(enc.duration??typed.durationSeconds),
      iterations,
      seed:number(enc.seed??typed.seed),
      targetHealth:number(enc.targetHealth??typed.targetMaximumHealth),
      targetLevel:number(typed.targetLevel??typed.bossLevel??typed.target?.level),
      targetArmor:number(typed.targetArmor??typed.bossArmor??typed.target?.armor),
      resistances:typed.targetResistances??typed.resistances??null,
      scriptedEncounter:enc.soloScript??typed.soloEncounter??null,
      additionalTargets:typed.additionalTargets??null,
      incomingBossAttacks:build.classKey==="warrior"?
        Boolean(enc.warrior?.enableBossAutoAttacks??typed.enableBossAutoAttacks):null,
      buffs:chosen(mods.buffs),foodAndPrepull:chosen(mods.consumables),
      targetDebuffs:chosen(mods.targetDebuffs),
      targetDebuffMode:mods.targetDebuffMode??"maintained",
      baselineMode:build.provisional===true?"historical-provisional":"reviewed"
    };
    // Explicitly report missing attribution; do not manufacture pet or owner zeroes.
    const sources=Object.entries(abilities).filter(([name,value])=>
      typeof name==="string"&&Number.isFinite(Number(value))).map(([name,value])=>
      ({name,damage:Number(value)})).sort((a,b)=>b.damage-a.damage);
    let reconciliation=null;
    if(damage!==null&&ownerDamage!==null&&petDamage!==null){
      const difference=Math.abs(damage-ownerDamage-petDamage);
      reconciliation={status:difference<=Math.max(0.05,Math.abs(damage)*0.000001)?
        "reconciled":"mismatch",difference};
    }
    return {report,build,summary,encounter,statistics,iterations,dps,damage,
      ownerDamage,petDamage,sources,reconciliation,
      label:build.classKey+" / "+build.specialization+" (seed "+(encounter.seed??"unreported")+")"};
  }
  function differences(a,b){
    const changes=[],left=a.build,right=b.build;
    if(left.classKey!==right.classKey)changes.push("Class: "+left.classKey+" → "+right.classKey);
    if(left.specialization!==right.specialization)
      changes.push("Preset: "+left.specialization+" → "+right.specialization);
    if(left.race!==right.race)changes.push("Race: "+left.race+" → "+right.race);
    const ar=left.talents?.ranks||{},br=right.talents?.ranks||{};
    for(const key of new Set([...Object.keys(ar),...Object.keys(br)])){
      const x=number(ar[key])??0,y=number(br[key])??0;
      if(x!==y)changes.push("Talent "+key+": "+x+" → "+y);
    }
    const equip=build=>Object.fromEntries((build.gear?.items||[]).map(item=>[
      item.slotKey,[item.itemId??"unknown",item.enchantSpellId??0,
        item.temporaryEnchantSpellId??0].join("/")]));
    const ae=equip(left),be=equip(right);
    for(const key of new Set([...Object.keys(ae),...Object.keys(be)]))
      if(ae[key]!==be[key])changes.push("Gear "+key+": "+(ae[key]||"empty")+
        " → "+(be[key]||"empty")+" (item/enchant/temporary)");
    const lx=left.encounter||{},rx=right.encounter||{};
    for(const [label,path] of [["Rotation", "customPriority"],
      ["Rotation conditions","priorityRows"],["Companion", "companionEnabled"],
      ["Pet family","petFamily"],["Demon","demonType"],["Combat inventory","combatItems"],
      ["Class combat settings","classSettings"],["Warrior combat settings","warrior"]]){
      if(!equal(lx[path],rx[path]))changes.push(label+" differs");
    }
    if(!equal(left.preparedCombatConsumables,right.preparedCombatConsumables))
      changes.push("Prepared combat consumables differ");
    for(const key of ["buffs","consumables","targetDebuffs"])
      if(!equal(chosen(left.modifiers?.[key]),chosen(right.modifiers?.[key])))
        changes.push("Selected "+key+" differ");
    return changes;
  }
  function compareReports(left,right){
    const a=inspectReport(left),b=inspectReport(right),warnings=[],caveats=[];
    function mismatch(label,x,y){if(!equal(x,y))warnings.push(label+" differs");}
    mismatch("Class",a.build.classKey,b.build.classKey);
    for(const [label,key] of [["Duration","duration"],["Iterations","iterations"],
      ["Seed","seed"],["Target health","targetHealth"],["Target level","targetLevel"],
      ["Target armor","targetArmor"],["Target resistances","resistances"],
      ["Scripted encounter","scriptedEncounter"],["Additional targets","additionalTargets"],
      ["Boss attack assumptions","incomingBossAttacks"],["Buffs","buffs"],
      ["Food and pre-pull effects","foodAndPrepull"],["Target debuffs","targetDebuffs"],
      ["Debuff application mode","targetDebuffMode"],["Baseline confidence","baselineMode"]])
      mismatch(label,a.encounter[key],b.encounter[key]);
    for(const key of ["duration","seed"]){
      if(a.encounter[key]===null||b.encounter[key]===null)
        warnings.push("Required "+key+" not recorded");
    }
    for(const key of ["targetLevel","targetArmor","resistances"])
      if(a.encounter[key]===null&&b.encounter[key]===null)
        caveats.push(key+" is not recorded in these reports");
    if(a.dps===null||b.dps===null)warnings.push("DPS not reported by both runs");
    if(a.reconciliation?.status==="mismatch"||b.reconciliation?.status==="mismatch")
      caveats.push("Owner and companion damage do not reconcile in at least one report");
    if(a.reconciliation===null||b.reconciliation===null)
      caveats.push("Independent owner/companion reconciliation is unavailable for at least one run");
    const comparable=warnings.length===0,delta=comparable?b.dps-a.dps:null;
    return {a,b,comparable,warnings,caveats,changes:differences(a,b),
      dpsDelta:delta,dpsDeltaPercent:delta===null||a.dps===0?null:100*delta/a.dps,
      damageDelta:comparable&&!a.statistics&&!b.statistics&&
        a.damage!==null&&b.damage!==null?b.damage-a.damage:null};
  }
  const api={inspectReport,compareReports};
  if(typeof module==="object"&&module.exports)module.exports=api;
  if(root)root.WarcraftSimComparison=api;
  if(typeof document==="undefined")return;
  const $=id=>document.getElementById(id),slots={A:null,B:null};
  let current=null,pendingImport=null;
  const fmt=(value,digits=1)=>value===null||value===undefined?"Unavailable":
    Number(value).toLocaleString("en-US",{maximumFractionDigits:digits,
      minimumFractionDigits:digits});
  function line(text){
    const item=document.createElement("div");item.textContent=text;return item;
  }
  function appendMetric(label,l,r,delta){
    const row=document.createElement("tr");
    for(const item of [label,l,r,delta]){
      const cell=document.createElement("td");cell.textContent=String(item);
      row.append(cell);
    }
    $("compareRows").append(row);
  }
  function render(){
    for(const slot of ["A","B"])
      $("compareLabel"+slot).textContent=slots[slot]?slots[slot].label:"No report selected";
    $("compareUseA").disabled=$("compareUseB").disabled=!current;
    $("compareRows").replaceChildren();$("compareWarnings").replaceChildren();
    $("compareChanges").replaceChildren();
    if(!slots.A||!slots.B){
      $("compareDetails").hidden=true;
      $("compareStatus").textContent="Set A and B from successful runs, or import exported Solo DPS reports.";
      return;
    }
    const c=api.compareReports(slots.A.report,slots.B.report),a=c.a,b=c.b;
    $("compareDetails").hidden=false;
    $("compareStatus").textContent=c.comparable?
      "Encounter settings match. This is a descriptive comparison, not proof of a superior build.":
      "Not directly comparable. Different conditions prevent a meaningful DPS delta.";
    for(const warning of c.warnings)$("compareWarnings").append(line("Mismatch: "+warning));
    for(const caution of c.caveats)$("compareWarnings").append(line("Coverage: "+caution));
    const dpsLabel=a.statistics?"Mean DPS ("+a.iterations+" runs)":"DPS (single seed)";
    appendMetric(dpsLabel,fmt(a.dps),fmt(b.dps),
      c.dpsDelta===null?"Not comparable":(c.dpsDelta>=0?"+":"")+
        fmt(c.dpsDelta)+" ("+fmt(c.dpsDeltaPercent)+"%)");
    appendMetric(a.statistics||b.statistics?"Representative run damage (not mean)":"Total damage",
      fmt(a.damage,0),fmt(b.damage,0),c.damageDelta===null?
        "Not compared":(c.damageDelta>=0?"+":"")+fmt(c.damageDelta,0));
    appendMetric("Owner damage (representative)",fmt(a.ownerDamage,0),
      fmt(b.ownerDamage,0),"—");
    appendMetric("Pet / demon damage (representative)",fmt(a.petDamage,0),
      fmt(b.petDamage,0),"—");
    appendMetric("Reported top abilities (representative)",
      a.sources.slice(0,5).map(x=>x.name+" "+fmt(x.damage,0)).join(" · ")||"Unavailable",
      b.sources.slice(0,5).map(x=>x.name+" "+fmt(x.damage,0)).join(" · ")||"Unavailable","—");
    const changes=c.changes;
    if(!changes.length)$("compareChanges").append(line("No tracked selections differ."));
    else for(const change of changes.slice(0,18))$("compareChanges").append(line(change));
    if(changes.length>18)$("compareChanges").append(line("And "+(changes.length-18)+" more changes."));
  }
  function setSlot(slot,input){
    slots[slot]=api.inspectReport(input);render();
  }
  document.addEventListener("warcraftsim:simulation-complete",event=>{
    try{
      current=api.inspectReport(event.detail);
      render();
    }catch(error){$("compareStatus").textContent=error.message;}
  });
  for(const slot of ["A","B"]){
    $("compareUse"+slot).addEventListener("click",()=>{
      if(current)setSlot(slot,current.report);
    });
    $("compareImport"+slot).addEventListener("click",()=>{
      pendingImport=slot;$("compareFile").click();
    });
  }
  $("compareFile").addEventListener("change",async()=>{
    const file=$("compareFile").files?.[0],target=pendingImport;
    if(!file||!target)return;
    try{setSlot(target,JSON.parse(await file.text()));}
    catch(error){$("compareStatus").textContent="Report import rejected: "+error.message;}
    finally{$("compareFile").value="";pendingImport=null;}
  });
  render();
})(typeof window!=="undefined"?window:null);
