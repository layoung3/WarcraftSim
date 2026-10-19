using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp144TargetAuraEligibilityTests
{
    [Fact]
    public void TargetAuraRequirementRejectsMissingOrAnotherCastersHot()
    {
        var healer=new SimulationActorState {Key="druid",Name="druid",TeamKey="raid",Level=60};
        healer.InitializeHealth(5000m);
        healer.AddResource(new ResourceState("mana",2000m,2000m));
        healer.AddAbility(Heal());
        var ally=new SimulationActorState {Key="tank",Name="tank",TeamKey="raid",Level=60};
        ally.InitializeHealth(5000m,2500m);
        var context=Context(healer,ally);
        var executor=Executor();
        Assert.False(executor.TryStartAbility(context,"druid","tank","swiftmend").Success);
        ally.ActiveAuras.Add(Hot("rejuvenation","other-druid"));
        Assert.False(executor.TryStartAbility(context,"druid","tank","swiftmend").Success);
        ally.ActiveAuras.Add(Hot("regrowth","druid"));
        var use=executor.TryStartAbility(context,"druid","tank","swiftmend");
        Assert.True(use.Success,use.FailureReason);
    }
    private static AbilityDefinition Heal()=>new() {
        Key="swiftmend",Name="Swiftmend",RequiredAnyTargetAuraKeys=["rejuvenation","regrowth"],
        ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=300m}],
        Effects=[new AbilityEffectDefinition {Key="heal",EffectType=AbilityEffectTypes.DirectHealing,
            TargetType=AbilityTargetTypes.Friendly,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false,MinimumValue=400m,MaximumValue=400m}]
    };
    private static AuraInstance Hot(string key,string caster)=>new() {
        Definition=new AuraDefinition {Key=key,Name=key,DurationSeconds=10m},
        SourceActorKey=caster,TargetActorKey="tank",AppliedAtSeconds=0m,ExpiresAtSeconds=10m
    };
    private static SimulationContext Context(SimulationActorState h,SimulationActorState t)
    {
        var context=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=30m,PrimaryActorKey="druid"
        });
        context.AddActor(h);context.AddActor(t);return context;
    }
    private static AbilityExecutor Executor()=>new(new AuraManager(),
        new SimpleCombatRollResolver(hitChancePercent:100m,criticalChancePercent:0m),
        new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
            RulesetKey="cp144-test",Version="1"
        }));
}
