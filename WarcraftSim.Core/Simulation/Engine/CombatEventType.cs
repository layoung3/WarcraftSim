namespace WarcraftSim.Core.Simulation.Engine;

public enum CombatEventType
{
    SimulationStarted,
    SimulationEnded,

    RotationDecision,

    AbilityCastStarted,
    AbilityCastCompleted,
    AbilityEffectImpact,

    Damage,
    Healing,

    ResourceChanged,

    AuraApplied,
    AuraRemoved,
    AuraExpiration,

    PeriodicTick,

    ActorDied
}
