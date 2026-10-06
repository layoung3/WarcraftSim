namespace WarcraftSim.Core.Simulation.Engine;

public enum CombatEventType
{
    SimulationStarted,
    SimulationEnded,

    RotationDecision,

    AbilityCastStarted,
    AbilityCastCompleted,
    AbilityCastCancelled,
    AbilityEffectImpact,

    Damage,
    Healing,

    ResourceChanged,

    AuraApplied,
    AuraRemoved,
    AuraExpiration,

    PeriodicTick,

    ActorDied,

    EncounterPhaseStarted,
    EncounterPhaseEnded,

    ScriptedDamage,

    // Internal event. Targets are resolved when the pattern actually
    // occurs, so dead actors are naturally excluded at that moment.
    EncounterDamagePatternOccurrence
}
