using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Shaman;

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
        Assert.Equal(12,enhancement.Count);
        Assert.Equal(36,enhancement.Sum(x=>x.MaxRank));
        Assert.All(enhancement,x=>Assert.Equal("enhancement",x.Tree));
        Assert.Equal(CompleteRanks().OrderBy(x=>x.Key),
            enhancement.ToDictionary(x=>x.Key,x=>x.MaxRank).OrderBy(x=>x.Key));
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("shaman","enhancement","convection"));
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("shaman","elemental","flurry"));
        Assert.Equal(26,ForeverHistoricalCasterTalentCatalog.ForClass("shaman").Count);
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.ForClass("shaman"),
            ForeverHistoricalCasterTalentCatalog.ForSpecialization("shaman","elemental"));
        Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForSpecialization("shaman","restoration"));
        var catalog=Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Catalog("shaman","enhancement"));
        var json=JsonSerializer.SerializeToElement(catalog.Value,Web);
        Assert.Equal(12,json.GetProperty("talents").GetArrayLength());
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
    [InlineData("{\"ranks\":{\"convection\":0}}")]
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
            case "elemental-typed":talents.Ranks["convection"]=0;break;
            case "elemental-full":request.ItemizedTalentRanks!["convection"]=0;break;
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
        Assert.Throws<ArgumentException>(()=>ForeverItemizedPresetCatalog.Load("shaman","enhancement"));
    }
}
