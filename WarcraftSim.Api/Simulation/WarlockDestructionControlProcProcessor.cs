using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever beta Destruction control-proc receipts (70532/70707 tooltips;
/// 70753 tree cross-check). Aftermath: landed Conflagrate 20%/rank, 5s.
/// Pyroclasm stuns after Soul Fire direct damage (13/26%, 3s) or
/// Rain of Fire/Hellfire over a full channel (one roll per victim per cast).
/// These auras document trigger/expiry and ownership; no enemy movement,
/// cast interruption, CC immunity, or AI is inferred from the aura alone.
/// </summary>
public sealed class WarlockDestructionControlProcProcessor : ICombatEventProcessor
{
    public const string AftermathAura="warlock-aftermath-daze";
    public const string PyroclasmAura="warlock-pyroclasm-stun";

    private readonly AuraManager _auras;
    private readonly int _aftermathRank, _pyroclasmRank;
    private readonly bool _allowControlledTarget;
    private readonly decimal? _forcedChance;
    private readonly HashSet<(Guid ExecutionId,string TargetKey)> _channelTargetRolls = [];

    public int AftermathDazes { get; private set; }
    public int PyroclasmStuns { get; private set; }
    public int PyroclasmChannelRolls { get; private set; }

    public WarlockDestructionControlProcProcessor(AuraManager auras,
        int aftermathRank,int pyroclasmRank,bool allowControlledTarget,
        decimal? forcedChancePercent=null)
    {
        _auras=auras??throw new ArgumentNullException(nameof(auras));
        if(aftermathRank is <0 or >5 || pyroclasmRank is <0 or >2 ||
           forcedChancePercent is <0m or >100m)
            throw new ArgumentException("Invalid Destruction proc rank or test chance.");
        _aftermathRank=aftermathRank;
        _pyroclasmRank=pyroclasmRank;
        _allowControlledTarget=allowControlledTarget;
        _forcedChance=forcedChancePercent;
    }

    public static decimal AftermathDazeChancePercent(int rank) => rank is >=0 and <=5
        ? 20m*rank : throw new ArgumentOutOfRangeException(nameof(rank));

    public static decimal PyroclasmStunChancePercent(int rank) => rank switch {
        0=>0m,1=>13m,2=>26m,
        _=>throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public void Process(SimulationContext context,CombatEvent ev)
    {
        // The Solo DPS training boss has no modeled movement/casting, and
        // does not implicitly represent a CC-susceptible enemy.
        if(!_allowControlledTarget || ev.Type!=CombatEventType.Damage ||
           ev.SourceActorKey!="warlock" || ev.TargetActorKey is not { } targetKey ||
           ev.Amount is not >0m || ev.ResultKey is not ("hit" or "critical"))
            return;
        var owner=context.GetActor("warlock");
        var victim=context.GetActor(targetKey);
        if(owner is null || victim is null || !victim.IsAlive ||
           victim.TeamKey==owner.TeamKey)
            return;

        if(ev.AbilityKey=="conflagrate" && !ev.IsPeriodic && !ev.IsChannelTick &&
           _aftermathRank>0 && Chance(context,AftermathDazeChancePercent(_aftermathRank)))
        {
            _auras.ApplyAura(context,victim,new AuraDefinition {
                Key=AftermathAura,Name="Aftermath (50% movement daze; control marker)",
                DurationSeconds=5m,StackingMode=AuraStackingMode.Refresh
            },owner.Key,"aftermath","conflagrate-daze");
            AftermathDazes++;
            return;
        }

        if(_pyroclasmRank==0)return;
        var single=ev.AbilityKey=="soul-fire" && !ev.IsPeriodic && !ev.IsChannelTick;
        var channel=ev.AbilityKey is ("rain-of-fire" or "hellfire") &&
            ev.IsChannelTick && ev.AbilityExecutionId.HasValue;
        if(!single && !channel)return;

        // Tooltip specifies 13/26% over the ENTIRE channel duration, not per
        // tick. One roll per victim per cast avoids accidentally multiplying
        // that chance by the number of AoE damage occurrences.
        if(channel)
        {
            var key=(ev.AbilityExecutionId!.Value,targetKey.ToLowerInvariant());
            if(!_channelTargetRolls.Add(key))return;
            PyroclasmChannelRolls++;
        }
        if(!Chance(context,PyroclasmStunChancePercent(_pyroclasmRank)))return;

        _auras.ApplyAura(context,victim,new AuraDefinition {
            Key=PyroclasmAura,Name="Pyroclasm (3s stun; control marker)",
            DurationSeconds=3m,StackingMode=AuraStackingMode.Refresh
        },owner.Key,"pyroclasm","destruction-stun");
        PyroclasmStuns++;
    }

    private bool Chance(SimulationContext context,decimal percent) =>
        (decimal)context.Random.NextDouble()*100m < (_forcedChance ?? percent);
}
