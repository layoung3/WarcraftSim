using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// Shadow-specific reactive effects implemented as event listeners, not a
/// duplicate damage engine: personal Shadow Weaving, Vampiric Embrace,
/// Devouring Plague self healing and SW:D backlash.
/// </summary>
public sealed class ForeverPriestShadowReactiveProcessor : ICombatEventProcessor
{
    private readonly string _priestKey;
    private readonly AuraManager _auraManager;
    private readonly ForeverPriestShadowTalentBuild _talents;
    private readonly ForeverPriestShadowTuning _tuning;
    private readonly decimal _baseShadowBonus;

    public int MaximumStacksReached { get; private set; }

    public ForeverPriestShadowReactiveProcessor(string priestKey, AuraManager auraManager,
        ForeverPriestShadowTalentBuild talents, ForeverPriestShadowTuning tuning,
        decimal baseShadowBonus)
    {
        _priestKey = priestKey;
        _auraManager = auraManager;
        _talents = talents;
        _tuning = tuning;
        _baseShadowBonus = baseShadowBonus;
    }

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (e.Type == CombatEventType.AuraRemoved &&
            string.Equals(e.TargetActorKey, _priestKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.EffectKey, "shadow-weaving-personal", StringComparison.OrdinalIgnoreCase))
        {
            RefreshWeavingBonus(context);
            return;
        }

        if (e.Type != CombatEventType.Damage || e.Amount is not > 0m ||
            !string.Equals(e.SourceActorKey, _priestKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.AbilityKey, "shadow-word-death-backlash", StringComparison.OrdinalIgnoreCase))
            return;

        var priest = context.GetActor(_priestKey);
        var victim = e.TargetActorKey is null ? null : context.GetActor(e.TargetActorKey);
        if (priest is null || victim is null || priest.Key == victim.Key ||
            !string.Equals(e.SchoolKey, "shadow", StringComparison.OrdinalIgnoreCase)) return;

        if (e.AbilityKey == "shadow-word-death" && victim.IsAlive)
        {
            // WoW Forever SW:D backlash is 10% MAXIMUM caster health;
            // it is not a percentage of this spell's dealt damage.
            var reflected = priest.TakeDamage(priest.MaximumHealth *
                Math.Max(0m, _tuning.ShadowWordDeathBacklashFraction));
            if (reflected > 0m)
            {
                context.EmitEvent(new CombatEvent
                {
                    TimeSeconds = context.CurrentTimeSeconds,
                    Type = CombatEventType.Damage,
                    SourceActorKey = priest.Key, TargetActorKey = priest.Key,
                    AbilityKey = "shadow-word-death-backlash", SchoolKey = "shadow",
                    Amount = reflected, RawAmount = reflected,
                    Description = $"Shadow Word: Death backlash dealt {reflected} to its caster."
                });
                if (!priest.IsAlive)
                    context.EmitEvent(new CombatEvent
                    {
                        TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ActorDied,
                        SourceActorKey = priest.Key, TargetActorKey = priest.Key,
                        AbilityKey = "shadow-word-death-backlash"
                    });
            }
        }

        // Vampiric Embrace only benefits the Priest/party on Shadow damage
        // against the afflicted target. Solo preview has one player actor.
        var vampActive = victim.ActiveAuras.Any(a => a.IsActiveAt(context.CurrentTimeSeconds) &&
            a.Definition.Key == "vampiric-embrace" && a.SourceActorKey == priest.Key);
        if (vampActive)
            ApplySelfHealing(context, priest, e.Amount.Value * _tuning.VampiricEmbraceHealingFraction,
                "vampiric-embrace-heal");
        if (e.AbilityKey == "devouring-plague")
            ApplySelfHealing(context, priest, e.Amount.Value, "devouring-plague-heal");

        // In Forever, Shadow Weaving is PERSONAL, not a raid-wide boss debuff.
        var weaving = _talents.Rank("shadow-weaving");
        if (weaving > 0 && priest.IsAlive &&
            context.Random.NextDouble() * 100.0 <
                (weaving switch { 1 => 33.0, 2 => 67.0, _ => 100.0 }))
        {
            var aura = _auraManager.ApplyAura(context, priest, new AuraDefinition
            {
                Key = "shadow-weaving-personal", Name = "Shadow Weaving (personal)",
                DurationSeconds = 15m, StackingMode = AuraStackingMode.Stack, MaxStacks = 5
            }, priest.Key, e.AbilityKey, "shadow-weaving-personal");
            MaximumStacksReached = Math.Max(MaximumStacksReached, aura.Stacks);
            RefreshWeavingBonus(context);
        }
    }

    private void RefreshWeavingBonus(SimulationContext context)
    {
        var priest = context.GetActor(_priestKey);
        if (priest is null) return;
        var stacks = priest.ActiveAuras
            .Where(a => a.Definition.Key == "shadow-weaving-personal" &&
                a.IsActiveAt(context.CurrentTimeSeconds))
            .Sum(a => a.Stacks);
        priest.Stats.Set("shadow-damage-bonus-percent",
            _baseShadowBonus + Math.Min(5, stacks) * _tuning.ShadowWeavingDamagePerStackPercent);
    }

    private static void ApplySelfHealing(SimulationContext context,
        SimulationActorState priest, decimal amount, string abilityKey)
    {
        if (!priest.IsAlive || amount <= 0m) return;
        var healed = priest.Heal(amount);
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Healing,
            SourceActorKey = priest.Key, TargetActorKey = priest.Key,
            AbilityKey = abilityKey, SchoolKey = "shadow",
            RawAmount = amount, Amount = healed, OverhealingAmount = amount - healed,
            Description = $"{abilityKey} restored {healed} health."
        });
    }
}
