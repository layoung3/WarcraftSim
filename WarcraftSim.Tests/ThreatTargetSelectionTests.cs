using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class ThreatTargetSelectionTests
{
    [Fact]
    public void ThreatTable_CanResolveHighestThreatFromEligibleActors()
    {
        var table =
            new ThreatTableState();

        table.AddThreat(
            "tank",
            100m
        );

        table.AddThreat(
            "damage",
            300m
        );

        table.AddThreat(
            "healer",
            200m
        );

        Assert.Equal(
            "healer",
            table.GetHighestThreatActorKey(
                [
                    "tank",
                    "healer"
                ]
            )
        );
    }

    [Fact]
    public void EncounterSelector_HighestThreatChoosesHighestThreatLivingOpponent()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy",
                null
            );

        AddActor(
            context,
            "tank",
            "raid",
            SimulationType.Tank
        );

        var damage =
            AddActor(
                context,
                "damage",
                "raid",
                SimulationType.Dps
            );

        boss.ThreatTable.AddThreat(
            "tank",
            100m
        );

        boss.ThreatTable.AddThreat(
            "damage",
            300m
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreatePattern()
                )
            );

        Assert.Same(
            damage,
            selected
        );
    }

    [Fact]
    public void EncounterSelector_HighestThreatSkipsDeadActors()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy",
                null
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid",
                SimulationType.Dps
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            500m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            200m
        );

        tank.TakeDamage(
            tank.MaximumHealth
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreatePattern()
                )
            );

        Assert.Same(
            damage,
            selected
        );
    }

    [Fact]
    public void EncounterSelector_HighestThreatRespectsRoleFilters()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy",
                null
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid",
                SimulationType.Dps
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        var pattern =
            CreatePattern();

        pattern.TargetSelection!.AllowedRoles =
        [
            SimulationType.Tank
        ];

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    pattern
                )
            );

        Assert.Same(
            tank,
            selected
        );
    }

    [Fact]
    public void EncounterSelector_HighestThreatReturnsNoTargetWithoutPositiveThreat()
    {
        var context =
            CreateContext();

        AddActor(
            context,
            "boss",
            "enemy",
            null
        );

        AddActor(
            context,
            "tank",
            "raid",
            SimulationType.Tank
        );

        var targets =
            EncounterTargetSelector.Resolve(
                context,
                CreatePattern()
            );

        Assert.Empty(
            targets
        );
    }

    [Fact]
    public void EncounterPattern_HighestThreatDamageHitsCurrentThreatLeader()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Threat Target Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    2m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "threat-hit",

                        Name =
                            "Threat Hit",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            new EncounterTargetSelectionDefinition
                            {
                                Mode =
                                    EncounterTargetSelectionModes.HighestThreatActor,

                                Relationship =
                                    SimulationActorRelationshipTypes.Enemy
                            },

                        Amount =
                            100m,

                        SchoolKey =
                            "physical"
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

        var boss =
            AddActor(
                context,
                "boss",
                "enemy",
                null
            );

        AddActor(
            context,
            "tank",
            "raid",
            SimulationType.Tank
        );

        AddActor(
            context,
            "damage",
            "raid",
            SimulationType.Dps
        );

        boss.ThreatTable.AddThreat(
            "tank",
            150m
        );

        boss.ThreatTable.AddThreat(
            "damage",
            400m
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-threat-targeting",

                    Version =
                        "1"
                }
            );

        var result =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    )
                ]
            )
            .Run(
                context
            );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:threat-hit",
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        Assert.Equal(
            "damage",
            damage.TargetActorKey
        );
    }

    private static EncounterDamagePatternDefinition CreatePattern()
    {
        return new EncounterDamagePatternDefinition
        {
            Key =
                "threat-target",

            Name =
                "Threat Target",

            SourceActorKey =
                "boss",

            TargetSelection =
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.HighestThreatActor,

                    Relationship =
                        SimulationActorRelationshipTypes.Enemy
                }
        };
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    "boss"
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
        string key,
        string teamKey,
        SimulationType? role)
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
                    role,

                Level =
                    20
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }
}
