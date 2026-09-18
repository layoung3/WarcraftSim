using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationActorState
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string TeamKey { get; set; } = "";

    // Optional per-actor raid/simulation role. This is deliberately
    // separate from class/spec so any class can be assigned any role.
    public SimulationType? AssignedRole { get; set; }

    public int Level { get; set; } = 1;

    public decimal MaximumHealth { get; set; }

    public decimal CurrentHealth { get; private set; }

    public StatCollection Stats { get; set; } = new();

    public Dictionary<string, ResourceState> Resources { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, AbilityState> Abilities { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<AuraInstance> ActiveAuras { get; } = [];

    public decimal GlobalCooldownReadyAtSeconds { get; private set; }

    public decimal CastReadyAtSeconds { get; private set; }

    public Guid? CurrentCastExecutionId { get; private set; }

    public string? CurrentCastAbilityKey { get; private set; }

    public decimal InitialActionDelaySeconds { get; private set; }

    public decimal InputDelaySeconds { get; private set; }

    public decimal InputReadyAtSeconds { get; private set; }

    public bool IsAlive =>
        CurrentHealth > 0m;

    public void InitializeHealth(
        decimal maximumHealth,
        decimal? startingHealth = null)
    {
        MaximumHealth =
            Math.Max(
                0m,
                maximumHealth
            );

        CurrentHealth =
            Math.Clamp(
                startingHealth ?? MaximumHealth,
                0m,
                MaximumHealth
            );
    }

    public decimal TakeDamage(
        decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        var actualDamage =
            Math.Min(
                CurrentHealth,
                amount
            );

        CurrentHealth -=
            actualDamage;

        return actualDamage;
    }

    public decimal Heal(
        decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        var previousHealth =
            CurrentHealth;

        CurrentHealth =
            Math.Min(
                MaximumHealth,
                CurrentHealth + amount
            );

        return
            CurrentHealth -
            previousHealth;
    }

    public void AddResource(
        ResourceState resource)
    {
        Resources[
            resource.ResourceKey
        ] = resource;
    }

    public void RefreshResources(
        decimal currentTimeSeconds)
    {
        foreach (
            var resource in
            Resources.Values)
        {
            resource.AdvanceTo(
                currentTimeSeconds
            );
        }
    }

    public void AddAbility(
        AbilityDefinition definition)
    {
        Abilities[
            definition.Key
        ] = new AbilityState(
            definition
        );
    }

    public void ConfigureActionTiming(
        decimal initialActionDelaySeconds,
        decimal inputDelaySeconds,
        decimal simulationStartSeconds = 0m)
    {
        InitialActionDelaySeconds =
            Math.Max(
                0m,
                initialActionDelaySeconds
            );

        InputDelaySeconds =
            Math.Max(
                0m,
                inputDelaySeconds
            );

        InputReadyAtSeconds =
            Math.Max(
                0m,
                simulationStartSeconds
            ) +
            InitialActionDelaySeconds;
    }

    public bool IsInputReady(
        decimal currentTimeSeconds)
    {
        return
            currentTimeSeconds >=
            InputReadyAtSeconds;
    }

    public void RegisterActionStarted(
        decimal currentTimeSeconds,
        decimal castTimeSeconds,
        decimal globalCooldownSeconds)
    {
        var actionLockSeconds =
            Math.Max(
                Math.Max(
                    0m,
                    castTimeSeconds
                ),
                Math.Max(
                    0m,
                    globalCooldownSeconds
                )
            );

        InputReadyAtSeconds =
            Math.Max(
                InputReadyAtSeconds,
                currentTimeSeconds +
                actionLockSeconds +
                InputDelaySeconds
            );
    }

    public void TrackCurrentCast(
        Guid executionId,
        string abilityKey,
        decimal castTimeSeconds)
    {
        if (castTimeSeconds <= 0m)
        {
            return;
        }

        CurrentCastExecutionId =
            executionId;

        CurrentCastAbilityKey =
            abilityKey;
    }

    public void CompleteCurrentCast(
        Guid? executionId)
    {
        if (
            !executionId.HasValue ||
            !CurrentCastExecutionId.HasValue ||
            executionId.Value !=
                CurrentCastExecutionId.Value
        )
        {
            return;
        }

        CurrentCastExecutionId =
            null;

        CurrentCastAbilityKey =
            null;
    }

    public Guid? CancelCurrentCast(
        decimal currentTimeSeconds)
    {
        if (!CurrentCastExecutionId.HasValue)
        {
            return null;
        }

        var cancelledExecutionId =
            CurrentCastExecutionId;

        CurrentCastExecutionId =
            null;

        CurrentCastAbilityKey =
            null;

        CastReadyAtSeconds =
            currentTimeSeconds;

        InputReadyAtSeconds =
            currentTimeSeconds;

        return
            cancelledExecutionId;
    }

    public bool IsCasting(
        decimal currentTimeSeconds)
    {
        return
            CurrentCastExecutionId.HasValue &&
            currentTimeSeconds <
            CastReadyAtSeconds;
    }

    public void StartGlobalCooldown(
        decimal currentTimeSeconds,
        decimal durationSeconds)
    {
        if (durationSeconds <= 0m)
        {
            return;
        }

        GlobalCooldownReadyAtSeconds =
            Math.Max(
                GlobalCooldownReadyAtSeconds,
                currentTimeSeconds +
                durationSeconds
            );
    }

    public bool IsGlobalCooldownReady(
        decimal currentTimeSeconds)
    {
        return
            currentTimeSeconds >=
            GlobalCooldownReadyAtSeconds;
    }

    public void StartCast(
        decimal currentTimeSeconds,
        decimal castTimeSeconds)
    {
        CastReadyAtSeconds =
            Math.Max(
                CastReadyAtSeconds,
                currentTimeSeconds +
                Math.Max(
                    0m,
                    castTimeSeconds
                )
            );
    }

    public bool IsCastReady(
        decimal currentTimeSeconds)
    {
        return
            currentTimeSeconds >=
            CastReadyAtSeconds;
    }
}
