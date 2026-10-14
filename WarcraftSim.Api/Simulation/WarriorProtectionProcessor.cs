using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Per-iteration Protection reactions and temporary defensive states.
/// Real boss-melee outcomes open Revenge and consume Shield Block charges.
/// Shield Wall/Last Stand are action-activated, not permanent modifiers.</summary>
public sealed class WarriorProtectionProcessor : ICombatEventProcessor
{
    public const string ShieldWallActiveStat = "warrior-shield-wall-active";
    private const string ShieldBlockExpire = "warrior-shield-block-expire";
    private const string ShieldWallExpire = "warrior-shield-wall-expire";
    private const string LastStandExpire = "warrior-last-stand-expire";
    private readonly bool _shield;
    private int _blockCharges;
    private decimal _blockExpires, _wallExpires, _standExpires, _standHealthBonus;
    private int _blockGeneration, _wallGeneration, _standGeneration;

    public WarriorProtectionProcessor(WarriorSimRequest request) => _shield = request.ShieldEquipped;

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!_shield) return;
        var warrior = context.GetActor("warrior");
        if (warrior is null) return;
        var now = context.CurrentTimeSeconds;
        if (ev.Type == CombatEventType.AbilityCastCompleted && ev.SourceActorKey == warrior.Key)
        {
            switch (ev.AbilityKey)
            {
                case "shield-block":
                    StopBlock(warrior);
                    _blockGeneration++;
                    _blockCharges = 2;
                    _blockExpires = now + 7m;
                    warrior.Stats.Add("block-chance-percent", 75m);
                    Expire(context, ShieldBlockExpire, _blockExpires, _blockGeneration);
                    break;
                case "shield-wall":
                    _wallGeneration++;
                    _wallExpires = now + 12m;
                    warrior.Stats.Set(ShieldWallActiveStat, 1m);
                    Expire(context, ShieldWallExpire, _wallExpires, _wallGeneration);
                    break;
                case "last-stand":
                    if (_standHealthBonus > 0m) EndStand(warrior);
                    _standGeneration++;
                    _standHealthBonus = warrior.MaximumHealth * 0.30m;
                    warrior.InitializeHealth(warrior.MaximumHealth + _standHealthBonus,
                        warrior.CurrentHealth + _standHealthBonus);
                    _standExpires = now + 20m;
                    Expire(context, LastStandExpire, _standExpires, _standGeneration);
                    break;
            }
        }
        else if (ev.Type == CombatEventType.RotationDecision && ev.IsInternal &&
            ev.EncounterSequenceHitNumber is int generation)
        {
            if (ev.EffectKey == ShieldBlockExpire && generation == _blockGeneration && now >= _blockExpires)
                StopBlock(warrior);
            if (ev.EffectKey == ShieldWallExpire && generation == _wallGeneration && now >= _wallExpires)
                warrior.Stats.Set(ShieldWallActiveStat, 0m);
            if (ev.EffectKey == LastStandExpire && generation == _standGeneration && now >= _standExpires)
                EndStand(warrior);
        }
        else if (ev.Type == CombatEventType.Damage && ev.TargetActorKey == warrior.Key &&
            ev.AbilityKey == "boss-main-hand-auto")
        {
            if (ev.ResultKey is "block" or "dodge" or "parry" || (ev.BlockedAmount ?? 0m) > 0m)
            {
                warrior.GrantReactiveOpportunity(WarriorProtectionAbilityFactory.RevengeOpportunityKey,
                    "target", now, 5m);
                context.EmitEvent(new CombatEvent
                {
                    TimeSeconds = now, Type = CombatEventType.ReactiveOpportunityGranted,
                    SourceActorKey = warrior.Key, TargetActorKey = "target",
                    AbilityKey = "revenge", Description = "Revenge opportunity from incoming block/dodge/parry."
                });
            }
            if (_blockCharges > 0 && now < _blockExpires &&
                (ev.ResultKey == "block" || (ev.BlockedAmount ?? 0m) > 0m))
            {
                _blockCharges--;
                if (_blockCharges == 0) StopBlock(warrior);
            }
        }
    }

    private void StopBlock(SimulationActorState actor)
    {
        if (_blockCharges > 0)
            actor.Stats.Add("block-chance-percent", -75m);
        _blockCharges = 0;
    }

    private void EndStand(SimulationActorState actor)
    {
        if (_standHealthBonus <= 0m) return;
        actor.InitializeHealth(Math.Max(1m, actor.MaximumHealth - _standHealthBonus),
            Math.Max(0m, actor.CurrentHealth - _standHealthBonus));
        _standHealthBonus = 0m;
    }

    private static void Expire(SimulationContext context, string key, decimal when, int generation)
    {
        if (when >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = when, Type = CombatEventType.RotationDecision, IsInternal = true,
            EffectKey = key, SourceActorKey = "warrior", TargetActorKey = "warrior",
            EncounterSequenceHitNumber = generation
        });
    }
}
