"use strict";
(function(root){
  const legacyRogue=(typeof module==='object'&&module.exports?
    require('./solo-dps-assumptions.js'):root.SoloDpsAssumptions).legacyRogue;
  // Report-only comparison: never reruns, rewrites, or ranks mismatched simulations.
  const own=(object,key)=>Object.prototype.hasOwnProperty.call(object||{},key);
  const record=value=>value!==null&&typeof value==="object"&&!Array.isArray(value);
  const number=value=>
    (typeof value==="number"||(typeof value==="string"&&value.trim()!==""))&&
    Number.isFinite(Number(value))?Number(value):null;
  const keyFor=spec=>({"beast-mastery":"beastMastery",marksmanship:"marksmanship",
    survival:"survival"})[spec]||spec;
  const specs={rogue:["assassination","combat"],priest:["shadow"],druid:["balance","feral"],shaman:["elemental","enhancement"],mage:["arcane","fire","frost"],warlock:["affliction","demonology","destruction"],
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
    // Legacy admission has already checked the only permitted present value.
    if(classKey==='rogue'&&typed.bossAttacksUseWeapon===false)delete scenario.bossAttacksUseWeapon;
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
  // Historical/manual Cat reports have a separate strict DTO. Do not route a
  // Druid through Balance, reconstruct Cat AP, or invent unsupported equipment.
  function inspectFeralReport(input,reject){
    const report=JSON.parse(JSON.stringify(input)),build=report.build,
      request=report.originalSimulationRequest,raw=report.rawServerResponse,
      summary=report.combatSummary,integrityWarnings=[],integrityCaveats=[];
    const required=(value,label)=>{if(!record(value))reject("Feral "+label+" is required.");return value;};
    const keys=(value,names,label,exact=false)=>{
      required(value,label);
      if(names.some(key=>!own(value,key))||exact&&Object.keys(value).some(key=>!names.includes(key)))
        reject("Feral "+label+" fields are missing or unsupported.");
    };
    const numeric=(value,label,minimum=0)=>{
      if(typeof value!=="number"||!Number.isFinite(value)||value<minimum)
        reject("Feral "+label+" must be a finite JSON number in range.");
      return value;
    };
    const text=(value,label)=>{if(typeof value!=="string"||!value.trim())reject("Feral "+label+" is required.");};
    const notes=(value,label)=>{if(!Array.isArray(value)||!value.length||value.some(x=>typeof x!=="string"||!x.trim()))reject("Feral "+label+" is required.");};
    const same=(label,x,y)=>{if(!equal(x,y))integrityWarnings.push("Feral "+label+" disagrees between saved build/request/preparation/report/execution");};
    // Reports contain unrounded machine values. Only IEEE-754 arithmetic noise
    // is tolerated here, not the display-rounding tolerance of older reports.
    const near=(a,b)=>Math.abs(a-b)<=Math.max(1,Math.abs(a),Math.abs(b))*Number.EPSILON*8;
    const sameNumber=(label,x,y)=>{numeric(x,label);numeric(y,label);same(label,x,y);};
    const arithmetic=(label,x,y)=>{
      numeric(x,label);numeric(y,label);
      if(!near(x,y))integrityWarnings.push("Feral "+label+" does not reconcile with preparation or raw execution");
    };
    const inputMode="historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap",
      manualKeys=["preTalentCatAttackPower","weaponDps","meleeHitPercent","meleeCritPercent","shapeshiftMana","shiftingPowerBaseMana","shiftingPowerCostReductionPercent"],
      scenarioKeys=["role","seed","durationSeconds","captureTimeline","targetLevel","targetInitialHealthPercent","targetMaximumHealth","targetArmor","startInProwl","preferredCatOpener","targetBehind","useFaerieFire","useFeralCooldowns"],
      totalKeys=["balancePointsSpent","feralPointsSpent","restorationPointsSpent","totalPoints","unspentPoints"];
    keys(build,["schema","version","inputMode","itemizedMode","classKey","specialization","race","provisional","manualInputs","scenario","talents"],"saved build",true);
    keys(request,["classKey","specialization","itemizedTalentMode","itemizedTalentRanks","feral"],"strict envelope",true);
    const typed=required(request.feral,"typed historical request"),
      prep=required(raw.preparation,"preparation"),preview=required(prep.preview,"numeric preview"),
      baseline=required(prep.baseline,"complete historical baseline"),direct=required(prep.directRequest,"converted direct request"),
      tuning=required(direct.tuning,"direct tuning"),sim=required(raw.sim,"raw simulation"),
      run=required(sim.run,"raw run"),runSummary=required(run.summary,"run summary"),
      actor=required(runSummary.actorSummaries?.druid,"druid actor receipt");
    keys(typed,["character","role","useProvisionalLevel60Baseline","useReviewedLevel60Baseline","level60Baseline","manualInputs","talents",...scenarioKeys.filter(x=>x!=="role")],"typed historical request",true);
    keys(typed.character,["classKey","specializationKey","rulesetKey","level","race"],"character",true);
    same("character identity",typed.character,{classKey:"druid",specializationKey:"feral",rulesetKey:"wow-forever",level:60,race:build.race});
    if(!["night-elf","tauren","troll"].includes(build.race)||build.inputMode!==inputMode||prep.inputMode!==inputMode||
       build.itemizedMode!==true||request.itemizedTalentMode!==true||build.provisional!==true||
       typed.useProvisionalLevel60Baseline!==true||typed.useReviewedLevel60Baseline!==false||typed.level60Baseline!==null)
      reject("Feral historical/manual identity and explicit provisional opt-in are required; observations are not accepted.");
    same("envelope class",request.classKey,"druid");same("envelope specialization",request.specialization,"feral");
    same("response class",raw.classKey,"druid");same("response specialization",raw.specialization,"feral");same("response level",raw.level,60);
    same("returned historical request",required(prep.request,"returned historical request"),typed);
    same("complete preparation copy",required(report.feralPreparation,"exported preparation"),prep);
    same("resolved preparation copy",required(report.resolvedCharacter,"resolved preparation"),prep);
    for(const [label,manual] of [["saved manual inputs",build.manualInputs],["typed manual inputs",typed.manualInputs]]){
      keys(manual,manualKeys,label,true);
      for(const key of manualKeys)numeric(manual[key],label+" "+key);
      if(["meleeHitPercent","meleeCritPercent","shiftingPowerCostReductionPercent"].some(key=>manual[key]>100))reject("Feral manual percentages must not exceed 100.");
    }
    same("manual assumptions",build.manualInputs,typed.manualInputs);
    keys(build.scenario,scenarioKeys,"saved scenario",true);
    for(const key of scenarioKeys)same("scenario "+key,build.scenario[key],typed[key]);
    for(const s of [build.scenario,typed]){
      for(const key of ["durationSeconds","targetLevel","targetInitialHealthPercent","targetMaximumHealth","targetArmor"])numeric(s[key],key);
      if(!Number.isInteger(s.seed)||s.seed<-2147483648||s.seed>2147483647||s.durationSeconds<=0||s.durationSeconds>1200||
         s.targetMaximumHealth<=0||s.targetLevel!==63||s.targetInitialHealthPercent!==100||s.role!=="cat"||
         !["ravage","pounce"].includes(s.preferredCatOpener)||s.startInProwl&&s.preferredCatOpener==="ravage"&&!s.targetBehind)
        reject("Feral fixed-Cat scenario, seed, position or opener is invalid.");
      for(const key of ["captureTimeline","startInProwl","targetBehind","useFaerieFire","useFeralCooldowns"])
        if(typeof s[key]!=="boolean")reject("Feral "+key+" must be a boolean.");
    }
    // Exact maps retain zero-key presence. Positive-only rank filtering would
    // incorrectly certify a different strict executed map.
    const caps={ferocity:5,"heart-of-the-wild":5,"feral-swiftness":2,"shredding-attacks":3,"savage-fury":2,"sharpened-claws":2,"shifting-power":1,"predatory-strikes":3,"blood-frenzy":2,"improved-shifting-power":2,"leader-of-the-pack":1,"natural-instinct":2,berserk:1,furor:5};
    const rankMap=(value,label)=>{
      required(value,label);
      if(Object.entries(value).some(([key,rank])=>!own(caps,key)||!Number.isInteger(rank)||rank<0||rank>caps[key]||key==="furor"&&rank!==0&&rank!==5))
        reject("Feral "+label+" must preserve supported exact integer ranks, including zeros.");
    };
    rankMap(request.itemizedTalentRanks,"full talent map");
    for(const [label,talents] of [["saved talents",build.talents],["typed talents",typed.talents]]){
      keys(talents,["ranks",...totalKeys],label,true);rankMap(talents.ranks,label);
      for(const key of totalKeys)if(!Number.isInteger(talents[key])||talents[key]<0)reject("Feral declared totals must be JSON integers.");
      const total=Object.values(talents.ranks).reduce((a,b)=>a+b,0),restoration=talents.ranks.furor||0;
      same(label+" declared totals",Object.fromEntries(totalKeys.map(key=>[key,talents[key]])),
        {balancePointsSpent:0,feralPointsSpent:total-restoration,restorationPointsSpent:restoration,totalPoints:total,unspentPoints:51-total});
    }
    same("saved/typed talent declarations",build.talents,typed.talents);
    same("full/typed exact rank identity",request.itemizedTalentRanks,typed.talents.ranks);
    rankMap(direct.talents?.ranks,"direct talent map");same("direct exact rank identity",direct.talents.ranks,typed.talents.ranks);
    same("allocated points",raw.allocatedTalentPoints,build.talents.totalPoints);same("unspent points",raw.unspentTalentPoints,build.talents.unspentPoints);
    const allocation=required(report.talentAllocation,"talent allocation receipt");
    same("allocation mode",allocation.mode,"strict-rank-derived");same("allocation selected total",allocation.totalSelectedRanks,build.talents.totalPoints);
    same("allocation unspent total",allocation.unspentPoints,build.talents.unspentPoints);
    same("allocation tree totals",allocation.treePoints,Object.fromEntries(totalKeys.filter(x=>x.endsWith("PointsSpent")).map(key=>[key,build.talents[key]])));
    if(!Array.isArray(allocation.selected))reject("Feral selected talent receipt is required.");
    const selected={};
    for(const entry of allocation.selected){
      if(!record(entry)||!own(caps,entry.key)||own(selected,entry.key)||!Number.isInteger(entry.rank)||entry.rank<=0)reject("Feral selected talent receipt is invalid.");
      selected[entry.key]=entry.rank;
      same("selected talent source cap "+entry.key,entry.maxRank,caps[entry.key]);
      same("selected talent source tree "+entry.key,entry.tree,entry.key==="furor"?"restoration":"feral");
      for(const key of ["historicalValidationSourceStatus","historicalValidationSourceCommit","historicalValidationSourceUrl","provenance","simulationStatus","dataConfidence","knownLimitations"])text(entry[key],"selected talent "+entry.key+" "+key);
      same("selected talent source status "+entry.key,entry.historicalValidationSourceStatus,raw.talentSourceStatus);
      same("selected talent source commit "+entry.key,entry.historicalValidationSourceCommit,raw.talentSourceCommit);
      same("selected talent source URL "+entry.key,entry.historicalValidationSourceUrl,raw.talentSourceUrl);
      if(entry.sourceCommit!=null)same("selected visual source commit "+entry.key,entry.sourceCommit,raw.talentSourceCommit);
      same("selected talent confidence "+entry.key,entry.dataConfidence,"historical-provisional");
      same("selected talent partial status "+entry.key,entry.simulationStatus,["furor","feral-swiftness"].includes(entry.key)?"inactive-in-fixed-cat":"fixed-cat-partial");
    }
    same("selected talent receipt",selected,Object.fromEntries(Object.entries(typed.talents.ranks).filter(([,rank])=>rank>0)));
    const baselineKeys=["race","classKey","level","strength","agility","stamina","intellect","spirit","observedUnbuffedMaximumHealth","observedUnbuffedMaximumMana","observedUnbuffedMeleeAttackPower","observedUnbuffedRangedAttackPower","observedUnbuffedArmor","clientBuild","sourceKind","sourceReference","recordedAtUtc","verificationStatus","includesRacialPassives","confidenceClassification","derivationNote"];
    keys(baseline,baselineKeys,"complete baseline");
    for(const key of ["clientBuild","sourceKind","sourceReference","recordedAtUtc","verificationStatus","confidenceClassification","derivationNote"])text(baseline[key],"baseline "+key);
    for(const key of ["strength","agility","stamina","intellect","spirit","observedUnbuffedMaximumHealth","observedUnbuffedMaximumMana"])numeric(baseline[key],"baseline "+key);
    same("baseline race",baseline.race,build.race);same("baseline class",baseline.classKey,"druid");same("baseline level",baseline.level,60);
    if(baseline.includesRacialPassives!==false||baseline.observedUnbuffedMeleeAttackPower!==null||
       baseline.sourceKind!=="historical-classic-stat-proxy"||baseline.verificationStatus!=="provisional-documented-proxy")
      reject("Feral historical primary/resource baseline must not claim observed Cat AP or racial completeness.");
    const baselineSummary=required(raw.baseline,"baseline summary"),renamed={observedUnbuffedMaximumHealth:"nakedMaximumHealth",observedUnbuffedMaximumMana:"nakedMaximumMana",observedUnbuffedMeleeAttackPower:"nakedMeleeAttackPower",observedUnbuffedRangedAttackPower:"nakedRangedAttackPower",observedUnbuffedArmor:"nakedArmor"};
    keys(baselineSummary,[...baselineKeys.map(key=>renamed[key]||key),"baseMana","baseManaSourceBuild","warning"],"baseline summary");
    for(const key of baselineKeys)same("baseline summary "+key,baselineSummary[renamed[key]||key],baseline[key]);
    text(baselineSummary.baseManaSourceBuild,"base Mana source build");text(baselineSummary.warning,"baseline warning");numeric(baselineSummary.baseMana,"base Mana");
    same("baseline provenance copy",required(report.baselineProvenance,"baseline provenance"),baselineSummary);
    for(const key of ["talentSourceStatus","talentSourceCommit","talentSourceUrl"])text(raw[key],key);
    const metadata=required(report.metadata,"simulator metadata");
    for(const key of ["ruleset","simulatorVersion","sourceCommit","runtimeTargetBuild","historicalTalentReferenceCommit"])text(metadata[key],"metadata "+key);
    same("metadata ruleset",metadata.ruleset,"wow-forever");same("talent source commit",metadata.historicalTalentReferenceCommit,raw.talentSourceCommit);
    for(const [label,value] of [["adapter modeling notes",prep.modelingNotes],["preview modeling notes",preview.modelingNotes],["talent modeling notes",raw.talentModelingNotes],["exported warnings",report.warnings]])notes(value,label);
    for(const warning of [...prep.modelingNotes,...preview.modelingNotes,...raw.talentModelingNotes,...(metadata.notes||[]),...allocation.selected.map(entry=>entry.knownLimitations)])
      if(!report.warnings.includes(warning))integrityWarnings.push("Feral exported warnings omit an actual source/modeling assumption");
    keys(raw.inputTiming,["additionalInputLatencyMilliseconds","note"],"actual input timing");
    numeric(raw.inputTiming.additionalInputLatencyMilliseconds,"actual additional latency");text(raw.inputTiming.note,"timing caveat");
    same("input timing copy",required(report.inputTiming,"exported input timing"),raw.inputTiming);
    same("summary input timing",required(summary.inputTiming,"summary input timing"),raw.inputTiming);
    keys(direct,[...scenarioKeys.filter(key=>!["targetLevel","targetInitialHealthPercent"].includes(key)),"maximumHealth","maximumMana","useBearUtility","useThorns","competingThreatOnBoss","additionalTargets","targetTransitions","initialComboPointsByTarget","formTransitions","enableBossAttacks","bossHitDamage","bossSwingSeconds","bearBaseArmor","bearDodgePercent","incomingRageExpectedCreatureHealth","incomingRageExpectedArmorReductionPercent","tankBurstWindowSeconds","talents","tuning"],"complete converted direct request",true);
    for(const key of scenarioKeys.filter(key=>!["targetLevel","targetInitialHealthPercent"].includes(key))){
      if(!own(direct,key))reject("Feral direct "+key+" is required.");same("converted "+key,direct[key],typed[key]);
    }
    for(const [key,expected] of Object.entries({useBearUtility:false,useThorns:false,competingThreatOnBoss:0,additionalTargets:0,targetTransitions:[],initialComboPointsByTarget:{},formTransitions:[],enableBossAttacks:false})){
      if(!own(direct,key))reject("Feral direct restriction "+key+" is required.");same("fixed-Cat restriction "+key,direct[key],expected);
    }
    for(const key of ["bossHitDamage","bossSwingSeconds","bearBaseArmor","bearDodgePercent","incomingRageExpectedCreatureHealth","incomingRageExpectedArmorReductionPercent","tankBurstWindowSeconds"])numeric(direct[key],"retained direct assumption "+key);
    sameNumber("pre-talent Health",direct.maximumHealth,baseline.observedUnbuffedMaximumHealth);
    sameNumber("pre-talent Mana",direct.maximumMana,baseline.observedUnbuffedMaximumMana);
    const tuningKeys=["weaponDps","attackPower","meleeHitPercent","meleeCritPercent","shapeshiftMana","shiftingPowerBaseMana","shiftingPowerCostReductionPercent","omenProcPercent","catClawBonus","catShredBonus","catRakeDamage","catRakeTick","ripTickPerPoint","bitePerPoint","bearMaulBonus","bearSwipeBase","lacerateTickPerStack","thickHideArmorPercentPerRank","defenseSkill","thickHideFormArmorMultiplier","baseStrength","baseStamina","baseIntellect","attackPowerPerStrength","healthPerStamina","manaPerIntellect","feralInstinctSwipeDamagePercentPerRank","bearBaseThreatMultiplier","primalBiteThreatMultiplier","bearAutoRageOnHit","catOpenersBonus","faerieFireArmorReduction","thornsBaseDamage","thornsSpellPower","berserkCooldownSeconds","berserkDurationSeconds","frenziedRegenerationCooldownSeconds","frenziedRegenerationDurationSeconds","tankArmorReductionConstant"];
    keys(tuning,tuningKeys,"complete direct tuning",true);for(const key of tuningKeys)numeric(tuning[key],"direct tuning "+key);
    for(const key of manualKeys)sameNumber("manual/direct "+key,tuning[key==="preTalentCatAttackPower"?"attackPower":key],typed.manualInputs[key]);
    for(const key of ["strength","stamina","intellect"])sameNumber("baseline/direct "+key,tuning["base"+key[0].toUpperCase()+key.slice(1)],baseline[key]);
    const previewNumbers=["authoredAttackPower","heartOfTheWildAttackPowerIncrement","predatoryStrikesAttackPower","spellbookAttackPower","maximumHealth","maximumMana","initialShiftManaCost","manaAfterInitialShift","maximumEnergy","startingEnergy","maximumRage","startingRage"];
    for(const key of previewNumbers)numeric(preview[key],"preview "+key);
    same("initial form",preview.initialForm,"cat");same("direct preview input mode",preview.inputMode,"authored-inclusive");
    const hotw=typed.talents.ranks["heart-of-the-wild"]||0,predatory=typed.talents.ranks["predatory-strikes"]||0;
    sameNumber("authored manual AP",preview.authoredAttackPower,typed.manualInputs.preTalentCatAttackPower);
    arithmetic("incremental HOTW AP",preview.heartOfTheWildAttackPowerIncrement,baseline.strength*.02*hotw*tuning.attackPowerPerStrength);
    arithmetic("incremental Predatory AP",preview.predatoryStrikesAttackPower,60*.5*predatory);
    arithmetic("effective AP components",preview.spellbookAttackPower,preview.authoredAttackPower+preview.heartOfTheWildAttackPowerIncrement+preview.predatoryStrikesAttackPower);
    sameNumber("Cat Health",preview.maximumHealth,baseline.observedUnbuffedMaximumHealth);
    arithmetic("HOTW maximum Mana",preview.maximumMana,baseline.observedUnbuffedMaximumMana+baseline.intellect*.02*hotw*tuning.manaPerIntellect);
    sameNumber("initial shift cost",preview.initialShiftManaCost,typed.manualInputs.shapeshiftMana);
    if(preview.initialShiftManaCost>preview.maximumMana)reject("Feral initial shift is unaffordable.");
    arithmetic("post-shift Mana",preview.manaAfterInitialShift,preview.maximumMana-preview.initialShiftManaCost);
    for(const [key,value] of Object.entries({maximumEnergy:100,startingEnergy:0,maximumRage:100,startingRage:0}))sameNumber(key,preview[key],value);
    if(!Array.isArray(preview.abilities)||!preview.abilities.length)reject("Feral prepared abilities are required.");
    const abilityKeys=preview.abilities.map(ability=>ability?.key);
    if(abilityKeys.some(key=>typeof key!=="string"||!key)||new Set(abilityKeys).size!==abilityKeys.length)reject("Feral prepared ability identities are invalid.");
    const fixedAbilities=["claw","shred","rake","rip","ferocious-bite","maul","swipe","lacerate","enrage","pounce","ravage","cower","growl","faerie-fire","challenging-roar","demoralizing-roar","bash","frenzied-regeneration"];
    same("complete prepared ability identities",[...abilityKeys].sort(),[...fixedAbilities,...["shifting-power","berserk"].filter(key=>(typed.talents.ranks[key]||0)>0)].sort());
    for(const ability of preview.abilities){
      keys(ability,["key","name","classKey","specializationKey","cooldownSeconds","globalCooldownSeconds","resourceCosts","effects"],"prepared ability "+ability.key);
      same("prepared ability class "+ability.key,ability.classKey,"druid");same("prepared ability specialization "+ability.key,ability.specializationKey,"feral");
      if(!Array.isArray(ability.resourceCosts)||!Array.isArray(ability.effects))reject("Feral complete ability resource/effect receipts are required.");
    }
    for(const key of ["shifting-power","berserk"])same("learned "+key+" preparation",abilityKeys.includes(key),(typed.talents.ranks[key]||0)>0);
    const rotation=required(preview.configuredRotation,"configured priority");
    if(!Array.isArray(rotation.entries))reject("Feral configured priority entries are required.");
    same("configured priority class",rotation.classKey,"druid");same("configured priority ruleset",rotation.rulesetKey,"wow-forever");same("configured priority simulation type",rotation.simulationType,0);
    const priority=[...(typed.useFaerieFire?["faerie-fire"]:[]),...(typed.useFeralCooldowns?["berserk","frenzied-regeneration"]:[]),"ravage","pounce","shifting-power","rip","ferocious-bite","rake","shred","claw"].filter(key=>abilityKeys.includes(key));
    same("configured learned priority",rotation.entries.map(entry=>entry.abilityKey),priority);
    rotation.entries.forEach((entry,index)=>{
      same("configured priority default target",entry.target,{mode:"default",actorKey:null,includeSelf:true,teamKey:null,relationship:"any",allowedRoles:[],excludedRoles:[]});
      same("configured priority next-swing replacement",entry.nextSwingReplacementKey,"");same("configured priority auto attack",entry.autoAttackKey,"");
      if(entry.priority!==index||entry.actionType!=="ability"||entry.isEnabled!==true||entry.interruptCurrentCast!==false||
         !equal(entry.conditions,[])||!equal(entry.alternativeConditionGroups,[]))integrityWarnings.push("Feral configured priority policy is inconsistent with fixed Cat preparation");
    });
    same("configured priority copy",required(summary.configuredRotation,"summary configured priority"),rotation);
    for(const key of previewNumbers)sameNumber("summary "+key,summary[key],preview[key]);
    keys(actor.startingResources,["mana","energy","rage"],"actor starting resources");keys(actor.endingResources,["mana","energy","rage"],"actor ending resources");
    same("actor identity",actor.actorKey,"druid");same("summary actor identity",summary.actorKey,"druid");
    same("actor resource copy",required(summary.actorResources,"summary actor resources"),{starting:actor.startingResources,ending:actor.endingResources});
    // Engine AddActor records Mana before the mandatory shift; the preparation
    // reports the separate post-shift boundary. Neither is an ending Health.
    sameNumber("recorded starting Mana",actor.startingResources.mana,preview.maximumMana);
    sameNumber("summary starting Mana",summary.startingMana,actor.startingResources.mana);
    for(const key of ["energy","rage"])sameNumber("recorded starting "+key,actor.startingResources[key],preview["starting"+key[0].toUpperCase()+key.slice(1)]);
    for(const key of ["mana","energy","rage"]){
      const ending="ending"+key[0].toUpperCase()+key.slice(1),maximum="maximum"+key[0].toUpperCase()+key.slice(1);
      sameNumber("summary "+ending,summary[ending],sim[ending]);sameNumber("actor "+ending,actor.endingResources[key],sim[ending]);
      if(actor.endingResources[key]>preview[maximum])integrityWarnings.push("Feral ending "+key+" exceeds its prepared cap");
    }
    if(summary.endingHealth!==null)integrityWarnings.push("Feral ending Health is unavailable; tank projection is not an actual Health receipt");
    same("physical/bleed target semantics",required(summary.targetSemantics,"target semantics"),{level:63,mitigation:"physical-armor-bleeds-unmitigated",targetArmor:typed.targetArmor,targetCount:1,initialHealthPercent:100});
    same("executed seed",runSummary.seed,typed.seed);sameNumber("executed duration",runSummary.durationSeconds,typed.durationSeconds);
    if(!Array.isArray(run.timeline)||!Number.isInteger(run.processedEventCount)||run.processedEventCount<0)reject("Feral complete run/timeline receipt is required.");
    same("timeline event count",summary.timelineEventCount,run.timeline.length);
    if(!typed.captureTimeline&&run.timeline.length)integrityWarnings.push("Feral timeline capture disagrees with executed request");
    const custom=required(run.customMetrics,"runtime custom metrics");
    arithmetic("runtime HOTW AP",custom["feral-heart-of-the-wild-attack-power"],preview.authoredAttackPower+preview.heartOfTheWildAttackPowerIncrement);
    sameNumber("runtime maximum Health",custom["feral-heart-of-the-wild-maximum-health"],preview.maximumHealth);
    sameNumber("runtime maximum Mana",custom["feral-heart-of-the-wild-maximum-mana"],preview.maximumMana);
    for(const [field,key] of [["omenProcs","feral-omen-procs"],["clearcastsUsed","feral-clearcasts-used"],["comboPoints","feral-combo-points"]])sameNumber("runtime "+field,sim[field],custom[key]);
    // Preserve the complete direct-runner result, including latent legacy tank
    // fields, without representing those fields as supported Cat mechanics.
    const simulationNumbers=["dps","damageDone","endingMana","endingEnergy","endingRage","comboPoints","lacerateStacks","omenProcs","clearcastsUsed","formChanges","damageTaken","armorPreventedDamage","dodgedIncomingHits","maximumBurstDamage","maximumBurstPercentOfHealth","frenziedRegenerationHealing","projectedEndingHealth","minimumProjectedHealth","effectiveSelfHealing","wastedSelfHealing","threatGenerated","threatPerSecond"];
    keys(sim,[...simulationNumbers,"projectedHealthZeroAtSeconds","comboPointsByTarget","damageByAbility","run"],"complete raw simulation");
    for(const key of simulationNumbers)numeric(sim[key],"raw simulation "+key);
    if(sim.projectedHealthZeroAtSeconds!==null)numeric(sim.projectedHealthZeroAtSeconds,"legacy projected Health-zero time");
    required(sim.comboPointsByTarget,"raw target combo-point map");
    const dps=numeric(summary.dps,"DPS"),damage=numeric(summary.damageDone,"damage"),ownerDamage=numeric(summary.ownerDamage,"owner damage");
    sameNumber("DPS",dps,sim.dps);sameNumber("total damage",damage,sim.damageDone);sameNumber("owner damage",ownerDamage,actor.damageDone);
    sameNumber("run damage",damage,runSummary.damageDone);arithmetic("DPS and duration",damage,dps*typed.durationSeconds);
    sameNumber("owner/total damage",damage,ownerDamage);
    if(summary.petDamage!==0||summary.hawkDamage!==null)integrityWarnings.push("Feral fixed Cat has no companion damage");
    required(summary.damageByAbility,"summary ability damage");required(sim.damageByAbility,"raw ability damage");
    for(const [label,value] of [["raw ability damage",sim.damageByAbility],["actor ability damage",actor.damageDoneByAbility],["run ability damage",runSummary.damageDoneByAbility]]){
      required(value,label);same(label,summary.damageByAbility,value);
      for(const [key,amount] of Object.entries(value))numeric(amount,label+" "+key);
    }
    const metrics=required(runSummary.abilities,"ability metrics");same("summary ability metrics",required(summary.abilityMetrics,"exported ability metrics"),metrics);
    same("actor ability metrics",required(actor.abilities,"actor ability metrics"),metrics);
    for(const [key,ability] of Object.entries(metrics)){
      const countKeys=["castStartedCount","castCompletedCount","castCancelledCount","channelStartedCount","channelCompletedCount","channelCancelledCount","damageOccurrenceCount","healingOccurrenceCount","absorbConsumptionCount","criticalDamageCount","criticalHealingCount","directOccurrenceCount","periodicOccurrenceCount","channelTickOccurrenceCount"];
      keys(ability,["abilityKey",...countKeys,"damageDone","healingDone","overhealingDone","absorptionDone","resultCounts"],"complete ability metric "+key);
      for(const count of countKeys)if(!Number.isInteger(ability[count])||ability[count]<0)reject("Feral ability count "+key+" "+count+" must be a nonnegative integer.");
      for(const field of ["damageDone","healingDone","overhealingDone","absorptionDone"])numeric(ability[field],"ability "+key+" "+field);
      required(ability.resultCounts,"ability result counts "+key);
      if(Object.values(ability.resultCounts).some(count=>!Number.isInteger(count)||count<0))reject("Feral ability result counts must be nonnegative integers.");
      same("ability metric identity",ability.abilityKey,key);
      sameNumber("ability metric damage "+key,ability.damageDone,sim.damageByAbility[key]??0);
    }
    for(const key of Object.keys(sim.damageByAbility))if(!own(metrics,key))reject("Feral per-ability metric "+key+" is required.");
    const sources=Object.entries(summary.damageByAbility).map(([name,amount])=>({name,damage:amount})).sort((a,b)=>b.damage-a.damage),
      sum=sources.reduce((total,source)=>total+source.damage,0),abilityReconciliation={status:near(damage,sum)?"reconciled":"mismatch",difference:Math.abs(damage-sum)},
      reconciliation={status:near(damage,ownerDamage)?"reconciled":"mismatch",difference:Math.abs(damage-ownerDamage)};
    if(report.iterationStatistics!=null)reject("Feral supports one deterministic run, not distribution statistics.");
    const manualBuildKeys=["preTalentCatAttackPower","weaponDps","meleeHitPercent","meleeCritPercent"],
      manualDirectKeys=manualKeys.map(key=>key==="preTalentCatAttackPower"?"attackPower":key),
      sourcePolicy={metadata,baseline,baselineSummary,inputMode:prep.inputMode,talentSourceStatus:raw.talentSourceStatus,talentSourceCommit:raw.talentSourceCommit,talentSourceUrl:raw.talentSourceUrl,
        talentModelingNotes:raw.talentModelingNotes,modelingNotes:prep.modelingNotes,previewModelingNotes:preview.modelingNotes,
        additionalWarnings:report.warnings.filter(note=>![...prep.modelingNotes,...preview.modelingNotes,...raw.talentModelingNotes,...(metadata.notes||[]),...allocation.selected.map(entry=>entry.knownLimitations)].includes(note)),
        tuning:Object.fromEntries(Object.entries(tuning).filter(([key])=>!manualDirectKeys.includes(key)&&!["baseStrength","baseStamina","baseIntellect"].includes(key)))};
    const encounter={duration:typed.durationSeconds,iterations:1,seed:typed.seed,targetHealth:typed.targetMaximumHealth,targetLevel:63,targetArmor:typed.targetArmor,
      resistances:"physical armor; bleeds unmitigated",scriptedEncounter:{enabled:false},additionalTargets:0,incomingBossAttacks:false,
      buffs:[],foodAndPrepull:[],targetDebuffs:[],targetDebuffMode:"fixed-cat",baselineMode:"historical-provisional",
      scenario:{role:"cat",startInProwl:typed.startInProwl,preferredCatOpener:typed.preferredCatOpener,targetBehind:typed.targetBehind,useFaerieFire:typed.useFaerieFire,useFeralCooldowns:typed.useFeralCooldowns,
        manualReferenceInputs:Object.fromEntries(manualKeys.filter(key=>!manualBuildKeys.includes(key)).map(key=>[key,typed.manualInputs[key]]))},
      seedStrategy:null,sampleSeeds:null,inputTiming:raw.inputTiming,sourcePolicy};
    return {report,build,summary,encounter,statistics:false,iterations:1,dps,damage,ownerDamage,petDamage:0,hawkDamage:null,sources,
      reconciliation,abilityReconciliation,integrityWarnings,integrityCaveats,dpsLabel:"DPS (single seed)",label:"druid / feral (seed "+typed.seed+")"};
  }
  // Combat alone has mandatory execution-owned receipts. No warning-only path:
  // every contradiction throws before the caller can assign either report slot.
  function inspectCombatReport(input,reject){
    const fail=label=>reject('Combat '+label+'.');
    const obj=(v,label)=>{if(!record(v))fail(label+' is required');return v;};
    const same=(label,a,b)=>{if(a===undefined||b===undefined||!equal(a,b))fail(label+' disagrees or is missing');};
    const keys=(v,required,label,exact=false)=>{obj(v,label);if(required.some(k=>!own(v,k))||exact&&Object.keys(v).some(k=>!required.includes(k)))fail(label+' fields are missing or unsupported');};
    const num=(v,label,min=0)=>{if(typeof v!=='number'||!Number.isFinite(v)||v<min)fail(label+' must be a finite JSON number in range');return v;};
    const text=(v,label)=>{if(typeof v!=='string'||!v.trim())fail(label+' text is required');};
    const notes=(v,label)=>{if(!Array.isArray(v)||!v.length||v.some(x=>typeof x!=='string'||!x.trim()))fail(label+' notes are required');};
    const near=(label,a,b)=>{num(a,label);num(b,label);if(Math.abs(a-b)>Math.max(1,Math.abs(a),Math.abs(b))*Number.EPSILON*8)fail(label+' does not reconcile');};
    const report=JSON.parse(JSON.stringify(input)),build=report.build,request=report.originalSimulationRequest,raw=report.rawServerResponse,summary=report.combatSummary,
      typed=obj(request.rogue,'typed request'),prep=obj(raw.preparation,'owned preparation'),preview=obj(prep.preview,'preparation preview'),
      sim=obj(raw.sim,'simulation'),run=obj(sim.run,'run'),rs=obj(run.summary,'run summary'),actor=obj(rs.actorSummaries?.rogue,'Rogue actor'),enc=build.encounter;
    keys(request,['classKey','specialization','itemizedTalentMode','itemizedTalentRanks','rogue'],'envelope',true);
    for(const v of [build,request,raw]){same('class',v.classKey,'rogue');same('specialization',v.specialization,'combat');}
    same('typed specialization',typed.specialization,'combat');same('simulation specialization',sim.specialization,'combat');same('response level',raw.level,60);
    // Shared legacy admission checked all historical fields and inactive additions before cloning.
    keys(typed.character,['id','name','rulesetKey','classKey','specializationKey','level','race','baseStats','equipment','talents','buffs'],'character',true);
    if(typeof typed.character.id!=='string'||!(/^[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$/).test(typed.character.id)||typed.character.id==='00000000-0000-0000-0000-000000000000')fail('stable character GUID is required');
    text(typed.character.name,'character name');same('UI profile stats',typed.character.baseStats,{values:{}});
    for(const [key,value] of Object.entries({rulesetKey:'wow-forever',classKey:'rogue',specializationKey:'combat',level:60,race:build.race}))same('character '+key,typed.character[key],value);
    if(!['human','dwarf','night-elf','gnome','orc','undead','troll'].includes(build.race))fail('historical race is unsupported');
    same('itemized build',build.itemizedMode,true);same('itemized envelope',request.itemizedTalentMode,true);
    same('historical opt-in',build.provisional,true);same('typed historical opt-in',typed.useProvisionalLevel60Baseline,true);same('reviewed mode',typed.useReviewedLevel60Baseline,false);same('observation',typed.level60Baseline,null);
    same('owned request',obj(prep.request,'owned request'),typed);same('preparation copy',obj(report.combatPreparation,'preparation copy'),prep);same('resolved preview',obj(report.resolvedCharacter,'resolved preview'),preview);
    same('equipment',build.gear,{items:[]});same('typed equipment',typed.character.equipment,{items:[]});
    same('generic talents',typed.character.talents,{selections:[]});same('generic buffs',typed.character.buffs,{selections:[]});
    same('modifiers',build.modifiers,{buffs:[],consumables:[],targetDebuffs:[],targetDebuffMode:'maintained'});same('combat inventory',build.preparedCombatConsumables,[]);same('racial active',build.racialActive,false);
    keys(build,['schema','version','itemizedMode','classKey','specialization','race','provisional','gear','modifiers','preparedCombatConsumables','racialActive','encounter','talents'],'build',true);
    keys(enc,['duration','seed','iterations','targetHealth','timeline','soloScript','rogue'],'encounter',true);
    // Existing schema v1 saves these raw DOM controls as nonblank numeric strings.
    for(const [saved,executed] of [['duration','durationSeconds'],['seed','seed'],['targetHealth','targetMaximumHealth']]){
      if(!['number','string'].includes(typeof enc[saved])||typeof enc[saved]==='string'&&!enc[saved].trim())fail('saved '+saved+' is invalid');
      same(saved,number(enc[saved]),typed[executed]);
    }
    if(number(enc.iterations)!==1||report.iterationStatistics!==null)fail('requires one deterministic run');
    num(typed.durationSeconds,'duration');num(typed.targetMaximumHealth,'target Health');
    if(typed.durationSeconds<=0||typed.durationSeconds>1200||typed.targetMaximumHealth<=0||!Number.isInteger(typed.seed)||typed.seed<-2147483648||typed.seed>2147483647)fail('scenario bounds are invalid');
    if(typeof typed.captureTimeline!=='boolean')fail('timeline flag is invalid');same('timeline',enc.timeline,typed.captureTimeline);
    keys(enc.soloScript,['enabled','startingHealthPercent','bossHealthMilestones'],'script');
    same('script enabled',enc.soloScript.enabled,false);same('script Health',enc.soloScript.startingHealthPercent,100);same('script milestones',enc.soloScript.bossHealthMilestones,[]);
    if(Object.keys(enc.soloScript).some(k=>!['enabled','startingHealthPercent','bossHealthMilestones','addWindows'].includes(k))||own(enc.soloScript,'addWindows')&&!equal(enc.soloScript.addWindows,[]))fail('scripts and adds are unavailable');
    const settings=['targetArmor','targetDodgePercent','targetParryPercent','rogueMaximumHealth','startingEnergyPercent','poisonChargesPerWeapon','startInStealth','isBehindTarget','useExposeArmor','mainHandPoison','offHandPoison','useInstantPoison','bothWeaponsDaggers','mainHandWeaponType','offHandWeaponType','useUtilityAbilities','additionalTargets','enableBossAttacks','tuning'];
    keys(enc.rogue,settings,'saved settings',true);keys(typed,settings,'typed settings');
    for(const key of settings)same('saved '+key,
      key==='tuning'?legacyRogue.projectTuning(enc.rogue[key]):enc.rogue[key],
      key==='tuning'?legacyRogue.projectTuning(typed[key]):typed[key]);
    for(const [key,value] of Object.entries({useInstantPoison:true,bothWeaponsDaggers:false,mainHandWeaponType:'sword',offHandWeaponType:'sword',useUtilityAbilities:false,additionalTargets:0,enableBossAttacks:false}))same('fixed '+key,typed[key],value);
    for(const key of ['targetArmor','targetDodgePercent','targetParryPercent','rogueMaximumHealth','startingEnergyPercent','poisonChargesPerWeapon'])num(typed[key],key);
    if(typed.rogueMaximumHealth<=0||!Number.isInteger(typed.poisonChargesPerWeapon)||typed.poisonChargesPerWeapon>2147483647||['targetDodgePercent','targetParryPercent','startingEnergyPercent'].some(k=>typed[k]>100))fail('manual input bounds are invalid');
    num(typed.bossSwingSeconds,'boss swing interval');num(typed.bossHitDamage,'boss hit damage');if(typed.bossSwingSeconds<=0)fail('boss swing interval must be positive even with incoming attacks disabled');
    for(const key of ['startInStealth','isBehindTarget','useExposeArmor'])if(typeof typed[key]!=='boolean')fail(key+' must be a boolean');
    for(const key of ['mainHandPoison','offHandPoison'])if(!['none','instant','deadly','wound','mind-numbing','crippling'].includes(typed[key]))fail('poison coating is unsupported');
    // The shared report policy enforces all historical tuning keys and runner bounds.
    const caps={'improved-eviscerate':3,'lightning-reflexes':5,'puncturing-wounds':3,precision:3,endurance:2,'flawless-execution':1,'dual-wield-specialization':5,'blade-flurry':1,'hack-and-slash':5,'weapon-expertise':2,'adrenaline-rush':1},
      rows={'improved-eviscerate':1,'lightning-reflexes':1,'puncturing-wounds':2,precision:2,endurance:3,'flawless-execution':4,'dual-wield-specialization':4,'blade-flurry':5,'hack-and-slash':5,'weapon-expertise':6,'adrenaline-rush':7},
      totals=['assassinationPointsSpent','combatPointsSpent','subtletyPointsSpent'],ranks=obj(request.itemizedTalentRanks,'full rank map');
    for(const [key,rank] of Object.entries(ranks)){
      if(!own(caps,key)||!Number.isInteger(rank)||rank<0||rank>caps[key])fail('unsupported exact rank '+key);
      if(rank>0&&Object.entries(ranks).filter(([k])=>rows[k]<rows[key]).reduce((s,[,n])=>s+n,0)<(rows[key]-1)*5)fail('earlier-row rank requirement '+key);
    }
    if(ranks['dual-wield-specialization']>0&&ranks.precision!==3||ranks['weapon-expertise']>0&&ranks['blade-flurry']!==1)fail('full-parent rank requirement');
    const columns={'improved-eviscerate':1,'lightning-reflexes':3,'puncturing-wounds':1,precision:3,endurance:1,'flawless-execution':2,'dual-wield-specialization':3,'blade-flurry':2,'hack-and-slash':3,'weapon-expertise':2,'adrenaline-rush':2};
    const total=Object.values(ranks).reduce((s,n)=>s+n,0),tree={assassinationPointsSpent:0,combatPointsSpent:total,subtletyPointsSpent:0};
    for(const [label,t] of [['saved',build.talents],['typed',typed.talents]]){
      keys(t,['ranks',...totals],label+' talents',true);same(label+' ranks',t.ranks,ranks);for(const key of totals)same(label+' '+key,t[key],tree[key]);
    }
    same('allocated total',raw.allocatedTalentPoints,total);same('unspent total',raw.unspentTalentPoints,51-total);
    const allocation=obj(report.talentAllocation,'allocation');same('allocation mode',allocation.mode,'strict-rank-derived');same('allocation total',allocation.totalSelectedRanks,total);same('allocation unspent',allocation.unspentPoints,51-total);same('allocation trees',allocation.treePoints,tree);
    if(!Array.isArray(allocation.selected))fail('selected talent receipts required');const selected={};
    for(const entry of allocation.selected){
      if(!record(entry)||!own(caps,entry.key)||own(selected,entry.key)||!Number.isInteger(entry.rank)||entry.rank<=0)fail('selected talent identity is invalid');selected[entry.key]=entry.rank;
      same('selected cap',entry.maxRank,caps[entry.key]);same('selected tree',entry.tree,'combat');same('selected row',entry.row,rows[entry.key]);same('selected modeled',entry.modeled,true);same('selected column',entry.column,columns[entry.key]);same('selected prerequisite',entry.prerequisite,entry.key==='dual-wield-specialization'?'precision':entry.key==='weapon-expertise'?'blade-flurry':null);
      same('selected visual provenance',entry.provenance,'unmatched-engine-talent');same('selected visual source commit',entry.sourceCommit,null);same('selected visual source URL',entry.sourceUrl,null);
      same('selected status',entry.simulationStatus,['lightning-reflexes','puncturing-wounds','endurance'].includes(entry.key)?'inactive-in-fixed-sword-combat':'partial');same('selected confidence',entry.dataConfidence,null);
      for(const key of ['name','provenance','knownLimitations'])text(entry[key],'selected '+key);
      for(const [k,r] of [['historicalValidationSourceStatus','talentSourceStatus'],['historicalValidationSourceCommit','talentSourceCommit'],['historicalValidationSourceUrl','talentSourceUrl']])same('selected '+k,entry[k],raw[r]);
    }
    same('selected positive ranks',selected,Object.fromEntries(Object.entries(ranks).filter(([,n])=>n>0)));
    const sourceCommit='1cc5ae1e9abdbf941b7326d2d90421822dce31ba';
    same('historical source commit',raw.talentSourceCommit,sourceCommit);same('historical source status',raw.talentSourceStatus,'historical-provisional-not-current-client-certified');
    same('historical source URL',raw.talentSourceUrl,'https://github.com/gunba/wow-forever-sim/blob/'+sourceCommit+'/ui/core/talents/trees/rogue.json');
    const metadata=obj(report.metadata,'metadata');for(const key of ['ruleset','simulatorVersion','sourceCommit','runtimeTargetBuild','historicalTalentReferenceCommit'])text(metadata[key],'metadata '+key);
    same('metadata ruleset',metadata.ruleset,'wow-forever');same('metadata reference',metadata.historicalTalentReferenceCommit,sourceCommit);
    const baseline=obj(raw.baseline,'baseline');
    keys(baseline,['race','classKey','level','strength','agility','stamina','intellect','spirit','clientBuild','sourceKind','sourceReference','recordedAtUtc','verificationStatus','includesRacialPassives','confidenceClassification','derivationNote','baseMana','baseManaSourceBuild','nakedMaximumHealth','nakedMaximumMana','nakedMeleeAttackPower','nakedRangedAttackPower','nakedArmor','warning'],'baseline');
    same('baseline copy',report.baselineProvenance,baseline);same('preview baseline',preview.baseline,baseline);same('baseline race',baseline.race,build.race);same('baseline class',baseline.classKey,'rogue');same('baseline level',baseline.level,60);
    same('baseline source kind',baseline.sourceKind,'historical-classic-stat-proxy');same('baseline verification',baseline.verificationStatus,'provisional-documented-proxy');same('preview source',preview.sourceStatus,baseline.verificationStatus);same('baseline racial completeness',baseline.includesRacialPassives,false);
    for(const key of ['clientBuild','sourceReference','recordedAtUtc','confidenceClassification','derivationNote','baseManaSourceBuild','warning'])text(baseline[key],'baseline '+key);
    for(const key of ['strength','agility','stamina','intellect','spirit','baseMana','nakedMaximumHealth','nakedMaximumMana'])num(baseline[key],'baseline '+key);
    for(const key of ['nakedMeleeAttackPower','nakedRangedAttackPower','nakedArmor'])same('baseline '+key,baseline[key],null);
    notes(report.capabilityNotes,'capability');notes(report.warnings,'exported warning');notes(raw.talentModelingNotes,'talent');notes(preview.modelingNotes,'preview');notes(sim.modelingNotes,'simulation');
    for(const note of [...report.capabilityNotes,...raw.talentModelingNotes,...preview.modelingNotes,...sim.modelingNotes,...(metadata.notes||[]),...allocation.selected.map(x=>x.knownLimitations)])if(!report.warnings.includes(note))fail('exported warnings omit source/modeling assumptions');
    keys(raw.inputTiming,['additionalInputLatencyMilliseconds','note'],'input timing');num(raw.inputTiming.additionalInputLatencyMilliseconds,'actual latency');text(raw.inputTiming.note,'timing caveat');same('input timing copy',report.inputTiming,raw.inputTiming);same('summary input timing',summary.inputTiming,raw.inputTiming);
    if(!report.warnings.includes(raw.inputTiming.note))fail('timing warning is omitted');
    obj(preview.effectiveStats?.values,'effective stats');obj(preview.rotation,'configured priority');if(!Array.isArray(preview.rotation.entries)||!preview.rotation.entries.length)fail('configured priority entries required');
    same('configured priority copy',summary.configuredRotation,preview.rotation);same('AP copy',summary.attackPower,preview.attackPower);same('AP stats receipt',preview.attackPower,preview.effectiveStats.values['attack-power']);num(preview.attackPower,'AP');
    same('maximum Energy contract',preview.maximumEnergy,100);same('Energy stats receipt',preview.maximumEnergy,preview.effectiveStats.values.energy);same('Health stats receipt',preview.maximumHealth,preview.effectiveStats.values.health);
    for(const key of ['maximumEnergy','maximumHealth','startingEnergy']){num(preview[key],key);same('summary '+key,summary[key],preview[key]);}
    near('starting Energy percentage',preview.startingEnergy,preview.maximumEnergy*typed.startingEnergyPercent/100);
    for(const key of ['maximumEnergy','maximumHealth','endingEnergy','endingHealth']){num(sim[key],key);same('execution '+key,summary[key],sim[key]);}
    if(sim.endingEnergy>sim.maximumEnergy||sim.endingHealth>sim.maximumHealth)fail('ending resource bounds');
    same('no-incoming ending Health',sim.endingHealth,preview.maximumHealth);same('no-incoming actor damage',actor.damageTaken,0);same('no-incoming run damage',rs.damageTaken,0);same('actor survives',actor.died,false);same('no death time',actor.deathTimeSeconds,null);
    same('starting Energy actor receipt',actor.startingResources?.energy,preview.startingEnergy);same('ending Energy actor receipt',actor.endingResources?.energy,sim.endingEnergy);
    same('run seed',rs.seed,typed.seed);same('run duration',rs.durationSeconds,typed.durationSeconds);same('actor key',actor.actorKey,'rogue');same('summary actor',summary.actorKey,'rogue');
    same('target semantics',summary.targetSemantics,{level:63,mitigation:'physical-armor-nature-unmitigated',targetArmor:typed.targetArmor,targetDodgePercent:typed.targetDodgePercent,targetParryPercent:typed.targetParryPercent,targetCount:1,initialHealthPercent:100});
    for(const key of ['dps','damageDone']){num(sim[key],key);same('summary '+key,summary[key],sim[key]);}
    same('owner damage',summary.ownerDamage,sim.damageDone);same('pet damage',summary.petDamage,0);same('Hawk damage',summary.hawkDamage,null);same('run damage',rs.damageDone,sim.damageDone);same('actor damage',actor.damageDone,sim.damageDone);
    near('DPS/damage/duration',sim.dps*typed.durationSeconds,sim.damageDone);
    obj(sim.damageByAbility,'ability damage');same('ability damage copy',summary.damageByAbility,sim.damageByAbility);same('run ability damage',rs.damageDoneByAbility,sim.damageByAbility);same('actor ability damage',actor.damageDoneByAbility,sim.damageByAbility);
    obj(rs.abilities,'ability metrics');same('ability metrics copy',summary.abilityMetrics,rs.abilities);same('actor ability metrics',actor.abilities,rs.abilities);
    for(const [key,value] of Object.entries(sim.damageByAbility)){num(value,'ability damage');same('ability metric '+key,rs.abilities[key]?.damageDone,value);}
    near('ability damage total',Object.values(sim.damageByAbility).reduce((s,n)=>s+n,0),sim.damageDone);
    if(!Array.isArray(run.timeline))fail('timeline receipt required');same('timeline count',summary.timelineEventCount,run.timeline.length);if(!typed.captureTimeline&&run.timeline.length)fail('timeline recorded despite disabled capture');
    for(const key of ['combatItemUses','consumableDecisions'])same('unsupported '+key,summary[key],null);
    const sources=Object.entries(sim.damageByAbility).map(([name,damage])=>({name,damage})).sort((a,b)=>b.damage-a.damage),
      encounter={duration:typed.durationSeconds,iterations:1,seed:typed.seed,targetHealth:typed.targetMaximumHealth,targetLevel:63,targetArmor:typed.targetArmor,resistances:'physical armor; nature unmitigated',scriptedEncounter:{enabled:false},additionalTargets:0,incomingBossAttacks:false,
        buffs:[],foodAndPrepull:[],targetDebuffs:[],targetDebuffMode:'maintained',baselineMode:'historical-provisional',scenario:encounterScenario(typed,'rogue'),seedStrategy:null,sampleSeeds:null,inputTiming:raw.inputTiming,
        sourcePolicy:{metadata,baseline,talentSourceStatus:raw.talentSourceStatus,talentSourceCommit:raw.talentSourceCommit,talentSourceUrl:raw.talentSourceUrl,talentModelingNotes:raw.talentModelingNotes,previewModelingNotes:preview.modelingNotes,capabilityNotes:report.capabilityNotes}};
    return {report,build,summary,encounter,statistics:false,iterations:1,dps:sim.dps,damage:sim.damageDone,ownerDamage:sim.damageDone,petDamage:0,hawkDamage:null,sources,reconciliation:{status:'reconciled',difference:0},abilityReconciliation:{status:'reconciled',difference:0},integrityWarnings:[],integrityCaveats:['Combat imports prove internal receipt consistency, not server attestation.'],dpsLabel:'DPS (single seed)',label:'rogue / combat (seed '+typed.seed+')'};
  }
  // Fixed historical UI talent contract, not the broader legacy Rogue engine.
  // These checks establish internal agreement, not provenance or execution.
  function inspectAssassinationTalents(input,reject){
    const fail=detail=>reject("Assassination historical report "+detail),
      build=input.build,request=input.originalSimulationRequest,typed=request.rogue,
      raw=input.rawServerResponse,character=typed.character;
    if(build.itemizedMode!==true||request.itemizedTalentMode!==true||
       request.classKey!=="rogue"||request.specialization!=="assassination"||
       typed.specialization!=="assassination"||!record(character)||
       character.classKey!=="rogue"||character.specializationKey!=="assassination"||character.level!==60||
       raw.classKey!=="rogue"||raw.specialization!=="assassination"||raw.level!==60||raw.sim.specialization!=="assassination")
      fail("requires canonical Rogue/Assassination itemized identity at level 60.");
    const contract={malice:[5,1],ruthlessness:[3,2],"improved-slice-and-dice":[3,2],
      "improved-expose-armor":[2,3],lethality:[5,3,"malice"],"vile-poisons":[5,4],
      "cold-blood":[1,4],"improved-poisons":[5,4],vigor:[2,5],mutilate:[1,5],
      "seal-fate":[5,6],venom:[1,7,"mutilate"]};
    if(!record(build.talents)||!record(typed.talents))fail("talent rank maps are required in the saved, full and typed requests.");
    const maps=[build.talents.ranks,request.itemizedTalentRanks,typed.talents.ranks];
    for(const ranks of maps){
      if(!record(ranks))fail("talent rank maps are required in the saved, full and typed requests.");
      for(const [key,rank] of Object.entries(ranks)){
        if(!own(contract,key))fail("contains unsupported talent key: "+key+".");
        if(!Number.isInteger(rank)||rank<0||rank>contract[key][0])fail("contains invalid rank for talent: "+key+".");
      }
    }
    if(!equal(maps[0],maps[1])||!equal(maps[0],maps[2]))fail("talent rank maps must match, including explicit zero-key presence.");
    const ranks=maps[0],selected=Object.entries(ranks).filter(([,rank])=>rank>0),
      total=selected.reduce((sum,[,rank])=>sum+rank,0);
    for(const [key] of selected){
      const [,row,parent]=contract[key],earlier=selected.reduce((sum,[other,rank])=>sum+(contract[other][1]<row?rank:0),0);
      if(earlier<(row-1)*5)fail("talent "+key+" requires "+((row-1)*5)+" earlier-row points.");
      if(parent&&ranks[parent]!==contract[parent][0])fail("talent "+key+" requires "+parent+" rank "+contract[parent][0]+".");
    }
    const allocation=input.talentAllocation,
      count=(object,key,value)=>record(object)&&Number.isInteger(object[key])&&object[key]===value,
      trees=object=>count(object,"assassinationPointsSpent",total)&&count(object,"combatPointsSpent",0)&&count(object,"subtletyPointsSpent",0);
    if(!trees(build.talents)||!trees(typed.talents)||
       !count(raw,"allocatedTalentPoints",total)||!count(raw,"unspentTalentPoints",51-total)||
       !record(allocation)||allocation.mode!=="strict-rank-derived"||
       !count(allocation,"totalSelectedRanks",total)||!count(allocation,"unspentPoints",51-total)||!trees(allocation.treePoints))
      fail("talent allocation counts must match the ranked points and 51-point budget.");
    if(!Array.isArray(allocation.selected))fail("talent allocation selected entries are required.");
    const seen=new Set();
    for(const entry of allocation.selected){
      if(!record(entry)||typeof entry.key!=="string"||!own(contract,entry.key)||seen.has(entry.key)||
         !Number.isInteger(entry.rank)||entry.rank<=0||entry.rank!==ranks[entry.key])
        fail("talent allocation selected entries must match each positive talent key and rank exactly once.");
      seen.add(entry.key);
    }
    if(seen.size!==selected.length)fail("talent allocation selected entries must include every positive talent key and rank.");
  }
  function inspectReport(input){
    const reject=detail=>{throw Error("Select a complete Solo DPS report (schema v1), not a build or raw combat response. "+detail);};
    if(!record(input)||input.schema!=="warcraftsim.solo-dps-report"||input.version!==1||
       !record(input.build)||input.build.schema!=="warcraftsim.solo-dps-build"||input.build.version!==1||
       (!own(specs,input.build.classKey)||!specs[input.build.classKey].includes(input.build.specialization))||
       (!(input.build.classKey==="druid"&&input.build.specialization==="feral")&&!record(input.build.encounter))||!record(input.combatSummary)||
       !record(input.originalSimulationRequest)||!record(input.rawServerResponse?.sim))
      reject("Report, build, request and raw simulation objects are required.");
    if(input.build.classKey==='rogue'&&input.build.specialization==='combat'){
      // Validate original JSON before copying or comparison-only projections.
      try{
        legacyRogue.validateRequest(input.originalSimulationRequest.rogue);
        legacyRogue.validateReportTuning(input.build.encounter.rogue?.tuning);
      }catch(error){reject(error.message);}
    }
    if(input.build.classKey==="druid"&&input.build.specialization==="feral")return inspectFeralReport(input,reject);
    if(input.build.classKey==="rogue"&&input.build.specialization==="combat")return inspectCombatReport(input,reject);
    const buildInput=input.build,requestInput=input.originalSimulationRequest,
      typedKey=buildInput.classKey==="priest"?"shadow":buildInput.classKey==="druid"?"balance":buildInput.classKey==="shaman"?buildInput.specialization:buildInput.classKey==="hunter"?keyFor(buildInput.specialization):buildInput.classKey;
    if(!record(requestInput[typedKey])||
       ["mage","warlock","warrior","marksmanship","survival","beastMastery","elemental","enhancement","balance","shadow","rogue","feral"]
         .some(key=>key!==typedKey&&requestInput[key]!=null)||
       (own(requestInput,"classKey")&&requestInput.classKey!==buildInput.classKey)||
       (own(requestInput,"specialization")&&requestInput.specialization!==buildInput.specialization))
      reject("The typed simulation request must match the build's class and preset.");
    if(buildInput.classKey==="rogue"&&buildInput.specialization==="assassination"){
      inspectAssassinationTalents(input,reject);
      try{
        legacyRogue.validateRequest(requestInput.rogue);
        legacyRogue.validateReportTuning(buildInput.encounter.rogue?.tuning);
      }catch(error){reject(error.message);}
    }
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
      integrityCaveats.push("Assassination talent and allocation checks establish internal consistency, not server attestation.");
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
      for(const [key,value] of Object.entries(enc.rogue))consistent("Assassination "+key,value,typed[key],
        key==='tuning'?legacyRogue.projectTuning:value=>value);
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
  function feralScenarioDetails(a,b){
    if(![a,b].every(report=>report.build.classKey==="druid"&&report.build.specialization==="feral"))return [];
    // Add plain-text explanations for validated Feral inputs without replacing
    // the generic scenario incompatibility check or changing any numeric result.
    const left=a.encounter.scenario,right=b.encounter.scenario;
    return [
      ["Position behind target","targetBehind"],["Start in Prowl","startInProwl"],
      ["Cat opener","preferredCatOpener"],["Faerie Fire","useFaerieFire"],
      ["Feral cooldowns","useFeralCooldowns"],
      ["Initial shift Mana cost","shapeshiftMana",true],
      ["Shifting Power base Mana","shiftingPowerBaseMana",true],
      ["Shifting Power cost reduction percent","shiftingPowerCostReductionPercent",true]
    ].flatMap(([label,key,manual])=>{
      const x=(manual?left.manualReferenceInputs:left)[key],y=(manual?right.manualReferenceInputs:right)[key];
      return equal(x,y)?[]:["Encounter assumptions: "+label+" ("+key+"): "+String(x)+" → "+String(y)];
    });
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
        const lt=legacyRogue.projectTuning(lx.rogue?.tuning||{}),rt=legacyRogue.projectTuning(rx.rogue?.tuning||{});
        for(const field of new Set([...Object.keys(lt),...Object.keys(rt)]))
          if(!equal(lt[field],rt[field]))changes.push((left.specialization==="combat"||right.specialization==="combat"?"Combat":"Assassination")+" tuning "+field+": "+String(lt[field])+" → "+String(rt[field]));
      }else if(!equal(lx.rogue?.[key],rx.rogue?.[key]))changes.push((left.specialization==="combat"||right.specialization==="combat"?"Combat":"Assassination")+" "+key+": "+String(lx.rogue?.[key])+" → "+String(rx.rogue?.[key]));
    }
    if(left.specialization==="feral"||right.specialization==="feral"){
      for(const key of new Set([...Object.keys(left.manualInputs||{}),...Object.keys(right.manualInputs||{})]))
        if(!equal(left.manualInputs?.[key],right.manualInputs?.[key]))changes.push("Feral manual "+key+": "+String(left.manualInputs?.[key])+" → "+String(right.manualInputs?.[key]));
      if(!equal(a.summary.configuredRotation?.entries,b.summary.configuredRotation?.entries))changes.push("Feral configured learned priority differs");
      for(const key of new Set([...Object.keys(ar),...Object.keys(br)]))
        if(own(ar,key)!==own(br,key)&&(number(ar[key])??0)===(number(br[key])??0))changes.push("Talent "+key+": explicit zero-key presence differs");
    }
    if([left.specialization,right.specialization].some(spec=>["combat","assassination"].includes(spec)))for(const key of new Set([...Object.keys(ar),...Object.keys(br)]))
      if(own(ar,key)!==own(br,key)&&(number(ar[key])??0)===(number(br[key])??0))changes.push("Talent "+key+": explicit zero-key presence differs");
    if(!equal(left.preparedCombatConsumables,right.preparedCombatConsumables))
      changes.push("Prepared combat consumables differ");
    for(const key of ["buffs","consumables","targetDebuffs"])
      if(!equal(chosen(left.modifiers?.[key]),chosen(right.modifiers?.[key])))
        changes.push("Selected "+key+" differ");
    if(!equal(a.encounter.scenario,b.encounter.scenario))changes.push("Encounter assumptions differ");
    changes.push(...feralScenarioDetails(a,b));
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
    if(a.build.specialization==="feral"&&b.build.specialization==="feral"){
      // Changed learned talents may change configured priority. Compare source
      // receipts only for shared selected nodes, not their ranks or membership.
      const rightNodes=new Map(b.report.talentAllocation.selected.map(entry=>[entry.key,entry]));
      for(const entry of a.report.talentAllocation.selected){
        const other=rightNodes.get(entry.key);if(!other)continue;
        for(const key of ["provenance","sourceCommit","sourceUrl","historicalValidationSourceStatus","historicalValidationSourceCommit","historicalValidationSourceUrl","simulationStatus","dataConfidence","knownLimitations"])
          mismatch("Talent "+entry.key+" source "+key,entry[key],other[key]);
      }
    }
    if(a.build.specialization==="combat"&&b.build.specialization==="combat"){
      const rightNodes=new Map(b.report.talentAllocation.selected.map(entry=>[entry.key,entry]));
      for(const entry of a.report.talentAllocation.selected){const other=rightNodes.get(entry.key);if(!other)continue;
        for(const key of ["provenance","sourceCommit","sourceUrl","historicalValidationSourceStatus","historicalValidationSourceCommit","historicalValidationSourceUrl","simulationStatus","dataConfidence","knownLimitations"])mismatch("Talent "+entry.key+" source "+key,entry[key],other[key]);}
    }
    if([a,b].some(x=>x.build.specialization==="combat")&&[a,b].some(x=>x.build.specialization==="assassination"))caveats.push("Older Assassination reports do not carry the mandatory Combat preparation/source/timing receipts; cross-spec equivalence is unavailable.");
    if([a,b].every(x=>x.build.classKey==='rogue'&&x.build.specialization==='assassination')){
      // Assassination's older optional metadata remains advisory, unlike the
      // mandatory Combat source receipts. Differences must still be visible.
      const source=x=>({metadata:x.report.metadata??null,baseline:x.report.baselineProvenance??null,
        talentSourceStatus:x.report.rawServerResponse.talentSourceStatus??null,
        talentSourceCommit:x.report.rawServerResponse.talentSourceCommit??null,
        talentSourceUrl:x.report.rawServerResponse.talentSourceUrl??null});
      if(!equal(source(a),source(b)))caveats.push('Assassination source/reference metadata differs. Historical comparison checks internal consistency; it does not establish replay equivalence across source versions.');
    }
    for(const [label,key] of [["Duration","duration"],["Iterations","iterations"],
      ["Seed","seed"],["Target health","targetHealth"],["Target level","targetLevel"],
      ["Target armor","targetArmor"],["Target resistances","resistances"],
      ["Scripted encounter","scriptedEncounter"],["Additional targets","additionalTargets"],
      ["Boss attack assumptions","incomingBossAttacks"],["Encounter assumptions","scenario"],
      ["Seed strategy","seedStrategy"],["Sample seeds","sampleSeeds"],["Buffs","buffs"],
      ["Food and pre-pull effects","foodAndPrepull"],["Target debuffs","targetDebuffs"],
      ["Debuff application mode","targetDebuffMode"],["Baseline confidence","baselineMode"],
      ["Actual input timing","inputTiming"],["Source/reference assumptions","sourcePolicy"]])
      mismatch(label,a.encounter[key],b.encounter[key]);
    warnings.push(...feralScenarioDetails(a,b));
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
  let current=null,pendingImport=null,importGeneration=0;
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
    }catch(error){current=null;render();$("compareDetails").hidden=true;$("compareRows").replaceChildren();$("compareStatus").textContent=error.message;}
  });
  for(const slot of ["A","B"]){
    $("compareUse"+slot).addEventListener("click",()=>{
      if(current){
        importGeneration++;pendingImport=null;$("compareFile").value="";
        setSlot(slot,current.report);
      }
    });
    $("compareImport"+slot).addEventListener("click",()=>{
      importGeneration++;pendingImport=slot;$("compareFile").value="";
      $("compareFile").click();
    });
  }
  $("compareFile").addEventListener("change",async()=>{
    const file=$("compareFile").files?.[0],target=pendingImport,generation=importGeneration;
    if(!file||!target)return;
    pendingImport=null;
    try{
      const input=JSON.parse(await file.text());
      if(generation!==importGeneration)return;
      setSlot(target,input);
    }catch(error){
      // Inspection precedes assignment. Rejection is not a selection change,
      // and a late read cannot overwrite a newer import/use-run selection.
      if(generation===importGeneration)
        $("compareStatus").textContent="Report import rejected: "+error.message+" Slot "+target+" was not changed. Displayed reports and comparison remain the previous selections.";
    }finally{if(generation===importGeneration)$("compareFile").value="";}
  });
  render();
})(typeof window!=="undefined"?window:null);
