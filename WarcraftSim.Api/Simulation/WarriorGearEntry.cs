namespace WarcraftSim.Api.Simulation;

/// <summary>Catalog-independent item selection for the first gear-stat pass.
/// A future validated Forever catalog resolves real item IDs into this form.</summary>
public sealed class WarriorGearEntry
{
    public string Slot { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public string? EnchantName { get; set; }
    public Dictionary<string, decimal> EnchantStats { get; set; } = new();
}
