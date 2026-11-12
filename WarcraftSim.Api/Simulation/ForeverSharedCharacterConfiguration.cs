using System.Text.Json;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Class-independent transport for the four-class builder. The envelope is
/// structurally validated here; class adapters and combat engines remain the
/// final authority for equipment eligibility, talent prerequisites and actions.
/// A successful envelope validation is NOT a simulated or certified build.
/// </summary>
public sealed class ForeverSharedCharacterConfiguration
{
    public string ClassKey { get; set; } = "";
    public string Specialization { get; set; } = "";
    public string Race { get; set; } = "";
    public int Level { get; set; } = 60;
    public string BaselineMode { get; set; } = "historical-provisional";
    public ForeverLevel60BaselineObservation? Baseline { get; set; }
    public CharacterEquipmentLoadout Equipment { get; set; } = new();
    public Dictionary<string,int> ItemizedTalents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public ForeverExternalModifierSelection Modifiers { get; set; } = new();
    public CombatUseItemsOptions CombatItems { get; set; } = new();
    public bool UseRacialActive { get; set; }
    public ForeverSharedEncounterConfiguration Encounter { get; set; } = new();
    public ForeverSharedRotationConfiguration Rotation { get; set; } = new();
    public JsonElement ClassSettings { get; set; } = JsonSerializer.SerializeToElement(new {});
}

public sealed class ForeverSharedEncounterConfiguration
{
    public decimal DurationSeconds { get; set; } = 120m;
    public int Seed { get; set; } = 12345;
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal TargetArmor { get; set; } = 3500m;
    public bool CaptureTimeline { get; set; } = true;
    // A nonlethal DPS benchmark must eventually be explicitly implemented
    // by the corresponding class adapter. Do not treat this as an instruction
    // to silently resurrect or heal a currently lethal typed engine run.
    public string SurvivalPolicy { get; set; } = "existing-runner-behavior";
}

public sealed class ForeverSharedRotationConfiguration
{
    public string Mode { get; set; } = "automatic";
    public List<string> Priority { get; set; } = [];
}

public sealed record ForeverSharedConfigurationValidation(
    string ClassKey,string Specialization,int Level,int RankedPoints,
    string RotationMode,string SurvivalPolicy,bool ReadyForUnifiedSimulation,
    string[] Limitations);

public static class ForeverSharedCharacterConfigurationValidator
{
    public static ForeverSharedConfigurationValidation Validate(
        ForeverSharedCharacterConfiguration? build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var classKey=build.ClassKey?.Trim().ToLowerInvariant()??"";
        var spec=build.Specialization?.Trim().ToLowerInvariant()??"";
        var capability=ForeverClassSimulationCapabilities.Get(classKey);
        if(build.Level!=60)throw new ArgumentException("Shared character builder currently supports level 60 only.");
        if(!capability.Specializations.Contains(spec,StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported {classKey} starting specialization '{spec}'.");
        if(string.IsNullOrWhiteSpace(build.Race))
            throw new ArgumentException("Choose a race before validating the shared character.");
        if(build.BaselineMode is not ("strict-reviewed" or "historical-provisional" or "explicit-observation"))
            throw new ArgumentException("Unknown level-60 baseline mode.");
        if(build.Equipment?.Items is null)
            throw new ArgumentException("Equipment loadout cannot be null.");
        if(build.ItemizedTalents is null || build.ItemizedTalents.Count==0)
            throw new ArgumentException("Ranked talent allocations are required; no synthetic totals.");
        if(build.ItemizedTalents.Any(x=>string.IsNullOrWhiteSpace(x.Key) || x.Value is <1 or >5))
            throw new ArgumentException("Each allocated talent needs a named key and rank 1–5.");
        var spent=build.ItemizedTalents.Values.Sum();
        if(spent>51)throw new ArgumentException("Level-60 talent budget cannot exceed 51 ranked points.");
        if(build.Encounter is null || build.Encounter.DurationSeconds is <=0m or >1200m ||
            build.Encounter.TargetMaximumHealth<=0m)
            throw new ArgumentException("Shared encounter duration/target health is invalid.");
        if(build.Encounter.SurvivalPolicy is not ("existing-runner-behavior" or "nonlethal-dps-benchmark" or "survival"))
            throw new ArgumentException("Unknown survivability policy.");
        if(build.Encounter.SurvivalPolicy!="existing-runner-behavior")
            throw new ArgumentException("Requested shared survivability policy is not executable yet. Use the existing runner until its policy adapter is certified.");
        if(build.Rotation is null || build.Rotation.Mode is not ("automatic" or "custom") ||
            build.Rotation.Priority is null)
            throw new ArgumentException("Shared rotation policy is malformed.");
        if(build.Rotation.Priority.Any(x=>string.IsNullOrWhiteSpace(x)))
            throw new ArgumentException("Custom priority cannot contain blank actions.");
        if(build.Modifiers is null)throw new ArgumentException("Modifier selection cannot be null.");
        _=ForeverExternalModifierCatalog.Resolve(build.Modifiers,build.Level,build.Encounter.DurationSeconds);
        if(build.CombatItems is null)throw new ArgumentException("Combat item inventory cannot be null.");
        build.CombatItems.Validate(classKey);
        var limitations=capability.KnownRestrictions.ToList();
        limitations.Add("Envelope validation covers only structural rules. Runtime class talent prerequisites and exact equipment eligibility require adapter validation.");
        if(!capability.ExecutesThroughCurrentUnifiedEndpoint)
            limitations.Add("This class is not yet executable through the current unified level-60 endpoint.");
        return new(capability.ClassKey,spec,build.Level,spent,build.Rotation.Mode,
            build.Encounter.SurvivalPolicy,
            false, // Must remain false until the new class adapter is implemented and certified.
            limitations.ToArray());
    }
}
