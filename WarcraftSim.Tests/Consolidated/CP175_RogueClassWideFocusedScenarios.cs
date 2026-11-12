using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class Cp175RogueClassWideExecution
{
    [Theory][InlineData(0,.0,0)][InlineData(1,.49,1)][InlineData(1,.50,0)][InlineData(2,.99,1)]
    public void ImprovedSprintActuallyRemovesMovementImpairingAuras(int rank,double draw,int cleansed)
    {
        var r=Request(new Dictionary<string,int> { ["coup-de-grace"]=3,["efficient-strikes"]=2,["improved-sprint"]=rank });
        var h=new Harness(r,1,draw);
        h.At(0,()=>
        {
            h.Auras.ApplyAura(h.Context,h.Actor,new AuraDefinition { Key="test-root",Name="Root",DurationSeconds=10,Tags=["root"] },"target","root","root");
            h.Auras.ApplyAura(h.Context,h.Actor,new AuraDefinition { Key="unrelated-buff",Name="Other",DurationSeconds=10 },"rogue","other","other");
            h.Cast("sprint");
        });
        h.Run();Assert.Equal(cleansed,h.Combat.ClassWide!.SprintCleanses);
        Assert.Equal(cleansed==0,h.Actor.ActiveAuras.Any(a=>a.Definition.Key=="test-root"));
        Assert.Contains(h.Actor.ActiveAuras,a=>a.Definition.Key=="unrelated-buff");
    }
    [Theory][InlineData(0,300)][InlineData(1,210)][InlineData(2,120)]
    public void EnduranceUsesCurrentThirtyPercentRanks(int rank,int cooldown)
    {
        var ranks=Combat15();ranks["endurance"]=rank;var book=Book(Request(ranks));
        Assert.Equal(cooldown,book.Single(a=>a.Key=="sprint").CooldownSeconds);
        Assert.Equal(cooldown,book.Single(a=>a.Key=="evasion").CooldownSeconds);
    }
    [Theory][InlineData(0,.0,1)][InlineData(1,.49,2)][InlineData(1,.50,1)][InlineData(2,.99,2)]
    public void KickAndImprovedKickCancelCurrentAndSilencedSpellCasts(int rank,double draw,int interruptions)
    {
        var ranks=Combat15();ranks["improved-kick"]=rank;var h=new Harness(Request(ranks),8,draw);
        h.Target.AddAbility(new AbilityDefinition { Key="enemy-cast",Name="Enemy cast",CastTimeSeconds=5,
            Effects=[new AbilityEffectDefinition { Key="enemy-hit",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="fire",ResolutionType=CombatResolutionTypes.Spell,
                MinimumValue=100,MaximumValue=100,CanMiss=false,CanCrit=false }] });
        h.At(0,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"target","rogue","enemy-cast").Success));
        h.At(1,()=>h.Cast("kick"));
        h.At(1.5m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"target","rogue","enemy-cast").Success));
        h.Run();Assert.Equal(interruptions,h.Combat.ClassWide!.InterruptedCasts);
        Assert.Equal(interruptions==2?0:1,h.Damage("enemy-cast").Length);
    }
    [Theory][InlineData(0,.0,true)][InlineData(1,.079,false)][InlineData(1,.08,true)][InlineData(5,.39,false)][InlineData(5,.40,true)]
    public void VilePoisonsProtectsRealPoisonAuraAgainstAnEnemyDispel(int rank,double draw,bool dispelled)
    {
        var ranks=Assassination25();ranks["vile-poisons"]=rank;var r=Request(ranks);
        r.UseInstantPoison=true;r.MainHandPoison="wound";r.Tuning.InstantPoisonChancePercent=100;
        var h=new Harness(r,2,draw);bool? result=null;
        h.At(0,()=>h.Cast("sinister-strike"));h.At(1,()=>result=h.Poison.TryDispel(h.Context,h.Target,"wound"));h.Run();
        Assert.Equal(dispelled,result);Assert.Equal(!dispelled,h.Target.ActiveAuras.Any(a=>a.Definition.Key=="wound-poison"));
    }
    [Fact]
    public void WoundPoisonModifiesActualHealingAndDispelRestoresIt()
    {
        var r=Request();r.UseInstantPoison=true;r.MainHandPoison="wound";r.Tuning.InstantPoisonChancePercent=100;
        var h=new Harness(r,4);
        var healer=new SimulationActorState { Key="enemy-healer",TeamKey="enemy",Level=60,MaximumHealth=1000 };healer.InitializeHealth(1000);
        healer.AddAbility(new() { Key="test-heal",Name="Heal",Effects=[new() { Key="heal",EffectType=AbilityEffectTypes.DirectHealing,
            TargetType=AbilityTargetTypes.Friendly,ResolutionType=CombatResolutionTypes.AlwaysHits,CanCrit=false,CanMiss=false,
            MinimumValue=1000,MaximumValue=1000 }] });h.Context.AddActor(healer);
        h.Target.TakeDamage(10000);
        h.At(0,()=>h.Cast("sinister-strike"));
        h.At(1,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"enemy-healer","target","test-heal").Success));
        h.At(2,()=>Assert.True(h.Poison.TryDispel(h.Context,h.Target,"wound")));
        h.At(3,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"enemy-healer","target","test-heal").Success));h.Run();
        Assert.Equal(new decimal?[]{500,1000},h.Context.Timeline.Where(e=>e.Type==CombatEventType.Healing && e.AbilityKey=="test-heal").Select(e=>e.Amount));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void CurrentDirtyTricksCostsAndGougeRankDurationRemainDistinct(int rank)
    {
        var r=Request(new Dictionary<string,int> { ["opportunity"]=5,["dirty-tricks"]=rank,["improved-gouge"]=rank });r.IsBehindTarget=false;
        var book=Book(r);
        Assert.Equal(65m*(1m-.25m*rank),book.Single(a=>a.Key=="sap").ResourceCosts[0].Amount);
        Assert.Equal(30m*(1m-.25m*rank),book.Single(a=>a.Key=="blind").ResourceCosts[0].Amount);
        Assert.Equal(4m+.5m*rank,book.Single(a=>a.Key=="gouge").Effects.Single(e=>e.EffectType==AbilityEffectTypes.ApplyAura).DurationSeconds);
        r.IsBehindTarget=true;Assert.DoesNotContain(Book(r),a=>a.Key=="gouge");
    }
    [Theory][InlineData(0,1)][InlineData(1,2)][InlineData(2,2)]
    public void QuickGetawayEnforcesSourcedOneExtraSapTargetAtEitherRank(int rank,int active)
    {
        var ranks=Subtlety25();ranks["dirty-tricks"]=2;ranks["quick-getaway"]=rank;
        var r=Request(ranks);r.StartInStealth=true;var h=new Harness(r,3);
        var third=new SimulationActorState { Key="target-3",TeamKey="enemy",Level=63,MaximumHealth=10000 };third.InitializeHealth(10000);h.Context.AddActor(third);
        h.At(0,()=>h.Cast("sap"));h.At(1,()=>h.Cast("sap","target-2"));h.At(2,()=>h.Cast("sap","target-3"));h.Run();
        Assert.Equal(active,h.Context.Actors.Values.Sum(a=>a.ActiveAuras.Count(x=>x.Definition.Key=="rogue-sap-control" && x.SourceActorKey=="rogue")));
        Assert.DoesNotContain(h.Target.ActiveAuras,a=>a.Definition.Key=="rogue-sap-control");
    }
    [Fact]
    public void GougeDealsSourcedDamageAndControlsActualIncomingSwingsUntilDamageBreaksIt()
    {
        var r=Request(new Dictionary<string,int> { ["improved-gouge"]=3 });r.IsBehindTarget=false;
        var h=new Harness(r,3);decimal blocked=-1,after=-1;
        h.At(0,()=>h.Cast("gouge"));
        h.At(.5m,()=>blocked=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100));
        h.At(1,()=>h.Cast("sinister-strike"));
        h.At(2,()=>after=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100));
        h.Run();Assert.Equal(75m,Assert.Single(h.Damage("gouge")).Amount);Assert.Equal(0,blocked);Assert.Equal(100,after);
        Assert.Equal(2,h.Combat.ComboPointsFor("target"));
    }
    [Fact]
    public void RiposteRequiresARealParryAndDisarmStopsOnlyMainHandWeaponDamage()
    {
        var ranks=Combat15();ranks["deflection"]=3;ranks["riposte"]=1;var r=Request(ranks);r.Tuning.BaseParryPercent=100;r.Tuning.BaseDodgePercent=0;
        var h=new Harness(r,2);decimal weapon=-1,unarmed=-1;
        h.At(0,()=>
        {
            Assert.False(h.Executor.TryStartAbility(h.Context,"rogue","target","riposte").Success);
            h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100);
            h.Cast("riposte");
        });
        h.At(1,()=>
        {
            h.Request.Tuning.BaseParryPercent=0;h.Request.Tuning.DeflectionParryPercentPerRank=0;
            weapon=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical",WeaponHandKey=WeaponHandKeys.MainHand },100);
            unarmed=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100);
        });h.Run();Assert.Equal(0,weapon);Assert.Equal(100,unarmed);
        Assert.Equal(150m,Assert.Single(h.Damage("riposte")).Amount);
    }
    [Theory][InlineData(0,.0,false)][InlineData(1,.019,true)][InlineData(1,.020,false)][InlineData(2,.039,true)]
    public void HeightenedSensesExecutesSpellAndRangedAvoidance(int rank,double draw,bool avoids)
    {
        var ranks=Subtlety25();ranks["heightened-senses"]=rank;var r=Request(ranks);r.Tuning.BaseDodgePercent=0;
        var h=new Harness(r,1,draw);decimal spell=-1,ranged=-1;
        h.At(0,()=>
        {
            spell=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="fire",ResolutionType=CombatResolutionTypes.Spell },100);
            ranged=h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical",ResolutionType=CombatResolutionTypes.Ranged },100);
        });h.Run();Assert.Equal(avoids?0:100,spell);Assert.Equal(avoids?0:100,ranged);
    }
    [Fact]
    public void BloodSplatterExtendsActualGarroteTicksAndRankRemovalRestoresDuration()
    {
        var ranks=Assassination25();var r=Request(ranks);r.StartInStealth=true;
        var h=new Harness(r,28);h.At(0,()=>h.Cast("garrote"));h.Run();
        Assert.Equal(9,h.Damage("garrote").Count(e=>e.IsPeriodic));
        ranks.Remove("blood-splatter");r.Talents=ForeverRogueClassTalents.FromRanks(ranks);
        h=new(r,28);h.At(0,()=>h.Cast("garrote"));h.Run();Assert.Equal(6,h.Damage("garrote").Count(e=>e.IsPeriodic));
    }
    [Fact]
    public void VenomComesFromShankAndPoisonsConsumeLiveAttackPowerWithExpiration()
    {
        var ranks=Assassination25();ranks["venom"]=1;var r=Request(ranks);r.UseInstantPoison=true;
        r.MainHandPoison=r.OffHandPoison="instant";r.Tuning.InstantPoisonChancePercent=100;r.StartingEnergyPercent=40;
        var h=new Harness(r,9);h.Actor.Stats.Set("attack-power",1000);
        h.At(0,()=>h.Cast("shank"));h.At(1,()=>h.Cast("sinister-strike"));
        h.At(7,()=>{h.Actor.Stats.Set("attack-power",0);h.Cast("sinister-strike");});h.Run();
        var poisons=h.Damage("instant-poison");
        Assert.Contains(poisons,e=>e.TimeSeconds==1 && e.Amount==(90m+10m)*1.2m*1.3m);
        Assert.Contains(poisons,e=>e.TimeSeconds==7 && e.Amount==90m*1.2m);
        Assert.DoesNotContain(Book(r),a=>a.Key=="venom");
    }
    [Fact]
    public void RemorselessActivatesFromAnActualEligibleKillAndIsConsumedByNextStrike()
    {
        var r=Request(new Dictionary<string,int> { ["remorseless-attacks"]=2 });var h=new Harness(r,2,.3);
        h.Target.InitializeHealth(1);h.At(0,()=>h.Cast("sinister-strike"));h.At(1,()=>h.Cast("sinister-strike","target-2"));h.Run();
        Assert.Equal(1,h.Combat.ClassWide!.RemorselessActivations);
        var after=Assert.Single(h.Damage("sinister-strike"),e=>e.TargetActorKey=="target-2");Assert.True(after.IsCritical);
        Assert.DoesNotContain(h.Actor.ActiveAuras,a=>a.Definition.Key=="rogue-remorseless");
    }
    [Fact]
    public void CurrentPreviewDeclaresCustomPolicyWhileLegacySerializationOmitsModeFlag()
    {
        var preview=RogueSimRunner.Preview(Request());Assert.True(preview.ClassWide);
        Assert.Contains("validated-custom-order",preview.RotationPreviewScope);
        var legacy=RogueSimRunner.Preview(RogueSimRequest.CreatePreset("combat"));Assert.False(legacy.ClassWide);
        Assert.DoesNotContain("ClassWide",System.Text.Json.JsonSerializer.Serialize(legacy));
        Assert.Equal("fixed-configured-priority-no-custom-rotation",legacy.RotationPreviewScope);
    }
    [Fact]
    public void RealSharedEquipmentBuffUpdatesLiveWeaponAndPoisonDamageThenReversesExactly()
    {
        var r=Request();r.UseInstantPoison=true;r.MainHandPoison="instant";r.Tuning.InstantPoisonChancePercent=100;
        var h=new Harness(r,4);
        var effect=new ForeverExecutableEquipmentEffect { Key="synthetic-ap-lifetime",ItemId=999999,
            Kind="on-use",Trigger="manual",Modeled=true,AutoUse=true,DurationSeconds=2,CooldownSeconds=100,
            StatBonuses=new() { ["melee-attack-power"]=100,["strength"]=10 } };
        var apply=typeof(RogueSimRunner).GetMethod("ApplyEquipmentDelta",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var equipment=new ForeverEquipmentEffectProcessor(new(),"rogue","target",h.Auras,h.Executor,42,
            explicitBindings:[new("trinket1",effect)],classKey:"rogue",onStatDelta:(ctx,actor,delta)=>apply.Invoke(null,[ctx,actor,delta]));
        h.Extra.Add(equipment);h.At(0,()=>h.Cast("sinister-strike"));h.At(3,()=>h.Cast("sinister-strike"));h.Run();
        Assert.Equal(183.714285714m,Math.Round(h.Damage("sinister-strike")[0].Amount!.Value,9));
        Assert.Equal(168m,h.Damage("sinister-strike")[1].Amount);
        Assert.Equal(91.1m,h.Damage("instant-poison")[0].Amount);Assert.Equal(90m,h.Damage("instant-poison")[1].Amount);
        Assert.Equal(0,h.Actor.Stats.Get("attack-power"));Assert.Equal(0,h.Actor.Stats.Get("melee-attack-power"));
        Assert.Equal(0,h.Actor.Stats.Get("strength"));
    }
    [Fact]
    public void PublishedRogueWristAndReviewedEnchantChangeRealStatsDamageAndRevert()
    {
        var r=Request();r.Character.Race="human";r.CustomPriority=["sinister-strike"];r.DurationSeconds=20;
        var empty=RogueSimRunner.Preview(r);var baseline=new RogueSimRunner().Run(r);
        r.Character.Equipment.Items.Add(new WarcraftSim.Core.Characters.Equipment.CharacterEquipmentItem
            { SlotKey="wrist",ItemId=18452,EnchantSpellId=1248599 });
        var serialized=System.Text.Json.JsonSerializer.Serialize(r);
        var equipped=RogueSimRunner.Preview(r);var damage=new RogueSimRunner().Run(r);
        Assert.Equal(empty.AttackPower+16,equipped.AttackPower);
        Assert.Equal(empty.MaximumHealth+170,equipped.MaximumHealth);
        Assert.True(damage.DamageDone>baseline.DamageDone);
        Assert.Equal(serialized,System.Text.Json.JsonSerializer.Serialize(r));
        r.Character.Equipment.Items.Clear();
        Assert.Equal(empty.AttackPower,RogueSimRunner.Preview(r).AttackPower);
        Assert.Equal(baseline.DamageDone,new RogueSimRunner().Run(r).DamageDone);
    }
}
