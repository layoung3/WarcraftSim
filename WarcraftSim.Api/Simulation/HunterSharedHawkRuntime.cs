using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Reuses the same ordinary companion actors and owner executor as Beast
/// Mastery's existing two-Hawk implementation, for valid Survival off-tree
/// Summon Hawk allocations. Does not simulate damage in a separate engine.
/// </summary>
public sealed class HunterSharedHawkRuntime(
    SimulationActorState hunter,SimulationActorState target,
    IReadOnlyList<SimulationActorState> hawks,AuraManager auras,
    AbilityExecutor executor,AutoAttackProcessor autos,
    ForeverBeastMasteryHunterTuning tuning) : ICombatEventProcessor
{
    private readonly Dictionary<string,decimal> _expires=
        new(StringComparer.OrdinalIgnoreCase);
    private readonly AutoAttackDefinition _attack=new() {
        Key="hawk-auto",Name="Hawk Assault",
        SwingIntervalSeconds=tuning.HawkAttackInterval,
        DamageEffect=new AbilityEffectDefinition {
            Key="hawk-impact",EffectType=AbilityEffectTypes.DirectDamage,
            TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
            MitigationType=DamageMitigationTypes.Armor,
            ResolutionType=CombatResolutionTypes.Melee,
            CanMiss=true,CanCrit=true,
            MinimumValue=tuning.HawkAttackDamage,
            MaximumValue=tuning.HawkAttackDamage,
            ScalingStatKey="attack-power",
            ScalingCoefficient=tuning.HawkAttackInterval/14m
        }
    };
    public int Summons{get;private set;}
    public static List<SimulationActorState> Create(SimulationContext ctx,
        SimulationActorState hunter)
    {
        var result=new List<SimulationActorState>();
        for(var i=1;i<=2;i++)
        {
            var (hawk,_)=CompanionActorFactory.Create(ctx,hunter,new CompanionDefinition{
                Key=$"hawk-{i}",Name=$"Summoned Hawk {i}",
                OwnerKey=hunter.Key,FamilyKey="summoned-hawk",
                BaseHealth=450m,BaseAttackPower=0m,BaseArmor=0m,
                BaseResource=0m,ResourceRegenPerSecond=0m,
                OwnerAttackPowerCoefficient=.05m
            });
            result.Add(hawk);
        }
        return result;
    }
    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.AbilityKey=="summon-hawk" && ev.SourceActorKey==hunter.Key)
        {
            var hawk=hawks.OrderBy(h=>_expires.GetValueOrDefault(h.Key)).First();
            autos.Stop(ctx,hawk.Key,"hawk-auto");
            var expiry=ctx.CurrentTimeSeconds+18m;
            _expires[hawk.Key]=expiry;
            Summons++;
            var dive=108m+.05m*hunter.Stats.Get("attack-power");
            executor.ExecuteBackgroundDirectDamage(ctx,hawk,target,
                "summon-hawk","summon-hawk",new AbilityEffectDefinition {
                    Key="hawk-dive-impact",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                    MitigationType=DamageMitigationTypes.Armor,
                    ResolutionType=CombatResolutionTypes.Melee,
                    CanMiss=true,CanCrit=true,
                    MinimumValue=dive,MaximumValue=dive
                });
            auras.ApplyAura(ctx,hawk,new AuraDefinition {
                Key="hawk-engaged",Name="Summoned Hawk",DurationSeconds=18m
            },hunter.Key,"summon-hawk","hawk-engagement");
            autos.Start(ctx,hawk.Key,target.Key,_attack,0m);
            if(expiry<ctx.Options.DurationSeconds)
                ctx.ScheduleEvent(new CombatEvent{
                    TimeSeconds=expiry,Type=CombatEventType.RotationDecision,
                    SourceActorKey=hawk.Key,TargetActorKey=target.Key,
                    EffectKey="shared-hawk-expire",IsInternal=true
                });
        }
        if(ev.Type==CombatEventType.RotationDecision && ev.IsInternal &&
           ev.EffectKey=="shared-hawk-expire" &&
           ev.SourceActorKey is { } actor &&
           _expires.TryGetValue(actor,out var expiryAt) &&
           ctx.CurrentTimeSeconds>=expiryAt)
        {
            autos.Stop(ctx,actor,"hawk-auto");
            _expires.Remove(actor);
        }
    }
}
