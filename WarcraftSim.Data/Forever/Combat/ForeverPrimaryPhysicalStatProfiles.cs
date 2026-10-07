using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Class-dependent physical primary-stat data exposed by the current
/// WoW: Forever client. Level 30 is the current beta cap. Level 60 values
/// are present in the client but are pre-release data and may still change.
/// </summary>
public static class ForeverPrimaryPhysicalStatProfiles
{
    public const decimal ArmorPerAgility =
        2m;

    private static readonly IReadOnlyDictionary<string,
        ForeverPrimaryPhysicalClassStatProfile>
        Profiles =
            new Dictionary<string,
                ForeverPrimaryPhysicalClassStatProfile>(
                    StringComparer.OrdinalIgnoreCase
                )
            {
                [ForeverCharacterClassKeys.Warrior] =
                    Create(
                        ForeverCharacterClassKeys.Warrior,
                        attackPowerPerStrength:
                            2m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            2m,
                        agilityPerCritAt30:
                            10.4m,
                        agilityPerCritAt60:
                            20.0m
                    ),

                [ForeverCharacterClassKeys.Paladin] =
                    Create(
                        ForeverCharacterClassKeys.Paladin,
                        attackPowerPerStrength:
                            2m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            10.7m,
                        agilityPerCritAt60:
                            19.8m
                    ),

                [ForeverCharacterClassKeys.Hunter] =
                    Create(
                        ForeverCharacterClassKeys.Hunter,
                        attackPowerPerStrength:
                            1m,
                        attackPowerPerAgility:
                            1m,
                        rangedAttackPowerPerAgility:
                            2m,
                        agilityPerCritAt30:
                            24.0m,
                        agilityPerCritAt60:
                            52.9m
                    ),

                [ForeverCharacterClassKeys.Rogue] =
                    Create(
                        ForeverCharacterClassKeys.Rogue,
                        attackPowerPerStrength:
                            1m,
                        attackPowerPerAgility:
                            1m,
                        rangedAttackPowerPerAgility:
                            2m,
                        agilityPerCritAt30:
                            13.0m,
                        agilityPerCritAt60:
                            29.0m
                    ),

                [ForeverCharacterClassKeys.Priest] =
                    Create(
                        ForeverCharacterClassKeys.Priest,
                        attackPowerPerStrength:
                            1m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            14.0m,
                        agilityPerCritAt60:
                            20.0m
                    ),

                [ForeverCharacterClassKeys.Shaman] =
                    Create(
                        ForeverCharacterClassKeys.Shaman,
                        attackPowerPerStrength:
                            2m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            11.5m,
                        agilityPerCritAt60:
                            19.7m
                    ),

                [ForeverCharacterClassKeys.Mage] =
                    Create(
                        ForeverCharacterClassKeys.Mage,
                        attackPowerPerStrength:
                            1m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            14.5m,
                        agilityPerCritAt60:
                            19.5m
                    ),

                [ForeverCharacterClassKeys.Warlock] =
                    Create(
                        ForeverCharacterClassKeys.Warlock,
                        attackPowerPerStrength:
                            1m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            12.0m,
                        agilityPerCritAt60:
                            20.0m
                    ),

                [ForeverCharacterClassKeys.Druid] =
                    Create(
                        ForeverCharacterClassKeys.Druid,
                        attackPowerPerStrength:
                            2m,
                        attackPowerPerAgility:
                            0m,
                        rangedAttackPowerPerAgility:
                            0m,
                        agilityPerCritAt30:
                            11.2m,
                        agilityPerCritAt60:
                            20.0m
                    )
            };

    public static ForeverPrimaryPhysicalClassStatProfile GetRequired(
        string classKey)
    {
        if (string.IsNullOrWhiteSpace(
                classKey))
        {
            throw new ArgumentException(
                "Forever class key is required.",
                nameof(classKey)
            );
        }

        if (Profiles.TryGetValue(
                classKey,
                out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException(
            $"No Forever primary physical stat profile exists for class '{classKey}'."
        );
    }

    public static decimal GetAgilityPerCriticalPercent(
        string classKey,
        int level)
    {
        var profile =
            GetRequired(
                classKey
            );

        if (profile.AgilityPerCriticalPercentByLevel.TryGetValue(
                level,
                out var agilityPerPercent))
        {
            return agilityPerPercent;
        }

        throw new InvalidOperationException(
            $"Forever Agility-to-critical conversion for class '{classKey}' at level {level} is not configured yet. " +
            "CP81 intentionally includes only the current beta-cap level 30 and the level-60 values embedded in the client rather than interpolating unverified values."
        );
    }

    private static ForeverPrimaryPhysicalClassStatProfile Create(
        string classKey,
        decimal attackPowerPerStrength,
        decimal attackPowerPerAgility,
        decimal rangedAttackPowerPerAgility,
        decimal agilityPerCritAt30,
        decimal agilityPerCritAt60)
    {
        return new ForeverPrimaryPhysicalClassStatProfile
        {
            ClassKey =
                classKey,

            AttackPowerPerStrength =
                attackPowerPerStrength,

            AttackPowerPerAgility =
                attackPowerPerAgility,

            RangedAttackPowerPerAgility =
                rangedAttackPowerPerAgility,

            AgilityPerCriticalPercentByLevel =
                new Dictionary<int, decimal>
                {
                    [30] =
                        agilityPerCritAt30,

                    [60] =
                        agilityPerCritAt60
                }
        };
    }
}
