using System.Text.Json;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Allocation/source-contract regressions for the November rework.
/// Runtime event behavior is covered independently by Paladin mechanics tests.</summary>
public sealed class Cp175PaladinSources
{
    [Fact]
    public void CurrentTopologyIncludesEveryTreeAndDistinctStableIdentities()
    {
        var nodes=ForeverPaladinClassTalents.Entries;
        Assert.Equal(51,nodes.Count);
        Assert.Equal(51,nodes.Select(x=>x.Key).Distinct().Count());
        Assert.Equal(51,nodes.Select(x=>x.TalentId).Distinct().Count());
        Assert.Equal(51,nodes.Select(x=>x.SpellId).Distinct().Count());
        foreach(var tree in new[]{"holy","protection","retribution"})
        {
            var members=nodes.Where(x=>x.Tree==tree).ToArray();
            Assert.Equal(17,members.Length);
            Assert.Equal(7,members.Max(x=>x.Row));
            Assert.Equal(members.Length,members.Select(x=>(x.Row,x.Column)).Distinct().Count());
        }
        Assert.All(nodes,n=>{
            Assert.NotEmpty(n.EffectComponents);
            Assert.Contains("provisional",n.SourceConfidence);
            Assert.True(n.SpellId>0);
            if(n.ParentKey is not null)
            {
                var parent=Assert.IsType<PaladinClassTalentDefinition>(ForeverPaladinClassTalents.Find(n.ParentKey));
                Assert.Equal(parent.Tree,n.Tree);
                // Boundless Light's arrow points to Holy Shock in the same row.
                // Row gating and explicit parent requirements are independent.
                Assert.True(parent.Row<=n.Row);
                Assert.InRange(n.ParentRank,1,parent.MaxRank);
            }
        });
    }

    [Fact]
    public void ReworkPrerequisitesDoNotRestoreSupersededTalentIdentity()
    {
        Assert.Null(ForeverPaladinClassTalents.Find("twist-of-light"));
        Assert.Null(ForeverPaladinClassTalents.Find("unsealed-fury"));
        var wrath=ForeverPaladinClassTalents.Find("unsealed-wrath")!;
        Assert.Equal((105692,1310735,"repentance",1),
            (wrath.TalentId,wrath.SpellId,wrath.ParentKey,wrath.ParentRank));
        Assert.Equal("reverence",ForeverPaladinClassTalents.Find("illumination")!.ParentKey);
        Assert.Equal("holy-shock",ForeverPaladinClassTalents.Find("lights-vigil")!.ParentKey);
        var boundless=ForeverPaladinClassTalents.Find("boundless-light")!;
        Assert.Equal((5,"holy-shock",1),(boundless.Row,boundless.ParentKey,boundless.ParentRank));
        Assert.Equal("holy-shield",ForeverPaladinClassTalents.Find("shieldsworn")!.ParentKey);
        Assert.Equal("seal-of-command",ForeverPaladinClassTalents.Find("vengeance")!.ParentKey);
        Assert.Equal(2,ForeverPaladinClassTalents.Find("shield-specialization")!.MaxRank);
        Assert.Equal(3,ForeverPaladinClassTalents.Find("conviction")!.MaxRank);
        Assert.Equal(5,ForeverPaladinClassTalents.Find("champion-of-the-light")!.MaxRank);
    }

    [Theory]
    [InlineData("holy",31,5,8)]
    [InlineData("protection",12,31,8)]
    [InlineData("retribution",10,10,31)]
    public void ReviewedExamplesAreLegalCrossTreeCopies(string tree,int holy,int protection,int retribution)
    {
        var first=ForeverPaladinClassTalents.Starter(tree);
        ForeverPaladinClassTalents.Validate(first);
        int total(string t)=>ForeverPaladinClassTalents.Entries.Where(n=>n.Tree==t).Sum(n=>first.GetValueOrDefault(n.Key));
        Assert.Equal((holy,protection,retribution),(total("holy"),total("protection"),total("retribution")));
        var copy=ForeverPaladinClassTalents.FromRanks(first);
        var key=first.Keys.First(); first[key]=0;
        Assert.NotEqual(first[key],copy.Rank(key));
        Assert.NotEqual(first[key],ForeverPaladinClassTalents.Starter(tree)[key]);
    }

    [Fact]
    public void LegalNonpresetPartialRanksAndUnspentPointsSurviveJson()
    {
        var ranks=new Dictionary<string,int> {
            ["divine-strength"]=3,["spiritual-focus"]=2,["improved-seals"]=2,
            ["divine-intellect"]=3,["benediction"]=2,["deflection"]=0};
        var build=ForeverPaladinClassTalents.FromRanks(ranks);
        Assert.Equal(12,build.Ranks.Values.Sum());
        var roundtrip=JsonSerializer.Deserialize<ForeverPaladinTalents>(JsonSerializer.Serialize(build))!;
        roundtrip.Validate();
        foreach(var (key,rank) in ranks)Assert.Equal(rank,roundtrip.Rank(key));
        ForeverPaladinClassTalents.Validate(new Dictionary<string,int>());
        for(var rank=0;rank<=5;rank++)
            ForeverPaladinClassTalents.Validate(new Dictionary<string,int>{{"divine-strength",rank}});
    }

    [Fact]
    public void JsonKeyCasingCannotChangeLegalTalentExecution()
    {
        var build=JsonSerializer.Deserialize<ForeverPaladinTalents>(
            "{\"Ranks\":{\"DIVINE-STRENGTH\":3,\"Benediction\":2}}")!;
        build.Validate();
        Assert.Equal(3,build.Rank("divine-strength"));
        Assert.Equal(2,build.Rank("benediction"));
        Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(
            new Dictionary<string,int>{{"divine-strength",1},{"DIVINE-STRENGTH",1}}));
    }

    [Fact]
    public void UnknownNegativeOvercapAndLevelOverspendingFailIndependently()
    {
        foreach(var ranks in new[]{
            new Dictionary<string,int>{{"invented",1}},
            new Dictionary<string,int>{{"divine-strength",-1}},
            new Dictionary<string,int>{{"conviction",4}}})
            Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(ranks));
        ForeverPaladinClassTalents.Validate(new Dictionary<string,int>{{"divine-strength",1}},10);
        Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(
            new Dictionary<string,int>{{"divine-strength",2}},10));
        var tooMany=ForeverPaladinClassTalents.Starter("retribution");
        tooMany["champion-of-the-light"]++;
        Assert.Contains("budget",Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(tooMany)).Message);
    }

    [Fact]
    public void TierPointsMustBeInEarlierRowsAndExplicitParentsCannotBeRemoved()
    {
        Assert.Contains("earlier",Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(
            new Dictionary<string,int>{{"divine-strength",4},{"improved-seals",5}})).Message);
        // Ten earlier points keep the row gate open; missing Reverence must still fail.
        var illumination=new Dictionary<string,int> {
            ["divine-strength"]=5,["improved-seals"]=5,["illumination"]=1};
        Assert.Contains("requires reverence",Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(illumination)).Message);
        var ret=ForeverPaladinClassTalents.Starter("retribution");
        ret.Remove("repentance"); ret["turn-evil"]=1;
        Assert.Contains("requires repentance",Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(ret)).Message);
        var protection=ForeverPaladinClassTalents.Starter("protection");
        protection["redoubt"]=4; protection["divine-intellect"]=5; protection["guardians-favor"]=1;
        Assert.Contains("requires redoubt",Assert.Throws<ArgumentException>(()=>ForeverPaladinClassTalents.Validate(protection)).Message);
    }
}
