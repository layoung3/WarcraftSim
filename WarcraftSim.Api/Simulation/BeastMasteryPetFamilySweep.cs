using System.Text.Json;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Executes one standardized single-target and three-target combat test for
/// every registered pet family. Uses the normal Beast Mastery runner rather
/// than a separate combat or damage calculation.
/// </summary>
public sealed class BeastMasteryPetFamilySweep
{
    public BeastMasteryPetFamilySweepReport Run(
        BeastMasteryHunterSimRequest template, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        var cases = new List<BeastMasteryPetFamilySweepCase>();
        foreach (var family in ForeverHunterPetFamilies.All.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            foreach (var additionalTargets in new[] { 0, 2 })
            {
                token.ThrowIfCancellationRequested();
                // Clone the full request, including talents and tuning, so the
                // family runs cannot mutate the original input or each other.
                var request = JsonSerializer.Deserialize<BeastMasteryHunterSimRequest>(
                    JsonSerializer.Serialize(template))!;
                request.PetEnabled = true;
                request.PetFamily = family.Key;
                request.AdditionalTargets = additionalTargets;
                request.CaptureTimeline = true; // For spending/proc event checks; not exported.

                var result = new BeastMasteryHunterSimRunner().Run(request, token);
                var summaries = result.Run.Summary.ActorSummaries;
                if (!summaries.TryGetValue("hunter-pet", out var petSummary))
                    throw new InvalidOperationException($"Pet actor was not reported for {family.Key}.");

                var specialKey = "pet-" + family.SpecialAbility;
                petSummary.Abilities.TryGetValue(specialKey, out var special);
                var specialByTarget = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var key in new[] { "target", "add-1", "add-2" })
                {
                    if (summaries.TryGetValue(key, out var target))
                        specialByTarget.Add(key,
                            target.DamageTakenByAbility.GetValueOrDefault(specialKey));
                }

                var spent = result.Run.Timeline
                    .Where(e => e.SourceActorKey == "hunter-pet" && e.ResourceKey == "focus" && e.Amount < 0m)
                    .Sum(e => -(e.Amount ?? 0m));
                var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["combinedDamageReconciles"] = Math.Abs(result.DamageDone -
                        result.HunterDamage - result.PetDamage - result.HawkDamage) < .0001m,
                    ["petDamageReconciles"] = Math.Abs(result.PetDamage -
                        result.PetAutoDamage - result.PetSpecialDamage) < .0001m,
                    ["independentPetActor"] = petSummary.DamageDone > 0m &&
                        Math.Abs(petSummary.DamageDone - result.PetDamage) < .0001m,
                    ["petPerformedAutoAttacks"] = petSummary.Abilities.TryGetValue("pet-melee", out var autos) &&
                        autos.DamageOccurrenceCount > 0,
                    ["petUsedFamilySpecial"] = !template.UsePetSpecial ||
                        (special is not null && special.CastCompletedCount > 0)
                };
                if (additionalTargets == 2 && (family.SpecialAbility is "swipe" or "thunderstomp"))
                {
                    checks["multiTargetSpecial"] =
                        specialByTarget.GetValueOrDefault("add-1") > 0m &&
                        specialByTarget.GetValueOrDefault("add-2") > 0m;
                }

                cases.Add(new BeastMasteryPetFamilySweepCase
                {
                    Family = family.Key,
                    FamilySpecial = specialKey,
                    ProvisionalFamily = family.Key == "core-hound",
                    AdditionalTargets = additionalTargets,
                    DurationSeconds = request.DurationSeconds,
                    Seed = request.Seed,
                    CombinedDps = result.Dps,
                    CombinedDamage = result.DamageDone,
                    HunterDamage = result.HunterDamage,
                    PetDamage = result.PetDamage,
                    PetDps = result.PetDps,
                    PetAutoDamage = result.PetAutoDamage,
                    PetSpecialDamage = result.PetSpecialDamage,
                    HawkDamage = result.HawkDamage,
                    PetThreatOnBoss = result.PetThreat,
                    PetFocusRemaining = result.PetFocus,
                    PetFocusSpent = spent,
                    PetResourceStarvedSeconds = petSummary.ResourceStarvedSeconds,
                    PetActiveSeconds = result.PetActiveSeconds,
                    PetCriticalHits = result.PetCriticalHits,
                    PetMisses = result.PetMisses,
                    PetDied = result.PetDied,
                    FamilySpecialCasts = special?.CastCompletedCount ?? 0,
                    FamilySpecialOccurrences = special?.DamageOccurrenceCount ?? 0,
                    FamilySpecialDamage = special?.DamageDone ?? 0m,
                    FamilySpecialResults = special is null ? new Dictionary<string, int>() :
                        special.ResultCounts.ToDictionary(p => p.Key, p => p.Value),
                    FamilySpecialDamageByTarget = specialByTarget,
                    PetDamageByAbility = petSummary.DamageDoneByAbility.ToDictionary(p => p.Key, p => p.Value),
                    MechanicsChecks = checks
                });
            }
        }
        return new BeastMasteryPetFamilySweepReport
        {
            Specialization = "beast-mastery",
            Description = "Every registered family, once against one boss and once against boss plus two adds. " +
                "Each case uses the current simulation engine and the same seed/tuning. " +
                "Full combat timelines are not included.",
            InputSeed = template.Seed,
            InputDurationSeconds = template.DurationSeconds,
            FamilyCount = cases.Select(x => x.Family).Distinct(StringComparer.Ordinal).Count(),
            CaseCount = cases.Count,
            FailedCheckCount = cases.Sum(c => c.MechanicsChecks.Values.Count(pass => !pass)),
            Cases = cases
        };
    }
}

public sealed class BeastMasteryPetFamilySweepReport
{
    public string Specialization { get; init; } = "beast-mastery";
    public string Description { get; init; } = "";
    public int InputSeed { get; init; }
    public decimal InputDurationSeconds { get; init; }
    public int FamilyCount { get; init; }
    public int CaseCount { get; init; }
    public int FailedCheckCount { get; init; }
    public List<BeastMasteryPetFamilySweepCase> Cases { get; init; } = [];
}

public sealed class BeastMasteryPetFamilySweepCase
{
    public string Family { get; init; } = "";
    public string FamilySpecial { get; init; } = "";
    public bool ProvisionalFamily { get; init; }
    public int AdditionalTargets { get; init; }
    public decimal DurationSeconds { get; init; }
    public int Seed { get; init; }
    public decimal CombinedDps { get; init; }
    public decimal CombinedDamage { get; init; }
    public decimal HunterDamage { get; init; }
    public decimal PetDamage { get; init; }
    public decimal PetDps { get; init; }
    public decimal PetAutoDamage { get; init; }
    public decimal PetSpecialDamage { get; init; }
    public decimal HawkDamage { get; init; }
    public decimal PetThreatOnBoss { get; init; }
    public decimal PetFocusRemaining { get; init; }
    public decimal PetFocusSpent { get; init; }
    public decimal PetResourceStarvedSeconds { get; init; }
    public decimal PetActiveSeconds { get; init; }
    public int PetCriticalHits { get; init; }
    public int PetMisses { get; init; }
    public bool PetDied { get; init; }
    public int FamilySpecialCasts { get; init; }
    public int FamilySpecialOccurrences { get; init; }
    public decimal FamilySpecialDamage { get; init; }
    public Dictionary<string, int> FamilySpecialResults { get; init; } = new();
    public Dictionary<string, decimal> FamilySpecialDamageByTarget { get; init; } = new();
    public Dictionary<string, decimal> PetDamageByAbility { get; init; } = new();
    public Dictionary<string, bool> MechanicsChecks { get; init; } = new();
}
