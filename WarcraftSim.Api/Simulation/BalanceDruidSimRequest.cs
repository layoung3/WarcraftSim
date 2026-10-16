using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

public sealed class BalanceDruidSimRequest
{
    public int Seed {get;set;}=134001;
    public decimal DurationSeconds {get;set;}=120m;
    public bool CaptureTimeline {get;set;}=true;
    public decimal TargetMaximumHealth {get;set;}=200000m;
    public decimal MaximumHealth {get;set;}=3400m;
    public decimal StartingManaPercent {get;set;}=100m;
    public decimal BaseSpellHitPercent {get;set;}=83m;
    public decimal SpellCritPercent {get;set;}=14m;
    public bool UseMoonfire {get;set;}=true;
    public bool UseInsectSwarm {get;set;}=true;
    public bool UseStarfire {get;set;}=true;
    public bool UseEclipse {get;set;}=true;
    public bool StarfireOnlyWithEclipse {get;set;}=true;
    public int MajorManaPotionCharges {get;set;}=1;
    public decimal MajorManaPotionBelowManaPercent {get;set;}=50m;
    public CombatUseItemsOptions CombatItems {get;set;}=new();
    public ForeverBalanceDruidTalents Talents {get;set;}=new();
    public ForeverBalanceDruidTuning Tuning {get;set;}=new();
    public CharacterProfile Character {get;set;}=new()
    {
        Name="Illustrative Balance Druid",RulesetKey="wow-forever",ClassKey="druid",
        SpecializationKey="balance",Level=60,
        BaseStats=new StatCollection{Values=new Dictionary<string,decimal>
        { ["intellect"]=230m,["spirit"]=190m,["spell-power"]=210m,["mp5"]=20m}}
    };
}
public sealed class BalanceDruidSimResponse
{
    public decimal Dps {get;init;}
    public decimal DamageDone {get;init;}
    public decimal EndingMana {get;init;}
    public decimal MaximumMana {get;init;}
    public decimal SpellPower {get;init;}
    public int EclipseChargesGenerated {get;init;}
    public int EclipseEmpoweredStarfires {get;init;}
    public int MaximumEclipseCharges {get;init;}
    public int MajorManaPotionUses {get;init;}
    public Dictionary<string,int> CombatItemUses {get;init;}=new();
    public Dictionary<string,decimal> DamageByAbility {get;init;}=new();
    public SimulationRunResult Run {get;init;}=new();
    public List<string> ModelingNotes {get;init;}=[];
}
