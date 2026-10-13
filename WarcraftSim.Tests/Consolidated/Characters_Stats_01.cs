// Consolidated tests for Characters Stats.
// Original class names are retained for xUnit test filtering.

// ==================== BaseStatsCharacterSimulationRuntimeCalculatorTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;

public sealed class BaseStatsCharacterSimulationRuntimeCalculatorTests
{
    [Fact]
    public void Calculate_UsesResolvedHealthAndCopiesBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            4200m
        );

        profile.BaseStats.Set(
            "strength",
            150m
        );

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    character =>
                        character.Level * 100m
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            7000m,
            result.MaximumHealth
        );

        Assert.Null(
            result.StartingHealth
        );

        Assert.Equal(
            4200m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_CreatesIndependentEffectiveStatCollection()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        8000m
            );

        var result =
            calculator.Calculate(
                profile
            );

        result.EffectiveStats.Set(
            "armor",
            9000m
        );

        Assert.Equal(
            3000m,
            profile.BaseStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            9000m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Calculate_UsesOptionalStartingHealthResolver()
    {
        var profile =
            CreateProfile();

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        10000m,

                startingHealthResolver:
                    _ =>
                        7500m
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            10000m,
            result.MaximumHealth
        );

        Assert.Equal(
            7500m,
            result.StartingHealth
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_RejectsInvalidMaximumHealth(
        int maximumHealth)
    {
        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        maximumHealth
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                calculator.Calculate(
                    CreateProfile()
                )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Calculate_RejectsInvalidStartingHealth(
        int startingHealth)
    {
        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        10000m,

                startingHealthResolver:
                    _ =>
                        startingHealth
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                calculator.Calculate(
                    CreateProfile()
                )
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "38aaf89e-917a-489b-97a8-f14acbbf9f88"
                ),

            Name =
                "Base Stats Character",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }
}
}

// ==================== CharacterSimulationRuntimeCalculatorTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class CharacterSimulationRuntimeCalculatorTests
{
    [Fact]
    public void Build_UsesCalculatedHealthAndStatsWhenCalculatorIsConfigured()
    {
        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            7200m
        );

        effectiveStats.Set(
            "fire-resistance",
            90m
        );

        var calculator =
            new StubRuntimeCalculator(
                new CharacterSimulationRuntimeCalculationResult
                {
                    MaximumHealth =
                        18000m,

                    StartingHealth =
                        15000m,

                    EffectiveStats =
                        effectiveStats
                }
            );

        var builder =
            CreateBuilder(
                calculator
            );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        CreateProfile(),

                    // MaximumHealth intentionally omitted here. The runtime
                    // calculator now owns calculated health and final stats.
                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            ActorKey =
                                "calculated-tank",

                            TeamKey =
                                "raid",

                            AssignedRole =
                                SimulationType.Tank
                        }
                }
            );

        Assert.Equal(
            18000m,
            result.Actor.MaximumHealth
        );

        Assert.Equal(
            15000m,
            result.Actor.CurrentHealth
        );

        Assert.Equal(
            7200m,
            result.Actor.Stats.Get(
                "armor"
            )
        );

        Assert.Equal(
            90m,
            result.Actor.Stats.Get(
                "fire-resistance"
            )
        );

        Assert.Equal(
            "calculated-tank",
            result.Actor.Key
        );

        Assert.Equal(
            SimulationType.Tank,
            result.Actor.AssignedRole
        );

        Assert.Equal(
            1,
            calculator.CallCount
        );
    }

    [Fact]
    public void Build_PreservesRuntimeCalculationAndContributionBreakdown()
    {
        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            4200m
        );

        var calculation =
            new CharacterSimulationRuntimeCalculationResult
            {
                MaximumHealth =
                    12000m,

                EffectiveStats =
                    effectiveStats,

                StatContributions =
                [
                    new CharacterSimulationStatContribution(
                        contributorKey:
                            "character-bonuses",

                        sourceKey:
                            "helmet",

                        sourceType:
                            "gear",

                        sourceName:
                            "Heavy Helmet",

                        statKey:
                            "armor",

                        amount:
                            1200m
                    )
                ]
            };

        var result =
            CreateBuilder(
                    new StubRuntimeCalculator(
                        calculation
                    )
                )
                .Build(
                    new CharacterSimulationBuildRequest
                    {
                        Profile =
                            CreateProfile(),

                        Options =
                            new CharacterSimulationMappingOptions()
                    }
                );

        Assert.Same(
            calculation,
            result.RuntimeCalculation
        );

        var contribution =
            Assert.Single(
                result.RuntimeCalculation!
                    .StatContributions
            );

        Assert.Equal(
            "helmet",
            contribution.SourceKey
        );

        Assert.Equal(
            1200m,
            contribution.Amount
        );
    }

    [Fact]
    public void Build_CalculatedValuesOverrideTemporaryManualCombatValues()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            1000m
        );

        var calculatedStats =
            new StatCollection();

        calculatedStats.Set(
            "armor",
            6400m
        );

        var builder =
            CreateBuilder(
                new StubRuntimeCalculator(
                    new CharacterSimulationRuntimeCalculationResult
                    {
                        MaximumHealth =
                            16000m,

                        EffectiveStats =
                            calculatedStats
                    }
                )
            );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        profile,

                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            // These values represent the old/manual path.
                            // When a calculator is configured, calculated
                            // combat values take precedence.
                            MaximumHealth =
                                5000m,

                            StartingHealth =
                                2500m,

                            EffectiveStats =
                                profile.BaseStats
                        }
                }
            );

        Assert.Equal(
            16000m,
            result.Actor.MaximumHealth
        );

        Assert.Equal(
            16000m,
            result.Actor.CurrentHealth
        );

        Assert.Equal(
            6400m,
            result.Actor.Stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Build_RejectsInvalidCalculatedHealth()
    {
        var builder =
            CreateBuilder(
                new StubRuntimeCalculator(
                    new CharacterSimulationRuntimeCalculationResult
                    {
                        MaximumHealth =
                            0m
                    }
                )
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                builder.Build(
                    new CharacterSimulationBuildRequest
                    {
                        Profile =
                            CreateProfile(),

                        Options =
                            new CharacterSimulationMappingOptions()
                    }
                )
        );
    }

    private static CharacterSimulationActorBuilder CreateBuilder(
        ICharacterSimulationRuntimeCalculator runtimeCalculator)
    {
        var definition =
            new CharacterSimulationClassDefinition
            {
                RulesetKey =
                    "development",

                ClassKey =
                    "warrior"
            };

        return new CharacterSimulationActorBuilder(
            new CharacterSimulationClassCatalog(
                [
                    definition
                ]
            ),
            runtimeCalculator
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "205c8785-2d2b-4d47-9dc6-2e6db097b7ba"
                ),

            Name =
                "Calculated Warrior",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }

    private sealed class StubRuntimeCalculator :
        ICharacterSimulationRuntimeCalculator
    {
        private readonly CharacterSimulationRuntimeCalculationResult
            _result;

        public StubRuntimeCalculator(
            CharacterSimulationRuntimeCalculationResult result)
        {
            _result =
                result;
        }

        public int CallCount { get; private set; }

        public CharacterSimulationRuntimeCalculationResult Calculate(
            CharacterProfile profile)
        {
            CallCount++;

            return _result;
        }
    }
}
}

// ==================== CharacterSimulationStatContributionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class CharacterSimulationStatContributionTests
{
    [Fact]
    public void Calculate_ReportsBaseAndSourceContributionsPerStat()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    new CharacterSimulationStatBonusSource(
                        key:
                            "helmet",

                        sourceType:
                            "gear",

                        name:
                            "Heavy Helmet",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    900m
                            }
                    ),

                    new CharacterSimulationStatBonusSource(
                        key:
                            "armor-enchant",

                        sourceType:
                            "enchant",

                        name:
                            "Armor Enchant",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    300m
                            }
                    )
                ]
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            4200m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        var armorContributions =
            result.StatContributions
                .Where(
                    contribution =>
                        string.Equals(
                            contribution.StatKey,
                            "armor",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .ToArray();

        Assert.Equal(
            3,
            armorContributions.Length
        );

        Assert.Equal(
            3000m,
            armorContributions.Single(
                contribution =>
                    contribution.SourceType ==
                        "base"
            ).Amount
        );

        var helmet =
            armorContributions.Single(
                contribution =>
                    contribution.SourceKey ==
                        "helmet"
            );

        Assert.Equal(
            "character-bonuses",
            helmet.ContributorKey
        );

        Assert.Equal(
            "gear",
            helmet.SourceType
        );

        Assert.Equal(
            "Heavy Helmet",
            helmet.SourceName
        );

        Assert.Equal(
            900m,
            helmet.Amount
        );

        Assert.Equal(
            300m,
            armorContributions.Single(
                contribution =>
                    contribution.SourceKey ==
                        "armor-enchant"
            ).Amount
        );

        Assert.Equal(
            result.EffectiveStats.Get(
                "armor"
            ),
            armorContributions.Sum(
                contribution =>
                    contribution.Amount
            )
        );
    }

    [Fact]
    public void Calculate_OmitsInactiveAndZeroValueSourceContributions()
    {
        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    new CharacterSimulationStatBonusSource(
                        key:
                            "active",

                        sourceType:
                            "gear",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    25m,

                                ["armor"] =
                                    0m
                            }
                    ),

                    new CharacterSimulationStatBonusSource(
                        key:
                            "inactive",

                        sourceType:
                            "buff",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    100m
                            },

                        isActive:
                            false
                    )
                ]
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    CreateProfile()
                );

        var contribution =
            Assert.Single(
                result.StatContributions
            );

        Assert.Equal(
            "active",
            contribution.SourceKey
        );

        Assert.Equal(
            "strength",
            contribution.StatKey
        );

        Assert.Equal(
            25m,
            contribution.Amount
        );
    }

    [Fact]
    public void Calculate_UsesContributorDeltaFallbackForGenericContributor()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var contributor =
            new TestContributor(
                key:
                    "talent-conversion",

                order:
                    200,

                contribute:
                    (_, stats) =>
                        stats.Set(
                            "strength",
                            stats.Get(
                                "strength"
                            ) *
                            1.5m
                        )
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        var fallback =
            result.StatContributions.Single(
                contribution =>
                    contribution.ContributorKey ==
                        "talent-conversion"
            );

        Assert.Equal(
            "talent-conversion",
            fallback.SourceKey
        );

        Assert.Equal(
            "contributor",
            fallback.SourceType
        );

        Assert.Equal(
            50m,
            fallback.Amount
        );
    }

    [Fact]
    public void Calculate_ContributionBreakdownReconcilesToEffectiveStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        profile.BaseStats.Set(
            "armor",
            2000m
        );

        var calculator =
            CreateCalculator(
                new ExplicitStatBonusContributor(
                    key:
                        "manual-adjustments",

                    order:
                        100,

                    statBonuses:
                        new Dictionary<string, decimal>
                        {
                            ["strength"] =
                                20m,

                            ["armor"] =
                                -250m,

                            ["hit-rating"] =
                                15m
                        }
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        foreach (
            var statKey in
            result.EffectiveStats.Values.Keys)
        {
            var explainedValue =
                result.StatContributions
                    .Where(
                        contribution =>
                            string.Equals(
                                contribution.StatKey,
                                statKey,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    .Sum(
                        contribution =>
                            contribution.Amount
                    );

            Assert.Equal(
                result.EffectiveStats.Get(
                    statKey
                ),
                explainedValue
            );
        }
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator(
            params ICharacterSimulationStatContributor[] contributors)
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    10000m,

            statContributors:
                contributors
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "73927f9d-c2d4-44f3-8aed-3437a692df15"
                ),

            Name =
                "Contribution Character",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }

    private sealed class TestContributor :
        ICharacterSimulationStatContributor
    {
        private readonly Action<CharacterProfile, StatCollection>
            _contribute;

        public TestContributor(
            string key,
            int order,
            Action<CharacterProfile, StatCollection> contribute)
        {
            Key =
                key;

            Order =
                order;

            _contribute =
                contribute;
        }

        public string Key { get; }

        public int Order { get; }

        public void Contribute(
            CharacterProfile profile,
            StatCollection effectiveStats)
        {
            _contribute(
                profile,
                effectiveStats
            );
        }
    }
}
}

// ==================== CharacterSimulationStatContributorTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class CharacterSimulationStatContributorTests
{
    [Fact]
    public void Calculate_AppliesContributorsAfterBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "gear",

                    order:
                        100,

                    contribute:
                        (_, stats) =>
                        {
                            stats.Set(
                                "armor",
                                stats.Get(
                                    "armor"
                                ) +
                                1500m
                            );

                            stats.Set(
                                "strength",
                                stats.Get(
                                    "strength"
                                ) +
                                25m
                            );
                        }
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            4500m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            125m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_AppliesContributorsInConfiguredOrder()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var multiply =
            new TestContributor(
                key:
                    "talent-conversion",

                order:
                    200,

                contribute:
                    (_, stats) =>
                        stats.Set(
                            "strength",
                            stats.Get(
                                "strength"
                            ) *
                            2m
                        )
            );

        var add =
            new TestContributor(
                key:
                    "gear",

                order:
                    100,

                contribute:
                    (_, stats) =>
                        stats.Set(
                            "strength",
                            stats.Get(
                                "strength"
                            ) +
                            50m
                        )
            );

        var calculator =
            CreateCalculator(
                // Intentionally provided in reverse order. The pipeline
                // should still run gear first because its Order is lower.
                multiply,
                add
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            300m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_ContributorCanUseCharacterContext()
    {
        var profile =
            CreateProfile();

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "level-scaling",

                    order:
                        100,

                    contribute:
                        (character, stats) =>
                            stats.Set(
                                "level-derived-stat",
                                character.Level *
                                10m
                            )
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            700m,
            result.EffectiveStats.Get(
                "level-derived-stat"
            )
        );
    }

    [Fact]
    public void Calculate_ContributorsDoNotMutateStoredBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            2500m
        );

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "gear",

                    order:
                        100,

                    contribute:
                        (_, stats) =>
                            stats.Set(
                                "armor",
                                6000m
                            )
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            2500m,
            profile.BaseStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            6000m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_RejectsDuplicateContributorKeys()
    {
        Assert.Throws<ArgumentException>(
            () =>
                CreateCalculator(
                    new TestContributor(
                        key:
                            "gear",

                        order:
                            100,

                        contribute:
                            (_, _) =>
                            {
                            }
                    ),

                    new TestContributor(
                        key:
                            "GEAR",

                        order:
                            200,

                        contribute:
                            (_, _) =>
                            {
                            }
                    )
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator(
            params ICharacterSimulationStatContributor[] contributors)
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    10000m,

            statContributors:
                contributors
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "b8f8b786-3f41-4c90-908a-cfbccae46f7a"
                ),

            Name =
                "Contributor Character",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }

    private sealed class TestContributor :
        ICharacterSimulationStatContributor
    {
        private readonly Action<CharacterProfile, StatCollection>
            _contribute;

        public TestContributor(
            string key,
            int order,
            Action<CharacterProfile, StatCollection> contribute)
        {
            Key =
                key;

            Order =
                order;

            _contribute =
                contribute;
        }

        public string Key { get; }

        public int Order { get; }

        public void Contribute(
            CharacterProfile profile,
            StatCollection effectiveStats)
        {
            _contribute(
                profile,
                effectiveStats
            );
        }
    }
}
}

// ==================== ExplicitStatBonusContributorTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class ExplicitStatBonusContributorTests
{
    [Fact]
    public void Contribute_AddsBonusToExistingStat()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "armor",
            3000m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["armor"] =
                        1200m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            4200m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Contribute_AddsPreviouslyMissingStat()
    {
        var stats =
            new StatCollection();

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["spell-power"] =
                        35m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            35m,
            stats.Get(
                "spell-power"
            )
        );
    }

    [Fact]
    public void Contribute_AppliesMultipleStatBonuses()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "stamina",
            100m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["stamina"] =
                        20m,

                    ["hit-rating"] =
                        15m,

                    ["fire-resistance"] =
                        10m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            120m,
            stats.Get(
                "stamina"
            )
        );

        Assert.Equal(
            15m,
            stats.Get(
                "hit-rating"
            )
        );

        Assert.Equal(
            10m,
            stats.Get(
                "fire-resistance"
            )
        );
    }

    [Fact]
    public void Contribute_AllowsNegativeAdjustments()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "armor",
            5000m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["armor"] =
                        -500m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            4500m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_CopiesBonusDictionary()
    {
        var source =
            new Dictionary<string, decimal>
            {
                ["armor"] =
                    1000m
            };

        var contributor =
            CreateContributor(
                source
            );

        source["armor"] =
            9000m;

        var stats =
            new StatCollection();

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            1000m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_RejectsBlankStatKey()
    {
        var bonuses =
            new Dictionary<string, decimal>
            {
                [""] =
                    100m
            };

        Assert.Throws<ArgumentException>(
            () =>
                CreateContributor(
                    bonuses
                )
        );
    }

    private static ExplicitStatBonusContributor CreateContributor(
        IReadOnlyDictionary<string, decimal> bonuses)
    {
        return new ExplicitStatBonusContributor(
            key:
                "test-bonuses",

            order:
                100,

            statBonuses:
                bonuses
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "9b928a16-5f86-4bb4-a03c-8d8701cf0642"
                ),

            Name =
                "Stat Bonus Character",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }
}
}
