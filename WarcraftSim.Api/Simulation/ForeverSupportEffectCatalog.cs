using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Server-owned, versioned set of static pre-pull support effects. Unknown and
/// unmodeled effects are rejected instead of silently fabricated or ignored.
/// This is a starting point for other classes; it is not Warrior-specific.
/// </summary>
public static class ForeverSupportEffectCatalog
{
    private static readonly Lazy<ForeverSupportCatalogDocument> Catalog = new(Load);
    private static readonly string[] Categories = ["raid-buff", "consumable", "boss-debuff"];
    private static readonly HashSet<string> SupportedStats = new(StringComparer.OrdinalIgnoreCase)
    {
        "strength", "agility", "attack-power", "attack-critical-chance-percent", "hit-chance-percent", "haste-percent"
    };

    public static ForeverSupportCatalogDocument GetCatalog() => Catalog.Value;

    public static ForeverSupportPlan Resolve(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = new ForeverSupportPlan();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (category, ids) in new[]
        {
            ("raid-buff", request.RaidBuffIds),
            ("consumable", request.ConsumableIds),
            ("boss-debuff", request.BossDebuffIds)
        })
        {
            if (ids is null || ids.Count > 30)
                throw new ArgumentException($"{category} selection must contain at most 30 IDs.");
            foreach (var rawId in ids)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                    throw new ArgumentException($"{category} contains an empty effect ID.");
                var id = rawId.Trim();
                if (!selected.Add(id))
                    throw new ArgumentException($"Support effect '{id}' is selected more than once.");
                var effect = GetCatalog().Effects.FirstOrDefault(x =>
                    string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ??
                    throw new ArgumentException($"Unknown Forever support effect '{id}'.");
                if (effect.Category != category)
                    throw new ArgumentException($"'{id}' is a {effect.Category}, not a {category}.");
                if (!effect.Modeled)
                    throw new ArgumentException($"'{effect.Name}' is listed but not modeled; remove it from the simulation.");
                if (!string.IsNullOrWhiteSpace(effect.StackingGroup))
                {
                    if (groups.TryGetValue(effect.StackingGroup, out var other))
                        throw new ArgumentException($"'{effect.Name}' cannot be combined with '{other}' ({effect.StackingGroup}).");
                    groups.Add(effect.StackingGroup, effect.Name);
                }
                plan.SelectedEffects.Add(effect);
                if (category == "boss-debuff")
                {
                    if (effect.MaximumStacks > 1)
                    {
                        if (request.SunderArmorStacks < 1 || request.SunderArmorStacks > effect.MaximumStacks)
                            throw new ArgumentException($"'{effect.Name}' requires 1–{effect.MaximumStacks} stacks.");
                        plan.TotalArmorReduction += effect.ArmorReductionPerStack * request.SunderArmorStacks;
                    }
                    else
                    {
                        plan.TotalArmorReduction += effect.ArmorReduction;
                    }
                }
            }
        }
        return plan;
    }

    private static ForeverSupportCatalogDocument Load()
    {
        var assembly = typeof(ForeverSupportEffectCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("forever-support-effects-v1.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("Embedded support catalog unavailable.");
        var document = JsonSerializer.Deserialize<ForeverSupportCatalogDocument>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidOperationException("Support catalog is empty.");
        if (string.IsNullOrWhiteSpace(document.CatalogVersion) || document.Effects.Count == 0 ||
            document.Effects.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Effects.Count)
            throw new InvalidOperationException("Support catalog contains invalid or duplicate IDs.");
        foreach (var effect in document.Effects)
        {
            if (!Categories.Contains(effect.Category, StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(effect.Id) || string.IsNullOrWhiteSpace(effect.Name) ||
                effect.MaximumStacks < 1 || effect.ArmorReduction < 0m || effect.ArmorReductionPerStack < 0m ||
                effect.Stats.Any(x => !SupportedStats.Contains(x.Key) || x.Value < 0m || x.Value > 10000m))
                throw new InvalidOperationException($"Invalid Forever support catalog effect: '{effect.Id}'.");
            if (effect.Category != "boss-debuff" && (effect.ArmorReduction != 0m || effect.ArmorReductionPerStack != 0m))
                throw new InvalidOperationException($"Nondebuff effect '{effect.Id}' cannot reduce boss armor.");
        }
        return document;
    }
}

public sealed class ForeverSupportCatalogDocument
{
    public string CatalogVersion { get; set; } = "";
    public string Scope { get; set; } = "";
    public List<ForeverSupportEffect> Effects { get; set; } = [];
}

public sealed class ForeverSupportEffect
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Reach { get; set; } = "";
    public string StackingGroup { get; set; } = "";
    public bool Modeled { get; set; }
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public decimal ArmorReduction { get; set; }
    public decimal ArmorReductionPerStack { get; set; }
    public int MaximumStacks { get; set; } = 1;
    public string SourceUrl { get; set; } = "";
    public string SourceStatus { get; set; } = "";
}

public sealed class ForeverSupportPlan
{
    public List<ForeverSupportEffect> SelectedEffects { get; } = [];
    public decimal TotalArmorReduction { get; set; }
}
