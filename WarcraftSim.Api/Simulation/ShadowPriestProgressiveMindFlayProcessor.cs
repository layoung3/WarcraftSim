using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// October 15 beta: Improved Mind Flay increases each successive damage tick
/// by 10%/20% relative to the PREVIOUS tick. Applies before ability execution;
/// never leaks stacks into a subsequent channel.
/// </summary>
public sealed class ShadowPriestProgressiveMindFlayProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _priest;
    private readonly int _rank;
    private Guid? _executionId;
    private int _tickCount;
    private readonly decimal _originalMultiplier;

    public ShadowPriestProgressiveMindFlayProcessor(SimulationActorState priest, int rank)
    {
        _priest = priest ?? throw new ArgumentNullException(nameof(priest));
        if (rank is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(rank));
        _rank = rank;
        _originalMultiplier = _priest.Abilities.TryGetValue("mind-flay", out var ability)
            ? ability.Definition.Effects.Single(x => x.Key == "mind-flay-tick").DamageMultiplier
            : 1m;
    }

    public static decimal MultiplierForTick(int rank, int tickNumber)
    {
        if (rank is < 0 or > 2 || tickNumber < 1)
            throw new ArgumentOutOfRangeException(rank is < 0 or > 2 ? nameof(rank) : nameof(tickNumber));
        var step = 1m + rank * .10m;
        var value = 1m;
        for (var i = 1; i < tickNumber; i++) value *= step;
        return value;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (_rank == 0 || !string.Equals(ev.SourceActorKey, "priest", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(ev.AbilityKey, "mind-flay", StringComparison.OrdinalIgnoreCase) ||
            !_priest.Abilities.TryGetValue("mind-flay", out var ability)) return;
        if (ev.Type == CombatEventType.AbilityChannelTick)
        {
            if (_executionId != ev.AbilityExecutionId)
            {
                _executionId = ev.AbilityExecutionId;
                _tickCount = 0;
            }
            _tickCount++;
            ability.Definition.Effects.Single(x => x.Key == "mind-flay-tick").DamageMultiplier =
                _originalMultiplier * MultiplierForTick(_rank, _tickCount);
        }
        else if (ev.Type is CombatEventType.AbilityChannelCompleted or CombatEventType.AbilityChannelCancelled)
        {
            _executionId = null;
            _tickCount = 0;
            ability.Definition.Effects.Single(x => x.Key == "mind-flay-tick").DamageMultiplier =
                _originalMultiplier;
        }
    }
}
