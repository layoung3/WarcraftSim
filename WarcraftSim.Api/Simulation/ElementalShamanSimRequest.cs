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
    // 'adaptive' derives priorities from talents and encounter. 'legacy' reproduces CP135.
    public string RotationPolicy {get;set;}="adaptive";
    // 'auto' evaluates maintenance against the Mana budget; 'always' and 'never' override it.
    public string FlameShockPolicy {get;set;}="auto";
    public bool UseFlameShock {get;set;}=true;
    public bool UseEarthShock {get;set;}=false;
    public bool UseLightningShield {get;set;}
    public decimal IncomingBossHitDamage {get;set;}
    public decimal IncomingBossSwingSeconds {get;set;}=4m;
    public decimal LightningShieldInternalCooldownSeconds {get;set;}=3m;
    public bool UseSearingTotem {get;set;}=true;
    public bool UseStoneskinTotem {get;set;}
    public bool UseCallOfElements {get;set;}
    public bool UseTotemicRecall {get;set;}
    public List<string> CallOfElementsTotems {get;set;}=[
        "strength-of-earth-totem","searing-totem",
        "mana-spring-totem","windfury-totem"];
    public bool UseFireNova {get;set;}
    public bool UseElementalMastery {get;set;}=true;
    public bool AllowDownranking {get;set;}=false;
    public int TargetCount {get;set;}=1;
    // Nonempty list overrides default priority; entries are validated against known abilities.
    public List<string> CustomPriority {get;set;}=[];
    public bool UseLavaBurst {get;set;}=true;
    public bool UseChainLightning {get;set;}=true;
    public bool UseManaSpringTotem {get;set;}=true;
    public bool UseLightningOverload {get;set;}=true;
    public int MajorManaPotionCharges {get;set;}=1;
    public decimal MajorManaPotionBelowManaPercent {get;set;}=50m;
    public CombatUseItemsOptions CombatItems {get;set;}=new();
    public ForeverElementalShamanTalents Talents {get;set;}=ElementalPreRaidProfileCatalog.Talents("raid-sustain");
    public ForeverElementalShamanTuning Tuning {get;set;}=new();
    public CharacterProfile Character {get;set;}=ElementalPreRaidProfileCatalog.Character("throughput");
}
public sealed class ElementalShamanSimResponse
{
    public decimal Dps {get;init;}
    public decimal DamageDone {get;init;}
    public decimal EndingMana {get;init;}
    public decimal MaximumMana {get;init;}
    public decimal SpellPower {get;init;}
    public string ResolvedRotation {get;init;}="";
    public int ClearcastingProcs {get;init;}
    public int ClearcastingConsumed {get;init;}
    public int ElementalMasteryCasts {get;init;}
    public int LightningOverloadProcs {get;init;}
    public int LavaBurstFlameShockBuffed {get;init;}
    public int MajorManaPotionUses {get;init;}
    public Dictionary<string,int> CombatItemUses {get;init;}=new();
    public Dictionary<string,decimal> DamageByAbility {get;init;}=new();
    public SimulationRunResult Run {get;init;}=new();
    public List<string> ModelingNotes {get;init;}=[];
}
