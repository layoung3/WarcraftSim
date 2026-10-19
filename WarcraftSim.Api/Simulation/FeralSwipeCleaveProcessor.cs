using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Swipe strikes at most three enemies (primary + two others). Secondary
/// targets are explicitly represented actors. Target order is deterministic;
/// geometry and independently rolled hit tables remain encounter dependencies.
/// </summary>
public sealed class FeralSwipeCleaveProcessor : ICombatEventProcessor
{
    private readonly AbilityExecutor _executor;
    private readonly string _druid;
    public int SecondaryHits { get; private set; }

    public FeralSwipeCleaveProcessor(string druid, AbilityExecutor executor)
    {
        _druid = druid; _executor = executor;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.Damage || ev.SourceActorKey != _druid ||
            ev.AbilityKey != "swipe" || ev.IsPeriodic || ev.Amount is not > 0m ||
            ev.TargetActorKey is null) return;
        var druid = context.GetActor(_druid);
        if (druid is null) return;
        foreach (var enemy in context.Actors.Values.Where(a => a.IsAlive &&
            a.TeamKey != druid.TeamKey && a.Key != ev.TargetActorKey)
            .OrderBy(a => a.Key, StringComparer.Ordinal).Take(2))
        {
            var amount = ev.Amount.Value;
            _executor.ExecuteBackgroundDirectDamage(context, druid, enemy,
                "swipe-secondary", "Swipe (additional target)",
                new AbilityEffectDefinition {
                    Key = "swipe-cleave", EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false,
                    MinimumValue = amount, MaximumValue = amount
                });
            SecondaryHits++;
        }
    }
}
