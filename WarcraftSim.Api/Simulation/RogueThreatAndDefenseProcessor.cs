using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP146: Connect Rogue damage and Feint to the same enemy ThreatTable used by
/// encounter target selectors; keep defensive eligibility in the encounter's
/// incoming physical attack resolution instead of applying an inert aura.
/// Coefficients remain explicitly configurable pending level-60 verification.
/// </summary>
public sealed class RogueThreatAndDefenseProcessor : ICombatEventProcessor
{
    private readonly string _rogue;
    private readonly RogueSimRequest _request;
    private readonly AuraManager _auras;
    public int ParriedIncomingSwings { get; private set; }
    public decimal ThreatReducedByFeint { get; private set; }
    public int DodgedIncomingSwings { get; private set; }

    public RogueThreatAndDefenseProcessor(string rogue, RogueSimRequest request,
        AuraManager auras)
    {
        _rogue = rogue;
        _request = request;
        _auras = auras;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.SourceActorKey != _rogue) return;
        if (ev.Type == CombatEventType.Damage && ev.Amount is > 0m &&
            ev.TargetActorKey is { } targetKey && targetKey != _rogue)
        {
            var rogue = context.GetActor(_rogue);
            var enemy = context.GetActor(targetKey);
            if (rogue is null || enemy is null || rogue.TeamKey == enemy.TeamKey) return;
            var gained = ev.Amount.Value; // 1x preview; no invented bonus to utility.
            enemy.ThreatTable.AddThreat(_rogue, gained);
            context.EmitEvent(new CombatEvent {
                TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ThreatChanged,
                SourceActorKey = _rogue, TargetActorKey = enemy.Key,
                AbilityKey = ev.AbilityKey, EffectKey = ev.EffectKey,
                Amount = gained, Description = "Rogue damage threat added to shared table."
            });
            return;
        }
        if (ev.Type != CombatEventType.AbilityCastCompleted || ev.AbilityKey != "feint") return;
        foreach (var enemy in context.Actors.Values.Where(a => a.TeamKey !=
            context.GetActor(_rogue)!.TeamKey))
        {
            var previous = enemy.ThreatTable.GetThreat(_rogue);
            var updated = Math.Max(0m, previous - _request.Tuning.FeintThreatReduction);
            if (updated == previous) continue;
            enemy.ThreatTable.SetThreat(_rogue, updated);
            ThreatReducedByFeint += previous - updated;
            context.EmitEvent(new CombatEvent {
                TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ThreatChanged,
                SourceActorKey = _rogue, TargetActorKey = enemy.Key, AbilityKey = "feint",
                Amount = updated - previous,
                Description = "Feint reduced real enemy threat (provisional fixed amount)."
            });
        }
    }

    public decimal ResolveIncomingPhysicalDamage(SimulationContext context,
        SimulationActorState defender, CombatEvent ev, decimal raw)
    {
        if (defender.Key != _rogue || ev.SchoolKey != "physical") return raw;
        var evasionActive = defender.ActiveAuras.Any(a => a.Definition.Key == "rogue-evasion" &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        var chance = Math.Clamp(_request.Tuning.BaseDodgePercent +
            _request.Talents.Rank("lightning-reflexes") *
                _request.Tuning.LightningReflexesDodgePercentPerRank +
            _request.Talents.Rank("fleet-footed") *
                _request.Tuning.FleetFootedDodgePercentPerRank +
            (evasionActive ? _request.Tuning.EvasionBonusDodgePercent : 0m), 0m, 100m);
        if ((decimal)context.Random.NextDouble() * 100m < chance)
        {
            DodgedIncomingSwings++;
            return 0m;
        }
        var parry = Math.Clamp(_request.Tuning.BaseParryPercent +
            _request.Talents.Rank("deflection") *
                _request.Tuning.DeflectionParryPercentPerRank, 0m, 100m);
        if ((decimal)context.Random.NextDouble() * 100m < parry)
        {
            ParriedIncomingSwings++;
            if (_request.Talents.Rank("riposte") > 0)
                _auras.ApplyAura(context, defender, new AuraDefinition {
                    Key = "riposte-ready", Name = "Riposte opportunity",
                    DurationSeconds = 5m
                }, _rogue, "riposte", "riposte-parry-proc");
            return 0m;
        }
        return raw;
    }
}
