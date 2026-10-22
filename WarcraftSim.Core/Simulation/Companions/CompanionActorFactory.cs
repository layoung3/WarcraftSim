using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Companions;

/// <summary>A ruleset-independent companion identity and inherited-stat contract.
/// The companion is an ordinary independent engine actor, never a share of owner damage.</summary>
public sealed class CompanionDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string OwnerKey { get; init; }
    public required string FamilyKey { get; init; }
    public decimal BaseHealth { get; init; } = 2000m;
    public decimal BaseAttackPower { get; init; } = 300m;
    public decimal BaseArmor { get; init; } = 1500m;
    public decimal BaseResource { get; init; } = 100m;
    public string ResourceKey { get; init; } = "focus";
    public decimal ResourceRegenPerSecond { get; init; } = 5m;
    public decimal OwnerHealthCoefficient { get; init; }
    public decimal OwnerAttackPowerCoefficient { get; init; }
    public decimal OwnerArmorCoefficient { get; init; }
    public decimal FamilyHealthMultiplier { get; init; } = 1m;
    public decimal FamilyArmorMultiplier { get; init; } = 1m;
}

public sealed class CompanionIdentity
{
    public required string ActorKey { get; init; }
    public required string OwnerActorKey { get; init; }
    public required string FamilyKey { get; init; }
}

public static class CompanionActorFactory
{
    /// <summary>Registers a full actor in the existing context; caller supplies
    /// class-specific attacks, abilities, event processors, and threat rules.</summary>
    public static (SimulationActorState Actor, CompanionIdentity Identity) Create(
        SimulationContext context, SimulationActorState owner, CompanionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(definition);
        if (context.GetActor(owner.Key) is null || string.IsNullOrWhiteSpace(definition.Key) ||
            definition.OwnerKey != owner.Key || definition.BaseHealth <= 0m ||
            definition.BaseResource < 0m || definition.ResourceRegenPerSecond < 0m ||
            definition.FamilyHealthMultiplier <= 0m || definition.FamilyArmorMultiplier <= 0m)
            throw new ArgumentException("Companion requires a valid registered owner and nonnegative stats.");

        var inheritedAp = owner.Stats.Contains("companion-attack-power")
            ? owner.Stats.Get("companion-attack-power")
            : owner.Stats.Get("attack-power");
        var ap = definition.BaseAttackPower +
            inheritedAp * definition.OwnerAttackPowerCoefficient;
        var armor = (definition.BaseArmor +
            owner.Stats.Get("armor") * definition.OwnerArmorCoefficient) * definition.FamilyArmorMultiplier;
        var health = (definition.BaseHealth +
            owner.MaximumHealth * definition.OwnerHealthCoefficient) * definition.FamilyHealthMultiplier;
        var stats = new StatCollection();
        stats.Set("attack-power", Math.Max(0m, ap));
        stats.Set("armor", Math.Max(0m, armor));
        stats.Set("haste-percent", 0m);
        var pet = new SimulationActorState {
            Key = definition.Key, Name = definition.Name, Level = owner.Level,
            TeamKey = owner.TeamKey, MaximumHealth = health, Stats = stats
        };
        pet.InitializeHealth(health);
        pet.AddResource(new ResourceState(definition.ResourceKey,
            definition.BaseResource, definition.BaseResource,
            definition.ResourceRegenPerSecond));
        context.AddActor(pet);
        return (pet, new CompanionIdentity {
            ActorKey = pet.Key, OwnerActorKey = owner.Key, FamilyKey = definition.FamilyKey
        });
    }
}
