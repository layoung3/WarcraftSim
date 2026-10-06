using WarcraftSim.Core.Characters;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationClassCatalog
{
    private readonly IReadOnlyList<CharacterSimulationClassDefinition>
        _definitions;

    public CharacterSimulationClassCatalog(
        IEnumerable<CharacterSimulationClassDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(
            definitions
        );

        var materialized =
            definitions.ToList();

        ValidateDefinitions(
            materialized
        );

        _definitions =
            materialized;
    }

    public IReadOnlyList<CharacterSimulationClassDefinition>
        Definitions =>
            _definitions;

    public CharacterSimulationClassDefinition Resolve(
        CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        return Resolve(
            profile.RulesetKey,
            profile.ClassKey,
            profile.SpecializationKey
        );
    }

    public CharacterSimulationClassDefinition Resolve(
        string rulesetKey,
        string classKey,
        string? specializationKey)
    {
        if (string.IsNullOrWhiteSpace(
                rulesetKey))
        {
            throw new ArgumentException(
                "Ruleset key is required.",
                nameof(rulesetKey)
            );
        }

        if (string.IsNullOrWhiteSpace(
                classKey))
        {
            throw new ArgumentException(
                "Class key is required.",
                nameof(classKey)
            );
        }

        var classMatches =
            _definitions
                .Where(definition =>
                    string.Equals(
                        definition.RulesetKey,
                        rulesetKey,
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    string.Equals(
                        definition.ClassKey,
                        classKey,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToList();

        if (
            !string.IsNullOrWhiteSpace(
                specializationKey)
        )
        {
            var exact =
                classMatches
                    .FirstOrDefault(
                        definition =>
                            string.Equals(
                                definition.SpecializationKey,
                                specializationKey,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );

            if (exact is not null)
            {
                return exact;
            }
        }

        var classWide =
            classMatches
                .FirstOrDefault(
                    definition =>
                        string.IsNullOrWhiteSpace(
                            definition.SpecializationKey
                        )
                );

        if (classWide is not null)
        {
            return classWide;
        }

        throw new InvalidOperationException(
            $"No simulation class definition was found for ruleset '{rulesetKey}', class '{classKey}', specialization '{specializationKey ?? "(none)"}'."
        );
    }

    private static void ValidateDefinitions(
        IReadOnlyList<CharacterSimulationClassDefinition> definitions)
    {
        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var definition in
            definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException(
                    "Simulation class catalog cannot contain null definitions.",
                    nameof(definitions)
                );
            }

            if (string.IsNullOrWhiteSpace(
                    definition.RulesetKey))
            {
                throw new ArgumentException(
                    "Simulation class definitions require a ruleset key.",
                    nameof(definitions)
                );
            }

            if (string.IsNullOrWhiteSpace(
                    definition.ClassKey))
            {
                throw new ArgumentException(
                    "Simulation class definitions require a class key.",
                    nameof(definitions)
                );
            }

            var normalizedSpecialization =
                string.IsNullOrWhiteSpace(
                    definition.SpecializationKey)
                    ? "*"
                    : definition.SpecializationKey.Trim();

            var key =
                $"{definition.RulesetKey.Trim()}|{definition.ClassKey.Trim()}|{normalizedSpecialization}";

            if (!keys.Add(
                    key))
            {
                throw new ArgumentException(
                    $"Duplicate simulation class definition for ruleset '{definition.RulesetKey}', class '{definition.ClassKey}', specialization '{definition.SpecializationKey ?? "(class-wide)"}'.",
                    nameof(definitions)
                );
            }
        }
    }
}
