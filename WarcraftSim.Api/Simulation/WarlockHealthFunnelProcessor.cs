using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 Health Funnel R7. Reuses AbilityExecutor's actual 10-second
/// channel and scheduled ticks, healing the currently active demon actor.
/// This processor owns only the Warlock-to-pet health transfer, never pet AI.
/// Improved Health Funnel 1/2: +20/40% heal, -15/30% owner HP cost.
/// </summary>
public sealed class WarlockHealthFunnelProcessor(
    WarlockSimRequest request,SimulationActorState owner,
    Func<WarlockDemonCompanion?> activeCompanion,
    PriorityRotationExecutor unusedPriority,
    RotationProfile rotation,AbilityExecutor executor) : ICombatEventProcessor
{
    private readonly int _rank=request.Talents.Rank("improved-health-funnel");
    public int SuccessfulTicks { get; private set; }
    public decimal HealthSpent { get; private set; }
    public decimal PetHealingDone { get; private set; }

    private SimulationActorState? ActivePet()
    {
        var pet=activeCompanion();
        return pet?.IsActive==true?pet.Actor:null;
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.RotationDecision)
        {
            var pet=ActivePet();
            var canHeal=pet is not null &&
                owner.CurrentHealth>79m*(1m-.15m*_rank) &&
                pet.CurrentHealth<pet.MaximumHealth &&
                100m*pet.CurrentHealth/pet.MaximumHealth<
                    request.HealthFunnelBelowPetHealthPercent;
            foreach(var entry in rotation.Entries.Where(x=>x.AbilityKey=="health-funnel"))
                entry.IsEnabled=canHeal;
            return;
        }
        if(ev.Type!=CombatEventType.AbilityChannelTick ||
            ev.AbilityKey!="health-funnel" ||
            ev.SourceActorKey!=owner.Key ||
            ev.AbilityExecutionId is not Guid id ||
            context.GetAbilityExecution(id) is not {IsChanneling:true,IsCancelled:false})
            return;
        var target=ActivePet();
        if(target is null)
        {
            executor.TryCancelCurrentCast(context,owner.Key);
            return;
        }
        var healthCost=79m*(1m-.15m*_rank);
        if(owner.CurrentHealth<=healthCost)
        {
            executor.TryCancelCurrentCast(context,owner.Key);
            return;
        }
        var spent=owner.TakeDamage(healthCost);
        HealthSpent+=spent;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,
            Type=CombatEventType.HealthChanged,
            SourceActorKey=owner.Key,TargetActorKey=owner.Key,
            AbilityKey="health-funnel",EffectKey="health-funnel-cost",
            Amount=-spent,Description="Health Funnel paid its per-tick caster health cost."
        });
        var raw=153m*(1m+.20m*_rank);
        var effective=target.Heal(raw);
        SuccessfulTicks++;
        PetHealingDone+=effective;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.Healing,
            SourceActorKey=owner.Key,TargetActorKey=target.Key,
            AbilityKey="health-funnel",EffectKey="health-funnel-pet-heal",
            Amount=effective,RawAmount=raw,OverhealingAmount=raw-effective,
            Description="Health Funnel restored the active demon's health."
        });
    }
}
