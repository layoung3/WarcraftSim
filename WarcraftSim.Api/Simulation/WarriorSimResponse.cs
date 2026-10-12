using WarcraftSim.Core.Simulation.Batch;

namespace WarcraftSim.Api.Simulation;

public sealed class WarriorSimResponse
{
    public string Mode { get; set; } = "Level-60 Warrior DPS alpha";
    public string Preset { get; set; } = "arms";
    public string? EquipmentCatalogVersion { get; set; }
    public string? ResolvedMainHandWeapon { get; set; }
    public string? ResolvedOffHandWeapon { get; set; }
    public decimal MeanDps { get; set; }
    public decimal MedianDps { get; set; }
    public SimulationBatchResult Batch { get; set; } = new();
    public List<string> ModelingNotes { get; set; } = [];
    public Dictionary<string, decimal> EffectiveCharacterStats { get; set; } = new();
    public IReadOnlyList<WarcraftSim.Core.Simulation.Building.CharacterSimulationStatContribution>
        StatContributions { get; set; } = [];

}
