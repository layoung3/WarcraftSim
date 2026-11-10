using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HistoricalCasterStrictDispatchTests
{
    private static IActionResult Validate(string cls,string spec,object allocation) =>
        new ForeverSoloTalentController().ValidateItemized(cls,spec,
            JsonSerializer.SerializeToElement(allocation,new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static ForeverLevel60SimulationRequest Balance()
    {
        var typed=new BalanceDruidSimRequest {
            DurationSeconds=2,UseProvisionalLevel60Baseline=true,
            MajorManaPotionCharges=0,
            Talents=new ForeverBalanceDruidTalents {
                BalancePointsSpent=5,FeralPointsSpent=0,RestorationPointsSpent=0,
                Ranks=new(){["improved-wrath"]=5}}
        };
        typed.Character.Race="tauren";
        return new(){ClassKey="druid",Specialization="balance",
            ItemizedTalentMode=true,ItemizedTalentRanks=new(){["improved-wrath"]=5},Balance=typed};
    }
    private static ForeverLevel60SimulationRequest Elemental()
    {
        var typed=new ElementalShamanSimRequest {
            DurationSeconds=2,UseProvisionalLevel60Baseline=true,
            MajorManaPotionCharges=0,
            Talents=new ForeverElementalShamanTalents {
                ElementalPointsSpent=5,EnhancementPointsSpent=0,RestorationPointsSpent=0,
                Ranks=new(){["convection"]=5}}
        };
        typed.Character.Race="tauren";
        typed.Character.Equipment.Items.Clear();
        return new(){ClassKey="shaman",Specialization="elemental",
            ItemizedTalentMode=true,ItemizedTalentRanks=new(){["convection"]=5},Elemental=typed};
    }

    [Fact]
    public void MetadataIsRestrictedToReconciledHistoricalKeys()
    {
        Assert.Equal(19,ForeverHistoricalCasterTalentCatalog.ForClass("druid").Count);
        Assert.Equal(26,ForeverHistoricalCasterTalentCatalog.ForClass("shaman").Count);
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("druid","starlight-wrath"));
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("shaman","elemental-precision"));
        Assert.Contains("not-current-client-certified",ForeverHistoricalCasterTalentCatalog.SourceStatus);
        Assert.Equal(2,ForeverHistoricalCasterTalentCatalog.Find("druid","natures-splendor")!.PrerequisiteRank);
        Assert.Equal("improved-moonfire",ForeverHistoricalCasterTalentCatalog.Find("druid","vengeance")!.PrerequisiteKey);
    }

    [Theory]
    [InlineData("druid","balance","starlight-wrath")]
    [InlineData("shaman","elemental","elemental-precision")]
    [InlineData("druid","balance","overgrowth")]
    public void UnreconciledOrUnmodeledRanksAreRejected(string cls,string spec,string key) =>
        Assert.IsType<BadRequestObjectResult>(Validate(cls,spec,new{ranks=new Dictionary<string,int>{{key,1}}}));

    [Fact]
    public void AggregateAndEarlierRowGatesAreStrict()
    {
        Assert.IsType<BadRequestObjectResult>(Validate("druid","balance",new {
            ranks=new Dictionary<string,int>{{"improved-wrath",5}},balancePointsSpent=41}));
        Assert.IsType<BadRequestObjectResult>(Validate("shaman","elemental",new {
            ranks=new Dictionary<string,int>{{"convection",5}},enhancementPointsSpent=7}));
        Assert.IsType<BadRequestObjectResult>(Validate("druid","balance",new {
            ranks=new Dictionary<string,int>{{"moonkin-form",1}}}));
        Assert.IsType<BadRequestObjectResult>(Validate("shaman","elemental",new {
            ranks=new Dictionary<string,int>{{"elemental-mastery",1}}}));
    }

    [Fact]
    public void HistoricalParentMustBeFullyRanked()
    {
        var ranks=new Dictionary<string,int>{
            ["improved-wrath"]=5,["genesis"]=5,["natures-majesty"]=1,["natures-splendor"]=1};
        Assert.IsType<BadRequestObjectResult>(Validate("druid","balance",new{ranks}));
        ranks["natures-majesty"]=2;
        Assert.IsType<OkObjectResult>(Validate("druid","balance",new{ranks}));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestrictedTypedDispatchRunsAndRetainsProvisionalReceipt(bool elemental)
    {
        var request=elemental?Elemental():Balance();
        var before=JsonSerializer.Serialize(request);
        var controller=new ForeverLevel60SimulationController();
        var response=Assert.IsType<OkObjectResult>(await controller.Simulate(request,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(response.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,
            json.RootElement.GetProperty("talentSourceStatus").GetString());
        Assert.True(json.RootElement.GetProperty("sim").GetProperty("damageDone").GetDecimal()>=0);
        Assert.Equal(before,JsonSerializer.Serialize(request));
        var replay=Assert.IsType<OkObjectResult>(await controller.Simulate(request,default));
        using var replayJson=JsonDocument.Parse(JsonSerializer.Serialize(replay.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        // Execution correlation GUIDs are intentionally not a seeded gameplay metric.
        foreach(var key in new[]{"dps","damageDone","endingMana","endingHealth","damageByAbility","combatItemUses"})
            Assert.Equal(json.RootElement.GetProperty("sim").GetProperty(key).GetRawText(),
                replayJson.RootElement.GetProperty("sim").GetProperty(key).GetRawText());
    }

    [Fact]
    public async Task DispatchRejectsMismatchMultipleChildrenMissingBaselineAndSyntheticTotals()
    {
        var controller=new ForeverLevel60SimulationController();
        var request=Balance();
        request.ItemizedTalentRanks!["improved-wrath"]=4;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Balance();request.ItemizedTalentRanks!["genesis"]=1;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Balance();request.Elemental=Elemental().Elemental;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Balance();request.Balance!.Character=null!;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Elemental();request.Elemental!.Character=null!;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Balance();request.Specialization="feral";
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Balance();request.Balance!.UseProvisionalLevel60Baseline=false;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        request=Elemental();request.Elemental!.Talents.ElementalPointsSpent=31;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
    }

    [Theory]
    [InlineData(false,"upper")]
    [InlineData(false,"whitespace")]
    [InlineData(false,"duplicate-case")]
    [InlineData(true,"upper")]
    [InlineData(true,"whitespace")]
    [InlineData(true,"duplicate-case")]
    public async Task JsonShapedOrdinalAliasesCannotValidateOneRankAndExecuteAnother(bool elemental,string kind)
    {
        var original=elemental?Elemental():Balance();
        var key=elemental?"convection":"improved-wrath";
        var alias=kind=="whitespace"?" "+key+" ":key.ToUpperInvariant();
        foreach(var mode in new[]{(Typed:false,Full:true),(Typed:true,Full:false),(Typed:true,Full:true)})
        {
            var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var request=JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(
                JsonSerializer.Serialize(original,options),options)!;
            var maps=new List<Dictionary<string,int>>();
            if(mode.Typed)maps.Add(elemental?request.Elemental!.Talents.Ranks:request.Balance!.Talents.Ranks);
            if(mode.Full)maps.Add(request.ItemizedTalentRanks!);
            foreach(var ranks in maps)
            {
                if(kind!="duplicate-case")ranks.Remove(key);
                ranks.Add(alias,5);
            }
            var before=JsonSerializer.Serialize(request,options);
            Assert.IsType<BadRequestObjectResult>(
                await new ForeverLevel60SimulationController().Simulate(request,default));
            Assert.Equal(before,JsonSerializer.Serialize(request,options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullTypedOrFullRankMapIsRejected(bool elemental)
    {
        var request=elemental?Elemental():Balance();
        request.ItemizedTalentRanks=null;
        Assert.IsType<BadRequestObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request,default));
        request=elemental?Elemental():Balance();
        if(elemental)request.Elemental!.Talents.Ranks=null!;
        else request.Balance!.Talents.Ranks=null!;
        Assert.IsType<BadRequestObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request,default));
    }
    [Fact]
    public void ExistingElementalPresetPreservesExactlyTheHistoricalRaidSustainRanks()
    {
        var talents=ElementalPreRaidProfileCatalog.Talents("raid-sustain");
        var result=Assert.IsType<OkObjectResult>(Validate("shaman","elemental",talents));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(51,json.RootElement.GetProperty("pointsSpent").GetInt32());
        var partial=ForeverItemizedPresetCatalog.Load("druid","balance");
        var runtime=new WarcraftSim.Data.Forever.Druid.ForeverBalanceDruidTalents();
        Assert.Equal(runtime.Ranks.OrderBy(x=>x.Key),partial.Ranks.OrderBy(x=>x.Key));
        Assert.Equal(35,partial.TotalRankedPoints);
        Assert.Equal(16,partial.UnspentPoints);
        Assert.Contains("partial",partial.AllocationStatus);
        Assert.DoesNotContain("natures-grace",partial.Ranks.Keys);
        Assert.Throws<ArgumentException>(()=>ForeverItemizedPresetCatalog.Load("druid","feral"));
        var preset=ForeverItemizedPresetCatalog.Load("shaman","elemental");
        Assert.Equal(51,preset.TotalRankedPoints);
        Assert.Equal(talents.Ranks.OrderBy(x=>x.Key),preset.Ranks.OrderBy(x=>x.Key));
        Assert.Contains("historical-provisional",preset.AllocationStatus);
        Assert.Contains("Not current-client certification",preset.Provenance.GetProperty("statement").GetString());
        var enhancement=ForeverItemizedPresetCatalog.Load("shaman","enhancement");
        Assert.Equal(36,enhancement.TotalRankedPoints);
        Assert.Equal(15,enhancement.UnspentPoints);
        Assert.Throws<ArgumentException>(()=>ForeverItemizedPresetCatalog.Load("shaman","restoration"));
    }
}
