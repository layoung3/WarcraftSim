using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PaladinClassBuild
{
    private static JsonElement Json(object value)=>ForeverClassBuildJson.Element(value);
    private static JsonElement Ok(IActionResult response)=>Json(Assert.IsType<OkObjectResult>(response).Value!);
    private static ForeverClassBuild Build(string tree="retribution")
    {
        var build=ForeverClassBuildService.ApplyPreset(ForeverClassBuildService.Create("paladin"),"paladin-"+tree);
        build.Character.BaselineMode="historical-provisional";build.AcceptPartialCoverage=true;
        build.Character.Encounter.DurationSeconds=18m;
        build.Character.Equipment.Items=[Item("main-hand",12782)];
        build.Character.ClassSettings=Json(new {useManualCombatAssumptions=true,
            baseMeleeHitPercent=100m,baseRangedHitPercent=100m,baseSpellHitPercent=100m,criticalChancePercent=0m});
        return build;
    }
    private static CharacterEquipmentItem Item(string slot,int id,int? enchant=null)=>new() {
        SlotKey=slot,ItemId=id,EnchantSpellId=enchant};
    private static PaladinPrecombatPreview Preview(ForeverClassBuild build)=>
        Assert.IsType<PaladinPrecombatPreview>(ForeverClassBuildService.Validate(build).Preparation);
    private static JsonElement Sim(ForeverClassBuild build)=>Json(ForeverClassBuildService.Run(build,default));

    [Fact]
    public void NewPaladinRequiresExplicitBaselineConsentAndLeavesLegacyEndpointsUnsupported()
    {
        var build=ForeverClassBuildService.Create("paladin");
        Assert.Equal("strict-reviewed",build.Character.BaselineMode);
        var validation=ForeverClassBuildService.Validate(build);
        Assert.True(validation.IsLegal);Assert.True(validation.CanSave);Assert.False(validation.CanExecute);
        Assert.Contains(validation.ExecutionBlockers,x=>x.Contains("No verified Forever level-60 baseline"));
        Assert.False(ForeverClassSimulationCapabilities.Get("paladin").ExecutesThroughCurrentUnifiedEndpoint);
        Assert.True(ForeverClassBuildCatalog.CanExecuteClassBuild("paladin"));
        Assert.Equal("strict-reviewed",build.Character.BaselineMode);
        var discovery=Ok(new ForeverSharedCharacterController().Capabilities());
        Assert.Contains("paladin",discovery.GetProperty("classWide").GetProperty("classes").EnumerateArray().Select(x=>x.GetString()));
        var omitted=JsonNode.Parse(Json(build).GetRawText())!;
        omitted["character"]!.AsObject().Remove("baselineMode");
        Assert.IsType<BadRequestObjectResult>(new ForeverClassBuildController().Import(Json(omitted)));
    }

    [Theory]
    [InlineData("holy",44)]
    [InlineData("protection",51)]
    [InlineData("retribution",51)]
    public void EveryTreeUsesOnePreparedClassContractAndReviewedLegalMixedRanks(string tree,int total)
    {
        var build=Build(tree);var validation=ForeverClassBuildService.Validate(build);
        Assert.True(validation.IsLegal,string.Join(" ",validation.LegalityErrors));
        Assert.True(validation.CanExecute,string.Join(" ",validation.ExecutionBlockers));
        Assert.Equal(total,validation.AllocatedPoints);Assert.Equal(51-total,validation.UnspentPoints);
        Assert.All(validation.TreePoints.Values,points=>Assert.True(points>0));
        var preview=Assert.IsType<PaladinPrecombatPreview>(validation.Preparation);
        Assert.Equal("paladin",preview.Request.Character.ClassKey);
        Assert.Equal(Guid.Empty,preview.Request.Character.Id);
        Assert.Equal(build.Character.ItemizedTalents.OrderBy(x=>x.Key),preview.Request.Talents.Ranks.OrderBy(x=>x.Key));
        Assert.Equal("two-hand",preview.WeaponKind);
        Assert.Contains(preview.ModelingNotes,x=>x.Contains("provisional",StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("human","medium-historical-combination")]
    [InlineData("dwarf","medium-historical-combination")]
    [InlineData("undead","low-offset-extrapolated-forever-only-combination")]
    public void CurrentRaceChoicesPreserveTheirDifferentBaselineConfidence(string race,string confidence)
    {
        var build=Build();build.Character.Race=race;
        var preview=Preview(build);var baseline=Json(preview.Baseline!);
        Assert.Equal(race,baseline.GetProperty("race").GetString());
        Assert.Equal(confidence,baseline.GetProperty("confidenceClassification").GetString());
        Assert.Equal("provisional-documented-proxy",baseline.GetProperty("verificationStatus").GetString());
        Assert.False(baseline.GetProperty("includesRacialPassives").GetBoolean());
    }

    [Fact]
    public void ExplicitObservationIsNotRelabeledAndCannotCrossClassOrRace()
    {
        var build=Build();var observed=ForeverClassBuildJson.Clone(ForeverLevel60BaselineCatalog.FindProvisional("human","paladin"));
        observed.SourceKind="user-unverified-observation";observed.VerificationStatus="unverified-user-supplied";
        observed.SourceReference="owner test observation with declared uncertainty";
        observed.ClientBuild="1.60.1.70888";observed.ObservedUnbuffedMaximumHealth=3100m;
        build.Character.BaselineMode="explicit-observation";build.Character.Baseline=observed;
        Assert.Equal("unverified-user-supplied",Json(Preview(build).Baseline!).GetProperty("verificationStatus").GetString());
        observed.Race="dwarf";
        Assert.False(ForeverClassBuildService.Validate(build).CanExecute);
        Assert.Equal("dwarf",observed.Race);
    }

    [Fact]
    public void NonpresetPartialRanksAndZeroValuesRemainLegalWhileMalformedAllocationsDoNot()
    {
        var build=Build();build.PresetOrigin=null;
        build.Character.ItemizedTalents=new(){["divine-strength"]=2,["divine-intellect"]=3,["benediction"]=0};
        var validation=ForeverClassBuildService.Validate(build);
        Assert.True(validation.CanExecute,string.Join(" ",validation.ExecutionBlockers));
        Assert.Equal(5,validation.AllocatedPoints);Assert.Equal(46,validation.UnspentPoints);
        foreach(var ranks in new Dictionary<string,int>[] {
            new(){["divine-strength"]=6},new(){["divine-strength"]=-1},new(){["unknown-node"]=0},
            new(){["holy-shock"]=1},new(){["illumination"]=1,["divine-strength"]=5,["spiritual-focus"]=5},
            ForeverPaladinClassTalents.Entries.ToDictionary(x=>x.Key,x=>x.MaxRank)})
        {build.Character.ItemizedTalents=ranks;Assert.False(ForeverClassBuildService.Validate(build).CanSave);}
    }

    [Fact]
    public void PresetChangesOnlyRanksAndProvenancePreservingCustomInvalidPriorityForEditing()
    {
        var build=Build();build.Character.Race="undead";
        build.Character.Equipment.Items[0].EnchantSpellId=1248668;
        build.Character.Modifiers.Buffs=["arcane-intellect"];
        build.Character.CombatItems.MajorManaPotionCharges=2;
        build.Character.CombatItems.DisabledItemKeys=["major-mana-potion"];
        build.Character.Encounter.Seed=8318;build.Character.Encounter.TargetArmor=2176;
        build.Character.Rotation.Mode="custom";build.Character.Rotation.Priority=["seal-of-command"];
        var before=Json(build).GetRawText();var after=ForeverClassBuildService.ApplyPreset(build,"paladin-holy");
        var expected=ForeverClassBuildJson.Clone(build);expected.PresetOrigin=after.PresetOrigin;
        expected.Character.ItemizedTalents=after.Character.ItemizedTalents;
        Assert.Equal(Json(expected).GetRawText(),Json(after).GetRawText());Assert.Equal(before,Json(build).GetRawText());
        Assert.Equal("seal-of-command",Assert.Single(after.Character.Rotation.Priority));
        Assert.False(ForeverClassBuildService.Validate(after).CanExecute);
        Assert.True(ForeverClassBuildService.Validate(after).CanSave);
    }

    [Fact]
    public void CatalogWeaponAndEnchantStatsComposeOnceWithTalentsAndReverseInCombat()
    {
        var build=Build();build.Character.ItemizedTalents=new(){["divine-strength"]=5};
        var unenchanted=Preview(build);var original=Sim(build);
        build.Character.Equipment.Items[0].EnchantSpellId=1248668;
        var enchanted=Preview(build);var withEnchant=Sim(build);
        // Existing client profiles: 25 raw Strength, Divine Strength10%, 2 AP/Strength.
        Assert.Equal(27.5m,enchanted.EffectiveStats.Get("strength")-unenchanted.EffectiveStats.Get("strength"));
        Assert.Equal(55m,enchanted.AttackPower-unenchanted.AttackPower);
        Assert.True(withEnchant.GetProperty("simulation").GetProperty("damageDone").GetDecimal()>
            original.GetProperty("simulation").GetProperty("damageDone").GetDecimal());
        build.Character.Equipment.Items[0].EnchantSpellId=null;
        var restored=Sim(build);
        Assert.Equal(original.GetProperty("simulation").GetProperty("damageDone").GetDecimal(),
            restored.GetProperty("simulation").GetProperty("damageDone").GetDecimal());
        Assert.Equal(unenchanted.AttackPower,Preview(build).AttackPower);
        build.Character.Equipment.Items.Clear();
        var empty=Preview(build);Assert.Equal("none",empty.WeaponKind);Assert.DoesNotContain("holy-strike",empty.AvailableActions);
        Assert.Equal(66m,unenchanted.AttackPower-empty.AttackPower);
    }

    [Fact]
    public void ShieldAvailabilityAndEnchantsFollowActualSlotsWithNoManualOrTwoHandBypass()
    {
        var build=Build("protection");build.Character.Equipment.Items=[Item("main-hand",21395),Item("off-hand",21269)];
        var shield=Preview(build);Assert.True(shield.HasShield);Assert.Contains("holy-shield",shield.AvailableActions);
        Assert.Equal(55m+15m+shield.EffectiveStats.Get("strength")/20m,shield.EffectiveStats.Get("block-value"));
        Assert.Equal(300m+8m+4m*build.Character.ItemizedTalents.GetValueOrDefault("anticipation"),shield.EffectiveStats.Get("defense-skill"));
        Assert.Contains(shield.ModelingNotes,x=>x.Contains("independent proficiency evidence")&&x.Contains("provisional"));
        build.Character.Equipment.Items[1].EnchantSpellId=1219581;
        Assert.Equal(120m,Preview(build).MaximumHealth-shield.MaximumHealth);
        build.Character.Equipment.Items[1].EnchantSpellId=null;
        Assert.Equal(shield.MaximumHealth,Preview(build).MaximumHealth);
        build.Character.Equipment.Items.RemoveAt(1);
        var noShield=Preview(build);Assert.False(noShield.HasShield);Assert.DoesNotContain("holy-shield",noShield.AvailableActions);
        build.Character.Equipment.Items=[Item("main-hand",12782),Item("off-hand",21269)];
        Assert.False(ForeverClassBuildService.Validate(build).CanExecute);
        build.Character.Equipment.Items=[Item("main-hand",21395,1248668)];
        Assert.False(ForeverClassBuildService.Validate(build).CanExecute);
    }

    [Theory]
    [InlineData(12782)]
    [InlineData(21395)]
    [InlineData(21269)]
    public void PinnedItemAdmissionCannotBeForgedWithChangedMasksSourcesStatsOrEffects(int id)
    {
        var item=ForeverEquipmentCatalog.FindItem(id);
        var eligibility=ForeverEquipmentEligibility.Assess(item,"paladin");
        Assert.True(eligibility.CanExecute);
        Assert.Equal("1.60.1.70888",eligibility.ValueSourceBuild);
        Assert.Contains("provisional",eligibility.ProficiencyConfidence);
        Assert.Equal(id==21269?ForeverPaladinEquipmentCatalog.ShieldProficiencyUrl:
            ForeverPaladinEquipmentCatalog.ProficiencyUrl,eligibility.ProficiencySourceUrl);
        Assert.False(ForeverEquipmentEligibility.Assess(item,"warrior").CanExecute);
        foreach(var mutation in new Action<ForeverEquipmentItem>[] {
            x=>x.ClassRestrictionEvidence=null,x=>x.ClassRestrictionEvidence!.RawMask=-1,
            x=>x.ClassRestrictionEvidence!.RawMask=0,x=>x.ClassRestrictionEvidence!.RawMask=1,
            x=>x.ClassRestrictionEvidence!.SourceSha256=new string('0',64),
            x=>x.Stats["stamina"]=999m,x=>x.Effects.Add(new(){Kind="proc"}),
            x=>x.ItemSubclass="dagger"})
        {var changed=ForeverClassBuildJson.Clone(item);mutation(changed);Assert.False(ForeverEquipmentEligibility.Assess(changed,"paladin").CanExecute);}
        Assert.True(ForeverEquipmentEligibility.Assess(item,"paladin").CanExecute);
        Assert.False(ForeverEquipmentEligibility.Assess(ForeverEquipmentCatalog.FindItem(19102),"paladin").CanExecute);
    }

    [Fact]
    public void NarrowPaladinEquipmentReviewDoesNotExpandOlderClassAdapters()
    {
        foreach(var id in new[]{12782,21395,21269})
        {
            var slot=id==21269?"off-hand":"main-hand";
            foreach(var cls in new[]{"warrior","hunter","mage","warlock","rogue","shaman","druid","priest"})
                Assert.Contains("pending this separate published-item review",Assert.Throws<ArgumentException>(()=>
                    ForeverSharedEquipmentResolver.Resolve(new(){Items=[Item(slot,id)]},cls,60,"human")).Message);
            var warrior=new WarriorSimRequest {EquipmentMode="catalog",Gear=[new(){Slot=slot,ItemId=id,ItemKey=$"forever-item-{id}"}]};
            Assert.Contains("pending this separate published-item review",Assert.Throws<ArgumentException>(()=>
                ForeverEquipmentCatalog.ResolveWarriorLoadout(warrior)).Message);
            warrior.EquipmentMode="gear-bonuses";
            Assert.Contains("pending this separate published-item review",Assert.Throws<ArgumentException>(()=>
                new ForeverWarriorSimRunner().Run(warrior)).Message);
        }
    }

    [Fact]
    public async Task ServerRejectsCrossClassSettingsAndCoherentlyEditedReportsWithoutMutatingTheOriginal()
    {
        var controller=new ForeverClassBuildController();var build=Build();var before=Json(build).GetRawText();
        var report=Ok(await controller.Simulate(Json(build),default));
        Assert.True(Ok(controller.ValidateReport(report)).GetProperty("valid").GetBoolean());
        Assert.NotEmpty(report.GetProperty("simulation").GetProperty("run").GetProperty("timeline").EnumerateArray());
        var originalReport=report.GetRawText();
        foreach(var mutate in new Action<JsonNode>[] {
            x=>x["simulation"]!["classKey"]="rogue",
            x=>{x["originalSimulationRequest"]!["baseRangedHitPercent"]=0;x["preparation"]!["request"]!["baseRangedHitPercent"]=0;},
            x=>{x["simulation"]!["maximumMana"]=1;x["preparation"]!["maximumMana"]=1;x["validation"]!["preparation"]!["maximumMana"]=1;},
            x=>x["originalSimulationRequest"]!["character"]!["classKey"]="shaman",
            x=>x["coverage"]!.AsArray().RemoveAt(0),
            x=>x["simulation"]!["friendlyEndingHealth"]=9999999m,
            x=>x["simulation"]!["endingMaximumMana"]=-1m,
            x=>x["simulation"]!["endingMaximumHealth"]=-1m,
            x=>x["simulation"]!["run"]!["summary"]!["actorSummaries"]=null,
            x=>x["simulation"]!["run"]!["summary"]!["actorSummaries"]!["paladin"]!["healingDone"]=999m,
            x=>x["simulation"]!["run"]!["summary"]!["seed"]=-999,
            x=>x["simulation"]!["healingDone"]="0",
            x=>x["simulation"]!["damageByAbility"]!["invented"]=100})
        {var bad=JsonNode.Parse(originalReport)!;mutate(bad);Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(bad)));}
        foreach(var field in report.GetProperty("simulation").EnumerateObject().Select(x=>x.Name))
        {
            var missing=JsonNode.Parse(originalReport)!;missing["simulation"]!.AsObject().Remove(field);
            Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(missing)));
        }
        foreach(var field in new[]{"summary","timeline","processedEventCount","customMetrics"})
        {
            var missing=JsonNode.Parse(originalReport)!;missing["simulation"]!["run"]!.AsObject().Remove(field);
            Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(missing)));
        }
        var malformed=ForeverClassBuildJson.Clone(build);malformed.Character.BaselineMode="strict-reviewed";
        malformed.Character.ClassSettings=Json(new{mainHandPoison="instant"});
        Assert.IsType<BadRequestObjectResult>(controller.Import(Json(malformed)));
        foreach(var settings in new object[]{new{incomingBossHitDamage="400"},new{targetCount="1"},
            new{utilityScenario=new{initialTargetDistanceYards="3"}}})
        {
            malformed.Character.ClassSettings=Json(settings);
            Assert.IsType<BadRequestObjectResult>(controller.Import(Json(malformed)));
        }
        foreach(var badRank in new JsonNode?[] {JsonValue.Create(1.5m),JsonValue.Create("3"),JsonValue.Create(true),null})
        {
            var malformedRank=JsonNode.Parse(before)!;
            malformedRank["character"]!["itemizedTalents"]!["divine-strength"]=badRank;
            Assert.IsType<BadRequestObjectResult>(controller.Import(Json(malformedRank)));
        }
        Assert.Equal(before,Json(build).GetRawText());Assert.Equal(originalReport,report.GetRawText());
    }

    [Fact]
    public void LabelsDoNotSelectAnEngineOrChangeAnEffectivePaladinBuild()
    {
        var a=Build();a.Character.Encounter.CaptureTimeline=false;
        var b=ForeverClassBuildJson.Clone(a);b.PresetOrigin="unrelated-label";b.Character.Specialization="holy-origin-only";
        var x=Sim(a).GetProperty("simulation");var y=Sim(b).GetProperty("simulation");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(x.GetRawText()),JsonNode.Parse(y.GetRawText())));
    }

    [Fact]
    public async Task PaladinPairedComparisonKeepsPreparedInputsAndDistinctValidationCohortAndRejectsScenarioChanges()
    {
        var a=Build();a.Character.Encounter.DurationSeconds=8;a.Character.Encounter.Seed=41;
        var b=ForeverClassBuildJson.Clone(a);b.Character.Equipment.Items[0].EnchantSpellId=1248668;
        var request=new ForeverClassBuildComparisonRequest{Baseline=a,Candidate=b,Iterations=3,ValidationIterations=2};
        var controller=new ForeverClassBuildController();var result=Ok(await controller.Compare(Json(request),default));
        Assert.Equal(new[]{41,42,43},result.GetProperty("training").GetProperty("samples").EnumerateArray().Select(x=>x.GetProperty("seed").GetInt32()));
        Assert.Equal(new[]{1000044,1000045},result.GetProperty("validation").GetProperty("samples").EnumerateArray().Select(x=>x.GetProperty("seed").GetInt32()));
        Assert.Equal("paladin",result.GetProperty("candidatePreparedRequest").GetProperty("character").GetProperty("classKey").GetString());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("sourceCommit").GetString()));
        var mismatch=JsonNode.Parse(b.Character.ClassSettings.GetRawText())!;mismatch["friendlyTargetEnabled"]=true;
        b.Character.ClassSettings=Json(mismatch);
        Assert.IsType<BadRequestObjectResult>(await controller.Compare(Json(request),default));
        foreach(var (key,value) in new[]{("criticalChancePercent",100m),("manaRegenPerSecond",100m)})
        {
            mismatch=JsonNode.Parse(a.Character.ClassSettings.GetRawText())!;mismatch[key]=value;
            b.Character.ClassSettings=Json(mismatch);
            Assert.IsType<BadRequestObjectResult>(await controller.Compare(Json(request),default));
        }
        b.Character.ClassSettings=a.Character.ClassSettings;request.ValidationIterations=41;
        Assert.IsType<BadRequestObjectResult>(await controller.Compare(Json(request),default));
        Assert.Equal(41,a.Character.Encounter.Seed);Assert.True(a.Character.Encounter.CaptureTimeline);
    }
}
