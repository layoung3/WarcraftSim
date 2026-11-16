using WarcraftSim.Core.Abilities;
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
    private readonly Action<SimulationContext,string>? _onDodge;
    public int PreventedByControl { get; private set; }
    public int HeightenedSensesAvoided { get; private set; }
    public int ParriedIncomingSwings { get; private set; }
    public decimal ThreatReducedByFeint { get; private set; }
    public int DodgedIncomingSwings { get; private set; }

    public RogueThreatAndDefenseProcessor(string rogue, RogueSimRequest request,
        AuraManager auras, Action<SimulationContext,string>? onDodge = null)
    {
        _rogue = rogue;
        _request = request;
        _auras = auras;
        _onDodge=onDodge;
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
        if (defender.Key != _rogue) return raw;
        if (_request.UseClassWideTalents)
        {
            if(ev.SourceActorKey is {} attacker && context.GetActor(attacker) is {} enemy && enemy.ActiveAuras.Any(a=>
                a.SourceActorKey==_rogue && a.IsActiveAt(context.CurrentTimeSeconds) && a.Definition.Key is
                ("rogue-gouge-control" or "rogue-blind-control" or "rogue-sap-control" or "cheap-shot-stun" or "kidney-shot-stun")))
            {
                PreventedByControl++;
                return 0m;
            }
            if(ev.WeaponHandKey==WeaponHandKeys.MainHand && ev.SourceActorKey is {} armed && context.GetActor(armed) is {} wielding &&
                wielding.ActiveAuras.Any(a=>a.SourceActorKey==_rogue && a.Definition.Key=="rogue-riposte-disarm" && a.IsActiveAt(context.CurrentTimeSeconds)))
            { PreventedByControl++;return 0m; }
            if(ev.ResolutionType is CombatResolutionTypes.Spell or CombatResolutionTypes.Ranged)
            {
                if(_request.Talents.Rank("heightened-senses")>0 && (decimal)context.Random.NextDouble()*100m<
                    2m*_request.Talents.Rank("heightened-senses"))
                { HeightenedSensesAvoided++;return 0m; }
                return raw;
            }
        }
        if (ev.SchoolKey != "physical") return raw;
        var evasionActive = defender.ActiveAuras.Any(a => a.Definition.Key == "rogue-evasion" &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        var chance = Math.Clamp(_request.Tuning.BaseDodgePercent +
            _request.Talents.Rank("lightning-reflexes") *
                _request.Tuning.LightningReflexesDodgePercentPerRank +
            _request.Talents.Rank("fleet-footed") *
                _request.Tuning.FleetFootedDodgePercentPerRank +
            (evasionActive ? _request.Tuning.EvasionBonusDodgePercent : 0m) +
            (_request.UseClassWideTalents && defender.ActiveAuras.Any(a=>a.Definition.Key=="ghostly-strike-dodge" &&
                a.SourceActorKey==_rogue && a.IsActiveAt(context.CurrentTimeSeconds)) ? 15m : 0m), 0m, 100m);
        if ((decimal)context.Random.NextDouble() * 100m < chance)
        {
            DodgedIncomingSwings++;
            if(_request.UseClassWideTalents && ev.SourceActorKey is {} attacker)_onDodge?.Invoke(context,attacker);
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
