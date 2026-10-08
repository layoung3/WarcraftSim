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
    EncounterDamageSequenceHit = 19,

    // Threat events are appended to preserve the existing numeric contract.
    ThreatChanged = 20,

    ForcedTargetApplied = 21,
    ForcedTargetRemoved = 22,

    // Internal expiration check for a forced-target override.
    ForcedTargetExpiration = 23,

    AbsorbApplied = 24,
    AbsorbConsumed = 25,
    AbsorbRemoved = 26,

    // Internal expiration check for a temporary absorb shield.
    AbsorbExpiration = 27,

    AbilityChannelStarted = 28,

    // Internal driver event for one channel tick.
    AbilityChannelTick = 29,

    AbilityChannelCompleted = 30,
    AbilityChannelCancelled = 31,

    // Background auto-attack events are appended to preserve the existing
    // serialized numeric contract. The swing event is an internal driver.
    AutoAttackStarted = 32,
    AutoAttackSwing = 33,
    AutoAttackStopped = 34,

    // Direct health-cost/heal adjustments that intentionally bypass normal
    // damage/healing combat resolution are appended to preserve the contract.
    HealthChanged = 35
}
