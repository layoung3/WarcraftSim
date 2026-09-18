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
    EncounterPhaseEnded
}
