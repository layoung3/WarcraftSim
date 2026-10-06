using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class AuraManagerTests
{
    [Fact]
    public void Stack_FirstApplicationStartsAtOneStack()
    {
        var (context, target, manager) =
            CreateTestState();

        var aura =
            manager.ApplyAura(
                context,
                target,
                CreateAuraDefinition(
                    AuraStackingMode.Stack,
                    maxStacks:
                        5),
                sourceActorKey:
                    "source",
                abilityKey:
                    "test-ability",
                effectKey:
                    "test-effect",
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            1,
            aura.Stacks
        );

        Assert.Single(
            target.ActiveAuras
        );
    }

    [Fact]
    public void Stack_ReapplicationsIncreaseByOneAndRespectMaxStacks()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Stack,
                maxStacks:
                    3
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var second =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Same(
            first,
            second
        );

        Assert.Equal(
            2,
            second.Stacks
        );

        manager.ApplyAura(
            context,
            target,
            definition,
            "source",
            null,
            null,
            scheduleExpiration:
                false
        );

        var capped =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            3,
            capped.Stacks
        );

        Assert.Single(
            target.ActiveAuras
        );
    }

    [Fact]
    public void Refresh_ReapplicationKeepsStackCountAndRefreshesDuration()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Refresh,
                durationSeconds:
                    5m
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var originalInstanceId =
            first.InstanceId;

        AdvanceTo(
            context,
            3m
        );

        var refreshed =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Same(
            first,
            refreshed
        );

        Assert.Equal(
            1,
            refreshed.Stacks
        );

        Assert.NotEqual(
            originalInstanceId,
            refreshed.InstanceId
        );

        Assert.Equal(
            8m,
            refreshed.ExpiresAtSeconds
        );
    }

    [Fact]
    public void Replace_RemovesExistingAuraAndResetsStacks()
    {
        var (context, target, manager) =
            CreateTestState();

        var stackingDefinition =
            CreateAuraDefinition(
                AuraStackingMode.Stack,
                maxStacks:
                    5
            );

        manager.ApplyAura(
            context,
            target,
            stackingDefinition,
            "source",
            null,
            null,
            scheduleExpiration:
                false
        );

        var stacked =
            manager.ApplyAura(
                context,
                target,
                stackingDefinition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            2,
            stacked.Stacks
        );

        var oldInstanceId =
            stacked.InstanceId;

        var replacementDefinition =
            CreateAuraDefinition(
                AuraStackingMode.Replace,
                maxStacks:
                    5
            );

        var replacement =
            manager.ApplyAura(
                context,
                target,
                replacementDefinition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Single(
            target.ActiveAuras
        );

        Assert.Same(
            replacement,
            target.ActiveAuras[0]
        );

        Assert.Equal(
            1,
            replacement.Stacks
        );

        Assert.NotEqual(
            oldInstanceId,
            replacement.InstanceId
        );
    }

    [Fact]
    public void Independent_CreatesSeparateAuraInstances()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Independent
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var second =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            2,
            target.ActiveAuras.Count
        );

        Assert.NotSame(
            first,
            second
        );

        Assert.NotEqual(
            first.InstanceId,
            second.InstanceId
        );

        Assert.All(
            target.ActiveAuras,
            aura =>
                Assert.Equal(
                    1,
                    aura.Stacks
                )
        );
    }

    [Fact]
    public void Refresh_StaleExpirationDoesNotRemoveCurrentAura()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Refresh,
                durationSeconds:
                    5m
            );

        manager.ApplyAura(
            context,
            target,
            definition,
            "source",
            null,
            null,
            scheduleExpiration:
                true
        );

        AdvanceTo(
            context,
            2m
        );

        var refreshed =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    true
            );

        Assert.True(
            context.TryGetNextEvent(
                out var staleExpiration
            )
        );

        Assert.NotNull(
            staleExpiration
        );

        Assert.Equal(
            5m,
            staleExpiration!.TimeSeconds
        );

        manager.Process(
            context,
            staleExpiration
        );

        Assert.Single(
            target.ActiveAuras
        );

        Assert.Same(
            refreshed,
            target.ActiveAuras[0]
        );

        Assert.True(
            context.TryGetNextEvent(
                out var currentExpiration
            )
        );

        Assert.NotNull(
            currentExpiration
        );

        Assert.Equal(
            7m,
            currentExpiration!.TimeSeconds
        );

        manager.Process(
            context,
            currentExpiration
        );

        Assert.Empty(
            target.ActiveAuras
        );
    }

    private static (
        SimulationContext Context,
        SimulationActorState Target,
        AuraManager Manager)
        CreateTestState()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        30m,

                    PrimaryActorKey =
                        "target",

                    CaptureTimeline =
                        true
                }
            );

        var target =
            new SimulationActorState
            {
                Key =
                    "target",

                Name =
                    "Aura Target",

                TeamKey =
                    "raid"
            };

        target.InitializeHealth(
            10000m
        );

        context.AddActor(
            target
        );

        return (
            context,
            target,
            new AuraManager()
        );
    }

    private static AuraDefinition CreateAuraDefinition(
        AuraStackingMode stackingMode,
        int maxStacks = 1,
        decimal durationSeconds = 10m)
    {
        return new AuraDefinition
        {
            Key =
                "test-aura",

            Name =
                "Test Aura",

            DurationSeconds =
                durationSeconds,

            StackingMode =
                stackingMode,

            MaxStacks =
                maxStacks
        };
    }

    private static void AdvanceTo(
        SimulationContext context,
        decimal timeSeconds)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.RotationDecision,

                IsInternal =
                    true,

                Description =
                    "Aura test time advance."
            }
        );

        Assert.True(
            context.TryGetNextEvent(
                out var timeAdvance
            )
        );

        Assert.NotNull(
            timeAdvance
        );

        Assert.Equal(
            timeSeconds,
            context.CurrentTimeSeconds
        );
    }
}
