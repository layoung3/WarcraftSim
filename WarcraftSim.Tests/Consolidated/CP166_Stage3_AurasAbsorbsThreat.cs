using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage3AurasAbsorbsThreatTests
{
    [Fact]
    public void SameAuraFromDifferentCastersRemainsIndependent()
    {
        var (ctx,first,target)=Context(1m);
        var second=Actor("second","raid");
        ctx.AddActor(second);
        var auras=new AuraManager();
        var definition=new AuraDefinition{
            Key="renew",Name="Renew",DurationSeconds=10m,
            StackingMode=AuraStackingMode.Refresh
        };
        auras.ApplyAura(ctx,target,definition,first.Key,"renew","aura");
        auras.ApplyAura(ctx,target,definition,second.Key,"renew","aura");
        Assert.Equal(2,target.ActiveAuras.Count);
        Assert.Equal(1,auras.RemoveAuras(ctx,target,"renew",first.Key));
        Assert.Single(target.ActiveAuras);
        Assert.Equal(second.Key,target.ActiveAuras.Single().SourceActorKey);
    }

    [Fact]
    public void RefreshingAuraInvalidatesOldExpirationAndRetainsLatestDuration()
    {
        var (ctx,first,target)=Context(3.5m);
        var manager=new AuraManager();
        var driver=new RefreshCheck(manager,target,first.Key);
        var run=new SimulationEngine([manager,driver]).Run(ctx,start=>{
            manager.ApplyAura(start,target,new AuraDefinition{
                Key="refresh-me",Name="Refresh",DurationSeconds=2m
            },first.Key,"aura","refresh");
            start.ScheduleEvent(new CombatEvent{
                TimeSeconds=1m,Type=CombatEventType.RotationDecision,
                SourceActorKey=first.Key,EffectKey="refresh-check-start",IsInternal=true
            });
            start.ScheduleEvent(new CombatEvent{
                TimeSeconds=2.5m,Type=CombatEventType.RotationDecision,
                SourceActorKey=first.Key,EffectKey="refresh-check-inspect",IsInternal=true
            });
        });
        Assert.True(driver.WasActiveAfterOldExpiration);
        Assert.Empty(target.ActiveAuras);
        Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="aura" && e.TimeSeconds==3m);
    }

    [Fact]
    public void StackingAuraRespectsCapAndRefreshesRuntimeIdentity()
    {
        var (ctx,first,target)=Context(1m);
        var manager=new AuraManager();
        var def=new AuraDefinition{
            Key="stack",Name="Stack",DurationSeconds=5m,
            MaxStacks=3,StackingMode=AuraStackingMode.Stack
        };
        var a=manager.ApplyAura(ctx,target,def,first.Key,"stack","stack");
        var id=a.InstanceId;
        manager.ApplyAura(ctx,target,def,first.Key,"stack","stack");
        Assert.Equal(2,a.Stacks);
        Assert.NotEqual(id,a.InstanceId);
        manager.ApplyAura(ctx,target,def,first.Key,"stack","stack");
        manager.ApplyAura(ctx,target,def,first.Key,"stack","stack");
        Assert.Equal(3,a.Stacks);
        Assert.Single(target.ActiveAuras);
    }

    [Fact]
    public void AbsorptionConsumesBeforeHealthAndConservesRawDamage()
    {
        var (ctx,first,target)=Context(1m);
        var manager=new AbsorbManager();
        manager.ApplyAbsorb(ctx,target,"shield","Shield",50m,10m,
            AbsorbStackingMode.Refresh,1,first.Key,"shield","absorb");
        var before=target.CurrentHealth;
        var resolved=manager.ApplyDamage(ctx,target,80m,"enemy","hit","damage");
        Assert.Equal(80m,resolved.IncomingDamage);
        Assert.Equal(50m,resolved.AbsorbedDamage);
        Assert.Equal(30m,resolved.HealthDamage);
        Assert.Equal(30m,before-target.CurrentHealth);
        Assert.Equal(80m,resolved.AbsorbedDamage+resolved.HealthDamage);
        Assert.Empty(target.ActiveAbsorbs);
        var run=new SimulationEngine([manager]).Run(ctx);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.AbsorbConsumed &&
            e.Amount==50m);
    }

    [Fact]
    public void HealingThreatUsesEffectiveHealingNotOverhealAndSplitsAcrossEnemies()
    {
        var (ctx,healer,target)=Context(1m);
        var enemyOne=Actor("enemy-one","enemy");
        var enemyTwo=Actor("enemy-two","enemy");
        ctx.AddActor(enemyOne);ctx.AddActor(enemyTwo);
        var resolver=new HealingThreatResolver(.5m);
        var contributions=resolver.Resolve(ctx,new CombatEvent{
            TimeSeconds=0m,Type=CombatEventType.Healing,
            SourceActorKey=healer.Key,TargetActorKey=target.Key,
            Amount=40m,RawAmount=100m,OverhealingAmount=60m
        });
        Assert.Equal(2,contributions.Count);
        Assert.All(contributions,c=>{
            Assert.Equal(10m,c.Amount);
            Assert.Equal(healer.Key,c.ThreatSourceActorKey);
        });
        Assert.Equal(20m,contributions.Sum(x=>x.Amount));
    }

    private static (SimulationContext,SimulationActorState,SimulationActorState)
        Context(decimal duration)
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=duration,PrimaryActorKey="healer",
            Seed=166366,CaptureTimeline=true
        });
        var healer=Actor("healer","raid");
        var target=Actor("ally","raid");
        ctx.AddActor(healer);ctx.AddActor(target);
        return (ctx,healer,target);
    }

    private static SimulationActorState Actor(string key,string team)
    {
        var a=new SimulationActorState{Key=key,Name=key,Level=60,TeamKey=team};
        a.InitializeHealth(1000m);
        return a;
    }

    private sealed class RefreshCheck(AuraManager auras,SimulationActorState target,
        string sourceKey) : ICombatEventProcessor
    {
        public bool WasActiveAfterOldExpiration {get;private set;}
        public void Process(SimulationContext ctx,CombatEvent ev)
        {
            if(ev.Type!=CombatEventType.RotationDecision ||
                !ev.IsInternal ||ev.SourceActorKey!=sourceKey)return;
            if(ev.EffectKey=="refresh-check-start")
                auras.ApplyAura(ctx,target,new AuraDefinition {
                    Key="refresh-me",Name="Refresh",DurationSeconds=2m
                },sourceKey,"aura","refresh");
            if(ev.EffectKey=="refresh-check-inspect")
                WasActiveAfterOldExpiration=target.ActiveAuras.Any(a=>
                    a.IsActiveAt(ctx.CurrentTimeSeconds));
        }
    }
}
