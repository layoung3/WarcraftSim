using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HistoricalRogueStrictDispatchTests
{
    private static readonly JsonSerializerOptions Web=new(JsonSerializerDefaults.Web);
    // Historical metadata-valid test allocation, not a curated or optimized preset.
    private static Dictionary<string,int> Ranks() => new(StringComparer.Ordinal) {
        ["malice"]=5,["ruthlessness"]=3,["improved-slice-and-dice"]=3,
        ["improved-expose-armor"]=2,["lethality"]=5,["vile-poisons"]=5,
        ["cold-blood"]=1,["improved-poisons"]=5,["vigor"]=2,
        ["mutilate"]=1,["seal-fate"]=5,["venom"]=1
    };
    private static RogueSimRequest Input(int points=38)
    {
        var input=RogueSimRequest.CreatePreset("assassination");
        input.DurationSeconds=20;input.CaptureTimeline=true;
        input.Character.Race="human";input.UseProvisionalLevel60Baseline=true;
        input.Talents=new(){AssassinationPointsSpent=points,Ranks=Ranks()};
        if(points==36)input.Talents.Ranks.Remove("improved-expose-armor");
        return input;
    }
    // JSON boundary makes this test fail on the pre-admission production contract.
    private static ForeverLevel60SimulationRequest Envelope(RogueSimRequest input,
        Dictionary<string,int>? full=null,string cls="rogue",string spec="assassination") =>
        JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(JsonSerializer.Serialize(new {
            classKey=cls,specialization=spec,itemizedTalentMode=true,
            itemizedTalentRanks=full??input.Talents?.Ranks,rogue=input
        },Web),Web)!;
    private static JsonElement Json(object? value)=>JsonSerializer.SerializeToElement(value,Web);
    private static IActionResult Validate(object? talents)=>new ForeverSoloTalentController()
        .ValidateItemized("rogue","assassination",Json(talents));
    private static async Task<JsonElement> Run(ForeverLevel60SimulationRequest request)
    {
        var before=JsonSerializer.Serialize(request,Web);
        var result=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
        return Json(result.Value);
    }
    private static async Task Reject(ForeverLevel60SimulationRequest request)
    {
        var before=JsonSerializer.Serialize(request,Web);
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }

    [Fact]
    public void ExactTwelveKeyCatalogDoesNotAdmitOtherSpecsOrClassWideDefaults()
    {
        Assert.True(ForeverHistoricalCasterTalentCatalog.Supports("rogue","assassination"));
        var catalog=ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue","assassination");
        Assert.Equal(12,catalog.Count);
        Assert.Equal(Ranks().OrderBy(x=>x.Key),catalog.ToDictionary(x=>x.Key,x=>x.MaxRank).OrderBy(x=>x.Key));
        Assert.All(catalog,t=>Assert.Equal("assassination",t.Tree));
        Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForClass("rogue"));
        var controller=new ForeverSoloTalentController();
        foreach(var spec in new[]{"combat","subtlety","","Assassination"})
        {
            Assert.False(ForeverHistoricalCasterTalentCatalog.Supports("rogue",spec));
            Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue",spec));
            Assert.IsType<BadRequestObjectResult>(controller.Catalog("rogue",spec));
            Assert.IsType<BadRequestObjectResult>(controller.ValidateItemized("rogue",spec,Json(Input().Talents)));
        }
        Assert.IsType<BadRequestObjectResult>(controller.DefaultItemized("rogue","assassination"));
        Assert.IsType<BadRequestObjectResult>(controller.ItemizedCatalog("rogue","assassination"));
        var rows=Json(Assert.IsType<OkObjectResult>(controller.Catalog("rogue","assassination")).Value).GetProperty("talents");
        Assert.Equal(12,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
        {
            Assert.Equal("partial",row.GetProperty("simulationStatus").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,row.GetProperty("historicalValidationSourceCommit").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,row.GetProperty("historicalValidationSourceStatus").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceUrl("rogue"),row.GetProperty("sourceUrl").GetString());
        }
        var supported=Json(Assert.IsType<OkObjectResult>(new ForeverLevel60SimulationController().Supported()).Value);
        Assert.DoesNotContain("rogue",supported.GetProperty("classes").GetRawText());
    }

    [Theory]
    [InlineData("malice",5,1,3,null,0)]
    [InlineData("ruthlessness",3,2,1,null,0)]
    [InlineData("improved-slice-and-dice",3,2,4,null,0)]
    [InlineData("improved-expose-armor",2,3,2,null,0)]
    [InlineData("lethality",5,3,3,"malice",5)]
    [InlineData("vile-poisons",5,4,1,null,0)]
    [InlineData("cold-blood",1,4,2,null,0)]
    [InlineData("improved-poisons",5,4,3,null,0)]
    [InlineData("vigor",2,5,1,null,0)]
    [InlineData("mutilate",1,5,2,null,0)]
    [InlineData("seal-fate",5,6,3,null,0)]
    [InlineData("venom",1,7,2,"mutilate",1)]
    public void PinnedSourcePositionsAndDisclosedFullParentConvention(string key,int cap,int row,int col,string? parent,int rank)
    {
        var entry=Assert.IsType<ForeverHistoricalCasterTalentCatalog.Entry>(ForeverHistoricalCasterTalentCatalog.Find("rogue","assassination",key));
        Assert.Equal((cap,row,col,parent,rank),(entry.MaxRank,entry.Row,entry.Column,entry.PrerequisiteKey,entry.PrerequisiteRank));
    }

    [Theory][InlineData(38)][InlineData(36)]
    public async Task PartialAllocationsRunWithExactDirectParityAndReplay(int points)
    {
        var input=Input(points);var before=JsonSerializer.Serialize(input,Web);
        var valid=Json(Assert.IsType<OkObjectResult>(Validate(input.Talents)).Value);
        Assert.Equal(points,valid.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(51-points,valid.GetProperty("remaining").GetInt32());
        Assert.Equal(points,valid.GetProperty("treePoints").GetProperty("assassination").GetInt32());
        Assert.Empty(valid.GetProperty("unmodeledSelections").EnumerateArray());
        Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Validate("rogue","assassination",Json(input.Talents)));
        var request=Envelope(input);var result=await Run(request);var replay=await Run(request);
        var direct=Json(new RogueSimRunner().Run(input));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,result.GetProperty("talentSourceCommit").GetString());
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,result.GetProperty("talentSourceStatus").GetString());
        foreach(var key in new[]{"dps","damageDone","endingEnergy","maximumEnergy","maximumHealth","endingHealth","endingComboPoints","poisonProcs","finishersUsed","stealthOpeners","damageByAbility"})
        {
            Assert.Equal(direct.GetProperty(key).GetRawText(),result.GetProperty("sim").GetProperty(key).GetRawText());
            Assert.Equal(direct.GetProperty(key).GetRawText(),replay.GetProperty("sim").GetProperty(key).GetRawText());
        }
        static string Timeline(JsonElement value)=>JsonSerializer.Serialize(value.GetProperty("run").GetProperty("timeline").EnumerateArray().Select(e=>new {
            time=e.GetProperty("timeSeconds"),type=e.GetProperty("type"),ability=e.GetProperty("abilityKey"),
            amount=e.GetProperty("amount"),result=e.GetProperty("resultKey") }));
        Assert.Equal(Timeline(direct),Timeline(result.GetProperty("sim")));
        Assert.Equal(Timeline(direct),Timeline(replay.GetProperty("sim")));
        var notes=result.GetProperty("talentModelingNotes").GetRawText();
        foreach(var phrase in new[]{"partial","unspent","not optimized","17.25","13","Deadly","dispel","full-parent","decoded","not current-client"})
            Assert.Contains(phrase,notes);
        var preview=RogueSimRunner.Preview(input);
        Assert.Equal(110,preview.MaximumEnergy);
        Assert.Equal(direct.GetProperty("maximumHealth").GetDecimal(),preview.MaximumHealth);
        Assert.Equal(before,JsonSerializer.Serialize(input,Web));
    }

    [Theory]
    [InlineData("precision")][InlineData("deflection")][InlineData("cutthroat")][InlineData("improved-kidney-shot")]
    [InlineData("improved-garrote")][InlineData("quick-recovery")][InlineData("fleet-footed")][InlineData("master-of-subtlety")]
    [InlineData("improved-eviscerate")][InlineData("riposte")][InlineData("unknown")]
    public async Task UnadmittedKeysRejectEvenAtZeroInEitherMap(string key)
    {
        foreach(var rank in new[]{0,1})
        {
            var input=Input();input.Talents.Ranks[key]=rank;
            Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));
            Assert.IsType<BadRequestObjectResult>(new ForeverSoloTalentController().Validate("rogue","assassination",Json(input.Talents)));
            await Reject(Envelope(input));
            var full=Ranks();full[key]=rank;await Reject(Envelope(Input(),full));
        }
    }
    [Theory][InlineData("MALICE")][InlineData(" malice ")]
    public async Task AliasesAreRejectedWithoutNormalizingComparers(string alias)
    {
        foreach(var mode in new[]{"typed","full","both"})
        {
            var input=Input();var full=Ranks();
            if(mode is "typed" or "both"){input.Talents.Ranks.Remove("malice");input.Talents.Ranks[alias]=5;}
            if(mode is "full" or "both"){full.Remove("malice");full[alias]=5;}
            await Reject(Envelope(input,full));
        }
    }
    [Theory]
    [InlineData("{\"ranks\":{\"malice\":5,\"malice\":5}}")]
    [InlineData("{\"ranks\":{\"malice\":5,\"MALICE\":5}}")]
    [InlineData("{\"ranks\":{\"malice\":6}}")]
    [InlineData("{\"ranks\":{\"malice\":-1}}")]
    [InlineData("{\"ranks\":{\"malice\":1.5}}")]
    [InlineData("{\"ranks\":null}")][InlineData("null")]
    public void RawValidationRejectsMalformedAndDuplicateKeys(string raw)
    {
        using var json=JsonDocument.Parse(raw);var controller=new ForeverSoloTalentController();
        Assert.IsType<BadRequestObjectResult>(controller.ValidateItemized("rogue","assassination",json.RootElement));
        Assert.IsType<BadRequestObjectResult>(controller.Validate("rogue","assassination",json.RootElement));
    }
    [Fact]
    public async Task EveryLaterRowNeedsActualEarlierRanks()
    {
        foreach(var key in Ranks().Keys.Where(k=>k!="malice"))
        {
            var input=Input();input.Talents=new(){AssassinationPointsSpent=1,Ranks=new(){[key]=1}};
            Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
        }
    }
    [Theory][InlineData("malice",4)][InlineData("mutilate",0)]
    public async Task ParentRanksCannotBeInventedByDeclaredTotals(string key,int rank)
    {
        var input=Input();input.Talents.Ranks[key]=rank;
        input.Talents.AssassinationPointsSpent=input.Talents.Ranks.Values.Sum();
        Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
    }
    [Theory]
    [InlineData("rank")][InlineData("missing-full")][InlineData("missing-typed")]
    [InlineData("full-zero")][InlineData("typed-zero")][InlineData("aggregate")][InlineData("combat-total")][InlineData("subtlety-total")]
    public async Task ExactMapAndDeclaredAggregateEqualityIncludesExplicitZeroKeys(string fault)
    {
        var input=Input();var full=Ranks();
        switch(fault)
        {
            case "rank":full["improved-expose-armor"]=1;break;
            case "missing-full":full.Remove("malice");break;
            case "missing-typed":input.Talents.Ranks.Remove("malice");break;
            case "full-zero":input=Input(36);full=new(input.Talents.Ranks);full["improved-expose-armor"]=0;break;
            case "typed-zero":input=Input(36);full=new(input.Talents.Ranks);input.Talents.Ranks["improved-expose-armor"]=0;break;
            case "aggregate":input.Talents.AssassinationPointsSpent=41;break;
            case "combat-total":input.Talents.CombatPointsSpent=10;break;
            case "subtlety-total":input.Talents.SubtletyPointsSpent=1;break;
        }
        await Reject(Envelope(input,full));
    }
    [Theory]
    [InlineData("character")][InlineData("talents")][InlineData("ranks")][InlineData("tuning")]
    [InlineData("equipment")][InlineData("items")][InlineData("entry")][InlineData("baseline")]
    [InlineData("level")][InlineData("class")][InlineData("character-spec")][InlineData("typed-spec")]
    [InlineData("baseline-race")][InlineData("baseline-modes")]
    public async Task TypedIdentityAndPreparationMustBeComplete(string fault)
    {
        var input=Input();
        switch(fault)
        {
            case "character":input.Character=null!;break;
            case "talents":input.Talents=null!;break;
            case "ranks":input.Talents.Ranks=null!;break;
            case "tuning":input.Tuning=null!;break;
            case "equipment":input.Character.Equipment=null!;break;
            case "items":input.Character.Equipment.Items=null!;break;
            case "entry":input.Character.Equipment.Items.Add(null!);break;
            case "baseline":input.UseProvisionalLevel60Baseline=false;break;
            case "level":input.Character.Level=59;break;
            case "class":input.Character.ClassKey="mage";break;
            case "character-spec":input.Character.SpecializationKey="combat";break;
            case "typed-spec":input.Specialization="combat";break;
            case "baseline-race":input.Character.Race="not-a-race";break;
            case "baseline-modes":input.UseReviewedLevel60Baseline=true;break;
        }
        await Reject(Envelope(input,Ranks()));
    }
    [Theory][InlineData("rogue","combat")][InlineData("rogue","subtlety")][InlineData("mage","assassination")]
    [InlineData("Rogue","assassination")][InlineData("rogue","Assassination")][InlineData("rogue"," assassination ")]
    public async Task WrapperIdentityMustBeExact(string cls,string spec)=>await Reject(Envelope(Input(),cls:cls,spec:spec));
    [Fact]
    public async Task MultiplePayloadsAndMissingStrictMapsReject()
    {
        foreach(var fault in new[]{"multiple","mode","map","child"})
        {
            var payload=JsonNode.Parse(JsonSerializer.Serialize(Envelope(Input()),Web))!.AsObject();
            if(fault=="multiple")payload["mage"]=JsonSerializer.SerializeToNode(new MageSimRequest(),Web);
            if(fault=="mode")payload["itemizedTalentMode"]=false;
            if(fault=="map")payload["itemizedTalentRanks"]=null;
            if(fault=="child")payload["rogue"]=null;
            await Reject(payload.Deserialize<ForeverLevel60SimulationRequest>(Web)!);
        }
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public async Task CatalogAndEnchantSelectionsRemainUnsupported(int kind)
    {
        var input=Input();input.Character.Equipment.Items.Add(new(){SlotKey="main-hand",ItemKey="unsupported",
            ItemId=kind==0?19102:null,EnchantSpellId=kind==1?20034:null,TemporaryEnchantSpellId=kind==2?25122:null});
        await Reject(Envelope(input));
    }
    [Theory][InlineData("assassination",41,10,0)][InlineData("combat",10,41,0)][InlineData("subtlety",10,0,41)]
    public async Task LegacyDefaultAggregatesAndDirectAcceptanceRemainUnchanged(string spec,int a,int c,int s)
    {
        var input=RogueSimRequest.CreatePreset(spec);input.DurationSeconds=20;
        Assert.Equal((a,c,s),(input.Talents.AssassinationPointsSpent,input.Talents.CombatPointsSpent,input.Talents.SubtletyPointsSpent));
        Assert.False(input.UseExposeArmor);Assert.Equal(0,input.Talents.Rank("improved-expose-armor"));
        Assert.True(new RogueSimRunner().Run(input).DamageDone>0);
        input.Character.Race="human";input.UseProvisionalLevel60Baseline=true;
        await Reject(Envelope(input,spec:spec));
    }
    [Fact]
    public async Task EveryOtherLegacyModeledKeyStaysOutsideStrictAdmission()
    {
        var excluded=ForeverRogueTalentBuild.SupportedTalentMetadata.Where(x=>!Ranks().ContainsKey(x.Key)).ToArray();
        Assert.Equal(30,excluded.Length);
        foreach(var talent in excluded)
            await UnadmittedKeysRejectEvenAtZeroInEitherMap(talent.Key);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task InMemoryDispatchPreservesOriginalObjectsAndDictionaryComparers(bool ignoreCase)
    {
        var input=Input();var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        input.Talents.Ranks=new(input.Talents.Ranks,comparer);
        input.Character.BaseStats.Values=new(input.Character.BaseStats.Values,comparer);
        var ranks=input.Talents.Ranks;var stats=input.Character.BaseStats.Values;
        var full=new Dictionary<string,int>(ranks,comparer);
        var request=new ForeverLevel60SimulationRequest {ClassKey="rogue",Specialization="assassination",
            ItemizedTalentMode=true,ItemizedTalentRanks=full,Rogue=input};
        var result=await Run(request);Assert.True(result.GetProperty("sim").GetProperty("damageDone").GetDecimal()>0);
        Assert.Same(input,request.Rogue);Assert.Same(full,request.ItemizedTalentRanks);
        Assert.Same(ranks,input.Talents.Ranks);Assert.Same(comparer,ranks.Comparer);
        Assert.Same(stats,input.Character.BaseStats.Values);Assert.Same(comparer,stats.Comparer);
    }
    [Theory][InlineData(false,4150)][InlineData(true,3150)]
    public async Task ExplicitObservationAndAuthoredBudgetsUseExistingPreparationOnce(bool observed,int expectedHealth)
    {
        var input=Input();input.UseProvisionalLevel60Baseline=false;
        input.Level60Baseline=new() {Race="human",ClassKey="rogue",Strength=100,Agility=200,Stamina=100,
            Intellect=40,Spirit=40,ClientBuild="controlled-test-build",SourceReference="synthetic-test-not-live-data",
            RecordedAtUtc="2026-11-09T00:00:00Z",ObservedUnbuffedMeleeAttackPower=700,
            ObservedUnbuffedMaximumHealth=observed?3000:null};
        input.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="synthetic-budget",
            Stats=new(){Values=new(){["strength"]=50,["agility"]=25,["attack-power"]=125,["stamina"]=10,["health"]=50}}});
        var preview=RogueSimRunner.Preview(input);Assert.Equal(900,preview.AttackPower);Assert.Equal(expectedHealth,preview.MaximumHealth);
        var result=await Run(Envelope(input));var direct=Json(new RogueSimRunner().Run(input));
        Assert.Equal(expectedHealth,result.GetProperty("sim").GetProperty("maximumHealth").GetDecimal());
        Assert.Equal(direct.GetProperty("damageDone").GetDecimal(),result.GetProperty("sim").GetProperty("damageDone").GetDecimal());
    }
    [Theory][InlineData(-1)][InlineData(52)][InlineData(int.MaxValue)]
    public async Task InvalidDeclaredBudgetsFailWithoutOverflow(int points)
    {
        var input=Input();input.Talents.AssassinationPointsSpent=points;
        Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
    }

}
