using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Backward-compatible Warrior-preview adapter for the shared Core boss melee
/// stream. The legacy solo mode intentionally scores all incoming damage
/// without depleting Warrior HP; raid-mode callers use real health instead.
/// </summary>
public sealed class WarriorBossAttackProcessor : ICombatEventProcessor
{
    public const string SwingEffect = EncounterBossMeleeProcessor.SwingEffectKey;
    private readonly EncounterBossMeleeProcessor _bossMelee;

    public WarriorBossAttackProcessor(WarriorSimRequest settings,
        ICombatRollResolver rolls, IDamageMitigationResolver mitigation,
        Func<decimal, bool>? thunderClapActive = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _bossMelee = new EncounterBossMeleeProcessor(
            new EncounterBossMeleeDefinition
            {
                BossActorKey = "target",
                PreferredTargetActorKey = "warrior",
                AbilityKey = "boss-main-hand-auto",
                TargetMode = EncounterBossMeleeTargetModes.Fixed,
                FirstSwingSeconds = settings.BossFirstSwingSeconds,
                SwingSpeedSeconds = settings.BossSwingSpeedSeconds,
                MinimumDamage = settings.BossSwingMinimumDamage,
                MaximumDamage = settings.BossSwingMaximumDamage,
                CanCrush = settings.BossMeleeCrushPercent > 0m,
                EnableBossParryHaste = settings.BossParryHaste,
                PreserveTargetHealth = true,
                ResolutionType = ForeverCombatResolutionTypes.CreatureMeleeAuto,
                TargetDefenseSkillStatKey = ForeverCombatStatKeys.DefenseSkill
            }, rolls, mitigation,
            intervalMultiplier: ctx => thunderClapActive?.Invoke(ctx.CurrentTimeSeconds) == true
                ? 1.20m : 1m,
            rawDamageModifier: (context, actor, raw) =>
            {
                if (actor.ActiveAuras.Any(a =>
                    a.Definition.Key == ForeverWarriorAuraKeys.BerserkerStance &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
                    raw *= 1.10m;
                else if (actor.ActiveAuras.Any(a =>
                    a.Definition.Key == ForeverWarriorAuraKeys.DefensiveStance &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
                    raw *= 0.90m;
                if (actor.Stats.Get(WarriorProtectionProcessor.ShieldWallActiveStat) > 0m)
                    raw *= 0.40m;
                return raw;
            });
    }

    public void ScheduleFirst(SimulationContext context)
    {
        // Preserve the previous opt-in behavior of solo boss attacks.
        _bossMelee.ScheduleFirst(context);
    }

    public void Process(SimulationContext context, CombatEvent combatEvent) =>
        _bossMelee.Process(context, combatEvent);
}
