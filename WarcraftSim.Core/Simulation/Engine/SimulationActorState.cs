using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationActorState
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

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

    public void AddAbility(
        AbilityDefinition definition)
    {
        Abilities[
            definition.Key
        ] = new AbilityState(
            definition
        );
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
