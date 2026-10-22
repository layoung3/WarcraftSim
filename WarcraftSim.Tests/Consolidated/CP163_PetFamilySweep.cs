using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp163PetFamilySweepTests
{
    [Fact]
    public void EveryRegisteredPetFamilyRunsAgainstOneAndThreeTargetsWithoutDoubleCounting()
    {
        var request = new BeastMasteryHunterSimRequest
        {
            DurationSeconds = 30m,
            Seed = 163001,
            CaptureTimeline = false
        };
        var report = new BeastMasteryPetFamilySweep().Run(request);
        Assert.Equal(ForeverHunterPetFamilies.All.Count, report.FamilyCount);
        Assert.Equal(report.FamilyCount * 2, report.CaseCount);
        Assert.Equal(0, report.FailedCheckCount);
        foreach (var family in ForeverHunterPetFamilies.All)
        {
            var pair = report.Cases.Where(c => c.Family == family.Key).ToArray();
            Assert.Equal(2, pair.Length);
            Assert.Equal(new[] { 0, 2 }, pair.Select(x => x.AdditionalTargets).OrderBy(x => x));
            Assert.All(pair, test =>
            {
                Assert.True(test.PetDamage > 0m, $"{family.Key}: pet did not deal damage");
                Assert.True(test.PetFocusSpent >= 0m);
                Assert.True(test.CombinedDamage > test.PetDamage);
                Assert.All(test.MechanicsChecks.Values, passed => Assert.True(passed));
            });
        }
        var bear = Assert.Single(report.Cases, c => c.Family == "bear" && c.AdditionalTargets == 2);
        Assert.Equal(bear.FamilySpecialCasts * 3, bear.FamilySpecialOccurrences);
        Assert.True(bear.FamilySpecialDamageByTarget.GetValueOrDefault("add-1") > 0m);
        Assert.True(bear.FamilySpecialDamageByTarget.GetValueOrDefault("add-2") > 0m);
    }
}
