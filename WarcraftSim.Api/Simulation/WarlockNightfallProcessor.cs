using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current Forever 70532 Nightfall rank 1/2: landed Corruption, Drain Soul,
/// Drain Life, or Wrack damage has 2/4% chance to grant Shadow Trance.
/// The next Shadow Bolt is instant. Uses Decimation's existing timing provider
/// for Soul Fire so the two talents compose without a second casting engine.
/// </summary>
public sealed class WarlockNightfallProcessor : ICombatEventProcessor, IAbilityTimingProvider
{
    private readonly SimulationActorState _owner;
    private readonly AuraManager _auras;
    private readonly WarlockDecimationProcessor _decimation;
    private readonly int _rank;
    private readonly decimal? _testChance;
    private decimal _expires;
    private bool _active;
    public int Procs { get; private set; }
    public int Consumed { get; private set; }

    public WarlockNightfallProcessor(SimulationActorState owner,AuraManager auras,
        WarlockDecimationProcessor decimation,int rank,decimal? testChance=null)
    {
        _owner=owner ?? throw new ArgumentNullException(nameof(owner));
        _auras=auras ?? throw new ArgumentNullException(nameof(auras));
        _decimation=decimation ?? throw new ArgumentNullException(nameof(decimation));
        if(rank is <0 or >2 || testChance is <0m or >100m)
            throw new ArgumentException("Invalid Nightfall rank or test chance.");
        _rank=rank;
        _testChance=testChance;
    }

    public static decimal ProcChancePercent(int rank) => rank switch {
        0 => 0m, 1 => 2m, 2 => 4m,
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public AbilityTimingSnapshot Resolve(SimulationContext context,
        SimulationActorState source,AbilityDefinition ability)
    {
        var baseTiming=_decimation.Resolve(context,source,ability);
        if(source.Key!=_owner.Key || ability.Key!="shadow-bolt" ||
            !_active || context.CurrentTimeSeconds>=_expires)
            return baseTiming;
        return new AbilityTimingSnapshot {
            CastTimeSeconds=0m,
            GlobalCooldownSeconds=baseTiming.GlobalCooldownSeconds,
            ChannelDurationSeconds=baseTiming.ChannelDurationSeconds,
            ChannelTickIntervalSeconds=baseTiming.ChannelTickIntervalSeconds
        };
    }

    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(_rank<=0)return;
        var now=ctx.CurrentTimeSeconds;
        if(_active && now>=_expires)
        {
            _active=false;
            _auras.RemoveAuras(ctx,_owner,"warlock-shadow-trance",_owner.Key,
                "Nightfall duration expired");
        }
        if(ev.Type==CombatEventType.AuraRemoved &&
            ev.AbilityKey=="nightfall" && ev.TargetActorKey==_owner.Key &&
            now>=_expires)
            _active=false;

        if(ev.SourceActorKey!=_owner.Key)return;

        // The executor already snapshotted the instant timing. Consume only
        // after a real Shadow Bolt cast starts, never on a failed attempt.
        if(ev.Type==CombatEventType.AbilityCastStarted &&
            ev.AbilityKey=="shadow-bolt" && _active && now<_expires)
        {
            _active=false;
            _expires=0m;
            Consumed++;
            _auras.RemoveAuras(ctx,_owner,"warlock-shadow-trance",_owner.Key,
                "instant Shadow Bolt started");
            return;
        }
        if(ev.Type!=CombatEventType.Damage || ev.Amount is not >0m ||
            ev.ResultKey is not ("hit" or "critical") ||
            ev.AbilityKey is not ("corruption" or "drain-soul" or "drain-life" or "wrack") ||
            ev.TargetActorKey is not { } targetKey)return;
        var target=ctx.GetActor(targetKey);
        if(target is null || target.TeamKey==_owner.TeamKey)return;
        if((decimal)ctx.Random.NextDouble()*100m >=
            (_testChance ?? ProcChancePercent(_rank)))return;

        _active=true;
        _expires=now+10m; // provisional Shadow Trance lifetime, client does not list duration.
        Procs++;
        _auras.ApplyAura(ctx,_owner,new AuraDefinition {
            Key="warlock-shadow-trance",Name="Shadow Trance",DurationSeconds=10m
        },_owner.Key,"nightfall","nightfall-proc");
    }
}
