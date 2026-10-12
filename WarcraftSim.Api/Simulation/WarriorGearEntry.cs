namespace WarcraftSim.Api.Simulation;

/// <summary>Catalog-independent item selection for the first gear-stat pass.
/// A future validated Forever catalog resolves real item IDs into this form.</summary>
public sealed class WarriorGearEntry
{
    public string Slot { get; set; } = "";
    // Numeric game IDs are authoritative in 'catalog' mode. Client-supplied
    // Stats and names are explicitly rejected and resolved by the API.
    public int? ItemId { get; set; }
    public int? EnchantSpellId { get; set; }
    public string? SetBonusName { get; set; }
    public Dictionary<string, decimal> SetBonusStats { get; set; } = new();
    public string ItemKey { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public string? EnchantName { get; set; }
    public Dictionary<string, decimal> EnchantStats { get; set; } = new();
}
