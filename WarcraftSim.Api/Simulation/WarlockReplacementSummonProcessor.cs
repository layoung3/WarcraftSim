using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Optional scheduled in-combat demon replacement. Executes actual Warlock
/// Fel Domination and Summon ability casts through the existing AbilityExecutor;
/// replacement companion becomes active only after cast completion. Failed
/// casts/retries never create a free demon. This does not create a pet engine.
/// </summary>
public sealed class WarlockReplacementSummonProcessor(
    SimulationActorState warlock,WarlockDemonCompanion replacement,
    AbilityExecutor executor,AuraManager auras,decimal plannedAt,
    bool useFelDomination) : ICombatEventProcessor
{
    private const string Attempt="warlock-replacement-summon-attempt";
    private readonly string _summonKey="summon-"+replacement.Family.Key;
    private int _stage;
    private bool _cancelledOriginal;
    public int CompletedSummons { get; private set; }
    public int RetryAttempts { get; private set; }

    public void Start(SimulationContext context) => Schedule(context,plannedAt);

    private void Schedule(SimulationContext context,decimal at)
    {
        if(at<0m || at>=context.Options.DurationSeconds)return;
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds=at,Type=CombatEventType.RotationDecision,
            SourceActorKey=warlock.Key,TargetActorKey=warlock.Key,
            EffectKey=Attempt,IsInternal=true
        });
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.SourceActorKey!=warlock.Key)return;
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.AbilityKey=="fel-domination" && _stage==1)
        {
            _stage=2;
            Schedule(context,context.CurrentTimeSeconds+.01m);
            return;
        }
        if(ev.Type==CombatEventType.AbilityCastStarted &&
           ev.AbilityKey==_summonKey && _stage==3)
        {
            if(useFelDomination)
                auras.RemoveAuras(context,warlock,"warlock-fel-domination",
                    warlock.Key,"consumed by summon");
            return;
        }
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.AbilityKey==_summonKey && _stage==3)
        {
            _stage=4;
            CompletedSummons++;
            replacement.EngageAfterSuccessfulSummon(context,context.CurrentTimeSeconds);
            return;
        }
        if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
           ev.EffectKey!=Attempt || _stage>=3)return;

        // The explicit replacement decision interrupts the current cast once.
        if(!_cancelledOriginal)
        {
            _cancelledOriginal=true;
            executor.TryCancelCurrentCast(context,warlock.Key);
        }
        var spell=_stage switch {
            0 when useFelDomination=>"fel-domination",
            _=>_summonKey
        };
        var started=executor.TryStartAbility(context,warlock.Key,warlock.Key,spell);
        if(started.Success)
        {
            _stage=spell=="fel-domination"?1:3;
            // The shared executor may emit the cast-start event before this
            // processor's stage transition. Consume Fel Domination here too,
            // atomically with a SUCCESSFUL summon start, never on a retry.
            if(spell==_summonKey && useFelDomination)
                auras.RemoveAuras(context,warlock,"warlock-fel-domination",
                    warlock.Key,"consumed by summon cast start");
        }
        else
        {
            RetryAttempts++;
            Schedule(context,context.CurrentTimeSeconds+.1m);
        }
    }
}
