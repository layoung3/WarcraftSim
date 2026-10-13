// Consolidated tests for Encounters Targeting.
// Original class names are retained for xUnit test filtering.

// ==================== EffectTargetResolverTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EffectTargetResolverTests
{
    [Fact]
    public void Resolve_SelfTargetsSourceInsteadOfPrimaryTarget()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "source",
                "raid"
            );

        var enemy =
            CreateActor(
                "enemy",
                "enemy"
            );

        context.AddActor(
            source
        );

        context.AddActor(
            enemy
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                enemy,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Self,

                    MaxTargets =
                        1
                }
            );

        var resolved =
            Assert.Single(
                targets
            );

        Assert.Same(
            source,
            resolved
        );
    }

    [Fact]
    public void Resolve_FriendlyPrioritizesPrimaryTargetThenUsesStableKeyOrder()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var primary =
            CreateActor(
                "tank",
                "raid"
            );

        var allyB =
            CreateActor(
                "z-ally",
                "raid"
            );

        var allyA =
            CreateActor(
                "a-ally",
                "raid"
            );

        var enemy =
            CreateActor(
                "enemy",
                "enemy"
            );

        // Intentionally add actors in an order that differs from key order.
        context.AddActor(
            source
        );

        context.AddActor(
            primary
        );

        context.AddActor(
            allyB
        );

        context.AddActor(
            enemy
        );

        context.AddActor(
            allyA
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                primary,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Friendly,

                    MaxTargets =
                        3
                }
            );

        Assert.Equal(
            new[]
            {
                "tank",
                "a-ally",
                "caster"
            },
            targets.Select(
                    target =>
                        target.Key
                )
                .ToArray()
        );
    }

    [Fact]
    public void Resolve_EnemyPrioritizesPrimaryAndExcludesFriendlyActors()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "source",
                "raid"
            );

        var primary =
            CreateActor(
                "boss",
                "enemy"
            );

        var addB =
            CreateActor(
                "z-add",
                "enemy"
            );

        var addA =
            CreateActor(
                "a-add",
                "enemy"
            );

        var ally =
            CreateActor(
                "ally",
                "raid"
            );

        context.AddActor(
            source
        );

        context.AddActor(
            addB
        );

        context.AddActor(
            ally
        );

        context.AddActor(
            primary
        );

        context.AddActor(
            addA
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                primary,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Enemy,

                    MaxTargets =
                        3
                }
            );

        Assert.Equal(
            new[]
            {
                "boss",
                "a-add",
                "z-add"
            },
            targets.Select(
                    target =>
                        target.Key
                )
                .ToArray()
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    5m,

                PrimaryActorKey =
                    "source"
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }
}
}

// ==================== EncounterDamageSequenceTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterDamageSequenceTests
{
    [Fact]
    public void SameSelection_HitsOneSelectedActorThreeTimes()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Dps
                    ],

                    Count =
                        1,

                    AllowDuplicateTargets =
                        false
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        1m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.SameSelection
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            [
                1m,
                2m,
                3m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.Single(
            hits.Select(
                    hit =>
                        hit.TargetActorKey
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void SequentialSelection_HitsThreeUniqueTargetsOnePerSecond()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Dps
                    ],

                    Count =
                        3,

                    AllowDuplicateTargets =
                        false
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        1m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.SequentialSelection
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            3,
            hits.Count
        );

        Assert.Equal(
            [
                1m,
                2m,
                3m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.Equal(
            3,
            hits.Select(
                    hit =>
                        hit.TargetActorKey
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .Count()
        );
    }

    [Fact]
    public void ReselectEachHit_CanHitTheSameEligibleActorRepeatedly()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Healing
                    ],

                    Count =
                        1,

                    AllowDuplicateTargets =
                        true
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        0.5m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.ReselectEachHit
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            [
                1m,
                1.5m,
                2m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.All(
            hits,
            hit =>
                Assert.Equal(
                    "healer",
                    hit.TargetActorKey
                )
        );
    }

    private static SimulationRunResult RunSequence(
        EncounterTargetSelectionDefinition selection,
        EncounterDamageSequenceDefinition sequence)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Encounter Damage Sequence Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    4m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "sequence-test",

                        Name =
                            "Sequence Test",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            selection,

                        Sequence =
                            sequence,

                        Amount =
                            100m,

                        SchoolKey =
                            "arcane"
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        encounter.DurationSeconds,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        context.AddActor(CreateActor("boss", "enemy", null));
        context.AddActor(CreateActor("tank", "raid", SimulationType.Tank));
        context.AddActor(CreateActor("healer", "raid", SimulationType.Healing));
        context.AddActor(CreateActor("dps-1", "raid", SimulationType.Dps));
        context.AddActor(CreateActor("dps-2", "raid", SimulationType.Dps));
        context.AddActor(CreateActor("dps-3", "raid", SimulationType.Dps));

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-sequence",

                    Version =
                        "1"
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    )
                ]
            );

        return engine.Run(
            context
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        SimulationType? assignedRole)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey,

                AssignedRole =
                    assignedRole,

                Level =
                    20
            };

        actor.InitializeHealth(
            5000m
        );

        return actor;
    }

    private static List<CombatEvent> GetSequenceHits(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:sequence-test",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .OrderBy(
                combatEvent =>
                    combatEvent.TimeSeconds
            )
            .ToList();
    }
}
}

// ==================== EncounterLiveSequenceTargetingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterLiveSequenceTargetingTests
{
    [Fact]
    public void ReselectEachHit_UsesOnlyActorsAliveAtThatHitTime()
    {
        var result =
            RunSequence(
                targetMode:
                    EncounterDamageSequenceTargetModes.ReselectEachHit,

                targetSelection:
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.AllMatchingActors,

                        TeamKey =
                            "raid",

                        AllowedRoles =
                        [
                            SimulationType.Dps
                        ]
                    },

                lockedActorKey:
                    null
            );

        var sequenceHits =
            GetSequenceDamage(
                result
            );

        var firstHitTargets =
            sequenceHits
                .Where(hit =>
                    hit.TimeSeconds == 1m)
                .Select(hit =>
                    hit.TargetActorKey)
                .ToList();

        var secondHitTargets =
            sequenceHits
                .Where(hit =>
                    hit.TimeSeconds == 2m)
                .Select(hit =>
                    hit.TargetActorKey)
                .ToList();

        Assert.Contains(
            "dps-1",
            firstHitTargets
        );

        Assert.Contains(
            "dps-2",
            firstHitTargets
        );

        // DPS 1 is killed at 1.5 seconds.
        Assert.DoesNotContain(
            "dps-1",
            secondHitTargets
        );

        Assert.Contains(
            "dps-2",
            secondHitTargets
        );
    }

    [Fact]
    public void SameSelection_DoesNotRetargetAfterLockedActorDies()
    {
        var result =
            RunSequence(
                targetMode:
                    EncounterDamageSequenceTargetModes.SameSelection,

                targetSelection:
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.FixedActor,

                        ActorKey =
                            "dps-1",

                        TeamKey =
                            "raid"
                    },

                lockedActorKey:
                    "dps-1"
            );

        var sequenceHits =
            GetSequenceDamage(
                result
            );

        Assert.Single(
            sequenceHits
        );

        Assert.Equal(
            1m,
            sequenceHits[0].TimeSeconds
        );

        Assert.Equal(
            "dps-1",
            sequenceHits[0].TargetActorKey
        );

        Assert.DoesNotContain(
            sequenceHits,
            hit =>
                string.Equals(
                    hit.TargetActorKey,
                    "dps-2",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static SimulationRunResult RunSequence(
        string targetMode,
        EncounterTargetSelectionDefinition targetSelection,
        string? lockedActorKey)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Live Sequence Targeting Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    3m,

                DamageEvents =
                [
                    new EncounterDamageEventDefinition
                    {
                        Key =
                            "kill-dps-1",

                        Name =
                            "Kill DPS One",

                        TimeSeconds =
                            1.5m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "dps-1",

                        Amount =
                            10000m,

                        SchoolKey =
                            "shadow"
                    }
                ],

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "live-sequence",

                        Name =
                            "Live Sequence",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            lockedActorKey ?? "",

                        TargetSelection =
                            targetSelection,

                        Sequence =
                            new EncounterDamageSequenceDefinition
                            {
                                HitCount =
                                    2,

                                HitIntervalSeconds =
                                    1m,

                                TargetMode =
                                    targetMode
                            },

                        Amount =
                            100m,

                        SchoolKey =
                            "arcane"
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        encounter.DurationSeconds,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        context.AddActor(
            CreateActor(
                "boss",
                "enemy",
                null
            )
        );

        context.AddActor(
            CreateActor(
                "dps-1",
                "raid",
                SimulationType.Dps
            )
        );

        context.AddActor(
            CreateActor(
                "dps-2",
                "raid",
                SimulationType.Dps
            )
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-live-targeting",

                    Version =
                        "1"
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    )
                ]
            );

        return engine.Run(
            context
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        SimulationType? assignedRole)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey,

                AssignedRole =
                    assignedRole,

                Level =
                    20
            };

        actor.InitializeHealth(
            5000m
        );

        return actor;
    }

    private static List<CombatEvent> GetSequenceDamage(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:live-sequence",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .OrderBy(
                combatEvent =>
                    combatEvent.TimeSeconds
            )
            .ToList();
    }
}
}

// ==================== EncounterSequenceSelectionHistoryTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterSequenceSelectionHistoryTests
{
    [Fact]
    public void UniqueAcrossSequence_DoesNotRepeatTargets()
    {
        var result =
            RunHistoryScenario(
                dpsCount: 3,
                hitCount: 3,
                selectionHistoryMode:
                    EncounterSequenceSelectionHistoryModes.UniqueAcrossSequence
            );

        var targets = GetHistoryTargets(result);

        Assert.Equal(3, targets.Count);
        Assert.Equal(
            3,
            targets.Distinct(StringComparer.OrdinalIgnoreCase).Count()
        );
    }

    [Fact]
    public void UniqueAcrossSequence_StopsProducingDamageWhenPoolIsExhausted()
    {
        var result =
            RunHistoryScenario(
                dpsCount: 2,
                hitCount: 3,
                selectionHistoryMode:
                    EncounterSequenceSelectionHistoryModes.UniqueAcrossSequence
            );

        var hits = GetHistoryDamage(result);

        Assert.Equal(2, hits.Count);
        Assert.Equal(
            2,
            hits
                .Select(hit => hit.TargetActorKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
        );
    }

    [Fact]
    public void AllowRepeats_CanSelectTheOnlyEligibleTargetEveryHit()
    {
        var selection =
            new EncounterTargetSelectionDefinition
            {
                Mode = EncounterTargetSelectionModes.RandomMatchingActors,
                TeamKey = "raid",
                AllowedRoles = [SimulationType.Healing],
                Count = 1,
                AllowDuplicateTargets = true
            };

        var encounter =
            BuildEncounter(
                selection,
                hitCount: 3,
                selectionHistoryMode:
                    EncounterSequenceSelectionHistoryModes.AllowRepeats
            );

        var context = CreateContext(encounter);
        context.AddActor(CreateActor("boss", "enemy", null));
        context.AddActor(CreateActor("healer", "raid", SimulationType.Healing));

        var targets = GetHistoryTargets(Run(context));

        Assert.Equal(3, targets.Count);
        Assert.All(targets, target => Assert.Equal("healer", target));
    }

    private static SimulationRunResult RunHistoryScenario(
        int dpsCount,
        int hitCount,
        string selectionHistoryMode)
    {
        var selection =
            new EncounterTargetSelectionDefinition
            {
                Mode = EncounterTargetSelectionModes.RandomMatchingActors,
                TeamKey = "raid",
                AllowedRoles = [SimulationType.Dps],
                Count = 1,
                AllowDuplicateTargets = true
            };

        var encounter =
            BuildEncounter(
                selection,
                hitCount,
                selectionHistoryMode
            );

        var context = CreateContext(encounter);
        context.AddActor(CreateActor("boss", "enemy", null));

        for (var index = 1; index <= dpsCount; index++)
        {
            context.AddActor(
                CreateActor(
                    $"dps-{index}",
                    "raid",
                    SimulationType.Dps
                )
            );
        }

        return Run(context);
    }

    private static EncounterProfile BuildEncounter(
        EncounterTargetSelectionDefinition selection,
        int hitCount,
        string selectionHistoryMode)
    {
        return new EncounterProfile
        {
            Name = "Sequence Selection History Test",
            RulesetKey = "development",
            DurationSeconds = 4m,
            DamagePatterns =
            [
                new EncounterDamagePatternDefinition
                {
                    Key = "history-test",
                    Name = "History Test",
                    StartTimeSeconds = 1m,
                    EndTimeSeconds = 1m,
                    IntervalSeconds = 1m,
                    SourceActorKey = "boss",
                    TargetSelection = selection,
                    Sequence =
                        new EncounterDamageSequenceDefinition
                        {
                            HitCount = hitCount,
                            HitIntervalSeconds = 0.5m,
                            TargetMode = EncounterDamageSequenceTargetModes.ReselectEachHit,
                            SelectionHistoryMode = selectionHistoryMode
                        },
                    Amount = 100m,
                    SchoolKey = "arcane"
                }
            ]
        };
    }

    private static SimulationContext CreateContext(
        EncounterProfile encounter)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = encounter.DurationSeconds,
                PrimaryActorKey = "boss",
                CaptureTimeline = true
            },
            encounter
        );
    }

    private static SimulationRunResult Run(
        SimulationContext context)
    {
        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "development-history",
                    Version = "1"
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),
                    new ScriptedEncounterDamageProcessor(mitigationResolver)
                ]
            );

        return engine.Run(context);
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        SimulationType? assignedRole)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey = teamKey,
                AssignedRole = assignedRole,
                Level = 20
            };

        actor.InitializeHealth(5000m);
        return actor;
    }

    private static List<CombatEvent> GetHistoryDamage(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:history-test",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .OrderBy(combatEvent => combatEvent.TimeSeconds)
            .ToList();
    }

    private static List<string> GetHistoryTargets(
        SimulationRunResult result)
    {
        return GetHistoryDamage(result)
            .Select(hit => hit.TargetActorKey)
            .Where(actorKey => !string.IsNullOrWhiteSpace(actorKey))
            .Select(actorKey => actorKey!)
            .ToList();
    }
}
}

// ==================== EncounterTargetSelectionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterTargetSelectionTests
{
    [Fact]
    public void AllMatchingActors_HitsEveryMatchingRole()
    {
        var result =
            RunPattern(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.AllMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Dps
                    ]
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.Contains(
            "dps-1",
            targets
        );

        Assert.Contains(
            "dps-2",
            targets
        );

        Assert.Contains(
            "dps-3",
            targets
        );

        Assert.DoesNotContain(
            "tank",
            targets
        );

        Assert.DoesNotContain(
            "healer",
            targets
        );
    }

    [Fact]
    public void RandomMatchingActors_CanChooseUniqueNonTanks()
    {
        var result =
            RunPattern(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    ExcludedRoles =
                    [
                        SimulationType.Tank
                    ],

                    Count =
                        3,

                    AllowDuplicateTargets =
                        false
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.Equal(
            3,
            targets.Distinct(
                StringComparer.OrdinalIgnoreCase
            ).Count()
        );

        Assert.DoesNotContain(
            "tank",
            targets
        );
    }

    [Fact]
    public void RandomMatchingActors_CanRepeatTheSameTarget()
    {
        var result =
            RunPattern(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Healing
                    ],

                    Count =
                        3,

                    AllowDuplicateTargets =
                        true
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.All(
            targets,
            target =>
                Assert.Equal(
                    "healer",
                    target
                )
        );
    }

    private static SimulationRunResult RunPattern(
        EncounterTargetSelectionDefinition selection)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Encounter Target Selection Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    2m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "multi-target-test",

                        Name =
                            "Multi Target Test",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            selection,

                        Amount =
                            100m,

                        SchoolKey =
                            "arcane"
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        2m,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        context.AddActor(
            CreateActor(
                "boss",
                "enemy",
                null
            )
        );

        context.AddActor(
            CreateActor(
                "tank",
                "raid",
                SimulationType.Tank
            )
        );

        context.AddActor(
            CreateActor(
                "healer",
                "raid",
                SimulationType.Healing
            )
        );

        context.AddActor(
            CreateActor(
                "dps-1",
                "raid",
                SimulationType.Dps
            )
        );

        context.AddActor(
            CreateActor(
                "dps-2",
                "raid",
                SimulationType.Dps
            )
        );

        context.AddActor(
            CreateActor(
                "dps-3",
                "raid",
                SimulationType.Dps
            )
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-targeting",

                    Version =
                        "1"
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    )
                ]
            );

        return engine.Run(
            context
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        SimulationType? assignedRole)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey,

                AssignedRole =
                    assignedRole,

                Level =
                    20
            };

        actor.InitializeHealth(
            5000m
        );

        return actor;
    }

    private static List<string> GetPatternDamageTargets(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:multi-target-test",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .Select(
                combatEvent =>
                    combatEvent.TargetActorKey
            )
            .Where(
                actorKey =>
                    !string.IsNullOrWhiteSpace(
                        actorKey
                    )
            )
            .Select(
                actorKey =>
                    actorKey!
            )
            .ToList();
    }
}
}
