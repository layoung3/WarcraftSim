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

    /// <summary>Restore charges and clear pending recoveries when a class ability
    /// explicitly resets this cooldown (for example, Rogue Preparation).
    /// Does not alter shared cooldown groups or the actor's global cooldown.
    /// </summary>
    public void ResetCooldown(decimal currentTimeSeconds)
    {
        if (currentTimeSeconds < 0m)
            throw new ArgumentOutOfRangeException(nameof(currentTimeSeconds));
        _chargeRecoveryTimes.Clear();
        CurrentCharges = Math.Max(1, Definition.MaxCharges);
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