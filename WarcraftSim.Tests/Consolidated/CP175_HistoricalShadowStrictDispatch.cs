using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Priest;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HistoricalShadowStrictDispatchTests
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static IActionResult Validate(object talents) =>
        new ForeverSoloTalentController().ValidateItemized("priest","shadow",
            JsonSerializer.SerializeToElement(talents,Json));
    private static ForeverLevel60SimulationRequest Request()
    {
        var shadow=new ShadowPriestSimRequest {
            DurationSeconds=20,UseProvisionalLevel60Baseline=true
        };
        shadow.Character.Race="human";
        // Keep the existing ranks unchanged; only replace legacy unnamed totals.
        shadow.Talents.DisciplinePointsSpent=0;
        shadow.Talents.HolyPointsSpent=0;
        shadow.Talents.ShadowPointsSpent=shadow.Talents.Ranks.Values.Sum();
        return new(){ClassKey="priest",Specialization="shadow",Shadow=shadow,
            ItemizedTalentMode=true,ItemizedTalentRanks=new(shadow.Talents.Ranks,StringComparer.Ordinal)};
    }

    [Fact]
    public void SixteenKeysHaveHistoricalMetadataWithoutPromisingDisciplineActions()
    {
        Assert.Equal(16,ForeverHistoricalCasterTalentCatalog.ForClass("priest").Count);
        Assert.True(ForeverHistoricalCasterTalentCatalog.Supports("priest","shadow"));
        Assert.False(ForeverHistoricalCasterTalentCatalog.Supports("priest","holy"));
        var pain=ForeverHistoricalCasterTalentCatalog.Find("priest","improved-shadow-word-pain")!;
        Assert.Equal(("shadow",2,2,3),(pain.Tree,pain.MaxRank,pain.Row,pain.Column));
        var form=ForeverHistoricalCasterTalentCatalog.Find("priest","shadowform")!;
        Assert.Equal(("vampiric-embrace",1),(form.PrerequisiteKey,form.PrerequisiteRank));
        var catalog=Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Catalog("priest","shadow"));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(catalog.Value,Json));
        var penance=json.RootElement.GetProperty("talents").EnumerateArray()
            .Single(x=>x.GetProperty("key").GetString()=="penance");
        Assert.Equal("unreachable-in-restricted-shadow-allocation",
            penance.GetProperty("simulationStatus").GetString());
        Assert.Contains("not advertised",penance.GetProperty("knownLimitations").GetString());
        Assert.Throws<ArgumentException>(()=>ForeverItemizedPresetCatalog.Load("priest","shadow"));
    }

    [Fact]
    public async Task ExistingThirtySixRanksRunWithHonestTotalsAndDeterministicGameplay()
    {
        var request=Request();
        Assert.Equal(36,request.Shadow!.Talents.Ranks.Values.Sum());
        Assert.Equal(36,request.Shadow.Talents.ShadowPointsSpent);
        var validation=Assert.IsType<OkObjectResult>(Validate(request.Shadow.Talents));
        using var validationJson=JsonDocument.Parse(JsonSerializer.Serialize(validation.Value,Json));
        Assert.Equal(36,validationJson.RootElement.GetProperty("pointsSpent").GetInt32());
        var before=JsonSerializer.Serialize(request,Json);
        var controller=new ForeverLevel60SimulationController();
        var first=Assert.IsType<OkObjectResult>(await controller.Simulate(request,default));
        var second=Assert.IsType<OkObjectResult>(await controller.Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Json));
        using var a=JsonDocument.Parse(JsonSerializer.Serialize(first.Value,Json));
        using var b=JsonDocument.Parse(JsonSerializer.Serialize(second.Value,Json));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,
            a.RootElement.GetProperty("talentSourceStatus").GetString());
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,
            a.RootElement.GetProperty("talentSourceCommit").GetString());
        Assert.True(a.RootElement.GetProperty("sim").GetProperty("damageDone").GetDecimal()>0);
        foreach(var key in new[]{"dps","damageDone","healingDone","endingMana","endingHealth","damageByAbility","priestDied"})
            Assert.Equal(a.RootElement.GetProperty("sim").GetProperty(key).GetRawText(),
                b.RootElement.GetProperty("sim").GetProperty(key).GetRawText());
    }

    [Fact]
    public void LegacyTotalsUnknownFillersAndMissingParentsAreRejected()
    {
        Assert.IsType<BadRequestObjectResult>(Validate(new ForeverPriestShadowTalentBuild()));
        var ranks=new Dictionary<string,int>(Request().Shadow!.Talents.Ranks);
        ranks["invented-discipline-filler"]=10;
        Assert.IsType<BadRequestObjectResult>(Validate(new{ranks}));
        foreach(var parent in new[]{"vampiric-embrace","mind-flay"})
        {
            ranks=new(Request().Shadow!.Talents.Ranks);
            ranks.Remove(parent);
            Assert.IsType<BadRequestObjectResult>(Validate(new{ranks}));
        }
    }

    [Theory]
    [InlineData("inner-focus",11)]
    [InlineData("penance",21)]
    [InlineData("power-infusion",31)]
    public void CrossTreeCooldownsCannotUseUnitemizedDisciplineFillers(string key,int total)
    {
        Assert.IsType<BadRequestObjectResult>(Validate(new{
            ranks=new Dictionary<string,int>{{key,1}},disciplinePointsSpent=total,
            holyPointsSpent=0,shadowPointsSpent=0}));
    }

    [Theory]
    [InlineData("upper")]
    [InlineData("whitespace")]
    [InlineData("duplicate-case")]
    public async Task JsonAliasesInEitherOrBothMapsFailWithoutMutatingCaller(string kind)
    {
        const string key="shadow-focus";
        var alias=kind=="whitespace"?" "+key+" ":key.ToUpperInvariant();
        foreach(var mode in new[]{(Typed:false,Full:true),(Typed:true,Full:false),(Typed:true,Full:true)})
        {
            var request=JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(
                JsonSerializer.Serialize(Request(),Json),Json)!;
            var maps=new List<Dictionary<string,int>>();
            if(mode.Typed)maps.Add(request.Shadow!.Talents.Ranks);
            if(mode.Full)maps.Add(request.ItemizedTalentRanks!);
            foreach(var ranks in maps){
                if(kind!="duplicate-case")ranks.Remove(key);
                ranks.Add(alias,5);
            }
            var before=JsonSerializer.Serialize(request,Json);
            Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(request,default));
            Assert.Equal(before,JsonSerializer.Serialize(request,Json));
        }
    }

    [Fact]
    public async Task IdentityBaselineRankMapsAndMalformedNestedInputsFailClosed()
    {
        var controller=new ForeverLevel60SimulationController();
        var mutations=new Action<ForeverLevel60SimulationRequest>[] {
            r=>r.Shadow!.Character=null!,
            r=>r.Shadow!.Character.ClassKey="mage",
            r=>r.Shadow!.Character.SpecializationKey="holy",
            r=>r.Shadow!.Character.Level=59,
            r=>r.Specialization="holy",
            r=>r.Shadow!.UseProvisionalLevel60Baseline=false,
            r=>r.Shadow!.Talents=null!,
            r=>r.Shadow!.Talents.Ranks=null!,
            r=>r.ItemizedTalentRanks=null,
            r=>r.ItemizedTalentRanks!["shadow-focus"]=4,
            r=>r.ItemizedTalentRanks!["inner-focus"]=1,
            r=>r.Shadow!.Talents.DisciplinePointsSpent=10,
            r=>r.Shadow!.Character.Equipment=null!,
            r=>r.Balance=new(),
        };
        foreach(var mutate in mutations){
            var request=Request();mutate(request);
            Assert.IsType<BadRequestObjectResult>(await controller.Simulate(request,default));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPreparedBalanceAndElementalRejectWrongCharacterClass(bool elemental)
    {
        var request=new ForeverLevel60SimulationRequest {ItemizedTalentMode=true};
        if(elemental){
            var typed=new ElementalShamanSimRequest {
                DurationSeconds=2,UseProvisionalLevel60Baseline=true,
                Talents=new ForeverElementalShamanTalents {
                    ElementalPointsSpent=5,EnhancementPointsSpent=0,RestorationPointsSpent=0,
                    Ranks=new(){["convection"]=5}}};
            typed.Character.ClassKey="mage";typed.Character.Race="tauren";
            request.ClassKey="shaman";request.Specialization="elemental";request.Elemental=typed;
            request.ItemizedTalentRanks=new(typed.Talents.Ranks);
        }else{
            var typed=new BalanceDruidSimRequest {
                DurationSeconds=2,UseProvisionalLevel60Baseline=true,
                Talents=new ForeverBalanceDruidTalents {
                    BalancePointsSpent=5,FeralPointsSpent=0,RestorationPointsSpent=0,
                    Ranks=new(){["improved-wrath"]=5}}};
            typed.Character.ClassKey="mage";typed.Character.Race="tauren";
            request.ClassKey="druid";request.Specialization="balance";request.Balance=typed;
            request.ItemizedTalentRanks=new(typed.Talents.Ranks);
        }
        Assert.IsType<BadRequestObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request,default));
    }
}
