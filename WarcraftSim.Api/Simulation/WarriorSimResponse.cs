using WarcraftSim.Core.Simulation.Batch;

namespace WarcraftSim.Api.Simulation;

public sealed class WarriorSimResponse
{
    public string Mode { get; set; } = "Level-60 Warrior DPS alpha";
    public string Preset { get; set; } = "arms";
    public string? EquipmentCatalogVersion { get; set; }
    public string? SupportCatalogVersion { get; set; }
    public List<string> ActiveSupportEffects { get; set; } = [];
    public decimal EffectiveBossArmor { get; set; }
    public string? ResolvedMainHandWeapon { get; set; }
    public string? ResolvedOffHandWeapon { get; set; }
    public decimal MeanDps { get; set; }
    public decimal MeanDamageTakenPerSecond { get; set; }
    public decimal MeanMaximumWindowPercentOfHealth { get; set; }
    public decimal P95MaximumWindowPercentOfHealth { get; set; }
    public decimal RepresentativeMaximumWindowDamage { get; set; }
    public decimal RepresentativeMaximumWindowPercentOfHealth { get; set; }
    public decimal RepresentativeWindowThresholdExceeded { get; set; }

    public decimal DeathRatePercent { get; set; }
    public decimal RepresentativeBossThreat { get; set; }
    public decimal RepresentativeThreatPerSecond { get; set; }
    public decimal MedianDps { get; set; }
    public SimulationBatchResult Batch { get; set; } = new();
    public List<string> ModelingNotes { get; set; } = [];
    public Dictionary<string, decimal> EffectiveCharacterStats { get; set; } = new();
    public IReadOnlyList<WarcraftSim.Core.Simulation.Building.CharacterSimulationStatContribution>
        StatContributions { get; set; } = [];

}
