using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class CP175_ShamanClassWideMechanics
{
    private static string StableTimeline(EnhancementShamanSimResponse run) => JsonSerializer.Serialize(
        run.Run.Timeline.Select(x=>new{x.TimeSeconds,x.Type,x.AbilityKey,x.EffectKey,x.SourceActorKey,
            x.TargetActorKey,x.Amount,x.RawAmount,x.ResultKey,x.IsCritical,x.IsPeriodic}));

    [Fact]
    public void PublishedTwoPieceAndBothEnchantChoicesReachRealCombatAndReverseExactly()
    {
        var r=Request(Enhancement());r.DurationSeconds=20m;r.TargetDistanceYards=30m;
        var naked=Run(r);
        r.Character.Equipment.Items=[new(){ItemId=19829,SlotKey="waist"},new(){ItemId=19830,SlotKey="wrist"}];
        var original=JsonSerializer.Serialize(r);var gear=Run(r);
        Assert.Equal(naked.MaximumMana+528m,gear.MaximumMana);
        Assert.Equal(naked.MaximumHealth+220m,gear.MaximumHealth);
        Assert.Equal(naked.AttackPower+35.2m,gear.AttackPower);
        Assert.Equal(naked.SpellPower+25m,gear.SpellPower);
        Assert.Equal(naked.EndingMana+528m+42m,gear.EndingMana);
        Assert.True(gear.DamageDone>naked.DamageDone);
        Assert.Equal(original,JsonSerializer.Serialize(r));
        Assert.Equal(StableTimeline(gear),StableTimeline(Run(r)));
        r.Character.Equipment.Items[1].EnchantSpellId=1220624;
        var power=Run(r);Assert.Equal(gear.SpellPower+16m,power.SpellPower);
        Assert.True(power.DamageDone>gear.DamageDone);Assert.Equal(gear.MaximumMana,power.MaximumMana);
        r.Character.Equipment.Items[1].EnchantSpellId=1248661;
        var intellect=Run(r);Assert.Equal(gear.MaximumMana+148.5m,intellect.MaximumMana);
        Assert.Equal(gear.AttackPower+9.9m,intellect.AttackPower);Assert.Equal(gear.SpellPower,intellect.SpellPower);
        r.Character.Equipment.Items.RemoveAt(0);
        Assert.Equal(4m,EnhancementShamanSimRunner.Preview(r).EffectiveStats.Get("mp5"));
        r.Character.Equipment.Items.Clear();Assert.Equal(StableTimeline(naked),StableTimeline(Run(r)));
    }

    [Fact]
    public void FaerieFireReadsLiveArmorAndRestoresItAtTheNaturalBoundary()
    {
        var r=Request();r.DurationSeconds=50m;r.TargetArmor=2000m;r.CustomPriority=["lightning-shield"];
        r.ExternalModifiers=new(){TargetDebuffs=["faerie-fire-rank4"]};
        var initial=Run(r);
        var hits=Events(initial,CombatEventType.Damage,"main-hand-auto").Where(x=>x.Amount>0m).ToArray();
        Assert.All(hits.Where(x=>x.TimeSeconds<40m),x=>Assert.Equal(x.RawAmount!.Value*5500m/(5500m+1495m),x.Amount!.Value,10));
        Assert.All(hits.Where(x=>x.TimeSeconds>40m),x=>Assert.Equal(x.RawAmount!.Value*5500m/(5500m+2000m),x.Amount!.Value,10));
        Assert.Contains(initial.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved&&x.AbilityKey=="faerie-fire-rank4"&&x.TimeSeconds==40m);
        r.ExternalModifiers.TargetDebuffMode="maintained";
        var maintained=Run(r);
        Assert.All(Events(maintained,CombatEventType.Damage,"main-hand-auto").Where(x=>x.TimeSeconds>40m&&x.Amount>0m),
            x=>Assert.Equal(x.RawAmount!.Value*5500m/(5500m+1495m),x.Amount!.Value,10));
        Assert.True(maintained.DamageDone>initial.DamageDone);
    }

    [Fact]
    public void ClearcastingTriggersFromCompletedMissesAndPaysOnlyForNonClearcastSpells()
    {
        var r=Request(Elemental());r.DurationSeconds=240m;r.TargetDistanceYards=30m;
        r.BaseSpellHitPercent=0m;
        var run=Run(r);var casts=Events(run,CombatEventType.AbilityCastCompleted,"lightning-bolt").Length;
        var consumed=run.Run.CustomMetrics["shaman-clearcasting-consumed"];
        Assert.Equal(0m,run.DamageDone);Assert.True(run.Run.CustomMetrics["shaman-clearcasting-procs"]>0m);
        Assert.True(consumed>0m);Assert.True(consumed<casts);
        var cost=Book(r).Single(x=>x.Key=="lightning-bolt").ResourceCosts.Single().Amount;
        Assert.Equal((casts-consumed)*cost,run.MaximumMana-run.EndingMana);
        Assert.Equal(casts-consumed,Events(run,CombatEventType.ResourceChanged,"lightning-bolt").Count(x=>x.Amount<0m));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void OverloadPartialRanksExecuteHalfDamageWithoutThreatOrRecursiveCasts(int rank)
    {
        var ranks=Elemental();ranks["lightning-overload"]=rank;
        var r=Request(ranks);r.DurationSeconds=240m;r.TargetDistanceYards=30m;
        var run=Run(r);var procs=Events(run,CombatEventType.Damage,"lightning-overload-bolt");
        Assert.NotEmpty(procs);Assert.Equal(procs.Length,run.Run.CustomMetrics["shaman-lightning-overload-procs"]);
        Assert.True(procs.Length<Events(run,CombatEventType.AbilityCastCompleted,"lightning-bolt").Length);
        Assert.All(procs,x=>Assert.Equal((353m+100m*.714m)*1.05m*.5m*(x.IsCritical?1.5m:1m),x.RawAmount));
        Assert.Empty(Events(run,CombatEventType.ThreatChanged,"lightning-overload-bolt"));
        Assert.Empty(Events(run,CombatEventType.AbilityCastStarted,"lightning-overload-bolt"));
        Assert.Empty(Events(run,CombatEventType.ResourceChanged,"lightning-overload-bolt"));
    }

    [Theory]
    [InlineData(0,0)] [InlineData(1,3)] [InlineData(2,7)] [InlineData(3,10)]
    public void WardingUsesAllRankValuesOnlyForSupportedIncomingSchools(int rank,int percent)
    {
        var ranks=new Dictionary<string,int>{["convection"]=5,["concussion"]=5,["elemental-warding"]=rank};
        var r=Request(ranks);r.DurationSeconds=5m;r.CustomPriority=["lightning-shield"];
        r.IncomingBossHitDamage=1000m;r.UtilityScenario=new(){IncomingAttackKind="spell"};
        foreach(var school in new[]{"fire","frost","nature","shadow"})
        {
            r.IncomingDamageSchool=school;
            Assert.All(Events(Run(r),CombatEventType.Damage,"encounter:shaman-classwide-incoming"),
                x=>Assert.Equal(school=="shadow"?1000m:1000m*(1m-percent/100m),x.Amount));
        }
    }

    [Theory]
    [InlineData("eye-of-the-storm","lightning-bolt")]
    [InlineData("healing-focus","healing-wave")]
    public void PushbackTalentsResistRealInterruptedCastTimeWithoutGrantingImmunity(string talent,string spell)
    {
        var ranks=talent=="eye-of-the-storm"?Elemental():Restoration();ranks[talent]=3;
        var r=Request(ranks);r.DurationSeconds=100m;r.CustomPriority=[spell];
        r.IncomingBossHitDamage=100m;r.IncomingBossSwingSeconds=.4m;r.IncomingDamageSchool="shadow";
        var talented=Run(r);
        ranks[talent]=0;SetRanks(r,ranks);var zero=Run(r);
        Assert.True(talented.Run.CustomMetrics["shaman-pushback-resisted"]>0m);
        Assert.Equal(0m,zero.Run.CustomMetrics["shaman-pushback-resisted"]);
        Assert.True(talented.Run.CustomMetrics["shaman-pushback-seconds"]>0m);
        Assert.True(talented.Run.CustomMetrics["shaman-pushback-seconds"]<zero.Run.CustomMetrics["shaman-pushback-seconds"]);
        Assert.True(Events(talented,CombatEventType.AbilityCastCompleted,spell).Length>
            Events(zero,CombatEventType.AbilityCastCompleted,spell).Length);
    }

    [Fact]
    public void ActualIncomingAvoidanceTriggersImprovedStormstrikeResetAndSpiritRegeneration()
    {
        var ranks=Enhancement();ranks["anticipation"]=3;ranks["spirit-weapons"]=1;ranks["improved-stormstrike"]=2;
        var r=Request(ranks);r.DurationSeconds=100m;r.CustomPriority=["stormstrike"];
        r.IncomingBossHitDamage=1m;r.IncomingBossSwingSeconds=.25m;r.Tuning.ManaRegenPerSecond=50m;
        var run=Run(r);
        Assert.Contains(Events(run,CombatEventType.Damage,"encounter:shaman-classwide-incoming"),x=>x.ResultKey=="dodge");
        Assert.Contains(Events(run,CombatEventType.Damage,"encounter:shaman-classwide-incoming"),x=>x.ResultKey=="parry");
        Assert.True(run.ImprovedStormstrikeCooldownResets>0);Assert.True(run.ImprovedStormstrikeRegenProcs>0);
        var times=Events(run,CombatEventType.AbilityCastCompleted,"stormstrike").Select(x=>x.TimeSeconds).ToArray();
        Assert.Contains(times.Zip(times.Skip(1),(a,b)=>b-a),x=>x<8m);
        ranks["improved-stormstrike"]=0;SetRanks(r,ranks);var zero=Run(r);
        Assert.Equal(0,zero.ImprovedStormstrikeCooldownResets);Assert.Equal(0,zero.ImprovedStormstrikeRegenProcs);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void HealingOnlyTidalMasteryDoesNotChangeLightningGameplay(int rank)
    {
        var ranks=Restoration();ranks["tidal-mastery"]=rank;
        var r=Request(ranks);r.DurationSeconds=120m;r.TargetDistanceYards=30m;
        var talented=Run(r);ranks["tidal-mastery"]=0;SetRanks(r,ranks);var zero=Run(r);
        Assert.Equal(StableTimeline(zero),StableTimeline(talented));
    }

    [Fact]
    public void CaseInsensitiveRanksStillEnforceTierAndParentLegality()
    {
        Assert.Throws<ArgumentException>(()=>ForeverShamanClassTalents.Validate(new Dictionary<string,int>
            {["LIGHTNING-OVERLOAD"]=1},1,0,0));
        Assert.Throws<ArgumentException>(()=>ForeverShamanClassTalents.Validate(new Dictionary<string,int>
            {["convection"]=5,["CONVECTION"]=5},10,0,0));
    }

    [Fact]
    public void LiveMovementRespectsShockAndSearingRangesWithoutAffectingTalentLegality()
    {
        foreach(var key in new[]{"earth-shock","flame-shock","frost-shock","searing-totem"})
        {
            var r=Request();r.DurationSeconds=6m;r.TargetDistanceYards=21m;r.CustomPriority=[key];
            var far=Run(r);Assert.Equal(0m,far.DamageDone);
            if(key!="searing-totem")Assert.Empty(Events(far,CombatEventType.AbilityCastCompleted,key));
            r.TargetDistanceYards=20m;var near=Run(r);
            Assert.True(near.DamageByAbility.GetValueOrDefault(key)>0m);
        }
    }

    [Fact]
    public void FarseerAndFlurryMultiplyWeaponSpeedThenExpireWithoutAcceleratingSpells()
    {
        var ranks=Enhancement();ranks["mental-quickness"]=2;ranks["improved-stormstrike"]=2;
        ranks["rage-of-the-farseer"]=1;
        var r=Request(ranks);r.DurationSeconds=34m;r.CriticalChancePercent=100m;
        r.CustomPriority=["rage-of-the-farseer","stormstrike"];
        var run=Run(r);var swings=Events(run,CombatEventType.Damage,"main-hand-auto").Select(x=>x.TimeSeconds).ToArray();
        var intervals=swings.Zip(swings.Skip(1),(a,b)=>new{At=b,Duration=b-a}).ToArray();
        Assert.Contains(intervals,x=>x.At is >2m and <24m&&Math.Abs(x.Duration-2m/1.69m)<.000000001m);
        Assert.Contains(intervals,x=>x.At>28m&&Math.Abs(x.Duration-2m/1.3m)<.000000001m);
        r.TargetDistanceYards=30m;r.CustomPriority=["rage-of-the-farseer","lightning-bolt"];
        run=Run(r);
        var starts=Events(run,CombatEventType.AbilityCastStarted,"lightning-bolt");
        var ends=Events(run,CombatEventType.AbilityCastCompleted,"lightning-bolt");
        Assert.All(starts.Zip(ends,(a,b)=>b.TimeSeconds-a.TimeSeconds),x=>Assert.Equal(2.5m,x));
    }
}
