using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// A read-only, race/class-specific naked level-60 observation. This structure
/// deliberately does not contain gear, talents, spell power, rating or buffs.
/// Source status is ALWAYS visible: client files do not establish race primary
/// attributes and this model cannot certify user-submitted values as Forever.
/// </summary>
public sealed class ForeverLevel60BaselineObservation
{
    public string Race { get; set; }="";
    public string ClassKey { get; set; }="";
    public int Level { get; set; }=60;
    public decimal Strength { get; set; }
    public decimal Agility { get; set; }
    public decimal Stamina { get; set; }
    public decimal Intellect { get; set; }
    public decimal Spirit { get; set; }
    // Optional naked maximum Health as shown in game, with passive racial
    // bonuses already removed/identified. A captured maximum is not Base HP.
    public decimal? ObservedUnbuffedMaximumHealth { get; set; }
    public decimal? ObservedUnbuffedMaximumMana { get; set; }
    public decimal? ObservedUnbuffedMeleeAttackPower { get; set; }
    public decimal? ObservedUnbuffedRangedAttackPower { get; set; }
    public decimal? ObservedUnbuffedArmor { get; set; }
    public string ClientBuild { get; set; }="";
    public string SourceKind { get; set; }="user-unverified-observation";
    public string SourceReference { get; set; }="";
    public string RecordedAtUtc { get; set; }="";
    public string VerificationStatus { get; set; }="unverified-user-supplied";
}

public static class ForeverLevel60Baseline
{
    public static void Validate(ForeverLevel60BaselineObservation baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if(baseline.Level!=60 || string.IsNullOrWhiteSpace(baseline.Race) ||
           string.IsNullOrWhiteSpace(baseline.ClassKey))
            throw new ArgumentException("A level-60 race/class baseline is required.");
        ForeverPlayableRaceCatalog.Validate(baseline.Race,baseline.ClassKey,60);
        if(new[]{baseline.Strength,baseline.Agility,baseline.Stamina,
                baseline.Intellect,baseline.Spirit}.Any(x=>x is <=0m or >3000m) ||
           baseline.ObservedUnbuffedMaximumHealth is <=0m ||
           baseline.ObservedUnbuffedMaximumMana is <0m ||
           baseline.ObservedUnbuffedMeleeAttackPower is <0m ||
           baseline.ObservedUnbuffedRangedAttackPower is <0m ||
           baseline.ObservedUnbuffedArmor is <0m)
            throw new ArgumentException("Baseline primary stats and observed resources are invalid.");
        if(string.IsNullOrWhiteSpace(baseline.ClientBuild) ||
           string.IsNullOrWhiteSpace(baseline.SourceReference) ||
           string.IsNullOrWhiteSpace(baseline.RecordedAtUtc) ||
           !DateTimeOffset.TryParse(baseline.RecordedAtUtc,out _))
            throw new ArgumentException("Baseline must carry client build, source reference and UTC observation time.");
        if(baseline.VerificationStatus is not (
            "unverified-user-supplied" or "forever-in-game-observed" or
            "reviewed-forever-in-game" or "provisional-documented-proxy"))
            throw new ArgumentException("Unsupported baseline provenance status.");
        if(baseline.VerificationStatus is ("forever-in-game-observed" or
            "reviewed-forever-in-game") &&
           baseline.SourceKind!="in-game-character-sheet")
            throw new ArgumentException("Only directly observed Forever character sheets can request observed status.");
        // Source kind/status are evidence LABELS, not server attestation.
    }

    public static ForeverLevel60BaselineObservation? Resolve(
        ForeverLevel60BaselineObservation? explicitObservation,bool useReviewed,
        string race,string classKey)
    {
        if(explicitObservation is not null && useReviewed)
            throw new ArgumentException("Use either an explicit observation or a reviewed catalog baseline.");
        return explicitObservation ?? (useReviewed ?
            ForeverLevel60BaselineCatalog.Find(race,classKey) : null);
    }

    /// <summary>Generate only base raw attributes; equipment, external buffs,
    /// race passives and talents remain in the original profile to be resolved
    /// once by the existing CP170–173 contributor and class pipelines.</summary>
    public static CharacterProfile Generate(CharacterProfile source,
        ForeverLevel60BaselineObservation baseline)
    {
        ArgumentNullException.ThrowIfNull(source);
        Validate(baseline);
        if(source.Level!=60 || !string.Equals(source.ClassKey,baseline.ClassKey,
            StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(source.Race,baseline.Race,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Baseline must match the selected character race/class/level.");
        var copy=JsonSerializer.Deserialize<CharacterProfile>(
            JsonSerializer.Serialize(source)) ??
            throw new InvalidOperationException("Character profile copy failed.");
        copy.BaseStats=new StatCollection {
            Values=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase) {
                ["strength"]=baseline.Strength,
                ["agility"]=baseline.Agility,
                ["stamina"]=baseline.Stamina,
                ["intellect"]=baseline.Intellect,
                ["spirit"]=baseline.Spirit
            }
        };
        return copy;
    }

    public static object Summary(ForeverLevel60BaselineObservation baseline) => new {
        baseline.Race,baseline.ClassKey,baseline.Level,
        baseline.ClientBuild,baseline.SourceKind,baseline.SourceReference,
        baseline.RecordedAtUtc,baseline.VerificationStatus,
        baseMana=ForeverClientBaseManaCatalog.GetBaseMana(baseline.ClassKey,60),
        baseManaSourceBuild=ForeverClientBaseManaCatalog.SourceBuild,
        nakedMaximumHealth=baseline.ObservedUnbuffedMaximumHealth,
        nakedMaximumMana=baseline.ObservedUnbuffedMaximumMana,
        nakedMeleeAttackPower=baseline.ObservedUnbuffedMeleeAttackPower,
        nakedRangedAttackPower=baseline.ObservedUnbuffedRangedAttackPower,
        nakedArmor=baseline.ObservedUnbuffedArmor,
        warning="Provenance metadata does not independently certify user-supplied values. No unsourced race/class stat values are inferred."
    };
}
