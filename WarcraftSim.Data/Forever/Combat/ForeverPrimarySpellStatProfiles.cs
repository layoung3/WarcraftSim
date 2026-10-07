using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Class-and-level Intellect-to-spell-critical data exposed by the current
/// WoW: Forever client's PlayerExpectedStat table. Level 30 is the current
/// beta cap. Level 60 values are client-embedded pre-release data and may
/// change before that level becomes playable.
/// </summary>
public static class ForeverPrimarySpellStatProfiles
{
    private static readonly IReadOnlyDictionary<string,
        ForeverPrimarySpellClassStatProfile>
        Profiles =
            new Dictionary<string,
                ForeverPrimarySpellClassStatProfile>(
                    StringComparer.OrdinalIgnoreCase
                )
            {
                [ForeverCharacterClassKeys.Warrior] =
                    CreateWithoutMana(
                        ForeverCharacterClassKeys.Warrior
                    ),

                [ForeverCharacterClassKeys.Paladin] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Paladin,
                        intellectPerSpellCritAt30:
                            31.9m,
                        intellectPerSpellCritAt60:
                            59.9m
                    ),

                [ForeverCharacterClassKeys.Hunter] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Hunter,
                        intellectPerSpellCritAt30:
                            32.9m,
                        intellectPerSpellCritAt60:
                            60.6m
                    ),

                [ForeverCharacterClassKeys.Rogue] =
                    CreateWithoutMana(
                        ForeverCharacterClassKeys.Rogue
                    ),

                [ForeverCharacterClassKeys.Priest] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Priest,
                        intellectPerSpellCritAt30:
                            26.9m,
                        intellectPerSpellCritAt60:
                            59.5m
                    ),

                [ForeverCharacterClassKeys.Shaman] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Shaman,
                        intellectPerSpellCritAt30:
                            28.2m,
                        intellectPerSpellCritAt60:
                            59.2m
                    ),

                [ForeverCharacterClassKeys.Mage] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Mage,
                        intellectPerSpellCritAt30:
                            27.3m,
                        intellectPerSpellCritAt60:
                            59.5m
                    ),

                [ForeverCharacterClassKeys.Warlock] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Warlock,
                        intellectPerSpellCritAt30:
                            28.2m,
                        intellectPerSpellCritAt60:
                            60.6m
                    ),

                [ForeverCharacterClassKeys.Druid] =
                    CreateManaClass(
                        ForeverCharacterClassKeys.Druid,
                        intellectPerSpellCritAt30:
                            28.4m,
                        intellectPerSpellCritAt60:
                            59.9m
                    )
            };

    public static ForeverPrimarySpellClassStatProfile GetRequired(
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
            $"No Forever primary spell stat profile exists for class '{classKey}'."
        );
    }

    public static decimal GetIntellectPerSpellCriticalPercent(
        string classKey,
        int level)
    {
        var profile =
            GetRequired(
                classKey
            );

        if (!profile.UsesMana)
        {
            throw new InvalidOperationException(
                $"Forever class '{classKey}' does not have client Intellect-to-spell-critical data."
            );
        }

        if (profile.IntellectPerSpellCriticalPercentByLevel.TryGetValue(
                level,
                out var intellectPerPercent))
        {
            return intellectPerPercent;
        }

        throw new InvalidOperationException(
            $"Forever Intellect-to-spell-critical conversion for class '{classKey}' at level {level} is not configured yet. " +
            "CP82 intentionally includes only the current beta-cap level 30 and the level-60 values embedded in the client rather than interpolating unverified values."
        );
    }

    private static ForeverPrimarySpellClassStatProfile CreateWithoutMana(
        string classKey)
    {
        return new ForeverPrimarySpellClassStatProfile
        {
            ClassKey =
                classKey,

            UsesMana =
                false
        };
    }

    private static ForeverPrimarySpellClassStatProfile CreateManaClass(
        string classKey,
        decimal intellectPerSpellCritAt30,
        decimal intellectPerSpellCritAt60)
    {
        return new ForeverPrimarySpellClassStatProfile
        {
            ClassKey =
                classKey,

            UsesMana =
                true,

            IntellectPerSpellCriticalPercentByLevel =
                new Dictionary<int, decimal>
                {
                    [30] =
                        intellectPerSpellCritAt30,

                    [60] =
                        intellectPerSpellCritAt60
                }
        };
    }
}
