using WarcraftSim.Api.Development;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class SimulationRegressionTests
{
    [Fact]
    public void CombatSmoke_IsDeterministic()
    {
        var first =
            DevelopmentScenarios.RunCombatSmoke(
                seed: 12345,
                captureTimeline: true
            );

        var second =
            DevelopmentScenarios.RunCombatSmoke(
                seed: 12345,
                captureTimeline: true
            );

        Assert.Equal(
            first.Summary.DamageDone,
            second.Summary.DamageDone
        );

        Assert.True(
            first.Summary.DamageDone > 0m
        );

        Assert.Contains(
            first.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );
    }

    [Fact]
    public void HealingThroughput_TracksResourceStarvation()
    {
        var result =
            DevelopmentScenarios.RunHealingThroughput(
                seed: 12345,
                captureTimeline: true
            );

        var metrics =
            result.Metrics;

        Assert.Equal(
            60m,
            metrics.WindowSeconds
        );

        Assert.True(
            metrics.EffectiveHealing > 0m
        );

        Assert.True(
            metrics.EffectiveHps > 0m
        );

        Assert.True(
            metrics.EndingResource <
            metrics.StartingResource
        );

        Assert.True(
            metrics.BecameResourceStarved
        );

        Assert.NotNull(
            metrics.FirstResourceStarvedAtSeconds
        );

        Assert.True(
            metrics.ResourceStarvedSeconds > 0m
        );

        Assert.True(
            metrics.CompletedCastsByAbility.Count >= 2
        );
    }

    [Fact]
    public void RaidHealing_UsesNamedEncounterDamageBreakdowns()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var tankSummary =
            result.Summary.ActorSummaries[
                "tank"
            ];

        Assert.Contains(
            "encounter:tank-spike-1",
            tankSummary.DamageTakenByAbility.Keys
        );

        Assert.Contains(
            "encounter:tank-spike-2",
            tankSummary.DamageTakenByAbility.Keys
        );

        Assert.DoesNotContain(
            "unknown",
            tankSummary.DamageTakenByAbility.Keys
        );
    }

    [Fact]
    public void RaidHealing_AppliesMitigationToScriptedDamage()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var tankSpike =
            result.Timeline.First(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:tank-spike-1",
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        Assert.NotNull(
            tankSpike.RawAmount
        );

        Assert.NotNull(
            tankSpike.MitigatedAmount
        );

        Assert.NotNull(
            tankSpike.MitigationPercent
        );

        Assert.NotNull(
            tankSpike.Amount
        );

        Assert.True(
            tankSpike.RawAmount!.Value >
            tankSpike.Amount!.Value
        );

        Assert.True(
            tankSpike.MitigatedAmount!.Value > 0m
        );

        Assert.True(
            tankSpike.MitigationPercent!.Value > 0m
        );
    }

    [Fact]
    public void RaidHealing_HealsMultipleRaidMembers()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var healedTargets =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Healing &&
                        string.Equals(
                            combatEvent.SourceActorKey,
                            "healer",
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
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .ToList();

        Assert.Contains(
            "tank",
            healedTargets
        );

        Assert.Contains(
            "dps-1",
            healedTargets
        );

        Assert.Contains(
            "dps-2",
            healedTargets
        );
    }
}
