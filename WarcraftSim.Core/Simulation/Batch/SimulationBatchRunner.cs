using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Batch;

public sealed class SimulationBatchRunner
{
    public SimulationBatchResult Run(
        SimulationBatchOptions options,
        Func<int, bool, SimulationRunResult> runSimulation)
    {
        if (options.Iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Iterations),
                "Iterations must be greater than zero."
            );
        }

        var summaries =
            new List<SimulationRunSummary>(
                options.Iterations
            );

        for (
            var iteration = 0;
            iteration < options.Iterations;
            iteration++)
        {
            var seed =
                unchecked(
                    options.BaseSeed +
                    iteration
                );

            var result =
                runSimulation(
                    seed,
                    false
                );

            summaries.Add(
                result.Summary
            );
        }

        var durationSeconds =
            summaries.Count == 0
                ? 0m
                : summaries[0].DurationSeconds;

        var damageDone =
            summaries
                .Select(summary =>
                    summary.DamageDone)
                .ToList();

        var damageTaken =
            summaries
                .Select(summary =>
                    summary.DamageTaken)
                .ToList();

        var healingDone =
            summaries
                .Select(summary =>
                    summary.HealingDone)
                .ToList();

        var damagePerSecond =
            ToPerSecond(
                damageDone,
                durationSeconds
            );

        var damageTakenPerSecond =
            ToPerSecond(
                damageTaken,
                durationSeconds
            );

        var healingPerSecond =
            ToPerSecond(
                healingDone,
                durationSeconds
            );

        var representativeSeed =
            SelectRepresentativeSeed(
                summaries,
                options
            );

        var representativeRun =
            runSimulation(
                representativeSeed,
                true
            );

        return new SimulationBatchResult
        {
            Summary =
                new SimulationBatchSummary
                {
                    Iterations =
                        options.Iterations,

                    BaseSeed =
                        options.BaseSeed,

                    SimulationType =
                        options.SimulationType,

                    DurationSeconds =
                        durationSeconds,

                    DamageDone =
                        BuildDistribution(
                            damageDone
                        ),

                    DamagePerSecond =
                        BuildDistribution(
                            damagePerSecond
                        ),

                    DamageTaken =
                        BuildDistribution(
                            damageTaken
                        ),

                    DamageTakenPerSecond =
                        BuildDistribution(
                            damageTakenPerSecond
                        ),

                    HealingDone =
                        BuildDistribution(
                            healingDone
                        ),

                    HealingPerSecond =
                        BuildDistribution(
                            healingPerSecond
                        ),

                    PrimaryActorDeathRatePercent =
                        summaries.Count == 0
                            ? 0m
                            : summaries.Count(
                                summary =>
                                    summary.PrimaryActorDied
                              ) *
                              100m /
                              summaries.Count,

                    AverageDamageDoneByAbility =
                        AverageBreakdown(
                            summaries,
                            summary =>
                                summary.DamageDoneByAbility
                        ),

                    AverageDamageTakenByAbility =
                        AverageBreakdown(
                            summaries,
                            summary =>
                                summary.DamageTakenByAbility
                        ),

                    AverageHealingDoneByAbility =
                        AverageBreakdown(
                            summaries,
                            summary =>
                                summary.HealingDoneByAbility
                        )
                },

            RepresentativeSeed =
                representativeSeed,

            RepresentativeRun =
                representativeRun
        };
    }

    private static int SelectRepresentativeSeed(
        IReadOnlyList<SimulationRunSummary> summaries,
        SimulationBatchOptions options)
    {
        if (summaries.Count == 0)
        {
            return options.BaseSeed;
        }

        var values =
            summaries
                .Select(summary =>
                    GetPrimaryMetric(
                        summary,
                        options.SimulationType
                    ))
                .OrderBy(value => value)
                .ToList();

        var median =
            Percentile(
                values,
                0.50m
            );

        var bestIndex = 0;
        var bestDistance =
            decimal.MaxValue;

        for (
            var index = 0;
            index < summaries.Count;
            index++)
        {
            var metric =
                GetPrimaryMetric(
                    summaries[index],
                    options.SimulationType
                );

            var distance =
                Math.Abs(
                    metric - median
                );

            if (distance <
                bestDistance)
            {
                bestDistance =
                    distance;

                bestIndex =
                    index;
            }
        }

        return unchecked(
            options.BaseSeed +
            bestIndex
        );
    }

    private static decimal GetPrimaryMetric(
        SimulationRunSummary summary,
        SimulationType simulationType)
    {
        return simulationType switch
        {
            SimulationType.Healing =>
                summary.HealingDone,

            SimulationType.Tank =>
                summary.DamageTaken,

            _ =>
                summary.DamageDone
        };
    }

    private static List<decimal> ToPerSecond(
        IReadOnlyList<decimal> totals,
        decimal durationSeconds)
    {
        if (durationSeconds <= 0m)
        {
            return totals
                .Select(_ => 0m)
                .ToList();
        }

        return totals
            .Select(total =>
                total /
                durationSeconds)
            .ToList();
    }

    private static DistributionSummary BuildDistribution(
        IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return new DistributionSummary();
        }

        var sorted =
            values
                .OrderBy(value => value)
                .ToList();

        var mean =
            values.Average();

        var variance =
            values
                .Select(value =>
                {
                    var difference =
                        value - mean;

                    return
                        difference *
                        difference;
                })
                .Average();

        var standardDeviation =
            (decimal)Math.Sqrt(
                (double)variance
            );

        return new DistributionSummary
        {
            Count =
                values.Count,

            Mean =
                mean,

            StandardDeviation =
                standardDeviation,

            Minimum =
                sorted[0],

            Percentile05 =
                Percentile(
                    sorted,
                    0.05m
                ),

            Percentile25 =
                Percentile(
                    sorted,
                    0.25m
                ),

            Median =
                Percentile(
                    sorted,
                    0.50m
                ),

            Percentile75 =
                Percentile(
                    sorted,
                    0.75m
                ),

            Percentile95 =
                Percentile(
                    sorted,
                    0.95m
                ),

            Maximum =
                sorted[^1]
        };
    }

    private static decimal Percentile(
        IReadOnlyList<decimal> sortedValues,
        decimal percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0m;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var clampedPercentile =
            Math.Clamp(
                percentile,
                0m,
                1m
            );

        var position =
            clampedPercentile *
            (sortedValues.Count - 1);

        var lowerIndex =
            (int)Math.Floor(
                position
            );

        var upperIndex =
            (int)Math.Ceiling(
                position
            );

        if (lowerIndex ==
            upperIndex)
        {
            return
                sortedValues[
                    lowerIndex
                ];
        }

        var fraction =
            position -
            lowerIndex;

        return
            sortedValues[lowerIndex] +
            (
                sortedValues[upperIndex] -
                sortedValues[lowerIndex]
            ) *
            fraction;
    }

    private static Dictionary<string, decimal>
        AverageBreakdown(
            IReadOnlyList<SimulationRunSummary> summaries,
            Func<
                SimulationRunSummary,
                Dictionary<string, decimal>
            > selector)
    {
        var totals =
            new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var summary in summaries)
        {
            foreach (
                var pair in
                selector(summary))
            {
                if (totals.TryGetValue(
                        pair.Key,
                        out var currentValue))
                {
                    totals[pair.Key] =
                        currentValue +
                        pair.Value;
                }
                else
                {
                    totals[pair.Key] =
                        pair.Value;
                }
            }
        }

        if (summaries.Count == 0)
        {
            return totals;
        }

        foreach (
            var key in
            totals.Keys.ToList())
        {
            totals[key] =
                totals[key] /
                summaries.Count;
        }

        return totals;
    }
}
