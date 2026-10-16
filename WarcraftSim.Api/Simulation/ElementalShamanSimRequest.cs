using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

public sealed class ElementalShamanSimRequest
{
    public int Seed {get;set;}=133001;
    public decimal DurationSeconds {get;set;}=120m;
    public bool CaptureTimeline {get;set;}=true;
    public decimal TargetMaximumHealth {get;set;}=200000m;
    public decimal MaximumHealth {get;set;}=3500m;
    public decimal StartingManaPercent {get;set;}=100m;
    public decimal BaseSpellHitPercent {get;set;}=83m;
    public decimal SpellCritPercent {get;set;}=15m;
    public bool UseFlameShock {get;set;}=true;
    public bool UseLavaBurst {get;set;}=true;
    public bool UseChainLightning {get;set;}=true;
    public bool UseLightningOverload {get;set;}=true;
    public int MajorManaPotionCharges {get;set;}=1;
    public decimal MajorManaPotionBelowManaPercent {get;set;}=50m;
    public CombatUseItemsOptions CombatItems {get;set;}=new();
    public ForeverElementalShamanTalents Talents {get;set;}=new();
    public ForeverElementalShamanTuning Tuning {get;set;}=new();
    public CharacterProfile Character {get;set;}=new()
    {
        Name="Illustrative Elemental Shaman",RulesetKey="wow-forever",ClassKey="shaman",
        SpecializationKey="elemental",Level=60,
        BaseStats=new StatCollection{Values=new Dictionary<string,decimal>
        { ["intellect"]=220m,["spirit"]=175m,["spell-power"]=210m,["mp5"]=25m}}
    };
}
public sealed class ElementalShamanSimResponse
{
    public decimal Dps {get;init;}
    public decimal DamageDone {get;init;}
    public decimal EndingMana {get;init;}
    public decimal MaximumMana {get;init;}
    public decimal SpellPower {get;init;}
    public int LightningOverloadProcs {get;init;}
    public int LavaBurstFlameShockBuffed {get;init;}
    public int MajorManaPotionUses {get;init;}
    public Dictionary<string,int> CombatItemUses {get;init;}=new();
    public Dictionary<string,decimal> DamageByAbility {get;init;}=new();
    public SimulationRunResult Run {get;init;}=new();
    public List<string> ModelingNotes {get;init;}=[];
}
