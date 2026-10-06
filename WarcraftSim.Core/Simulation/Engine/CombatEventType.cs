namespace WarcraftSim.Core.Simulation.Engine;

public enum CombatEventType
{
    // Explicit values keep timeline/event serialization stable as new
    // internal event types are added.
    SimulationStarted = 0,
    SimulationEnded = 1,

    RotationDecision = 2,

    AbilityCastStarted = 3,
    AbilityCastCompleted = 4,
    AbilityCastCancelled = 5,
    AbilityEffectImpact = 6,

    Damage = 7,
    Healing = 8,

    ResourceChanged = 9,

    AuraApplied = 10,
    AuraRemoved = 11,
    AuraExpiration = 12,

    PeriodicTick = 13,

    ActorDied = 14,

    EncounterPhaseStarted = 15,
    EncounterPhaseEnded = 16,

    ScriptedDamage = 17,

    EncounterDamagePatternOccurrence = 18,

    // Internal event for a sub-hit inside one encounter pattern occurrence.
    // This allows dynamic target resolution at the actual sub-hit timestamp.
    EncounterDamageSequenceHit = 19
}
