using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class DuplicateRuntimeKeyTests
{
    [Fact]
    public void AddActor_RejectsDuplicateKeysCaseInsensitively()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        1,

                    DurationSeconds =
                        10m,

                    PrimaryActorKey =
                        "player"
                }
            );

        context.AddActor(
            CreateActor(
                "Player"
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    context.AddActor(
                        CreateActor(
                            "player"
                        )
                    )
            );

        Assert.Contains(
            "Duplicate simulation actor key",
            exception.Message
        );
    }

    [Fact]
    public void AddResource_RejectsDuplicateKeysCaseInsensitively()
    {
        var actor =
            CreateActor(
                "player"
            );

        actor.AddResource(
            new ResourceState(
                resourceKey:
                    "mana",

                maximum:
                    100m,

                startingValue:
                    100m
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    actor.AddResource(
                        new ResourceState(
                            resourceKey:
                                "MANA",

                            maximum:
                                200m,

                            startingValue:
                                200m
                        )
                    )
            );

        Assert.Contains(
            "Duplicate simulation resource key",
            exception.Message
        );
    }

    [Fact]
    public void AddAbility_RejectsDuplicateKeysCaseInsensitively()
    {
        var actor =
            CreateActor(
                "player"
            );

        actor.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "fireball",

                Name =
                    "Fireball"
            }
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    actor.AddAbility(
                        new AbilityDefinition
                        {
                            Key =
                                "FIREBALL",

                            Name =
                                "Other Fireball"
                        }
                    )
            );

        Assert.Contains(
            "Duplicate simulation ability key",
            exception.Message
        );
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }
}
