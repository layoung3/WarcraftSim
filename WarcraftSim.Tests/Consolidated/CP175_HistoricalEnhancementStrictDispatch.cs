using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HistoricalEnhancementStrictDispatchTests
{
    private static readonly JsonSerializerOptions Web=new(JsonSerializerDefaults.Web);

    // Source-named partial allocation, not the legacy synthetic 10/41/0 preset.
    private static Dictionary<string,int> CompleteRanks() => new(StringComparer.Ordinal) {
        ["thundering-strikes"]=5,["ancestral-knowledge"]=5,
        ["mental-dexterity"]=3,["improved-lightning-shield"]=3,
        ["elemental-weapons"]=3,["shamanistic-focus"]=1,
        ["flurry"]=5,["stormstrike"]=1,["mental-quickness"]=2,
        ["improved-stormstrike"]=2,["maelstrom-weapon"]=5,["rage-of-the-farseer"]=1
    };

    private static ForeverLevel60SimulationRequest Request()
    {
        var ranks=CompleteRanks();
        var typed=new EnhancementShamanSimRequest {
            DurationSeconds=8,UseProvisionalLevel60Baseline=true,
            UseLightningShield=true,IncomingBossHitDamage=10,
            Talents=new ForeverEnhancementShamanTalents {
                ElementalPointsSpent=0,EnhancementPointsSpent=36,RestorationPointsSpent=0,
                Ranks=new(ranks,StringComparer.Ordinal)}
        };
        typed.Character.Race="tauren";
        return new() {
            ClassKey="shaman",Specialization="enhancement",ItemizedTalentMode=true,
            ItemizedTalentRanks=ranks,Enhancement=typed
        };
    }

    private static IActionResult Validate(object talents) =>
        new ForeverSoloTalentController().ValidateItemized("shaman","enhancement",
            JsonSerializer.SerializeToElement(talents,Web));

    private static async Task<JsonElement> Run(ForeverLevel60SimulationRequest request)
    {
        var response=Assert.IsType<OkObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request,default));
        return JsonSerializer.SerializeToElement(response.Value,Web);
    }

    private static async Task Reject(ForeverLevel60SimulationRequest request)
    {
        var before=JsonSerializer.Serialize(request,Web);
        Assert.IsType<BadRequestObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Fact]
    public void DiscoveryIsSpecAwareAndPreservesElementalCatalog()
    {
        Assert.True(ForeverHistoricalCasterTalentCatalog.Supports("shaman","enhancement"));
        Assert.False(ForeverHistoricalCasterTalentCatalog.Supports("shaman","restoration"));
        var enhancement=ForeverHistoricalCasterTalentCatalog.ForSpecialization("shaman","enhancement");
        Assert.Equal(15,enhancement.Count);
        Assert.Equal(51,enhancement.Sum(x=>x.MaxRank));
        Assert.Equal(new[]{"concussion","convection","reverberation"},
            enhancement.Where(x=>x.Tree=="elemental").Select(x=>x.Key).OrderBy(x=>x));
        Assert.Equal(12,enhancement.Count(x=>x.Tree=="enhancement"));
        Assert.Equal(CompleteRanks().OrderBy(x=>x.Key),
            enhancement.Where(x=>x.Tree=="enhancement").ToDictionary(x=>x.Key,x=>x.MaxRank).OrderBy(x=>x.Key));
        Assert.Equal("elemental",ForeverHistoricalCasterTalentCatalog.Find("shaman","enhancement","convection")!.Tree);
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("shaman","enhancement","call-of-flame"));
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("shaman","elemental","flurry"));
        Assert.Equal(26,ForeverHistoricalCasterTalentCatalog.ForClass("shaman").Count);
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.ForClass("shaman"),
            ForeverHistoricalCasterTalentCatalog.ForSpecialization("shaman","elemental"));
        Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForSpecialization("shaman","restoration"));
        var catalog=Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Catalog("shaman","enhancement"));
        var json=JsonSerializer.SerializeToElement(catalog.Value,Web);
        Assert.Equal(15,json.GetProperty("talents").GetArrayLength());
        foreach(var talent in json.GetProperty("talents").EnumerateArray())
        {
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,
                talent.GetProperty("historicalValidationSourceStatus").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,
                talent.GetProperty("historicalValidationSourceCommit").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceUrl("shaman"),
                talent.GetProperty("sourceUrl").GetString());
            Assert.Equal((talent.GetProperty("row").GetInt32()-1)*5,
                talent.GetProperty("requiredEarlierPoints").GetInt32());
        }
    }

    [Theory]
    [InlineData("convection",5,1,2,null,0)]
    [InlineData("concussion",5,1,3,null,0)]
    [InlineData("reverberation",5,2,2,null,0)]
    [InlineData("thundering-strikes",5,1,2,null,0)]
    [InlineData("ancestral-knowledge",5,1,3,null,0)]
    [InlineData("mental-dexterity",3,2,2,null,0)]
    [InlineData("improved-lightning-shield",3,2,4,null,0)]
    [InlineData("elemental-weapons",3,3,1,null,0)]
    [InlineData("shamanistic-focus",1,3,3,null,0)]
    [InlineData("flurry",5,4,2,"mental-dexterity",3)]
    [InlineData("stormstrike",1,4,3,null,0)]
    [InlineData("mental-quickness",2,5,2,null,0)]
    [InlineData("improved-stormstrike",2,5,3,"stormstrike",1)]
    [InlineData("maelstrom-weapon",5,6,3,null,0)]
    [InlineData("rage-of-the-farseer",1,7,2,"mental-quickness",2)]
    public void RestrictedHistoricalPositionsAndFullRankParentConventionRemainPinned(
        string key,int cap,int row,int column,string? parent,int parentRank)
    {
        var talent=Assert.IsType<ForeverHistoricalCasterTalentCatalog.Entry>(
            ForeverHistoricalCasterTalentCatalog.Find("shaman","enhancement",key));
        Assert.Equal((cap,row,column,parent,parentRank),
            (talent.MaxRank,talent.Row,talent.Column,talent.PrerequisiteKey,talent.PrerequisiteRank));
    }

    [Fact]
    public void RealThirtySixRankAllocationPassesWithFifteenUnspent()
    {
        var request=Request();
        var before=JsonSerializer.Serialize(request,Web);
        var response=Assert.IsType<OkObjectResult>(Validate(request.Enhancement!.Talents));
        var json=JsonSerializer.SerializeToElement(response.Value,Web);
        Assert.Equal(36,json.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(15,json.GetProperty("remaining").GetInt32());
        Assert.Equal(36,json.GetProperty("treePoints").GetProperty("enhancement").GetInt32());
        Assert.Equal(0,json.GetProperty("unmodeledSelections").GetArrayLength());
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,json.GetProperty("sourceStatus").GetString());
        Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Validate("shaman","enhancement",
            JsonSerializer.SerializeToElement(request.Enhancement.Talents,Web)));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Fact]
    public void CuratedEnhancementPresetIsExactlySourceBackedPartialThirtySix()
    {
        var preset=ForeverItemizedPresetCatalog.Load("shaman","enhancement");
        Assert.Equal(36,preset.TotalRankedPoints);
        Assert.Equal(15,preset.UnspentPoints);
        Assert.Equal(CompleteRanks().OrderBy(x=>x.Key),preset.Ranks.OrderBy(x=>x.Key));
        Assert.Equal("source-backed-partial-historical-provisional-36-ranks",preset.AllocationStatus);
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,
            preset.Provenance.GetProperty("sourceCommit").GetString());
        Assert.Equal("ca93c9b67fc585c5aed52eabb3a90d6048fce25a",
            preset.Provenance.GetProperty("sourceBlob").GetString());
        Assert.Contains("15 unspent",preset.Provenance.GetProperty("statement").GetString());
        Assert.Contains("not current-client certified",preset.Provenance.GetProperty("statement").GetString());
        Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().DefaultItemized("shaman","enhancement"));
    }

    [Fact]
    public async Task LegalThirtyFiveRankBuildKeepsSixteenUnspentAndStillExecutes()
    {
        var request=Request();
        request.Enhancement!.Talents.Ranks.Remove("rage-of-the-farseer");
        request.Enhancement.Talents.EnhancementPointsSpent=35;
        request.ItemizedTalentRanks!.Remove("rage-of-the-farseer");
        var before=JsonSerializer.Serialize(request,Web);
        var validation=Assert.IsType<OkObjectResult>(Validate(request.Enhancement.Talents));
        var allocation=JsonSerializer.SerializeToElement(validation.Value,Web);
        Assert.Equal(35,allocation.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(16,allocation.GetProperty("remaining").GetInt32());
        var response=await Run(request);
        Assert.True(response.GetProperty("sim").GetProperty("damageDone").GetDecimal()>0);
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Fact]
    public async Task StrictDispatchRunsActualEnhancementAndReplaysWithoutMutatingInput()
    {
        // JSON roundtrip gives both dictionaries the same ordinal semantics as HTTP.
        var request=JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(
            JsonSerializer.Serialize(Request(),Web),Web)!;
        var before=JsonSerializer.Serialize(request,Web);
        var response=await Run(request);
        Assert.Equal("shaman",response.GetProperty("classKey").GetString());
        Assert.Equal("enhancement",response.GetProperty("specialization").GetString());
        Assert.Equal(60,response.GetProperty("level").GetInt32());
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,response.GetProperty("talentSourceStatus").GetString());
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,response.GetProperty("talentSourceCommit").GetString());
        Assert.True(response.GetProperty("sim").GetProperty("damageDone").GetDecimal()>0);
        Assert.Contains("lightning-shield",response.GetProperty("sim").GetProperty("damageByAbility").GetRawText());
        var direct=JsonSerializer.SerializeToElement(new EnhancementShamanSimRunner().Run(request.Enhancement!),Web);
        var replay=await Run(request);
        foreach(var key in new[]{"dps","damageDone","endingMana","maximumMana","endingHealth",
            "maximumHealth","attackPower","spellPower","damageByAbility","combatItemUses",
            "maelstromInstantBolts","improvedStormstrikeRegenProcs"})
        {
            Assert.Equal(direct.GetProperty(key).GetRawText(),response.GetProperty("sim").GetProperty(key).GetRawText());
            Assert.Equal(response.GetProperty("sim").GetProperty(key).GetRawText(),replay.GetProperty("sim").GetProperty(key).GetRawText());
        }
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Theory]
    [InlineData("upper")]
    [InlineData("whitespace")]
    [InlineData("duplicate-case")]
    public async Task AliasesCannotValidateCanonicalRanksAndExecuteDifferentRanks(string kind)
    {
        const string key="ancestral-knowledge";
        var alias=kind=="whitespace"?" "+key+" ":key.ToUpperInvariant();
        foreach(var mode in new[]{(Typed:false,Full:true),(Typed:true,Full:false),(Typed:true,Full:true)})
        {
            var request=Request();
            var maps=new List<Dictionary<string,int>>();
            if(mode.Typed)maps.Add(request.Enhancement!.Talents.Ranks);
            if(mode.Full)maps.Add(request.ItemizedTalentRanks!);
            foreach(var ranks in maps)
            {
                if(kind!="duplicate-case")ranks.Remove(key);
                ranks.Add(alias,5);
            }
            await Reject(request);
        }
        using var json=JsonDocument.Parse("{\"ranks\":{\""+key+"\":5,\""+alias+"\":5}}");
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloTalentController()
            .ValidateItemized("shaman","enhancement",json.RootElement));
    }

    [Theory]
    [InlineData("{\"ranks\":{\"thundering-strikes\":5,\"thundering-strikes\":5}}")]
    [InlineData("{\"ranks\":{\"call-of-flame\":0}}")]
    [InlineData("{\"ranks\":{\"unknown\":0}}")]
    [InlineData("{\"ranks\":{\"ancestral-knowledge\":6}}")]
    [InlineData("{\"ranks\":{\"improved-lightning-shield\":4}}")]
    [InlineData("{\"ranks\":{\"thundering-strikes\":-1}}")]
    [InlineData("{\"ranks\":{\"thundering-strikes\":1.5}}")]
    [InlineData("{\"ranks\":null}")]
    [InlineData("null")]
    public void StrictItemizedValidationRejectsMalformedOrUnreconciledRankMaps(string payload)
    {
        using var json=JsonDocument.Parse(payload);
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloTalentController()
            .ValidateItemized("shaman","enhancement",json.RootElement));
    }

    [Theory]
    [InlineData("mental-dexterity")]
    [InlineData("improved-lightning-shield")]
    [InlineData("elemental-weapons")]
    [InlineData("shamanistic-focus")]
    [InlineData("flurry")]
    [InlineData("stormstrike")]
    [InlineData("mental-quickness")]
    [InlineData("improved-stormstrike")]
    [InlineData("maelstrom-weapon")]
    [InlineData("rage-of-the-farseer")]
    public void EveryLaterRowNeedsRealEarlierRanks(string key)
    {
        Assert.IsType<BadRequestObjectResult>(Validate(new {
            ranks=new Dictionary<string,int>{{key,1}},elementalPointsSpent=0,
            enhancementPointsSpent=1,restorationPointsSpent=0}));
    }

    [Theory]
    [InlineData("flurry","mental-dexterity",2)]
    [InlineData("improved-stormstrike","stormstrike",0)]
    [InlineData("rage-of-the-farseer","mental-quickness",1)]
    public async Task HistoricalParentMustHaveItsFullRankEvenWithEnoughEarlierRows(string child,string parent,int rank)
    {
        var request=Request();
        request.Enhancement!.Talents.Ranks[parent]=rank;
        request.ItemizedTalentRanks![parent]=rank;
        request.Enhancement.Talents.EnhancementPointsSpent=request.ItemizedTalentRanks.Values.Sum();
        var result=Assert.IsType<BadRequestObjectResult>(Validate(request.Enhancement.Talents));
        var error=JsonSerializer.SerializeToElement(result.Value,Web).GetProperty("error").GetString();
        Assert.Contains(child,error);
        Assert.Contains(parent,error);
        await Reject(request);
    }

    [Theory]
    [InlineData("typed-rank")]
    [InlineData("full-rank")]
    [InlineData("full-only")]
    [InlineData("unknown-typed")]
    [InlineData("unknown-full")]
    [InlineData("elemental-typed")]
    [InlineData("elemental-full")]
    [InlineData("synthetic-enhancement")]
    [InlineData("synthetic-elemental")]
    [InlineData("synthetic-restoration")]
    [InlineData("early-rows")]
    public async Task FullMapAndTypedRanksMustMatchRealSpecSpecificAllocation(string fault)
    {
        var request=Request();
        var talents=request.Enhancement!.Talents;
        switch(fault)
        {
            case "typed-rank":talents.Ranks["ancestral-knowledge"]=4;break;
            case "full-rank":request.ItemizedTalentRanks!["ancestral-knowledge"]=4;break;
            case "full-only":talents.Ranks.Remove("ancestral-knowledge");break;
            case "unknown-typed":talents.Ranks["unknown"]=0;break;
            case "unknown-full":request.ItemizedTalentRanks!["unknown"]=0;break;
            case "elemental-typed":talents.Ranks["call-of-flame"]=0;break;
            case "elemental-full":request.ItemizedTalentRanks!["call-of-flame"]=0;break;
            case "synthetic-enhancement":talents.EnhancementPointsSpent=41;break;
            case "synthetic-elemental":talents.ElementalPointsSpent=10;break;
            case "synthetic-restoration":talents.RestorationPointsSpent=1;break;
            case "early-rows":
                foreach(var key in new[]{"ancestral-knowledge","improved-lightning-shield"})
                {
                    talents.Ranks.Remove(key);
                    request.ItemizedTalentRanks!.Remove(key);
                }
                talents.EnhancementPointsSpent=28;
                break;
        }
        await Reject(request);
    }

    [Theory]
    [InlineData("wrapper")]
    [InlineData("child")]
    [InlineData("character")]
    [InlineData("talents")]
    [InlineData("typed-ranks")]
    [InlineData("full-ranks")]
    [InlineData("equipment")]
    [InlineData("tuning")]
    [InlineData("combat-items")]
    [InlineData("mode")]
    [InlineData("baseline")]
    [InlineData("multiple")]
    [InlineData("wrapper-class")]
    [InlineData("wrapper-spec")]
    [InlineData("character-level")]
    [InlineData("character-class")]
    [InlineData("character-spec")]
    public async Task MissingOrConflictingRequestGraphFailsClosed(string fault)
    {
        var request=Request();
        var typed=request.Enhancement!;
        switch(fault)
        {
            case "wrapper":
                Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(null,default));
                return;
            case "child":request.Enhancement=null;break;
            case "character":typed.Character=null!;break;
            case "talents":typed.Talents=null!;break;
            case "typed-ranks":typed.Talents.Ranks=null!;break;
            case "full-ranks":request.ItemizedTalentRanks=null;break;
            case "equipment":typed.Character.Equipment=null!;break;
            case "tuning":typed.Tuning=null!;break;
            case "combat-items":typed.CombatItems=null!;break;
            case "mode":request.ItemizedTalentMode=false;break;
            case "baseline":typed.UseProvisionalLevel60Baseline=false;break;
            case "multiple":request.Elemental=new ElementalShamanSimRequest();break;
            case "wrapper-class":request.ClassKey="druid";break;
            case "wrapper-spec":request.Specialization="elemental";break;
            case "character-level":typed.Character.Level=59;break;
            case "character-class":typed.Character.ClassKey="druid";break;
            case "character-spec":typed.Character.SpecializationKey="elemental";break;
        }
        await Reject(request);
    }


    private static ForeverLevel60SimulationRequest CrossTreeRequest(int elemental)
    {
        var request=Request();
        var talents=request.Enhancement!.Talents;
        if(elemental>=10)
        {
            talents.Ranks["convection"]=5;
            talents.Ranks["concussion"]=5;
        }
        if(elemental==15)talents.Ranks["reverberation"]=5;
        talents.ElementalPointsSpent=elemental;
        request.ItemizedTalentRanks=new(talents.Ranks,StringComparer.Ordinal);
        return request;
    }

    [Theory]
    [InlineData(0,36,15)] [InlineData(10,46,5)] [InlineData(15,51,0)]
    public void CrossTreeAllocationsCountRealRanksInTheirOwnTrees(int elemental,int spent,int unspent)
    {
        var request=CrossTreeRequest(elemental);
        var before=JsonSerializer.Serialize(request,Web);
        request.Enhancement!.Talents.Validate(60);
        var validation=Assert.IsType<OkObjectResult>(Validate(request.Enhancement.Talents));
        var result=JsonSerializer.SerializeToElement(validation.Value,Web);
        Assert.Equal(spent,result.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(unspent,result.GetProperty("remaining").GetInt32());
        Assert.Equal(36,result.GetProperty("treePoints").GetProperty("enhancement").GetInt32());
        Assert.Equal(elemental,result.GetProperty("treePoints").TryGetProperty("elemental",out var points)?points.GetInt32():0);
        Assert.Empty(result.GetProperty("unmodeledSelections").EnumerateArray());
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Theory]
    [InlineData("convection")] [InlineData("concussion")] [InlineData("reverberation")]
    public void CrossTreeRanksZeroThroughFiveAndRemovalRemainLegal(string key)
    {
        for(var rank=0;rank<=5;rank++)
        {
            var request=Request();
            var talents=request.Enhancement!.Talents;
            if(key=="reverberation")talents.Ranks["convection"]=5;
            talents.Ranks[key]=rank;
            talents.ElementalPointsSpent=rank+(key=="reverberation"?5:0);
            Assert.IsType<OkObjectResult>(Validate(talents));
            talents.Ranks.Remove(key);
            talents.ElementalPointsSpent=key=="reverberation"?5:0;
            Assert.IsType<OkObjectResult>(Validate(talents));
        }
    }

    [Theory]
    [InlineData("convection")] [InlineData("concussion")] [InlineData("reverberation")]
    public void CrossTreeCapsRejectSixAndNegativeRanks(string key)
    {
        foreach(var rank in new[]{-1,6})
        {
            var request=CrossTreeRequest(15);
            request.Enhancement!.Talents.Ranks[key]=rank;
            Assert.IsType<BadRequestObjectResult>(Validate(request.Enhancement.Talents));
            Assert.Throws<ArgumentException>(()=>request.Enhancement.Talents.Validate(60));
        }
    }

    [Theory]
    [InlineData("wrong-tree")] [InlineData("over-budget")] [InlineData("missing-row")]
    [InlineData("typed-rank")] [InlineData("full-rank")] [InlineData("synthetic-restoration")]
    public async Task CrossTreeStrictDispatchRejectsInvalidAllocationOrMismatchedMaps(string fault)
    {
        var request=CrossTreeRequest(15);
        var talents=request.Enhancement!.Talents;
        switch(fault)
        {
            case "wrong-tree":talents.ElementalPointsSpent=0;talents.EnhancementPointsSpent=51;break;
            case "over-budget":talents.ElementalPointsSpent=16;break;
            case "missing-row":
                talents.Ranks.Remove("convection");talents.Ranks.Remove("concussion");
                talents.ElementalPointsSpent=5;
                request.ItemizedTalentRanks=new(talents.Ranks,StringComparer.Ordinal);break;
            case "typed-rank":talents.Ranks["concussion"]=4;talents.ElementalPointsSpent=14;break;
            case "full-rank":request.ItemizedTalentRanks!["reverberation"]=4;break;
            case "synthetic-restoration":talents.ElementalPointsSpent=14;talents.RestorationPointsSpent=1;break;
        }
        await Reject(request);
    }

    [Fact]
    public void CrossTreeLegacyCapacityDoesNotBecomeStrictEqualityOrWrongTreeCapacity()
    {
        var talents=new ForeverEnhancementShamanTalents();
        talents.Ranks["convection"]=5;
        talents.Ranks["concussion"]=5;
        talents.Validate(60); // Existing 10/41 totals remain aggregate capacities.
        Assert.IsType<BadRequestObjectResult>(Validate(talents));
        talents.ElementalPointsSpent=9; // Spare Enhancement capacity cannot fund Elemental ranks.
        Assert.Throws<ArgumentException>(()=>talents.Validate(60));
        talents.ElementalPointsSpent=10;
        talents.EnhancementPointsSpent=27; // Spare Elemental capacity cannot fund Enhancement ranks.
        Assert.Throws<ArgumentException>(()=>talents.Validate(60));
    }

    // Runtime GUIDs are intentionally fresh per run; compare every other event field.
    private static string TimelineProjection(JsonElement response) => JsonSerializer.Serialize(
        response.GetProperty("run").GetProperty("timeline").EnumerateArray().Select(e=>
            e.EnumerateObject().Where(p=>!p.Name.EndsWith("Id",StringComparison.Ordinal))
                .ToDictionary(p=>p.Name,p=>p.Value)),Web);

    [Theory]
    [InlineData(0,0)] [InlineData(10,0)] [InlineData(15,0)]
    [InlineData(0,80)] [InlineData(10,80)] [InlineData(15,80)]
    public async Task CrossTreeJsonRoundTripSharedDirectParityAndReplayUseExplicitEqualLatency(int elemental,int milliseconds)
    {
        var request=CrossTreeRequest(elemental);
        request.Enhancement!.CaptureTimeline=true;
        request.Enhancement.DurationSeconds=18m;
        request=JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(JsonSerializer.Serialize(request,Web),Web)!;
        var before=JsonSerializer.Serialize(request,Web);
        var previous=SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds=milliseconds/1000m;
            var direct=JsonSerializer.SerializeToElement(new EnhancementShamanSimRunner().Run(request.Enhancement!),Web);
            var shared=await Run(request);
            var replay=await Run(JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(before,Web)!);
            foreach(var key in new[]{"damageDone","damageByAbility","endingMana","maximumMana","endingHealth",
                "maximumHealth","attackPower","spellPower","maelstromInstantBolts"})
            {
                Assert.Equal(direct.GetProperty(key).GetRawText(),shared.GetProperty("sim").GetProperty(key).GetRawText());
                Assert.Equal(shared.GetProperty("sim").GetProperty(key).GetRawText(),replay.GetProperty("sim").GetProperty(key).GetRawText());
            }
            Assert.Equal(TimelineProjection(direct),TimelineProjection(shared.GetProperty("sim")));
            Assert.Equal(TimelineProjection(shared.GetProperty("sim")),TimelineProjection(replay.GetProperty("sim")));
            Assert.Equal(before,JsonSerializer.Serialize(request,Web));
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds=previous; }
    }

    [Fact]
    public void ZeroCrossTreeRanksMatchAbsentRanksInDefaultTimeline()
    {
        var request=CrossTreeRequest(0).Enhancement!;
        request.CaptureTimeline=true;
        var previous=SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds=0;
            var before=JsonSerializer.SerializeToElement(new EnhancementShamanSimRunner().Run(request),Web);
            foreach(var key in new[]{"convection","concussion","reverberation"})request.Talents.Ranks[key]=0;
            var zero=JsonSerializer.SerializeToElement(new EnhancementShamanSimRunner().Run(request),Web);
            Assert.Equal(before.GetProperty("damageDone").GetRawText(),zero.GetProperty("damageDone").GetRawText());
            Assert.Equal(before.GetProperty("endingMana").GetRawText(),zero.GetProperty("endingMana").GetRawText());
            Assert.Equal(TimelineProjection(before),TimelineProjection(zero));
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds=previous; }
    }

    [Fact]
    public void CrossTreePassivesDoNotChangeDerivedPrecombatStats()
    {
        var initial=EnhancementShamanSimRunner.Preview(CrossTreeRequest(0).Enhancement!);
        var selected=EnhancementShamanSimRunner.Preview(CrossTreeRequest(15).Enhancement!);
        Assert.Equal(initial.AttackPower,selected.AttackPower);
        Assert.Equal(initial.SpellPower,selected.SpellPower);
        Assert.Equal(initial.MaximumMana,selected.MaximumMana);
        Assert.Equal(initial.MaximumHealth,selected.MaximumHealth);
        Assert.Equal(JsonSerializer.Serialize(initial.EffectiveStats,Web),JsonSerializer.Serialize(selected.EffectiveStats,Web));
    }

    [Fact]
    public async Task LegacyTenFortyOneDefaultRemainsUnchangedAndRejectedByStrictMode()
    {
        var legacy=new EnhancementShamanSimRequest {DurationSeconds=2};
        Assert.Equal((10,41,0),(legacy.Talents.ElementalPointsSpent,
            legacy.Talents.EnhancementPointsSpent,legacy.Talents.RestorationPointsSpent));
        Assert.Equal(10,legacy.Talents.Ranks.Count);
        Assert.Equal(28,legacy.Talents.Ranks.Values.Sum());
        Assert.False(legacy.Talents.Ranks.ContainsKey("ancestral-knowledge"));
        Assert.False(legacy.Talents.Ranks.ContainsKey("improved-lightning-shield"));
        var before=JsonSerializer.Serialize(legacy,Web);
        Assert.True(new EnhancementShamanSimRunner().Run(legacy).DamageDone>0);
        Assert.Equal(before,JsonSerializer.Serialize(legacy,Web));
        Assert.IsType<BadRequestObjectResult>(Validate(legacy.Talents));
        var request=Request();
        request.Enhancement!.Talents=legacy.Talents;
        request.ItemizedTalentRanks=new(legacy.Talents.Ranks,StringComparer.Ordinal);
        await Reject(request);
        var strict=ForeverItemizedPresetCatalog.Load("shaman","enhancement");
        Assert.Equal(36,strict.TotalRankedPoints);
        Assert.Equal(CompleteRanks().OrderBy(x=>x.Key),strict.Ranks.OrderBy(x=>x.Key));
        Assert.NotEqual(legacy.Talents.Ranks.Values.Sum(),strict.TotalRankedPoints);
    }
}
