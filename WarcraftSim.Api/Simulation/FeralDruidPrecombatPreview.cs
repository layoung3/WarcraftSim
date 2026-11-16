using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Api.Simulation;

/// <summary>Initial-form boundary after the mandatory shift, before any combat decision.
/// Authored totals include ordinary attribute contributions; this is not a complete effective stat sheet.</summary>
public sealed class FeralDruidPrecombatPreview
{
    public string InitialForm { get; init; } = "";
    public string InputMode { get; init; } = "authored-inclusive";
    public decimal AuthoredAttackPower { get; init; }
    public decimal HeartOfTheWildAttackPowerIncrement { get; init; }
    public decimal PredatoryStrikesAttackPower { get; init; }
    public decimal SpellbookAttackPower { get; init; }
    public decimal MaximumHealth { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal InitialShiftManaCost { get; init; }
    public decimal ManaAfterInitialShift { get; init; }
    public decimal MaximumEnergy { get; init; } = 100m;
    public decimal StartingEnergy { get; init; }
    public decimal MaximumRage { get; init; } = 100m;
    public decimal StartingRage { get; init; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    // Configured ordering, not a promise that any entry is eligible immediately.
    public RotationProfile ConfiguredRotation { get; init; } = new();
    public IReadOnlyList<string> ModelingNotes { get; init; } = [
        "Legacy authored-inclusive totals already include ordinary Strength/Stamina/Intellect contributions; no baseline or complete effective attribute sheet is claimed.",
        "Initial form only. Heart of the Wild remains frozen to the initial form during mixed-form simulations.",
        "Configured priority is not immediate eligibility. Shifting Power may act at time zero after this preview boundary.",
        "Energy starts at zero; legacy Rage starts at zero in Cat or twenty in Bear. Lower-rank Furor is not modeled as a probabilistic starting resource grant.",
        "Feral mechanics and AP coefficients remain partial/provisional. White attacks, Ravage, Rip, Bite, Maul, Primal Bite and Pounce currently do not consume Attack Power.",
        "Selected Predatory Strikes adds 30/60/90 AP after Heart of the Wild for this level-60 actor. If authored AP already includes Predatory Strikes, remove that manual bonus to avoid double counting; no heuristic correction is applied.",
        "Historical pinned talent evidence is not current-client certification or strict-mode admission."
    ];
}
