using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

public static class ForeverWarriorDamageTakenRageFactory
{
    public const string DefinitionKey =
        "forever-warrior-damage-taken-rage";

    public const decimal RagePerMaximumHealthOfEligibleDamage =
        10m;

    // Blizzard has verified the behavioral rules (ignore Armor and absorbs),
    // while this coefficient is currently based on Forever beta combat-log
    // measurements and should remain easy to replace if server tuning changes.
    public const bool CoefficientVerifiedByBlizzard = false;

    public static DamageTakenResourceGenerationDefinition Create()
    {
        return new DamageTakenResourceGenerationDefinition
        {
            Key =
                DefinitionKey,

            Name =
                "Forever Warrior Rage from Damage Taken",

            ResourceKey =
                ForeverWarriorAutoAttackFactory.RageResourceKey,

            ResourcePerMaximumHealthOfEligibleDamage =
                RagePerMaximumHealthOfEligibleDamage,

            IgnoreArmorMitigation =
                true,

            IgnoreAbsorbs =
                true,

            BlockReducesEligibleDamage =
                true,

            RequiresExternalSourceActor =
                true
        };
    }

    public static void Configure(
        SimulationActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        actor.AddDamageTakenResourceGeneration(
            Create()
        );
    }
}
