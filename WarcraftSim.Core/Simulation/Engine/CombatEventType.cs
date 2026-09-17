namespace WarcraftSim.Core.Simulation.Engine;

public enum CombatEventType
{
    SimulationStarted,
    SimulationEnded,

    AbilityCastStarted,
    AbilityCastCompleted,

    Damage,
    Healing,

    ResourceChanged,

    AuraApplied,
    AuraRemoved,

    PeriodicTick,

    ActorDied
}