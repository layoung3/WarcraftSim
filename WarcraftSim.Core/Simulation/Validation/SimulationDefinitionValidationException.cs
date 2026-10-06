namespace WarcraftSim.Core.Simulation.Validation;

public sealed class SimulationDefinitionValidationException :
    InvalidOperationException
{
    public SimulationDefinitionValidationException(
        IEnumerable<string> errors)
        : base(
            BuildMessage(
                errors
            )
        )
    {
        Errors = errors
            .Where(error =>
                !string.IsNullOrWhiteSpace(
                    error))
            .Distinct(
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }

    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(
        IEnumerable<string> errors)
    {
        var materialized = errors
            .Where(error =>
                !string.IsNullOrWhiteSpace(
                    error))
            .Distinct(
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        if (materialized.Count == 0)
        {
            return "Simulation definition validation failed.";
        }

        return
            "Simulation definition validation failed:" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                materialized.Select(
                    error =>
                        $" - {error}"
                )
            );
    }
}
