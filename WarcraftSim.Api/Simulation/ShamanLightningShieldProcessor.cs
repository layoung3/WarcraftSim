using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Source-owned Lightning Shield charges. Direct incoming landed damage from
/// an opposing actor triggers one Nature strike subject to a configurable ICD.
/// It does not trigger itself, duplicate hit rolls, or react to periodic damage.
/// The exact server-side "few seconds" ICD remains provisional.
/// </summary>
public sealed class ShamanLightningShieldProcessor(
    SimulationActorState owner,AuraManager auras,AbilityExecutor executor,
    decimal internalCooldownSeconds = 3m) : ICombatEventProcessor
{
    private int _charges;
    private decimal _nextEligible;
    public int Triggers {get;private set;}
    public int Charges => _charges;

    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (internalCooldownSeconds < 0m)
            throw new ArgumentOutOfRangeException(nameof(internalCooldownSeconds));
        if (ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="lightning-shield" &&
            ev.TargetActorKey==owner.Key && ev.SourceActorKey==owner.Key)
        {
            _charges=3;
            _nextEligible=ctx.CurrentTimeSeconds;
            return;
        }
        if (ev.Type==CombatEventType.AuraRemoved && ev.AbilityKey=="lightning-shield" &&
            ev.TargetActorKey==owner.Key && ev.SourceActorKey==owner.Key)
        {
            _charges=0;
            return;
        }
        if (ev.Type!=CombatEventType.Damage || ev.TargetActorKey!=owner.Key ||
            ev.SourceActorKey is null || ev.Amount is not >0m ||
            ev.IsPeriodic || ev.AbilityKey=="lightning-shield" ||
            _charges==0 || ctx.CurrentTimeSeconds<_nextEligible) return;
        var enemy=ctx.GetActor(ev.SourceActorKey);
        if(enemy is null || enemy.TeamKey==owner.TeamKey || !enemy.IsAlive) return;
        _charges--;
        _nextEligible=ctx.CurrentTimeSeconds+internalCooldownSeconds;
        _executor.ExecuteBackgroundDirectDamage(ctx,owner,enemy,"lightning-shield",
            "Lightning Shield",new AbilityEffectDefinition {
                Key="lightning-shield-retaliation",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="nature",
                MitigationType=DamageMitigationTypes.None,
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false,MinimumValue=178m,MaximumValue=178m
            });
        Triggers++;
        if(_charges==0)
            auras.RemoveAuras(ctx,owner,"shaman-lightning-shield",owner.Key,
                "Lightning Shield charges exhausted");
    }
}
