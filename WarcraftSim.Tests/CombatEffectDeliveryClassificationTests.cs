using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class CombatEffectDeliveryClassificationTests
{
    [Theory]
    [InlineData(
        CombatEffectDeliveryType.Direct,
        false,
        false)]
    [InlineData(
        CombatEffectDeliveryType.Periodic,
        true,
        false)]
    [InlineData(
        CombatEffectDeliveryType.ChannelTick,
        false,
        true)]
    public void CombatEvent_DeliveryTypeExposesExpectedClassification(
        CombatEffectDeliveryType deliveryType,
        bool expectedPeriodic,
        bool expectedChannelTick)
    {
        var combatEvent =
            new CombatEvent
            {
                EffectDeliveryType =
                    deliveryType
            };

        Assert.Equal(
            expectedPeriodic,
            combatEvent.IsPeriodic
        );

        Assert.Equal(
            expectedChannelTick,
            combatEvent.IsChannelTick
        );
    }
}
