using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class CP175_ShamanClassWideMechanics
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EarthsGraspIncreasesRealStoneclawHealthAndAbsorbedEnemyDamage(int rank)
    {
        var r=Request(new(){["earths-grasp"]=rank});r.DurationSeconds=10m;
        r.CustomPriority=["stoneclaw-totem"];r.IncomingBossHitDamage=200m;r.IncomingBossSwingSeconds=1m;
        r.UtilityScenario=new(){TargetCanBeControlled=true};
        var run=Run(r);
        var redirected=Events(run,CombatEventType.Damage,"encounter:stoneclaw-intercept");
        Assert.Equal(480m*(1m+.25m*rank),redirected.Sum(x=>x.Amount??0m));
        Assert.True(run.Run.CustomMetrics["shaman-stoneclaw-intercepts"]>0m);
        Assert.Contains(Events(run,CombatEventType.Damage),x=>x.TargetActorKey=="shaman"&&x.Amount>0m);
    }

    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,true)]
    public void EarthsGraspRadiusControlsEarthboundRootAndActualMovement(int rank,bool inRange)
    {
        var ranks=Elemental();ranks["earthbound"]=1;ranks["earths-grasp"]=rank;
        var r=Request(ranks);r.DurationSeconds=4m;r.TargetDistanceYards=11.5m;r.CustomPriority=["earthbind-totem"];
        r.UtilityScenario=new(){TargetCanBeControlled=true,TargetMovesTowardPlayer=true,TargetSpeedYardsPerSecond=1m,StopDistanceYards=0m};
        var run=Run(r);
        Assert.Equal(inRange?1m:0m,run.Run.CustomMetrics["shaman-earthbound-roots"]);
        if(inRange)Assert.Equal(0m,run.Run.CustomMetrics["shaman-target-distance-travelled"]);
        else Assert.True(run.Run.CustomMetrics["shaman-target-distance-travelled"]>0m);
    }

    [Fact]
    public void BossControlImmunityDoesNotTurnEarthboundIntoAnUnimplementedMechanic()
    {
        var ranks=Elemental();ranks["earthbound"]=1;
        var r=Request(ranks);r.DurationSeconds=4m;r.TargetDistanceYards=8m;r.CustomPriority=["earthbind-totem"];
        r.UtilityScenario=new(){TargetCanBeControlled=false,TargetMovesTowardPlayer=true,TargetSpeedYardsPerSecond=1m,StopDistanceYards=0m};
        var run=Run(r);Assert.Equal(0m,run.Run.CustomMetrics["shaman-earthbound-roots"]);
        Assert.Equal(3.9m,run.Run.CustomMetrics["shaman-target-distance-travelled"]);
    }

    [Theory]
    [InlineData(0,3)] [InlineData(1,2)] [InlineData(2,0)]
    public void GhostWolfRankChangesActualCastAndTravelWithIndoorGates(int rank,int cast)
    {
        var ranks=Enhancement();ranks["improved-ghost-wolf"]=rank;
        var r=Request(ranks);r.DurationSeconds=8m;r.TargetDistanceYards=100m;r.CustomPriority=["ghost-wolf"];
        r.UtilityScenario=new(){PlayerMovesTowardTarget=true,PlayerSpeedYardsPerSecond=7m};
        var run=Run(r);var start=Assert.Single(Events(run,CombatEventType.AbilityCastStarted,"ghost-wolf"));
        var end=Assert.Single(Events(run,CombatEventType.AbilityCastCompleted,"ghost-wolf"));
        Assert.Equal((decimal)cast,end.TimeSeconds-start.TimeSeconds);
        Assert.InRange(run.Run.CustomMetrics["shaman-player-distance-travelled"],(7.8m-cast)*9.8m,(8m-cast)*9.8m);
        r.UtilityScenario.IsIndoors=true;
        if(rank==0)Assert.Throws<ArgumentException>(()=>Run(r));
        else Assert.Single(Events(Run(r),CombatEventType.AbilityCastCompleted,"ghost-wolf"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void GuardianTotemsChangesRealRangedMitigationAndGroundingCooldown(int rank)
    {
        var r=Request(new(){["thundering-strikes"]=5,["guardian-totems"]=rank});
        r.DurationSeconds=8m;r.CustomPriority=["windwall-totem"];
        r.IncomingBossHitDamage=1000m;r.UtilityScenario=new(){IncomingAttackKind="ranged"};
        var incoming=Events(Run(r),CombatEventType.Damage,"encounter:shaman-classwide-incoming");
        Assert.All(incoming,x=>Assert.Equal(1000m-64m*(1m+.1m*rank),x.RawAmount));
        Assert.Equal(15m-rank,Book(r).Single(x=>x.Key=="grounding-totem").CooldownSeconds);
        r.CustomPriority=["grounding-totem"];r.IncomingDamageSchool="fire";r.UtilityScenario.IncomingAttackKind="spell";
        var guarded=Run(r);Assert.Equal(1m,guarded.Run.CustomMetrics["shaman-grounded-spells"]);
        r.UtilityScenario.IncomingSpellsAreArea=true;
        var area=Run(r);Assert.Equal(0m,area.Run.CustomMetrics["shaman-grounded-spells"]);
        Assert.True(area.EndingHealth<guarded.EndingHealth);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ReincarnationActuallyRestoresRankHealthManaAndCooldown(int rank)
    {
        var r=Request(new(){["improved-healing-wave"]=5,["tidal-focus"]=5,["improved-reincarnation"]=rank,["water-shield"]=1});
        r.DurationSeconds=3m;r.IncomingBossHitDamage=1000000m;r.CustomPriority=["water-shield"];
        r.UtilityScenario=new(){UseReincarnation=true};
        var run=Run(r);
        Assert.Equal(1m,run.Run.CustomMetrics["shaman-reincarnations"]);
        Assert.Equal(run.MaximumHealth*(.2m+.1m*rank),run.EndingHealth);
        Assert.Equal(run.MaximumMana*(.2m+.1m*rank),run.EndingMana);
        Assert.Equal(2m+3600m-600m*rank,run.Run.CustomMetrics["shaman-reincarnation-ready-at"]);
        Assert.Single(Events(run,CombatEventType.ActorDied));
        Assert.Single(Events(run,CombatEventType.HealthChanged,"reincarnation"));
    }

    [Fact]
    public void TotemicCallRelocatesARealFireTotemIntoDamageRange()
    {
        var r=Request();r.DurationSeconds=6m;r.TargetDistanceYards=10m;r.CustomPriority=["magma-totem"];
        r.UtilityScenario=new(){PlayerMovesTowardTarget=true,PlayerSpeedYardsPerSecond=7m,StopDistanceYards=3m};
        var stationary=Run(r);Assert.Equal(0m,stationary.DamageByAbility.GetValueOrDefault("magma-totem"));
        r.CustomPriority=["magma-totem","totemic-call"];
        var moved=Run(r);Assert.True(moved.DamageByAbility.GetValueOrDefault("magma-totem")>0m);
        Assert.Equal(1m,moved.Run.CustomMetrics["shaman-relocated-totems"]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void FlametongueTotemIsRealMainHandFireDamageModifiedByCallOfFlame(int rank)
    {
        var ranks=new Dictionary<string,int>{["convection"]=5,["call-of-flame"]=rank};
        var r=Request(ranks);r.DurationSeconds=12m;r.CustomPriority=["flametongue-totem"];
        var run=Run(r);
        var procs=Events(run,CombatEventType.Damage,"flametongue-totem-proc");
        Assert.NotEmpty(procs);
        Assert.All(procs,x=>Assert.Equal(13.63m*2m*(1m+.05m*rank),x.RawAmount));
        Assert.True(run.Run.CustomMetrics["shaman-flametongue-totem-procs"]>0m);
        Assert.Contains(Events(run,CombatEventType.AuraApplied),x=>x.EffectKey=="fire-totem-slot");
    }

    [Fact]
    public void AllThreeShocksShareCooldownAndTalentCostComposition()
    {
        var ranks=Enhancement();ranks["convection"]=5;ranks["reverberation"]=5;
        var r=Request(ranks);r.DurationSeconds=20m;r.CustomPriority=["frost-shock","earth-shock","flame-shock"];
        var frost=Book(r).Single(x=>x.Key=="frost-shock");
        Assert.Equal(430m*.9m*.55m,frost.ResourceCosts.Single().Amount);Assert.Equal(5m,frost.CooldownSeconds);
        var run=Run(r);Assert.Equal(4,Events(run,CombatEventType.AbilityCastCompleted,"frost-shock").Length);
        Assert.Empty(Events(run,CombatEventType.AbilityCastCompleted,"earth-shock"));
        Assert.Empty(Events(run,CombatEventType.AbilityCastCompleted,"flame-shock"));
    }

    [Fact]
    public void CurrentChainLightningUsesRealThirtyPercentJumpFalloff()
    {
        var r=Request();r.DurationSeconds=4m;r.ClassWideTargetCount=3;r.CustomPriority=["chain-lightning"];
        var hits=Events(Run(r),CombatEventType.Damage,"chain-lightning");
        Assert.Equal(3,hits.Length);
        var expected=510m+100m*.571m;
        Assert.Equal(expected,hits.Single(x=>x.TargetActorKey=="target").Amount);
        Assert.Equal(expected*.7m,hits.Single(x=>x.TargetActorKey=="target-2").Amount);
        Assert.Equal(expected*.49m,hits.Single(x=>x.TargetActorKey=="target-3").Amount);
    }
}
