using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage3HealingAndProcsTests
{
    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct,true)]
    [InlineData(CombatEffectDeliveryType.ChannelTick,true)]
    [InlineData(CombatEffectDeliveryType.Periodic,false)]
    public void DivineAegis_RespondsOnlyToNonPeriodicCriticalHealing(
        CombatEffectDeliveryType delivery,bool shouldShield)
    {
        var (ctx,priest,ally,absorbManager)=CreateContext();
        var aegis=new DivineAegisProcessor(priest.Key,.3m,absorbManager);
        ctx.ScheduleEvent(new CombatEvent{
            TimeSeconds=.25m,Type=CombatEventType.Healing,
            SourceActorKey=priest.Key,TargetActorKey=ally.Key,
            AbilityKey="heal",RawAmount=100m,Amount=40m,
            IsCritical=true,EffectDeliveryType=delivery
        });
        var run=new SimulationEngine([aegis,absorbManager]).Run(ctx);
        if(shouldShield)
        {
            var absorb=Assert.Single(ally.ActiveAbsorbs);
            Assert.Equal(30m,absorb.RemainingAmount);
            Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
                e.AbilityKey=="divine-aegis");
        }
        else
        {
            Assert.Empty(ally.ActiveAbsorbs);
            Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied);
        }
    }

    [Fact]
    public void DivineAegis_RejectsNoncriticalAndOtherCasterHealing()
    {
        var (ctx,priest,ally,absorbManager)=CreateContext();
        var other=new SimulationActorState{Key="other-healer",Name="Other",TeamKey="raid",Level=60};
        other.InitializeHealth(1000m);
        ctx.AddActor(other);
        var aegis=new DivineAegisProcessor(priest.Key,.3m,absorbManager);
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=.25m,Type=CombatEventType.Healing,
            SourceActorKey=priest.Key,TargetActorKey=ally.Key,
            RawAmount=100m,Amount=100m,IsCritical=false
        });
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=.5m,Type=CombatEventType.Healing,
            SourceActorKey=other.Key,TargetActorKey=ally.Key,
            RawAmount=100m,Amount=100m,IsCritical=true
        });
        new SimulationEngine([aegis,absorbManager]).Run(ctx);
        Assert.Empty(ally.ActiveAbsorbs);
    }

    [Fact]
    public void HotTicksAreNeverMisses_CritAndOverhealStayConsistent()
    {
        var (ctx,priest,ally,absorbs)=CreateContext(duration:6m,startingHealth:940m);
        var auras=new AuraManager();
        var hot=new AbilityDefinition{Key="renew-test",Name="Renew test",ClassKey="priest",
            Effects=[new AbilityEffectDefinition{
                Key="hot",EffectType=AbilityEffectTypes.PeriodicHealing,
                TargetType=AbilityTargetTypes.Friendly,
                CanMiss=true,CanCrit=true,
                MinimumValue=50m,MaximumValue=50m,
                AuraKey="renew-aura",DurationSeconds=4m,
                TickIntervalSeconds=2m,IncludeExpirationBoundaryTick=true
            }]
        };
        priest.AddAbility(hot);
        var rolls=new SimpleCombatRollResolver(hitChancePercent:0m,
            criticalChancePercent:100m);
        var executor=new AbilityExecutor(auras,rolls,
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="hot-audit",Version="1"
            }));
        var run=new SimulationEngine([executor,auras]).Run(ctx,start=>{
            auras.ApplyAura(start,ally,new AuraDefinition{
                Key="healing-reduction",Name="Healing reduction",
                DurationSeconds=3m,HealingReceivedMultiplier=.5m
            },priest.Key,"healing-reduction","debuff");
            Assert.True(executor.TryStartAbility(start,priest.Key,ally.Key,hot.Key).Success);
        });
        var ticks=run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic &&
            e.AbilityKey=="renew-test").ToArray();
        Assert.Equal(2,ticks.Length);
        Assert.All(ticks,e=>{Assert.True(e.IsCritical);Assert.NotEqual(CombatResultTypes.Miss,e.ResultKey);});
        Assert.Equal(50m,ticks[0].RawAmount); // crit100 * .5 healing debuff
        Assert.Equal(50m,ticks[0].Amount);
        Assert.Equal(0m,ticks[0].OverhealingAmount);
        Assert.Equal(100m,ticks[1].RawAmount); // healing debuff expired at 3
        Assert.Equal(10m,ticks[1].Amount);
        Assert.Equal(90m,ticks[1].OverhealingAmount);
        Assert.Equal(1000m,ally.CurrentHealth);
    }

    [Fact]
    public void PrayerOfMendingProcUsesLiveHealingReceivedMultiplier()
    {
        var (ctx,priest,ally,absorbs)=CreateContext(
            duration:2m,startingHealth:900m);
        var auras=new AuraManager();
        var proc=new PrayerOfMendingProcessor(priest.Key,100m,auras);
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=.25m,Type=CombatEventType.Damage,
            SourceActorKey=priest.Key,TargetActorKey=ally.Key,
            Amount=1m,RawAmount=1m
        });
        var run=new SimulationEngine([auras,proc,absorbs]).Run(ctx,start=>{
            auras.ApplyAura(start,ally,new AuraDefinition {
                Key="healing-reduction",Name="Healing reduction",
                HealingReceivedMultiplier=.5m,DurationSeconds=1m
            },priest.Key,"healing-reduction","debuff");
            auras.ApplyAura(start,ally,new AuraDefinition {
                Key="prayer-of-mending",Name="Prayer of Mending",
                DurationSeconds=30m
            },priest.Key,"prayer-of-mending","prayer-of-mending");
        });
        var heal=Assert.Single(run.Timeline,e=>
            e.Type==CombatEventType.Healing &&
            e.AbilityKey=="prayer-of-mending");
        Assert.Equal(50m,heal.RawAmount);
        Assert.Equal(50m,heal.Amount);
        Assert.Equal(0m,heal.OverhealingAmount);
        Assert.Equal(950m,ally.CurrentHealth);
    }

    [Fact]
    public void HealingReceivedModifier_RejectsNegativeButAllowsIncreases()
    {
        var (ctx,priest,ally,_)=CreateContext();
        var auras=new AuraManager();
        auras.ApplyAura(ctx,ally,new AuraDefinition{Key="beneficial",
            Name="Beneficial",DurationSeconds=10m,
            HealingReceivedMultiplier=1.2m},priest.Key,"buff","buff");
        Assert.Equal(120m,HealingReceivedModifiers.Apply(ally,0m,100m));
        auras.ApplyAura(ctx,ally,new AuraDefinition{Key="invalid",
            Name="Invalid",DurationSeconds=10m,
            HealingReceivedMultiplier=-1m},priest.Key,"bad","bad");
        Assert.Throws<InvalidOperationException>(()=>
            HealingReceivedModifiers.Apply(ally,0m,100m));
    }

    private static (SimulationContext,SimulationActorState,SimulationActorState,AbsorbManager)
        CreateContext(decimal duration=1m,decimal startingHealth=1000m)
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=166311,DurationSeconds=duration,CaptureTimeline=true,
            PrimaryActorKey="priest"
        });
        var priest=new SimulationActorState{Key="priest",Name="Priest",TeamKey="raid",Level=60};
        priest.InitializeHealth(1000m);
        var ally=new SimulationActorState{Key="ally",Name="Ally",TeamKey="raid",Level=60};
        ally.InitializeHealth(1000m,startingHealth);
        ctx.AddActor(priest);ctx.AddActor(ally);
        return (ctx,priest,ally,new AbsorbManager());
    }
}
