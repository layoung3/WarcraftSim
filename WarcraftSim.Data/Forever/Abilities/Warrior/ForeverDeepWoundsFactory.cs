using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Forever beta Deep Wounds. October 8 developer notes confirm that its bleed
/// is rolling and does not scale with AP. Client rank text specifies 20/40/60%
/// of the triggering weapon's average *base* damage over 12 seconds.
/// A 2-second tick cadence is configurable and remains a beta assumption.
/// </summary>
public static class ForeverDeepWoundsFactory
{
    public const string ProcKey = "deep-wounds-on-critical";
    public const string AbilityKey = "deep-wounds";
    public const string AuraKey = "deep-wounds-bleed";

    public static CriticalStrikeRollingDamageDefinition Create(
        int talentRank,
        decimal mainHandMinimumBaseDamage,
        decimal mainHandMaximumBaseDamage,
        decimal? offHandMinimumBaseDamage = null,
        decimal? offHandMaximumBaseDamage = null,
        decimal tickIntervalSeconds = 2m)
    {
        if (talentRank < 1 || talentRank > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(talentRank),
                "Deep Wounds has three talent ranks (1-3).");
        }

        if (mainHandMinimumBaseDamage < 0m || mainHandMaximumBaseDamage <
            mainHandMinimumBaseDamage)
        {
            throw new ArgumentOutOfRangeException(nameof(mainHandMinimumBaseDamage),
                "Main-hand weapon damage range must be nonnegative and ordered.");
        }

        if (offHandMinimumBaseDamage.HasValue != offHandMaximumBaseDamage.HasValue)
        {
            throw new ArgumentException("Both off-hand base damage values must be supplied together.");
        }

        if (offHandMinimumBaseDamage is < 0m ||
            (offHandMinimumBaseDamage.HasValue &&
             offHandMaximumBaseDamage < offHandMinimumBaseDamage))
        {
            throw new ArgumentOutOfRangeException(nameof(offHandMinimumBaseDamage),
                "Off-hand weapon damage range must be nonnegative and ordered.");
        }

        if (tickIntervalSeconds <= 0m || 12m % tickIntervalSeconds != 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(tickIntervalSeconds),
                "Deep Wounds ticks must divide its 12-second duration evenly.");
        }

        var effect = new AbilityEffectDefinition
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy,
            AuraKey = AuraKey,
            SchoolKey = "physical",
            ResolutionType = CombatResolutionTypes.AlwaysHits,
            MitigationType = DamageMitigationTypes.None,
            CanMiss = false,
            CanCrit = false,
            DurationSeconds = 12m,
            TickIntervalSeconds = tickIntervalSeconds,
            IncludeExpirationBoundaryTick = true,
            Tags = ["bleed", "deep-wounds", "rolling"]
        };

        var definition = new CriticalStrikeRollingDamageDefinition
        {
            Key = ProcKey,
            DamageFractionOfWeaponAverage = talentRank * 0.20m,
            PeriodicEffectKey = effect.Key,
            PeriodicAbility = new AbilityDefinition
            {
                Key = AbilityKey,
                Name = "Deep Wounds",
                ClassKey = ForeverCharacterClassKeys.Warrior,
                IsPassive = true,
                IsOffGlobalCooldown = true,
                GlobalCooldownSeconds = 0m,
                Effects = [effect]
            }
        };

        definition.AverageBaseWeaponDamageByHand[WeaponHandKeys.MainHand] =
            (mainHandMinimumBaseDamage + mainHandMaximumBaseDamage) / 2m;

        if (offHandMinimumBaseDamage.HasValue)
        {
            definition.AverageBaseWeaponDamageByHand[WeaponHandKeys.OffHand] =
                (offHandMinimumBaseDamage.Value + offHandMaximumBaseDamage!.Value) / 2m;
        }

        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeAuto);
        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto);
        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeSpecial);

        return definition;
    }
}
