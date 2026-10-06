using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class CombatEventTypeContractTests
{
    [Fact]
    public void NumericValues_RemainStable()
    {
        var expected =
            new Dictionary<CombatEventType, int>
            {
                [CombatEventType.SimulationStarted] =
                    0,

                [CombatEventType.SimulationEnded] =
                    1,

                [CombatEventType.RotationDecision] =
                    2,

                [CombatEventType.AbilityCastStarted] =
                    3,

                [CombatEventType.AbilityCastCompleted] =
                    4,

                [CombatEventType.AbilityEffectImpact] =
                    5,

                [CombatEventType.Damage] =
                    6,

                [CombatEventType.Healing] =
                    7,

                [CombatEventType.ResourceChanged] =
                    8,

                [CombatEventType.AuraApplied] =
                    9,

                [CombatEventType.AuraRemoved] =
                    10,

                [CombatEventType.AuraExpiration] =
                    11,

                [CombatEventType.PeriodicTick] =
                    12,

                [CombatEventType.ActorDied] =
                    13,

                [CombatEventType.EncounterPhaseStarted] =
                    14,

                [CombatEventType.EncounterPhaseEnded] =
                    15,

                [CombatEventType.AbilityCastCancelled] =
                    16,

                [CombatEventType.ScriptedDamage] =
                    17,

                [CombatEventType.EncounterDamagePatternOccurrence] =
                    18,

                [CombatEventType.EncounterDamageSequenceHit] =
                    19,

                [CombatEventType.ThreatChanged] =
                    20
            };

        Assert.Equal(
            expected.Count,
            Enum.GetValues<CombatEventType>().Length
        );

        foreach (
            var expectedValue in
            expected)
        {
            Assert.Equal(
                expectedValue.Value,
                (int)expectedValue.Key
            );
        }
    }
}
