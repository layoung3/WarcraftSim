using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests.Consolidated;

// Independent source oracle: pinned consumes.go applies one OR-gated 2% pair,
// attributes independently, and grilled squid adds a separate 1% pair.
public sealed class Cp175SharedElixirCriticalComponents
{
    private const string Attack="attack-critical-chance-percent",Spell="spell-critical-chance-percent";
    private static readonly string[] Elixirs=["elixir-of-the-mongoose","elixir-of-the-grizzly","elixir-of-the-owl"];
    private static ForeverExternalModifierSelection Select(params string[] keys)=>new(){Consumables=[..keys]};
    public static IEnumerable<object[]> Combinations()
    {
        for(var mask=1;mask<8;mask++)
        {
            var keys=Elixirs.Where((_,i)=>(mask&(1<<i))!=0).ToArray();
            foreach(var order in Permute(keys))foreach(var food in new[]{false,true})yield return [order,food];
        }
    }
    private static IEnumerable<string[]> Permute(string[] values)
    {
        if(values.Length==0){yield return [];yield break;}
        for(var i=0;i<values.Length;i++)foreach(var rest in Permute(values.Where((_,j)=>j!=i).ToArray()))yield return [values[i],..rest];
    }
    [Theory][MemberData(nameof(Combinations))]
    public void SharedCritIsOnePositiveComponentAndAttributesCoexist(string[] keys,bool food)
    {
        var selected=Select(food?[..keys,"grilled-squid"]:keys);
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(selected,60,60m);
        Assert.Equal(food?3m:2m,raw.GetValueOrDefault(Attack));
        Assert.Equal(food?3m:2m,raw.GetValueOrDefault(Spell));
        foreach(var (name,index) in new[]{("agility",0),("strength",1),("intellect",2)})
            Assert.Equal(keys.Contains(Elixirs[index])?25m:0m,raw.GetValueOrDefault(name));
        var original=new CharacterProfile {Level=60};var before=JsonSerializer.Serialize(original);
        var applied=ForeverExternalModifierCatalog.ApplyToProfile(original,selected,60m);
        var group=Assert.Single(applied.Buffs.Selections,x=>x.BuffKey=="cp173-shared-elixir-critical-chance");
        Assert.Equal(2m,group.Stats.Values[Attack]);Assert.Equal(2m,group.Stats.Values[Spell]);
        Assert.All(applied.Buffs.Selections.SelectMany(x=>x.Stats.Values.Values),v=>Assert.True(v>=0m));
        foreach(var stat in raw.Keys)Assert.Equal(raw[stat],applied.Buffs.Selections.Sum(x=>x.Stats.Values.GetValueOrDefault(stat)));
        Assert.Equal(before,JsonSerializer.Serialize(original));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.ApplyToProfile(applied,selected,60m));
    }
    [Fact] public void SquidAloneIsIndependentOnePercentInBothChannels()
    {
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(Select("grilled-squid"),60,60m);
        Assert.Equal(1m,raw.GetValueOrDefault(Attack));Assert.Equal(1m,raw.GetValueOrDefault(Spell));
    }
    [Fact] public void FerocitySharesStrengthFamilyNotAgility()
    {
        Assert.Equal(2,ForeverExternalModifierCatalog.Resolve(Select("elixir-of-ferocity",Elixirs[0]),60,60m).Count);
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(Select("elixir-of-ferocity",Elixirs[1]),60,60m));
    }
    [Theory][InlineData("elixir-of-the-grizzly",250351)][InlineData("elixir-of-the-owl",250337)]
    [InlineData("elixir-of-ferocity",250350)][InlineData("elixir-of-greater-intellect",9179)]
    public void HistoricalExactIdentityIsBound(string key,int id)=>Assert.Equal(id,ForeverExternalModifierCatalog.Find(key).ItemId);
    [Fact] public void SagesClassicIdentityIsNotTheDifferentHistoricalSages()
    {
        var definition=ForeverExternalModifierCatalog.Find("elixir-of-the-sages");
        Assert.Equal(13447,definition.ItemId);
        Assert.Equal(18m,definition.Stats["intellect"]);Assert.Equal(18m,definition.Stats["spirit"]);
        Assert.False(definition.Stats.ContainsKey(Attack));Assert.False(definition.Stats.ContainsKey(Spell));
    }
    [Theory][InlineData(" ELIXIR-OF-THE-MONGOOSE ")][InlineData(" CP173-ELIXIR-OF-THE-OWL ")]
    [InlineData(" cp173-shared-elixir-critical-chance ")]
    public void AuthoredEnabledAliasesCannotEvadeGroupGuard(string alias)
    {
        var p=new CharacterProfile {Level=60};p.Buffs.Selections.Add(new CharacterBuffSelection {BuffKey=alias,IsEnabled=true,Stats=new StatCollection {Values=new(){[Attack]=2m}}});
        var before=JsonSerializer.Serialize(p);
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.ApplyToProfile(p,Select(Elixirs[1]),60m));
        Assert.Equal(before,JsonSerializer.Serialize(p));
    }
    [Fact] public void LegacyWarriorMongooseAndSharedGrizzlyDoNotDoubleCrit()
    {
        var baseline=new WarriorSimRequest {Level=60,DurationSeconds=60m};
        baseline.ConsumableIds.Add(Elixirs[0]);
        var before=WarriorGearStatCalculator.Calculate(baseline).EffectiveStats;
        baseline.ExternalModifiers.Consumables.Add(Elixirs[1]);
        var after=WarriorGearStatCalculator.Calculate(baseline).EffectiveStats;
        Assert.Equal(25m,after.Get("strength")-before.Get("strength"));
        Assert.Equal(0m,after.Get(Attack)-before.Get(Attack));
        Assert.Equal(0m,after.Get(Spell)-before.Get(Spell));
    }
    [Fact] public void ReturnedContributionsAndIndependentBreakdownsDoNotMutateCatalog()
    {
        var selected=Select(Elixirs);
        var original=JsonSerializer.Serialize(ForeverExternalModifierCatalog.Entries);
        var profile=ForeverExternalModifierCatalog.ApplyToProfile(new CharacterProfile {Level=60},selected,60m);
        foreach(var row in profile.Buffs.Selections)foreach(var key in row.Stats.Values.Keys.ToArray())row.Stats.Values[key]=999m;
        foreach(var entry in ForeverExternalModifierCatalog.Resolve(selected,60,60m))
        {
            var detached=entry.IndependentStats;
            foreach(var key in detached.Keys.ToArray())detached[key]=999m;
        }
        Assert.Equal(original,JsonSerializer.Serialize(ForeverExternalModifierCatalog.Entries));
        var again=ForeverExternalModifierCatalog.SumSelfRawStats(selected,60,60m);
        Assert.Equal(2m,again[Attack]);Assert.Equal(2m,again[Spell]);Assert.Equal(25m,again["agility"]);
    }

    [Fact] public void DisabledSharedAliasIsReplacedWithoutMutatingCaller()
    {
        var source=new CharacterProfile {Level=60};
        source.Buffs.Selections.Add(new CharacterBuffSelection {BuffKey=" CP173-SHARED-ELIXIR-CRITICAL-CHANCE ",IsEnabled=false,Stats=new StatCollection {Values=new(){[Attack]=999m}}});
        var before=JsonSerializer.Serialize(source);
        var after=ForeverExternalModifierCatalog.ApplyToProfile(source,Select(Elixirs[0]),60m);
        Assert.Single(after.Buffs.Selections,x=>x.BuffKey.Trim().Equals("cp173-shared-elixir-critical-chance",StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before,JsonSerializer.Serialize(source));
        Assert.Equal(2m,after.Buffs.Selections.Where(x=>x.IsEnabled).Sum(x=>x.Stats.Values.GetValueOrDefault(Attack)));
    }
    [Fact] public void EmptySelectionAndUnchangedAgilityElixirAreNotRewritten()
    {
        var source=new CharacterProfile {Level=60};
        Assert.Same(source,ForeverExternalModifierCatalog.ApplyToProfile(source,Select(),60m));
        Assert.Empty(ForeverExternalModifierCatalog.SumSelfRawStats(Select(),60,60m));
        var selected=ForeverExternalModifierCatalog.SumSelfRawStats(Select("elixir-of-greater-agility"),60,60m);
        Assert.Equal(25m,selected["agility"]);Assert.False(selected.ContainsKey(Attack));Assert.False(selected.ContainsKey(Spell));
    }

}
