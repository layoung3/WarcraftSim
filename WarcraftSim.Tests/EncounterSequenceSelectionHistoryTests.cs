using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
