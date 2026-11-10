"use strict";
(function(root){
  // Report-only comparison: never reruns, rewrites, or ranks mismatched simulations.
  const own=(object,key)=>Object.prototype.hasOwnProperty.call(object||{},key);
  const record=value=>value!==null&&typeof value==="object"&&!Array.isArray(value);
  const number=value=>
    (typeof value==="number"||(typeof value==="string"&&value.trim()!==""))&&
    Number.isFinite(Number(value))?Number(value):null;
  const keyFor=spec=>({"beast-mastery":"beastMastery",marksmanship:"marksmanship",
    survival:"survival"})[spec]||spec;
  const specs={rogue:["assassination"],priest:["shadow"],druid:["balance"],shaman:["elemental","enhancement"],mage:["arcane","fire","frost"],warlock:["affliction","demonology","destruction"],
    hunter:["beast-mastery","marksmanship","survival"],warrior:["arms","fury","protection"]};
  function stable(value){
    if(Array.isArray(value))return value.map(stable);
    if(record(value)){
      return Object.fromEntries(Object.keys(value).sort().filter(key=>value[key]!==undefined)
        .map(key=>[key,stable(value[key])]));
    }
    return value;
  }
  const equal=(a,b)=>JSON.stringify(stable(a))===JSON.stringify(stable(b));
  const chosen=values=>Array.isArray(values)?[...values].sort():null;
  const close=(a,b)=>Math.abs(a-b)<=Math.max(0.05,Math.abs(a)*0.000001);
  const reported=(object,key,fallback)=>number(own(object,key)?object[key]:fallback);
  // Only encounter-owned fields belong here. Player AP, crit, weapons, spell
  // coefficients and companion stats are build choices, not target assumptions.
  function encounterScenario(typed,classKey){
    const scenario=Object.fromEntries(Object.entries(typed).filter(([key])=>
      /^(target|boss|incomingDamage|demon(?:Incoming|SecondIncoming|Target)|petIncoming|scriptedParry|additionalTarget)/i.test(key)||
      ["behindTarget","assumeExternalHealingForLifeTap"].includes(key)));
    const extra={
      rogue:["isBehindTarget","enableBossAttacks"],
      druid:["incomingBossHitDamage","bossSwingIntervalSeconds"],
      shaman:["incomingBossHitDamage","incomingBossSwingSeconds","lightningShieldInternalCooldownSeconds"],
      warrior:["executePhaseDurationSeconds","encounterPhases",
        "incomingRageExpectedCreatureHealth","incomingRageExpectedArmorReductionPercent"],
      warlock:["demonDeathAtSeconds","demonSacrificeAtSeconds",
        "demonReplacementAtSeconds","voidwalkerSacrificeAtSeconds"],
      hunter:["scriptedAvoidanceEverySeconds","petUptimePercent",
        "triggerTraps","triggerRangedTraps","trapTriggerDelaySeconds"]
    };
    for(const key of extra[classKey]||[])if(own(typed,key))scenario[key]=typed[key];
    if(classKey==="hunter"){
      for(const key of ["tuning","petTuning"]){
        if(!record(typed[key]))continue;
        // PetAvoidancePercent is the enemy dodge/parry table against pets in
        // HunterCompanionComposition, despite its legacy property name.
        const target=Object.fromEntries(Object.entries(typed[key]).filter(([field])=>
          (field.startsWith("target")||field==="petAvoidancePercent")&&
          // MM/Survival use tuning.targetArmor for their shared owner/pet
          // mitigation. The serialized petTuning copy is not consumed.
          !(key==="petTuning"&&field==="targetArmor")));
        if(Object.keys(target).length)scenario[key]=target;
      }
    }
    return scenario;
  }
  function selectedRanks(ranks){
    if(!record(ranks)||Object.values(ranks).some(value=>
       !Number.isInteger(number(value))||number(value)<0))return null;
    return Object.fromEntries(Object.entries(ranks).filter(([,value])=>number(value)>0)
      .map(([key,value])=>[key,number(value)]));
  }
  function equipmentIdentity(items,nativeWarrior=false){
    if(!Array.isArray(items))return null;
    const slots=new Set(),result=[];
    for(const item of items){
      const slot=item?.slotKey??item?.slot,itemId=number(item?.itemId);
      if(typeof slot!=="string"||!slot||slots.has(slot)||
         !Number.isInteger(itemId)||itemId<=0)return null;
      slots.add(slot);
      const normalized={slot,itemId};
      for(const key of nativeWarrior?["enchantSpellId"]:["enchantSpellId","temporaryEnchantSpellId"]){
        const id=item[key]==null||item[key]===0?null:number(item[key]);
        if(id!==null&&(!Number.isInteger(id)||id<=0))return null;
        if(item[key]!=null&&number(item[key])===null)return null;
        normalized[key]=id;
      }
      result.push(normalized);
    }
    return result.sort((a,b)=>a.slot.localeCompare(b.slot));
  }
  function inspectReport(input){
    const reject=detail=>{throw Error("Select a complete Solo DPS report (schema v1), not a build or raw combat response. "+detail);};
    if(!record(input)||input.schema!=="warcraftsim.solo-dps-report"||input.version!==1||
       !record(input.build)||input.build.schema!=="warcraftsim.solo-dps-build"||input.build.version!==1||
       (!own(specs,input.build.classKey)||!specs[input.build.classKey].includes(input.build.specialization))||
       !record(input.build.encounter)||!record(input.combatSummary)||
       !record(input.originalSimulationRequest)||!record(input.rawServerResponse?.sim))
      reject("Report, build, request and raw simulation objects are required.");
    const buildInput=input.build,requestInput=input.originalSimulationRequest,
      typedKey=buildInput.classKey==="priest"?"shadow":buildInput.classKey==="druid"?"balance":buildInput.classKey==="shaman"?buildInput.specialization:buildInput.classKey==="hunter"?keyFor(buildInput.specialization):buildInput.classKey;
    if(!record(requestInput[typedKey])||
       ["mage","warlock","warrior","marksmanship","survival","beastMastery","elemental","enhancement","balance","shadow","rogue"]
         .some(key=>key!==typedKey&&requestInput[key]!=null)||
       (own(requestInput,"classKey")&&requestInput.classKey!==buildInput.classKey)||
       (own(requestInput,"specialization")&&requestInput.specialization!==buildInput.specialization))
      reject("The typed simulation request must match the build's class and preset.");
    for(const [label,value] of [["Gear",buildInput.gear],["Talents",buildInput.talents],
      ["Modifiers",buildInput.modifiers],["Iteration statistics",input.iterationStatistics],
      ["Ability damage",input.combatSummary.damageByAbility],
      ["Scripted encounter",buildInput.encounter.soloScript],
      ["Typed scripted encounter",requestInput[typedKey].soloEncounter],
      ["DPS distribution",input.iterationStatistics?.dps]])
      if(value!=null&&!record(value))reject(label+" must be an object.");
    if(buildInput.gear?.items!=null&&(!Array.isArray(buildInput.gear.items)||
       buildInput.gear.items.some(item=>!record(item)||typeof item.slotKey!=="string")))
      reject("Gear items must be a list of slotted items.");
    if(buildInput.talents?.ranks!=null&&!record(buildInput.talents.ranks))reject("Talent ranks must be an object.");
    for(const key of ["buffs","consumables","targetDebuffs"]){
      const values=buildInput.modifiers?.[key];
      if(values!=null&&(!Array.isArray(values)||values.some(value=>typeof value!=="string")))
        reject("Modifier selections must be lists of keys.");
    }
    for(const key of ["dps","damageDone","ownerDamage","petDamage","hawkDamage"]){
      const value=input.combatSummary[key];
      if(value!=null&&(number(value)===null||number(value)<0))reject("Invalid combat metric: "+key+".");
    }
    if(input.iterationStatistics?.schema!==undefined&&
       (input.iterationStatistics.schema!=="warcraftsim.level60-distribution"||input.iterationStatistics.version!==1))
      reject("Unsupported distribution schema.");
    const report=JSON.parse(JSON.stringify(input)),build=report.build,summary=report.combatSummary,
      request=report.originalSimulationRequest,enc=build.encounter,typed=request[typedKey],
      mods=build.modifiers||{},batch=report.iterationStatistics||null,
      iterations=number(batch?.iterations??enc.iterations??request.iterations),
      statistics=iterations!==null&&iterations>1,sim=report.rawServerResponse.sim,
      actor=sim.batch?.representativeRun?.summary?.actorSummaries?.warrior||{},
      representativeDps=reported(summary,"dps",sim.dps??sim.meanDps),
      dps=statistics?number(batch?.dps?.mean):representativeDps,
      damage=reported(summary,"damageDone",sim.damageDone??actor.damageDone),
      ownerDamage=number(summary.ownerDamage),petDamage=number(summary.petDamage),
      hawkDamage=reported(summary,"hawkDamage",sim.hawkDamage),
      abilities=own(summary,"damageByAbility")?summary.damageByAbility:
        sim.damageByAbility??actor.damageDoneByAbility,
      integrityWarnings=[],integrityCaveats=[];
    function consistent(label,x,y,normalize=value=>value){
      if(x!==undefined&&y!==undefined&&!equal(normalize(x),normalize(y)))
        integrityWarnings.push(label+" disagrees between the saved build/request/report");
    }
    function identity(label,x,y){
      if(x===null||y===null)integrityCaveats.push(label+" build/request identity is unavailable");
      else if(!equal(x,y))integrityWarnings.push(label+" disagrees between the saved build and executed request");
    }
    const race=value=>typeof value==="string"&&value.trim()?value.trim().toLowerCase():null;
    identity("Race",race(build.race),race(typed.race??typed.character?.race));
    const nativeWarrior=build.classKey==="warrior",
      requestItems=nativeWarrior?typed.gear:typed.equipment?.items??typed.character?.equipment?.items;
    identity("Gear",equipmentIdentity(build.gear?.items,nativeWarrior),
      equipmentIdentity(requestItems,nativeWarrior));
    if(nativeWarrior&&build.gear?.items?.some(item=>number(item.temporaryEnchantSpellId)>0))
      integrityCaveats.push("Temporary enchant identity is unavailable in the native Warrior request");
    // Unified itemized ranks include intentionally unmodeled selections. The
    // typed caster ranks may be filtered, so they cannot stand in for this map.
    const executedRanks=nativeWarrior&&typed.talentMode==="tree"?typed.talentRanks:
      request.itemizedTalentMode===true?request.itemizedTalentRanks:null;
    identity("Talent ranks",selectedRanks(build.talents?.ranks),selectedRanks(executedRanks));
    for(const [label,x,y] of [["Duration",enc.duration,typed.durationSeconds],
      ["Seed",enc.seed,typed.seed],["Iterations",enc.iterations,batch?.iterations],
      ["Iterations",enc.iterations,request.iterations],["Base seed",typed.seed,batch?.baseSeed]])
      consistent(label,x,y,number);
    if(build.classKey!=="warrior")consistent("Target health",enc.targetHealth,typed.targetMaximumHealth,number);
    consistent("Scripted encounter",enc.soloScript,typed.soloEncounter);
    consistent("Target armor",enc.warrior?.targetArmor,typed.targetArmor,number);
    if(typedKey==="enhancement"){
      for(const [key,value] of Object.entries(enc.enhancement||{}))consistent("Enhancement "+key,value,typed[key]);
      consistent("Enhancement target semantics",summary.targetSemantics,{level:63,mitigation:"physical-armor-nature-unmitigated",targetArmor:typed.targetArmor,targetCount:1,initialHealthPercent:100});
    }
    if(typedKey==="rogue"){
      if(!record(enc.rogue)||!record(enc.rogue.tuning))reject("Assassination settings and explicit tuning are required.");
      const required=['targetArmor','targetDodgePercent','targetParryPercent','rogueMaximumHealth','startingEnergyPercent','poisonChargesPerWeapon','startInStealth','isBehindTarget','useExposeArmor','mainHandPoison','offHandPoison','useInstantPoison','bothWeaponsDaggers','mainHandWeaponType','offHandWeaponType','useUtilityAbilities','additionalTargets','enableBossAttacks','tuning'];
      for(const key of required)if(!own(enc.rogue,key)||!own(typed,key))reject("Assassination saved and executed "+key+" are required.");
      if(!record(typed.tuning))reject("Assassination executed tuning is required.");
      for(const key of ['maximumEnergy','startingEnergy','endingEnergy','maximumHealth','endingHealth']){
        const actual=key==='startingEnergy'?sim.run?.summary?.actorSummaries?.rogue?.startingResources?.energy:sim[key];
        if(!own(summary,key)||number(summary[key])===null||number(actual)===null)reject("Assassination resource metric "+key+" is required in both summary and execution.");
        consistent("Assassination "+key,summary[key],actual);
      }
      if(!record(summary.targetSemantics))reject("Assassination target semantics are required.");
      for(const [key,value] of Object.entries(enc.rogue))consistent("Assassination "+key,value,typed[key]);
      consistent("Assassination target semantics",summary.targetSemantics,{level:63,mitigation:"physical-armor-nature-unmitigated",targetArmor:typed.targetArmor,targetDodgePercent:typed.targetDodgePercent,targetParryPercent:typed.targetParryPercent,targetCount:1,initialHealthPercent:100});
      consistent("Assassination maximum Energy",summary.maximumEnergy,sim.maximumEnergy);
      consistent("Assassination starting Energy",summary.startingEnergy,sim.run?.summary?.actorSummaries?.rogue?.startingResources?.energy);
      consistent("Assassination ending Energy",summary.endingEnergy,sim.endingEnergy);
      consistent("Assassination player maximum Health",summary.maximumHealth,sim.maximumHealth);
    }
    consistent("Boss attacks",enc.warrior?.enableBossAutoAttacks,typed.enableBossAutoAttacks);
    consistent("Scripted parries",enc.hunterParryEvery,typed.scriptedParryEverySeconds,number);
    for(const key of ["buffs","consumables","targetDebuffs"])
      consistent(key,mods[key],typed.externalModifiers?.[key],chosen);
    consistent("Debuff mode",mods.targetDebuffMode,typed.externalModifiers?.targetDebuffMode);
    consistent("Baseline confidence",build.provisional,typed.useProvisionalLevel60Baseline);
    consistent("Request preset",build.specialization,typed.specialization??typed.preset);
    consistent("Response class",build.classKey,report.rawServerResponse.classKey);
    consistent("Response preset",build.specialization,report.rawServerResponse.specialization);
    if(iterations!==null&&(!Number.isInteger(iterations)||iterations<1||iterations>100))
      reject("Iteration count must be an integer from 1 to 100.");
    if(batch?.samples!=null&&(!Array.isArray(batch.samples)||batch.samples.some(sample=>
      !record(sample)||!Number.isInteger(number(sample.seed))||number(sample.dps)===null||number(sample.dps)<0)))
      reject("Distribution samples must contain finite DPS and integer seeds.");
    if(statistics){
      if(!batch?.samples?.length)integrityWarnings.push("Distribution samples and seed sequence are unavailable");
      else{
        if(batch.samples.length!==iterations)integrityWarnings.push("Distribution sample count disagrees with iterations");
        if(new Set(batch.samples.map(sample=>number(sample.seed))).size!==batch.samples.length)
          integrityWarnings.push("Distribution repeats a sample seed");
        const mean=batch.samples.reduce((sum,sample)=>sum+number(sample.dps)/batch.samples.length,0);
        if(dps!==null&&!close(dps,mean))integrityWarnings.push("Mean DPS disagrees with the distribution samples");
        const representative=batch.samples.find(sample=>number(sample.seed)===number(batch.representativeSeed));
        if(!representative)integrityWarnings.push("Representative seed is not recorded in the distribution samples");
        else if(representativeDps!==null&&!close(representativeDps,number(representative.dps)))
          integrityWarnings.push("Representative DPS disagrees with its sampled seed");
      }
    }
    const scenario=encounterScenario(typed,build.classKey);
    const encounter={
      duration:number(enc.duration??typed.durationSeconds),iterations,
      seed:number(enc.seed??typed.seed),
      targetHealth:build.classKey==="warrior"?null:number(enc.targetHealth??typed.targetMaximumHealth),
      targetLevel:number(typed.targetLevel??typed.bossLevel??typed.target?.level??(["enhancement","rogue"].includes(typedKey)?63:null)),
      targetArmor:number(typed.targetArmor??typed.bossArmor??typed.target?.armor??typed.tuning?.targetArmor),
      resistances:typed.targetResistances??typed.resistances??null,
      scriptedEncounter:enc.soloScript??typed.soloEncounter??null,
      additionalTargets:typed.additionalTargets??null,
      scenario,seedStrategy:statistics?batch?.seedStrategy??null:null,
      sampleSeeds:statistics&&Array.isArray(batch?.samples)?batch.samples.map(sample=>number(sample.seed)):null,
      incomingBossAttacks:build.classKey==="warrior"?
        enc.warrior?.enableBossAutoAttacks??typed.enableBossAutoAttacks??null:null,
      buffs:chosen(mods.buffs??typed.externalModifiers?.buffs),
      foodAndPrepull:chosen(mods.consumables??typed.externalModifiers?.consumables),
      targetDebuffs:chosen(mods.targetDebuffs??typed.externalModifiers?.targetDebuffs),
      targetDebuffMode:mods.targetDebuffMode??typed.externalModifiers?.targetDebuffMode??null,
      baselineMode:typeof build.provisional==="boolean"?
        build.provisional?"historical-provisional":"reviewed":null
    };
    const sources=Object.entries(record(abilities)?abilities:{}).filter(([,value])=>
      number(value)!==null&&number(value)>=0).map(([name,value])=>
      ({name,damage:number(value)})).sort((a,b)=>b.damage-a.damage);
    const reconcile=(total,parts)=>{
      if(total===null||parts.some(value=>value===null))return null;
      const sum=parts.reduce((value,part)=>value+part,0);
      return {status:close(total,sum)?"reconciled":"mismatch",difference:Math.abs(total-sum)};
    };
    const rawAbilities=sim.damageByAbility??actor.damageDoneByAbility;
    if(record(abilities)&&record(rawAbilities)&&
       (!equal(Object.keys(abilities).sort(),Object.keys(rawAbilities).sort())||
        Object.keys(abilities).some(key=>number(abilities[key])!==null&&
          (number(rawAbilities[key])===null||!close(number(abilities[key]),number(rawAbilities[key]))))))
      integrityWarnings.push("Ability damage disagrees with the raw server response");
    // Hawk is independent of the permanent pet. Never label its damage as pet damage.
    const reconciliation=reconcile(damage,[ownerDamage,petDamage,...(hawkDamage===null?[]:[hawkDamage])]);
    const abilityReconciliation=record(abilities)&&Object.keys(abilities).length&&
      sources.length===Object.keys(abilities).length?reconcile(damage,sources.map(source=>source.damage)):null;
    for(const [label,x,y] of [["DPS",representativeDps,number(sim.dps??sim.meanDps)],
      ["Total damage",damage,number(sim.damageDone??actor.damageDone)],
      ["Owner damage",ownerDamage,number(sim.hunterDamage??sim.warlockDamage??actor.damageDone)],
      ["Pet damage",petDamage,number(sim.petDamage)],["Hawk damage",hawkDamage,number(sim.hawkDamage)]])
      if(x!==null&&y!==null&&!close(x,y))integrityWarnings.push(label+" disagrees with the raw server response");
    if(damage!==null&&representativeDps!==null&&encounter.duration>0&&
       !close(damage,representativeDps*encounter.duration))
      integrityWarnings.push("Representative damage does not reconcile with DPS and duration");
    return {report,build,summary,encounter,statistics,iterations,dps,damage,
      ownerDamage,petDamage,hawkDamage,sources,reconciliation,abilityReconciliation,integrityWarnings,integrityCaveats,
      dpsLabel:statistics?"Mean DPS ("+iterations+" runs)":
        iterations===null?"DPS (iteration count unavailable)":"DPS (single seed)",
      label:build.classKey+" / "+build.specialization+" (seed "+(encounter.seed??"unreported")+
        (statistics?", "+iterations+" runs":"")+")"};
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
    for(const key of new Set([...Object.keys(lx.enhancement||{}),...Object.keys(rx.enhancement||{})]))
      if(!equal(lx.enhancement?.[key],rx.enhancement?.[key]))changes.push("Enhancement "+key+": "+String(lx.enhancement?.[key])+" → "+String(rx.enhancement?.[key]));
    for(const key of new Set([...Object.keys(lx.rogue||{}),...Object.keys(rx.rogue||{})])){
      if(key==='tuning'){
        for(const field of new Set([...Object.keys(lx.rogue?.tuning||{}),...Object.keys(rx.rogue?.tuning||{})]))
          if(!equal(lx.rogue?.tuning?.[field],rx.rogue?.tuning?.[field]))changes.push("Assassination tuning "+field+": "+String(lx.rogue?.tuning?.[field])+" → "+String(rx.rogue?.tuning?.[field]));
      }else if(!equal(lx.rogue?.[key],rx.rogue?.[key]))changes.push("Assassination "+key+": "+String(lx.rogue?.[key])+" → "+String(rx.rogue?.[key]));
    }
    if(!equal(left.preparedCombatConsumables,right.preparedCombatConsumables))
      changes.push("Prepared combat consumables differ");
    for(const key of ["buffs","consumables","targetDebuffs"])
      if(!equal(chosen(left.modifiers?.[key]),chosen(right.modifiers?.[key])))
        changes.push("Selected "+key+" differ");
    if(!equal(a.encounter.scenario,b.encounter.scenario))changes.push("Encounter assumptions differ");
    return changes;
  }
  function compareReports(left,right){
    const a=inspectReport(left),b=inspectReport(right),warnings=[],caveats=[];
    for(const [slot,report] of [["A",a],["B",b]]){
      for(const warning of report.integrityWarnings)warnings.push(slot+": "+warning);
      for(const caveat of report.integrityCaveats)caveats.push(slot+": "+caveat);
    }
    function mismatch(label,x,y){if(!equal(x,y))warnings.push(label+" differs");}
    mismatch("Class",a.build.classKey,b.build.classKey);
    for(const [label,key] of [["Duration","duration"],["Iterations","iterations"],
      ["Seed","seed"],["Target health","targetHealth"],["Target level","targetLevel"],
      ["Target armor","targetArmor"],["Target resistances","resistances"],
      ["Scripted encounter","scriptedEncounter"],["Additional targets","additionalTargets"],
      ["Boss attack assumptions","incomingBossAttacks"],["Encounter assumptions","scenario"],
      ["Seed strategy","seedStrategy"],["Sample seeds","sampleSeeds"],["Buffs","buffs"],
      ["Food and pre-pull effects","foodAndPrepull"],["Target debuffs","targetDebuffs"],
      ["Debuff application mode","targetDebuffMode"],["Baseline confidence","baselineMode"]])
      mismatch(label,a.encounter[key],b.encounter[key]);
    for(const key of ["duration","seed","iterations","baselineMode","buffs","foodAndPrepull",
      "targetDebuffs","targetDebuffMode","scriptedEncounter"]){
      if(a.encounter[key]===null||b.encounter[key]===null)
        warnings.push("Required "+key+" not recorded");
    }
    for(const key of ["targetLevel","targetArmor","resistances"])
      if(a.encounter[key]===null&&b.encounter[key]===null)
        caveats.push(key+" is not recorded in these reports");
    if(a.encounter.duration<=0||b.encounter.duration<=0)warnings.push("Duration must be positive");
    if((a.encounter.seed!==null&&!Number.isInteger(a.encounter.seed))||
       (b.encounter.seed!==null&&!Number.isInteger(b.encounter.seed)))warnings.push("Seed must be an integer");
    if(a.dps===null||b.dps===null)warnings.push("DPS not reported by both runs");
    if(a.reconciliation?.status==="mismatch"||b.reconciliation?.status==="mismatch")
      warnings.push("Owner and companion damage do not reconcile in at least one report");
    if(a.abilityReconciliation?.status==="mismatch"||b.abilityReconciliation?.status==="mismatch")
      warnings.push("Ability damage does not reconcile with total damage in at least one report");
    if(a.abilityReconciliation===null||b.abilityReconciliation===null)
      caveats.push("Ability damage reconciliation is unavailable for at least one run");
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
      "Not directly comparable. Mismatched, missing or inconsistent report data prevent a meaningful DPS delta.";
    for(const warning of c.warnings)$("compareWarnings").append(line("Mismatch: "+warning));
    for(const caution of c.caveats)$("compareWarnings").append(line("Coverage: "+caution));
    const dpsLabel=a.dpsLabel===b.dpsLabel?a.dpsLabel:"DPS (see each run's mode)";
    appendMetric(dpsLabel,fmt(a.dps)+" ["+a.dpsLabel+"]",fmt(b.dps)+" ["+b.dpsLabel+"]",
      c.dpsDelta===null?"Not comparable":(c.dpsDelta>=0?"+":"")+
        fmt(c.dpsDelta)+" ("+fmt(c.dpsDeltaPercent)+"%)");
    appendMetric(a.statistics||b.statistics?"Representative run damage (not mean)":"Total damage",
      fmt(a.damage,0),fmt(b.damage,0),c.damageDelta===null?
        "Not compared":(c.damageDelta>=0?"+":"")+fmt(c.damageDelta,0));
    appendMetric("Owner damage (representative)",fmt(a.ownerDamage,0),
      fmt(b.ownerDamage,0),"—");
    appendMetric("Pet / demon damage (representative)",fmt(a.petDamage,0),
      fmt(b.petDamage,0),"—");
    if(a.hawkDamage!==null||b.hawkDamage!==null)
      appendMetric("Summoned Hawk damage (representative)",fmt(a.hawkDamage,0),fmt(b.hawkDamage,0),"-");
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
