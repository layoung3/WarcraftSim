using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityState
{
    private readonly List<decimal> _chargeRecoveryTimes = [];

    public AbilityDefinition Definition { get; }

    public int CurrentCharges { get; private set; }

    public AbilityState(AbilityDefinition definition)
    {
        Definition = definition;

        CurrentCharges = Math.Max(
            1,
            definition.MaxCharges
        );
    }

    public bool IsReady(decimal currentTimeSeconds)
    {
        RefreshCharges(currentTimeSeconds);

        return CurrentCharges > 0;
    }

    public decimal GetNextReadyTime(decimal currentTimeSeconds)
    {
        RefreshCharges(currentTimeSeconds);

        if (CurrentCharges > 0)
        {
            return currentTimeSeconds;
        }

        return _chargeRecoveryTimes.Count > 0
            ? _chargeRecoveryTimes[0]
            : currentTimeSeconds;
    }

    public bool ConsumeCharge(decimal currentTimeSeconds)
    {
        RefreshCharges(currentTimeSeconds);

        if (CurrentCharges <= 0)
        {
            return false;
        }

        CurrentCharges--;

        var recoveryDuration =
            Definition.ChargeRecoverySeconds ??
            Definition.CooldownSeconds;

        if (recoveryDuration <= 0m)
        {
            CurrentCharges++;

            return true;
        }

        var recoveryStart =
            _chargeRecoveryTimes.Count > 0
                ? Math.Max(
                    currentTimeSeconds,
                    _chargeRecoveryTimes[^1]
                )
                : currentTimeSeconds;

        _chargeRecoveryTimes.Add(
            recoveryStart + recoveryDuration
        );

        return true;
    }

    private void RefreshCharges(decimal currentTimeSeconds)
    {
        while (
            _chargeRecoveryTimes.Count > 0 &&
            _chargeRecoveryTimes[0] <= currentTimeSeconds
        )
        {
            _chargeRecoveryTimes.RemoveAt(0);

            CurrentCharges = Math.Min(
                Math.Max(1, Definition.MaxCharges),
                CurrentCharges + 1
            );
        }
    }
}