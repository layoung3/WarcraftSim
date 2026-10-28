using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Explicit deterministic inbound-damage fixture for solo caster
/// certification. No enemy AI, positioning, hit rolls or encounter geometry
/// are invented; callers specify when/how much damage occurs.
/// Shared AbsorbManager resolves shields before a real damage event is emitted.
/// </summary>
public sealed class ScriptedCasterIncomingDamageProcessor(
    SimulationActorState caster, SimulationActorState attacker,
    AbsorbManager absorbs, decimal? atSeconds, decimal amount, string school,
    bool fullyResisted=false)
    : ICombatEventProcessor
{
    public void Start(SimulationContext context)
    {
        if(atSeconds is null || amount<=0m || atSeconds.Value>=context.Options.DurationSeconds)
            return;
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds=atSeconds.Value,Type=CombatEventType.RotationDecision,
            SourceActorKey=attacker.Key,TargetActorKey=caster.Key,
            EffectKey="scripted-caster-incoming-damage",IsInternal=true
        });
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
            ev.SourceActorKey!=attacker.Key || ev.TargetActorKey!=caster.Key ||
            ev.EffectKey!="scripted-caster-incoming-damage" || !caster.IsAlive)
            return;
        // Current Ice Block provides full immunity, not an absorb that can
        // run out. Do not apply damage or spend any absorb shield.
        if(caster.ActiveAuras.Any(a=>a.Definition.Key=="mage-ice-block" &&
            a.IsActiveAt(context.CurrentTimeSeconds)))
        {
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,
                Type=CombatEventType.Damage,
                SourceActorKey=attacker.Key,TargetActorKey=caster.Key,
                AbilityKey="scripted-caster-incoming-damage",
                EffectKey="ice-block-immunity",SchoolKey=school,
                ResultKey="immune",Amount=0m,RawAmount=amount,
                Description="Ice Block negated the incoming hit."
            });
            return;
        }
        if(fullyResisted)
        {
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,
                Type=CombatEventType.Damage,
                SourceActorKey=attacker.Key,TargetActorKey=caster.Key,
                AbilityKey="scripted-caster-incoming-damage",
                EffectKey="scripted-full-resist",SchoolKey=school,
                ResolutionType=CombatResolutionTypes.Spell,
                ResultKey="resist",Amount=0m,RawAmount=amount,
                Description="Explicit fully resisted magical attack."
            });
            return;
        }
        var reduction=Math.Clamp(caster.Stats.Get("incoming-damage-reduction-percent"),
            0m,100m);
        var raw=amount*(1m-reduction/100m);
        var result=absorbs.ApplyDamage(context,caster,raw,attacker.Key,
            "scripted-caster-incoming-damage","scripted-hit");
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,
            Type=CombatEventType.Damage,
            SourceActorKey=attacker.Key,TargetActorKey=caster.Key,
            AbilityKey="scripted-caster-incoming-damage",
            EffectKey="scripted-hit",SchoolKey=school,
            ResolutionType=CombatResolutionTypes.AlwaysHits,
            ResultKey="hit",Amount=result.HealthDamage,
            RawAmount=amount,MitigatedAmount=amount-raw,
            AbsorbedAmount=result.AbsorbedDamage,
            Description="Explicit test-fixture incoming caster damage."
        });
    }
}
