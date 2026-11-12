using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class CP175_ShamanClassWideMechanics
{
    private static Dictionary<string,int> Elemental() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"]=5,["concussion"]=5,["elemental-alacrity"]=3,["call-of-flame"]=3,
        ["elemental-focus"]=1,["elemental-warding"]=3,["call-of-thunder"]=1
    };
    private static Dictionary<string,int> Enhancement() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["thundering-strikes"]=5,["ancestral-knowledge"]=5,["mental-dexterity"]=3,
        ["improved-lightning-shield"]=3,["elemental-weapons"]=3,["shamanistic-focus"]=1,
        ["stormstrike"]=1,["flurry"]=5,["maelstrom-weapon"]=1
    };
    private static Dictionary<string,int> Restoration() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=3,["natural-grace"]=3,
        ["water-shield"]=1,["restorative-totems"]=2,["mana-tide-totem"]=1,["nature-swiftness"]=1
    };
    private static Dictionary<string,int> DeepRestoration() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=3,["totemic-focus"]=2,
        ["ancestral-healing"]=3,["water-shield"]=1,["healing-focus"]=1,
        ["healing-way"]=3,["nature-swiftness"]=1,["restorative-totems"]=1,
        ["purification"]=5,["riptide"]=1
    };
    private static EnhancementShamanSimRequest Request(Dictionary<string,int>? ranks=null)
    {
        var r=new EnhancementShamanSimRequest
        {
            UseClassWideTalents=true,ClassWideRotationPolicy="custom",CustomPriority=["lightning-bolt"],
            DurationSeconds=40m,Seed=175760,CaptureTimeline=true,UseManualCombatAssumptions=true,
            BaseMeleeHitPercent=100m,BaseSpellHitPercent=100m,CriticalChancePercent=0m,
            TargetMaximumHealth=100000000m,TargetArmor=0m,MaximumHealth=100000m,
            MainHandImbue="none",UseWindfuryWeapon=false,UseWindfuryTotem=false,
            WeaponMinDamage=10m,WeaponMaxDamage=10m,WeaponSpeedSeconds=2m,
            Tuning=new(){ManaRegenPerSecond=0m},
            Character=new CharacterProfile
            {
                Name="Class-wide test",ClassKey="shaman",SpecializationKey="enhancement",Level=60,
                RulesetKey="wow-forever",BaseStats=new StatCollection{Values=new()
                { ["intellect"]=2000m,["stamina"]=100m,["armor"]=1000m,["spell-power"]=100m}}
            }
        };
        SetRanks(r,ranks??new(StringComparer.OrdinalIgnoreCase));
        return r;
    }
    private static void SetRanks(EnhancementShamanSimRequest r,Dictionary<string,int> ranks)
    {
        int total(string tree)=>ranks.Where(x=>ForeverShamanClassTalents.Find(x.Key)?.Tree==tree).Sum(x=>x.Value);
        r.Talents=new(){Ranks=new(ranks,StringComparer.OrdinalIgnoreCase),ElementalPointsSpent=total("elemental"),
            EnhancementPointsSpent=total("enhancement"),RestorationPointsSpent=total("restoration")};
    }
    private static Dictionary<string,int> Merge(Dictionary<string,int> first,Dictionary<string,int> second)=>
        first.Concat(second).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
    private static EnhancementShamanSimResponse Run(EnhancementShamanSimRequest r)=>new EnhancementShamanSimRunner().Run(r);
    private static CombatEvent[] Events(EnhancementShamanSimResponse r,CombatEventType type,string? ability=null)=>
        r.Run.Timeline.Where(x=>x.Type==type&&(ability is null||x.AbilityKey==ability)).ToArray();
    private static IReadOnlyList<AbilityDefinition> Book(EnhancementShamanSimRequest r)=>
        ForeverShamanClassSpellbook.Create(r.Talents,r.Tuning,ForeverShamanClassSpellbook.ReviewedElementalTuning(),
            new(){HealingPower=0m},r.WeaponSpeedSeconds,r.WeaponMinDamage,r.WeaponMaxDamage,r.DualWield);

    [Fact]
    public void SourceCatalogHasAllTreesExactCapsAndExplicitMechanicalCoverage()
    {
        Assert.Equal(50,ForeverShamanClassTalents.Entries.Count);
        Assert.Equal(new[]{16,18,16},new[]{"elemental","enhancement","restoration"}
            .Select(tree=>ForeverShamanClassTalents.Entries.Count(x=>x.Tree==tree)));
        Assert.Equal(50,ForeverShamanClassCoverage.Entries.Count);
        Assert.All(ForeverShamanClassTalents.Entries,x=>
        {
            Assert.NotEmpty(x.SourceUrl);Assert.NotEmpty(x.SourceConfidence);
            Assert.Contains(x.Coverage.Status,new[]{"implemented","partial","unimplemented"});
            Assert.NotEmpty(x.Coverage.ApplicableScenarios);
            if(x.Coverage.Status!="implemented")Assert.NotEmpty(x.Coverage.MissingEffects);
        });
        Assert.Equal(4,ForeverShamanClassTalents.Find("improved-ghost-wolf")!.Column);
        Assert.Null(ForeverShamanClassTalents.Find("lava-burst"));
    }

    [Fact]
    public void LegalPartialOffTreeWitnessesRetainUnspentPointsAndRealPrerequisites()
    {
        foreach(var ranks in new[]{Merge(Elemental(),Restoration()),Merge(Enhancement(),Restoration()),DeepRestoration()})
        {
            var r=Request(ranks);var preview=EnhancementShamanSimRunner.Preview(r);
            Assert.True(preview.ClassWide);Assert.True(ranks.Values.Sum()<51);
        }
        var bad=Elemental();bad["lightning-overload"]=1;bad["elemental-alacrity"]=2;
        Assert.Throws<ArgumentException>(()=>EnhancementShamanSimRunner.Preview(Request(bad)));
        bad=DeepRestoration();bad["healing-way"]=2;
        Assert.Throws<ArgumentException>(()=>EnhancementShamanSimRunner.Preview(Request(bad)));
        var retired=Request();retired.Talents.Ranks["lava-burst"]=1;
        Assert.Throws<ArgumentException>(()=>EnhancementShamanSimRunner.Preview(retired));
    }

    [Theory]
    [InlineData(0,0)] [InlineData(1,17)] [InlineData(2,33)] [InlineData(3,50)]
    public void MindfulnessUsesSourcedNonlinearRankCurveAndActualMana(int rank,int fraction)
    {
        var ranks=new Dictionary<string,int>{["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=rank};
        var r=Request(ranks);r.Tuning.ManaRegenPerSecond=100m;r.DurationSeconds=30m;
        var withTalent=Run(r);
        ranks["mindfulness"]=0;SetRanks(r,ranks);var zero=Run(r);
        Assert.Equal(fraction/100m,ForeverShamanClassTalents.MindfulnessFraction(rank));
        Assert.Equal(withTalent.DamageDone,zero.DamageDone);
        Assert.Equal(100m*(30m-2.5m)*fraction/100m,withTalent.EndingMana-zero.EndingMana,6);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TrialRunsAllMagmaTargetsAndExtendsRealLifetime(int rank)
    {
        var ranks=Elemental();ranks["trial-by-fire"]=rank;
        var r=Request(ranks);r.DurationSeconds=39m;r.ClassWideTargetCount=3;
        r.CustomPriority=["magma-totem","fire-nova"];var book=Book(r);
        var magma=book.Single(x=>x.Key=="magma-totem");
        Assert.Equal(20m+10m*rank,magma.Effects.Single().DurationSeconds);
        var run=Run(r);
        var casts=Events(run,CombatEventType.AbilityCastCompleted,"magma-totem");
        Assert.Equal(rank==2?1:2,casts.Length);
        var ticks=Events(run,CombatEventType.Damage,"magma-totem").Where(x=>x.Amount>0m).ToArray();
        Assert.Equal(3,ticks.Select(x=>x.TargetActorKey).Distinct().Count());
        Assert.Contains(ticks,x=>x.TimeSeconds>=20m);
        Assert.NotEmpty(Events(run,CombatEventType.Damage,"fire-nova"));
        Assert.Equal(10m-2m*rank,book.Single(x=>x.Key=="fire-nova").CooldownSeconds);
    }

    [Fact]
    public void RestorationCrossTreeWitnessExecutesHealingWaterShieldAndArmor()
    {
        var ranks=DeepRestoration();var r=Request(ranks);
        r.CriticalChancePercent=100m;r.IncomingBossHitDamage=1800m;r.IncomingBossSwingSeconds=2m;
        r.CustomPriority=["water-shield","healing-stream-totem","riptide","healing-wave"];
        var run=Run(r);
        Assert.Contains(Events(run,CombatEventType.Healing,"healing-wave"),x=>x.Amount>0m);
        Assert.DoesNotContain(Events(run,CombatEventType.Healing,"riptide"),x=>x.EffectKey=="riptide-initial");
        Assert.Contains(Events(run,CombatEventType.Healing,"riptide"),x=>x.IsPeriodic);
        Assert.Contains(Events(run,CombatEventType.AuraApplied,"ancestral-healing"),x=>x.TargetActorKey=="shaman");
        Assert.True(run.Run.CustomMetrics["shaman-water-shield-mana"]>0m);
        Assert.True(run.Run.CustomMetrics["shaman-healing-stream-pulses"]>0m);
        var incoming=Events(run,CombatEventType.Damage,"encounter:shaman-classwide-incoming").Where(x=>x.Amount>0m).ToArray();
        Assert.True(incoming.Last().Amount<incoming.First().Amount);
    }

    [Fact]
    public void WaterShieldDoesNotConsumeChargesFromMeleeCriticals()
    {
        var r=Request(new(){["improved-healing-wave"]=5,["tidal-focus"]=5,["water-shield"]=1});
        r.CriticalChancePercent=100m;r.CustomPriority=["water-shield"];
        var run=Run(r);
        Assert.Single(Events(run,CombatEventType.AbilityCastCompleted,"water-shield"));
        Assert.Contains(Events(run,CombatEventType.Damage,"main-hand-auto"),x=>x.IsCritical);
        Assert.Equal(0m,run.Run.CustomMetrics["shaman-water-shield-mana"]);
    }

    [Fact]
    public void NatureSwiftnessAppliesToLightningThenIsConsumedOnce()
    {
        var r=Request(Merge(Elemental(),Restoration()));r.DurationSeconds=12m;
        r.CustomPriority=["nature-swiftness","lightning-bolt"];
        var run=Run(r);var starts=Events(run,CombatEventType.AbilityCastStarted,"lightning-bolt");
        var ends=Events(run,CombatEventType.AbilityCastCompleted,"lightning-bolt");
        Assert.Equal(starts[0].TimeSeconds,ends[0].TimeSeconds);
        Assert.Equal(2m,ends[1].TimeSeconds-starts[1].TimeSeconds);
        Assert.Equal(1m,run.Run.CustomMetrics["shaman-nature-swiftness-consumed"]);
    }

    [Fact]
    public void RiptideBoostsPrimaryChainHealWithoutAnInitialHeal()
    {
        var r=Request(DeepRestoration());r.DurationSeconds=12m;r.IncomingBossHitDamage=1000m;
        r.CustomPriority=["riptide","chain-heal"];
        var run=Run(r);var raw=Events(run,CombatEventType.Healing,"chain-heal").First().RawAmount;
        Assert.Equal((490m+100m*.714m)*1.1m*1.25m,raw);
        Assert.DoesNotContain(Events(run,CombatEventType.Healing,"riptide"),x=>x.EffectKey=="riptide-initial");
        Assert.Equal(12m,Book(r).Single(x=>x.Key=="riptide").CooldownSeconds);
    }

    [Theory]
    [InlineData(1,0.33)] [InlineData(2,0.66)] [InlineData(3,1.0)]
    public void CurrentMentalDexterityAndAncestralKnowledgeApplyOnce(int rank,double ratio)
    {
        var ranks=new Dictionary<string,int>{["ancestral-knowledge"]=5,["thundering-strikes"]=5,["mental-dexterity"]=rank};
        var r=Request(ranks);var before=JsonSerializer.Serialize(r);var preview=EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(2200m,preview.EffectiveStats.Get("intellect"));
        Assert.Equal(2200m*(decimal)ratio,preview.AttackPower);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(preview.AttackPower,EnhancementShamanSimRunner.Preview(r).AttackPower);
    }

    [Fact]
    public void EquivalentEffectiveBuildsIgnoreSpecializationOriginAndReplayExactly()
    {
        var r=Request(Merge(Elemental(),Restoration()));r.ClassWideRotationPolicy="automatic";
        var original=JsonSerializer.Serialize(r);var a=Run(r);
        r.Character.SpecializationKey="elemental";var b=Run(r);
        Assert.Equal(a.DamageDone,b.DamageDone);Assert.Equal(a.EndingMana,b.EndingMana);
        static object[] Stable(EnhancementShamanSimResponse run)=>run.Run.Timeline.Select(x=>
            (object)new{x.TimeSeconds,x.Type,x.AbilityKey,x.EffectKey,x.TargetActorKey,x.Amount,x.RawAmount,x.ResultKey}).ToArray();
        Assert.Equal(JsonSerializer.Serialize(Stable(a)),JsonSerializer.Serialize(Stable(b)));
        r.Character.SpecializationKey="enhancement";Assert.Equal(original,JsonSerializer.Serialize(r));
    }

    [Fact]
    public void ManualGearStatChangesAreReversibleAndNotDuplicated()
    {
        var r=Request(Enhancement());var baseline=EnhancementShamanSimRunner.Preview(r);
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="manual-shaman-chest",Stats=new StatCollection{Values=new(){["intellect"]=20m,["stamina"]=30m,["spell-power"]=50m}}});
        var geared=EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(22m,geared.EffectiveStats.Get("intellect")-baseline.EffectiveStats.Get("intellect"));
        Assert.Equal(22m,geared.AttackPower-baseline.AttackPower);
        Assert.Equal(50m,geared.SpellPower-baseline.SpellPower);
        Assert.Equal(330m,geared.MaximumMana-baseline.MaximumMana);
        Assert.Equal(300m,geared.MaximumHealth-baseline.MaximumHealth);
        r.Character.Equipment.Items.Clear();
        Assert.Equal(JsonSerializer.Serialize(baseline),JsonSerializer.Serialize(EnhancementShamanSimRunner.Preview(r)));
    }

    [Fact]
    public void CustomPriorityRejectsRemovedTalentActionsAndPassiveProcsWithoutMutatingRequest()
    {
        var r=Request();r.CustomPriority=["stormstrike"];var before=JsonSerializer.Serialize(r);
        Assert.Throws<ArgumentException>(()=>Run(r));Assert.Equal(before,JsonSerializer.Serialize(r));
        r.CustomPriority=["lightning-overload-bolt"];
        Assert.Throws<ArgumentException>(()=>Run(r));
    }

    [Fact]
    public void FirstRankMaelstromCanProduceFreeInstantFiveStackBolt()
    {
        var r=Request(Enhancement());r.DurationSeconds=240m;r.WeaponSpeedSeconds=8m;
        r.CustomPriority=["maelstrom-lightning-bolt","stormstrike"];
        var run=Run(r);
        Assert.True(run.MaelstromInstantBolts>0);Assert.Equal(5,run.MaxMaelstromStacks);
        Assert.DoesNotContain(Events(run,CombatEventType.ResourceChanged,"maelstrom-lightning-bolt"),x=>x.Amount<0m);
    }

    [Fact]
    public void WindfuryWeaponAndTotemBothExecuteUnderNovemberHotfix()
    {
        var r=Request();r.MainHandImbue="windfury";r.CustomPriority=["windfury-totem"];
        r.Tuning.WindfuryWeaponProcChancePercent=100m;r.WindfuryTotemProcChancePercent=100m;
        var run=Run(r);
        Assert.True(run.WindfuryWeaponProcs>0);Assert.True(run.WindfuryTotemProcs>0);
        Assert.Contains("windfury-weapon-extra",run.DamageByAbility.Keys);
        Assert.Contains("windfury-totem-extra",run.DamageByAbility.Keys);
    }
}
