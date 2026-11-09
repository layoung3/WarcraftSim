using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175CasterSnapshotComparers
{
    private static StatCollection Stats(StringComparer comparer) => new() {
        Values=new(comparer) { ["INTELLECT"]=123m }
    };

    private static CharacterProfile Profile(StringComparer comparer) => new() {
        BaseStats=Stats(comparer),
        Equipment=new() { Items=[new() { Stats=Stats(comparer),
            Modifiers=[new() { Stats=Stats(comparer) }] }] },
        Talents=new() { Selections=[new() { Stats=Stats(comparer) }] },
        Buffs=new() { Selections=[new() { Stats=Stats(comparer) }] }
    };

    private static StatCollection[] AllStats(CharacterProfile profile) => [
        profile.BaseStats,profile.Equipment.Items[0].Stats,
        profile.Equipment.Items[0].Modifiers[0].Stats,
        profile.Talents.Selections[0].Stats,profile.Buffs.Selections[0].Stats
    ];

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void SnapshotRetainsOriginalComparersAndIsolatesEveryProfileStatSource(bool warlock,bool ignoreCase)
    {
        var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var original=Profile(comparer);
        CharacterProfile copy;
        if(warlock)
        {
            var request=new WarlockSimRequest { Character=original };
            request.Talents.Ranks=new(comparer) { ["DEMONIC-EMBRACE"]=5 };
            var snapshot=CasterSimulationRequestSnapshot.Create(request);
            Assert.Same(comparer,snapshot.Talents.Ranks.Comparer);
            Assert.Equal(ignoreCase?5:0,snapshot.Talents.Rank("demonic-embrace"));
            snapshot.Talents.Ranks.Clear();
            Assert.Single(request.Talents.Ranks);
            copy=snapshot.Character;
        }
        else
        {
            var request=new MageSimRequest { Character=original };
            request.Talents.Ranks=new(comparer) { ["ARCANE-MIND"]=5 };
            var snapshot=CasterSimulationRequestSnapshot.Create(request);
            Assert.Same(comparer,snapshot.Talents.Ranks.Comparer);
            Assert.Equal(ignoreCase?5:0,snapshot.Talents.Rank("arcane-mind"));
            snapshot.Talents.Ranks.Clear();
            Assert.Single(request.Talents.Ranks);
            copy=snapshot.Character;
        }
        var sources=AllStats(original);
        var copies=AllStats(copy);
        for(var i=0;i<sources.Length;i++)
        {
            Assert.NotSame(sources[i],copies[i]);
            Assert.NotSame(sources[i].Values,copies[i].Values);
            Assert.Same(sources[i].Values.Comparer,copies[i].Values.Comparer);
            Assert.Equal(ignoreCase?123m:0m,copies[i].Get("intellect"));
            copies[i].Set("INTELLECT",321m);
            Assert.Equal(123m,sources[i].Get("INTELLECT"));
        }
        copy.Equipment.Items[0].Modifiers.Clear();
        copy.Talents.Selections.Clear();
        copy.Buffs.Selections.Clear();
        Assert.Single(original.Equipment.Items[0].Modifiers);
        Assert.Single(original.Talents.Selections);
        Assert.Single(original.Buffs.Selections);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothWarlockTargetMapsRetainComparerAndDeepCopyLists(bool ignoreCase)
    {
        var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var request=new WarlockSimRequest {
            AfflictionAdditionalTargetPriority=new(comparer) { ["TARGET-1"]=["corruption"] },
            DestructionAdditionalTargetPriority=new(comparer) { ["TARGET-1"]=["immolate"] }
        };
        var copy=CasterSimulationRequestSnapshot.Create(request);
        foreach(var (source,snapshot) in new[]{
            (request.AfflictionAdditionalTargetPriority,copy.AfflictionAdditionalTargetPriority),
            (request.DestructionAdditionalTargetPriority,copy.DestructionAdditionalTargetPriority) })
        {
            Assert.Same(source.Comparer,snapshot.Comparer);
            Assert.Equal(ignoreCase,snapshot.ContainsKey("target-1"));
            Assert.NotSame(source["TARGET-1"],snapshot["TARGET-1"]);
            snapshot["TARGET-1"].Clear();
            Assert.Single(source["TARGET-1"]);
        }
    }

    [Fact]
    public void CaseInsensitiveWarlockTalentStillGrantsExactHealthAndReplay()
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.DurationSeconds=6m;request.EnablePet=false;request.UseLifeTap=false;
        request.CustomPriority=["shadow-bolt"];
        request.Character.BaseStats.Set("stamina",100m);
        request.Talents=new ForeverWarlockTalentBuild { DemonologyPointsSpent=5,
            Ranks=new(StringComparer.OrdinalIgnoreCase) { ["DEMONIC-EMBRACE"]=5 } };
        var before=JsonSerializer.Serialize(request);
        var first=new WarlockSimRunner().Run(request);
        var replay=new WarlockSimRunner().Run(request);
        Assert.Equal(4650m,first.EndingHealth);
        Assert.Equal(first.EndingHealth,replay.EndingHealth);
        Assert.Equal(first.DamageDone,replay.DamageDone);
        Assert.Equal(before,JsonSerializer.Serialize(request));
    }

    [Fact]
    public void CaseInsensitiveMageRanksAndLegacyAliasMatchCanonicalCombatWithoutMutatingCaller()
    {
        var canonical=MageSimRequest.CreatePreset("fire");
        canonical.DurationSeconds=12m;canonical.CustomPriority=["fireball"];
        canonical.UseEvocation=false;canonical.UseCombustion=false;
        var expected=new MageSimRunner().Run(canonical);
        var request=CasterSimulationRequestSnapshot.Create(canonical);
        request.Talents.Ranks=request.Talents.Ranks.ToDictionary(
            x=>x.Key=="accelerant"?"IMPROVED-FIREBALL":x.Key.ToUpperInvariant(),
            x=>x.Value,StringComparer.OrdinalIgnoreCase);
        var before=JsonSerializer.Serialize(request);
        var result=new MageSimRunner().Run(request);
        Assert.Equal(expected.DamageDone,result.DamageDone);
        Assert.Equal(expected.EndingMana,result.EndingMana);
        Assert.Equal(before,JsonSerializer.Serialize(request));
        Assert.True(request.Talents.Ranks.ContainsKey("IMPROVED-FIREBALL"));
        Assert.False(request.Talents.Ranks.ContainsKey("accelerant"));
    }

    [Fact]
    public void UppercaseAfflictionOverrideExecutesOnTheChosenAdditionalTarget()
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.DurationSeconds=12m;request.AdditionalTargets=1;
        request.EnablePet=false;request.UseLifeTap=false;request.BaseSpellHitPercent=100m;
        request.CustomPriority=["shadow-bolt"];
        request.AfflictionAdditionalTargetPriority=new(StringComparer.OrdinalIgnoreCase) {
            ["TARGET-1"]=["corruption"]
        };
        var before=JsonSerializer.Serialize(request);
        var result=new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="corruption" && e.TargetActorKey=="target-1");
        Assert.Equal(before,JsonSerializer.Serialize(request));
    }

    [Fact]
    public void NullInputsAndInvalidRanksStillReachExistingValidation()
    {
        Assert.Throws<ArgumentNullException>(()=>new MageSimRunner().Run(null!));
        Assert.Throws<ArgumentNullException>(()=>new WarlockSimRunner().Run(null!));
        var mage=MageSimRequest.CreatePreset("fire");
        mage.Talents=null!;
        Assert.Null(CasterSimulationRequestSnapshot.Create(mage).Talents);
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(mage));
        mage=MageSimRequest.CreatePreset("fire");mage.Talents.Ranks=null!;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(mage));
        mage=MageSimRequest.CreatePreset("fire");mage.Talents.Ranks["ACCELERANT"]=6;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(mage));
        var warlock=WarlockSimRequest.CreatePreset("affliction");
        warlock.Character=null!;
        Assert.Null(CasterSimulationRequestSnapshot.Create(warlock).Character);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(warlock));
        warlock=WarlockSimRequest.CreatePreset("affliction");
        warlock.AfflictionAdditionalTargetPriority=null!;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(warlock));
        warlock.AfflictionAdditionalTargetPriority=new();
        warlock.DestructionAdditionalTargetPriority=null!;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(warlock));
    }

    [Fact]
    public void SnapshotDoesNotReplaceNullNestedStatsOrCollectionsWithDefaults()
    {
        var request=new MageSimRequest { Character=Profile(StringComparer.OrdinalIgnoreCase) };
        request.Character.BaseStats.Values=null!;
        request.Character.Equipment.Items[0].Stats=null!;
        request.Character.Equipment.Items[0].Modifiers=[null!,new() { Stats=null! }];
        request.Character.Talents.Selections=[null!];
        request.Character.Buffs.Selections=null!;
        var snapshot=CasterSimulationRequestSnapshot.Create(request);
        Assert.Null(snapshot.Character.BaseStats.Values);
        Assert.Null(snapshot.Character.Equipment.Items[0].Stats);
        Assert.Null(snapshot.Character.Equipment.Items[0].Modifiers[0]);
        Assert.Null(snapshot.Character.Equipment.Items[0].Modifiers[1].Stats);
        Assert.Null(snapshot.Character.Talents.Selections[0]);
        Assert.Null(snapshot.Character.Buffs.Selections);
        var warlock=new WarlockSimRequest {
            AfflictionAdditionalTargetPriority=new() { ["target-1"]=null! },
            DestructionAdditionalTargetPriority=new() { ["target-1"]=null! }
        };
        var copied=CasterSimulationRequestSnapshot.Create(warlock);
        Assert.Null(copied.AfflictionAdditionalTargetPriority["target-1"]);
        Assert.Null(copied.DestructionAdditionalTargetPriority["target-1"]);
    }
}
