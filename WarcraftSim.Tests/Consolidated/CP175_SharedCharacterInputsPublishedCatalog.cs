using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class Cp175SharedCharacterInputs
{
    [Theory]
    [InlineData(18434,"druid",1024,58)]
    [InlineData(18452,"rogue",8,58)]
    [InlineData(19829,"shaman",64,0)]
    [InlineData(19830,"shaman",64,0)]
    public void ReviewedPublishedRecordsHaveExplicitClassMasksAndHonestLevels(int id,string cls,int mask,int level)
    {
        var item=ForeverEquipmentCatalog.FindItem(id);
        var evidence=ForeverEquipmentEligibility.Assess(item,cls);
        Assert.True(evidence.CanExecute);Assert.Equal("proven",evidence.RestrictionStatus);
        Assert.Equal("proven",evidence.ProficiencyStatus);Assert.Equal("provisional",evidence.RestrictionConfidence);
        Assert.Equal(ForeverPublishedEquipmentCatalog.EvidenceKind,evidence.RestrictionSourceKind);
        Assert.Equal(ForeverPublishedEquipmentCatalog.SourceSha256,evidence.RestrictionSourceSha256);
        Assert.Equal(ForeverPublishedEquipmentCatalog.SourceBuild,evidence.RestrictionSourceBuild);
        Assert.Equal(mask,item.ClassRestrictionEvidence!.RawMask);Assert.Equal(level,item.RequiredLevel);
        Assert.Equal("published-client-record",item.VerificationStatus);
        var other=ForeverEquipmentEligibility.Assess(item,cls=="druid"?"shaman":"druid");
        Assert.Equal("denied",other.RestrictionStatus);Assert.False(other.CanExecute);
    }

    [Theory]
    [InlineData("missing-class")][InlineData("zero-class")][InlineData("sentinel-class")]
    [InlineData("wrong-class")][InlineData("fallback")][InlineData("build")]
    [InlineData("source-hash")][InlineData("level")][InlineData("missing-level")]
    [InlineData("classification")][InlineData("stats")][InlineData("name")][InlineData("item-id")]
    public void PublishedSourceTamperingAndFallbackNeverGrantAdmission(string kind)
    {
        var raw=ForeverPublishedEquipmentCatalog.ReviewedRecords.Single(x=>x.GetProperty("id").GetInt32()==19830);
        var node=JsonNode.Parse(raw.GetRawText())!;
        var build=ForeverPublishedEquipmentCatalog.SourceBuild;var hash=ForeverPublishedEquipmentCatalog.SourceSha256;
        switch(kind)
        {
            case "missing-class":node.AsObject().Remove("cl");break;
            case "zero-class":node["cl"]=0;break;
            case "sentinel-class":node["cl"]=-1;break;
            case "wrong-class":node["cl"]=1024;break;
            case "fallback":node["fb"]=1;break;
            case "build":build="1.60.1.70707";break;
            case "source-hash":hash=new string('a',64);break;
            case "level":node["rl"]=60;break;
            case "missing-level":node.AsObject().Remove("rl");break;
            case "classification":node["sc"]=1;break;
            case "stats":node["stats"]!["sp"]=999;break;
            case "name":node["n"]!["en"]="Forged name";break;
            case "item-id":node["id"]=19682;break;
        }
        using var modified=JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<ArgumentException>(()=>ForeverPublishedEquipmentCatalog.ReadPinnedRecord(modified.RootElement,build,hash));
        var unchanged=ForeverPublishedEquipmentCatalog.ReadPinnedRecord(raw,
            ForeverPublishedEquipmentCatalog.SourceBuild,ForeverPublishedEquipmentCatalog.SourceSha256);
        Assert.Equal(13m,unchanged.Stats["spell-power"]);
    }

    [Theory]
    [InlineData("kind")][InlineData("hash")][InlineData("url")][InlineData("build")]
    [InlineData("mask")][InlineData("confidence")][InlineData("stats")][InlineData("level")]
    public void PublishedItemEvidenceCannotBeRelabeledOrChangedAfterReview(string kind)
    {
        var item=JsonSerializer.Deserialize<ForeverEquipmentItem>(JsonSerializer.Serialize(
            ForeverEquipmentCatalog.FindItem(19830)))!;
        var e=item.ClassRestrictionEvidence!;
        switch(kind)
        {
            case "kind":e.SourceKind="client-item-sparse";break;
            case "hash":e.SourceSha256=new string('a',64);break;
            case "url":e.SourceUrl="https://example.test/forged";break;
            case "build":e.SourceBuild=item.SourceBuild="1.60.1.70707";break;
            case "mask":e.RawMask=1024;e.SourceAllowedClasses=["druid"];item.AllowedClasses=["druid"];break;
            case "confidence":e.DataConfidence="verified";break;
            case "stats":item.Stats["spell-power"]=999m;break;
            case "level":item.RequiredLevel=60;break;
        }
        var status=ForeverEquipmentEligibility.Assess(item,"shaman");
        Assert.Equal("pending",status.RestrictionStatus);Assert.False(status.CanExecute);
        Assert.True(ForeverEquipmentEligibility.Assess(ForeverEquipmentCatalog.FindItem(19830),"shaman").CanExecute);
    }

    [Fact]
    public void PublishedRecordsDoNotBypassExistingPublicItemSparseImportGate()
    {
        var item=ForeverEquipmentCatalog.FindItem(19830);
        var manifest=SyntheticEquipmentRestrictionFixture.Manifest(item);
        manifest.ClientBuild=item.SourceBuild;
        Assert.Throws<InvalidOperationException>(()=>ForeverEquipmentSnapshotPipeline.Import(
            SyntheticEquipmentRestrictionFixture.Json(manifest),new()));
    }

    [Fact]
    public void RoguePublishedGearAndEnchantAreReversibleSingleContributions()
    {
        var p=Profile();p.Equipment.Items=[new(){ItemId=18452,SlotKey="wrist",EnchantSpellId=1248599}];
        var before=JsonSerializer.Serialize(p);
        var prepared=Prepare(p,baseline:Baseline());var stats=Calculate(prepared.Character);
        Assert.Equal(716m,stats.Get("attack-power"));Assert.Equal(116m,stats.Get("agility"));
        Assert.Equal(117m,stats.Get("stamina"));
        Assert.Single(prepared.Character.Equipment.Items[0].Modifiers);
        Assert.Contains(prepared.Notes,x=>x.Contains("published-client-item-record") && x.Contains("provisional"));
        Assert.Equal(before,JsonSerializer.Serialize(p));
        p.Equipment.Items[0].EnchantSpellId=null;
        Assert.Equal(707m,Calculate(Prepare(p,baseline:Baseline()).Character).Get("attack-power"));
        p.Equipment.Items.Clear();
        Assert.Equal(700m,Calculate(Prepare(p,baseline:Baseline()).Character).Get("attack-power"));
    }

    [Fact]
    public void PublishedShamanTwoPieceBonusAppliesOnceAndDisappearsAtThresholdRemoval()
    {
        var p=Profile("shaman");p.Equipment.Items=[new(){ItemId=19829,SlotKey="waist"},
            new(){ItemId=19830,SlotKey="wrist",EnchantSpellId=1248661}];
        var before=JsonSerializer.Serialize(p);
        var prepared=Prepare(p,baseline:Baseline("shaman"));var stats=Calculate(prepared.Character);
        Assert.Equal(191m,stats.Get("intellect"));Assert.Equal(122m,stats.Get("stamina"));
        Assert.Equal(25m,stats.Get("spell-power"));Assert.Equal(12m,stats.Get("mp5"));
        Assert.Single(prepared.Character.Equipment.Items.SelectMany(x=>x.Modifiers),x=>x.ModifierType=="set-bonus");
        Assert.Equal(before,JsonSerializer.Serialize(p));
        p.Equipment.Items.RemoveAt(0);
        var one=Prepare(p,baseline:Baseline("shaman"));
        Assert.Equal(4m,Calculate(one.Character).Get("mp5"));
        Assert.DoesNotContain(one.Character.Equipment.Items.SelectMany(x=>x.Modifiers),x=>x.ModifierType=="set-bonus");
        var utility=ForeverEquipmentCatalog.GetCatalog().Sets.Where(x=>x.Key==ForeverPublishedEquipmentCatalog.SetKey && x.RequiredPieces>2);
        Assert.All(utility,x=>{Assert.False(x.Modeled);Assert.Equal("not-implemented",x.ImplementationStatus);});
    }

    [Fact]
    public void RealDruidGearAndEnchantReachPreparedCombatAndReplay()
    {
        var r=new BalanceDruidSimRequest {DurationSeconds=20m,TargetMaximumHealth=10000000m,
            Level60Baseline=Baseline("druid"),StartingManaPercent=40m,MajorManaPotionCharges=0,
            UseOmenOfClarity=false,UseInnervate=false,BaseSpellHitPercent=100m,SpellCritPercent=0m,
            CustomPriority=["wrath"]};
        r.Character=Profile("druid");r.Character.SpecializationKey="balance";r.Talents.Ranks.Clear();
        r.Tuning.ManaRegenPerSecond=0m;
        var naked=new BalanceDruidSimRunner().Run(r);
        r.Character.Equipment.Items=[new(){ItemId=18434,SlotKey="wrist",EnchantSpellId=1220624}];
        var before=JsonSerializer.Serialize(r);var geared=new BalanceDruidSimRunner().Run(r);
        Assert.Equal(naked.MaximumMana+105m,geared.MaximumMana);
        Assert.Equal(naked.MaximumHealth+170m,geared.MaximumHealth);
        Assert.Equal(naked.SpellPower+16m,geared.SpellPower);Assert.True(geared.DamageDone>naked.DamageDone);
        Assert.Equal(CanonicalRun(geared.Run),CanonicalRun(new BalanceDruidSimRunner().Run(r).Run));
        Assert.Equal(before,JsonSerializer.Serialize(r));
        r.Character.Equipment.Items.Clear();
        Assert.Equal(CanonicalRun(naked.Run),CanonicalRun(new BalanceDruidSimRunner().Run(r).Run));
    }

    [Fact]
    public void RealShamanGearChangesManaHealthPowerAndActualRegeneration()
    {
        var r=new ElementalShamanSimRequest {DurationSeconds=20m,TargetMaximumHealth=10000000m,
            Level60Baseline=Baseline("shaman"),StartingManaPercent=40m,MajorManaPotionCharges=0,
            UseElementalMastery=false,BaseSpellHitPercent=100m,SpellCritPercent=0m,CustomPriority=["lightning-bolt"]};
        r.Character=Profile("shaman");r.Character.SpecializationKey="elemental";r.Talents.Ranks.Clear();
        r.Tuning.ManaRegenPerSecond=0m;
        var naked=new ElementalShamanSimRunner().Run(r);
        r.Character.Equipment.Items=[new(){ItemId=19829,SlotKey="waist"},new(){ItemId=19830,SlotKey="wrist"}];
        var geared=new ElementalShamanSimRunner().Run(r);
        Assert.Equal(naked.MaximumMana+480m,geared.MaximumMana);
        Assert.Equal(naked.MaximumHealth+220m,geared.MaximumHealth);
        Assert.Equal(naked.SpellPower+25m,geared.SpellPower);
        Assert.Equal(naked.EndingMana+240m,geared.EndingMana); // 40% of480 capacity +12MP5 across20s.
        Assert.True(geared.DamageDone>naked.DamageDone);
        Assert.Equal(CanonicalRun(geared.Run),CanonicalRun(new ElementalShamanSimRunner().Run(r).Run));
    }
}
