using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

public static class ForeverWarriorDamageTakenRageFactory
{
    public const string DefinitionKey =
        "forever-warrior-damage-taken-rage";

    // The October 8 beta notes verify the shape of the current server rule:
    // incoming Rage is normalized against expected creature health rather than
    // the player's own health; absorbs are ignored; and actual Armor is
    // replaced by a level-appropriate expected reduction. Blizzard has not
    // published the exact per-level expected-health / expected-Armor table.
    public const bool FormulaShapeVerifiedByBlizzard = true;
    public const bool CalibrationCurveVerifiedByBlizzard = false;

    // The previous beta behavior measured as 10 Rage per reference-health of
    // pre-Armor damage while Blizzard was balancing around 50% Armor. That is
    // equivalent to a 20-Rage unmitigated reference share, then applying the
    // configured expected Armor reduction. Keep this isolated and explicitly
    // provisional until the live server curve can be measured directly.
    public const decimal ProvisionalUnmitigatedRagePerReferenceHealth = 20m;

    public static DamageTakenResourceGenerationDefinition Create(
        decimal expectedCreatureHealth,
        decimal expectedArmorReductionPercent)
    {
        if (expectedCreatureHealth <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCreatureHealth),
                "Expected creature health must be positive."
            );
        }

        if (
            expectedArmorReductionPercent < 0m ||
            expectedArmorReductionPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedArmorReductionPercent),
                "Expected Armor reduction must be between 0 and 100 percent."
            );
        }

        return new DamageTakenResourceGenerationDefinition
        {
            Key =
                DefinitionKey,

            Name =
                "Forever Warrior Rage from Damage Taken",

            ResourceKey =
                ForeverWarriorAutoAttackFactory.RageResourceKey,

            ReferenceHealth =
                expectedCreatureHealth,

            ResourcePerReferenceHealthOfEligibleDamage =
                ProvisionalUnmitigatedRagePerReferenceHealth,

            IgnoreArmorMitigation =
                true,

            IgnoredArmorReplacementReductionPercent =
                expectedArmorReductionPercent,

            IgnoreAbsorbs =
                true,

            BlockReducesEligibleDamage =
                true,

            RequiresExternalSourceActor =
                true
        };
    }

    public static void Configure(
        SimulationActorState actor,
        decimal expectedCreatureHealth,
        decimal expectedArmorReductionPercent)
    {
        ArgumentNullException.ThrowIfNull(actor);

        actor.AddDamageTakenResourceGeneration(
            Create(
                expectedCreatureHealth,
                expectedArmorReductionPercent
            )
        );
    }
}
