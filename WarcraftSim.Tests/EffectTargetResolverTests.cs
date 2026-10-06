using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EffectTargetResolverTests
{
    [Fact]
    public void Resolve_SelfTargetsSourceInsteadOfPrimaryTarget()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "source",
                "raid"
            );

        var enemy =
            CreateActor(
                "enemy",
                "enemy"
            );

        context.AddActor(
            source
        );

        context.AddActor(
            enemy
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                enemy,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Self,

                    MaxTargets =
                        1
                }
            );

        var resolved =
            Assert.Single(
                targets
            );

        Assert.Same(
            source,
            resolved
        );
    }

    [Fact]
    public void Resolve_FriendlyPrioritizesPrimaryTargetThenUsesStableKeyOrder()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var primary =
            CreateActor(
                "tank",
                "raid"
            );

        var allyB =
            CreateActor(
                "z-ally",
                "raid"
            );

        var allyA =
            CreateActor(
                "a-ally",
                "raid"
            );

        var enemy =
            CreateActor(
                "enemy",
                "enemy"
            );

        // Intentionally add actors in an order that differs from key order.
        context.AddActor(
            source
        );

        context.AddActor(
            primary
        );

        context.AddActor(
            allyB
        );

        context.AddActor(
            enemy
        );

        context.AddActor(
            allyA
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                primary,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Friendly,

                    MaxTargets =
                        3
                }
            );

        Assert.Equal(
            new[]
            {
                "tank",
                "a-ally",
                "caster"
            },
            targets.Select(
                    target =>
                        target.Key
                )
                .ToArray()
        );
    }

    [Fact]
    public void Resolve_EnemyPrioritizesPrimaryAndExcludesFriendlyActors()
    {
        var context =
            CreateContext();

        var source =
            CreateActor(
                "source",
                "raid"
            );

        var primary =
            CreateActor(
                "boss",
                "enemy"
            );

        var addB =
            CreateActor(
                "z-add",
                "enemy"
            );

        var addA =
            CreateActor(
                "a-add",
                "enemy"
            );

        var ally =
            CreateActor(
                "ally",
                "raid"
            );

        context.AddActor(
            source
        );

        context.AddActor(
            addB
        );

        context.AddActor(
            ally
        );

        context.AddActor(
            primary
        );

        context.AddActor(
            addA
        );

        var targets =
            EffectTargetResolver.Resolve(
                context,
                source,
                primary,
                new AbilityEffectDefinition
                {
                    TargetType =
                        AbilityTargetTypes.Enemy,

                    MaxTargets =
                        3
                }
            );

        Assert.Equal(
            new[]
            {
                "boss",
                "a-add",
                "z-add"
            },
            targets.Select(
                    target =>
                        target.Key
                )
                .ToArray()
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    5m,

                PrimaryActorKey =
                    "source"
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }
}
