namespace WarcraftSim.Core.Simulation.Engine;

public enum CombatEventType
{
    // These numeric values are an intentional serialization contract.
    // Keep existing values stable when adding new event types.
    SimulationStarted = 0,
    SimulationEnded = 1,

    RotationDecision = 2,

    AbilityCastStarted = 3,
    AbilityCastCompleted = 4,
    AbilityEffectImpact = 5,

    Damage = 6,
    Healing = 7,

    ResourceChanged = 8,

    AuraApplied = 9,
    AuraRemoved = 10,
    AuraExpiration = 11,

    PeriodicTick = 12,

    ActorDied = 13,

    EncounterPhaseStarted = 14,
    EncounterPhaseEnded = 15,

    // Cancellation was added after the original public event sequence.
    // It is intentionally pinned here rather than shifting older values.
    AbilityCastCancelled = 16,

    ScriptedDamage = 17,

    EncounterDamagePatternOccurrence = 18,

    // Internal event for a sub-hit inside one encounter pattern occurrence.
    // This allows dynamic target resolution at the actual sub-hit timestamp.
    EncounterDamageSequenceHit = 19
}
