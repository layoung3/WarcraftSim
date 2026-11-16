using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Current 70888 client topology and November mechanics. These witnesses
/// exercise the existing executor/auras/rolls rather than a replacement Rogue engine.</summary>
public sealed partial class Cp175RogueClassWideExecution
{
    private static Dictionary<string,int> Combat15()=>new()
    { ["coup-de-grace"]=3,["efficient-strikes"]=2,["precision"]=3,["puncturing-wounds"]=3,["endurance"]=2,["composure"]=2 };
    private static Dictionary<string,int> Combat25()
    {
        var ranks=Combat15();ranks["aggression"]=3;ranks["flawless-execution"]=1;
        ranks["weapon-expertise"]=2;ranks["blade-flurry"]=1;ranks["hack-and-slash"]=3;
        return ranks;
    }
    private static Dictionary<string,int> Subtlety25()=>new()
    { ["opportunity"]=5,["setup"]=3,["dirty-deeds"]=2,["improved-ambush"]=3,["initiative"]=3,
      ["premeditation"]=1,["serrated-blades"]=3,["ghostly-strike"]=1,["elusiveness"]=2,["preparation"]=1,["hemorrhage"]=1 };
    private static Dictionary<string,int> Assassination25()=>new()
    { ["malice"]=5,["improved-gouge"]=3,["remorseless-attacks"]=2,["ruthlessness"]=3,
      ["improved-slice-and-dice"]=3,["vigor"]=2,["blood-splatter"]=1,["lethality"]=1,["vile-poisons"]=5 };
    private static RogueSimRequest Request(IReadOnlyDictionary<string,int>? ranks=null)=>new()
    {
        UseClassWideTalents=true,Talents=ForeverRogueClassTalents.FromRanks(ranks??new Dictionary<string,int>()),
        StartInStealth=false,StartingEnergyPercent=100,UseInstantPoison=false,
        MainHandPoison="none",OffHandPoison="none",DurationSeconds=30,TargetMaximumHealth=10000000,
        CaptureTimeline=true,Character=new() { Name="Class-wide witness",ClassKey="rogue",SpecializationKey="assassination",RulesetKey="wow-forever",Level=60,BaseStats=new() },
        Tuning=new() { MainHandWeaponDamage=100,OffHandWeaponDamage=80,MainHandSpeedSeconds=2,OffHandSpeedSeconds=2,
            MainHandHitPercent=100,OffHandHitPercent=100,PhysicalCritPercent=0,EnergyPerSecond=60 }
    };
    private static IReadOnlyList<AbilityDefinition> Book(RogueSimRequest r)=>ForeverRogueSpellbook.Create(r.Specialization,r.Talents,
        r.Tuning,0,r.MainHandWeaponType=="dagger" && r.OffHandWeaponType=="dagger",r.IsBehindTarget,r.MainHandWeaponType=="dagger",true);
    private sealed class ConstantRandom(double value):Random { public override double NextDouble()=>value; }
    private sealed class Harness:ICombatEventProcessor
    {
        public RogueSimRequest Request { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor=>Context.GetActor("rogue")!;
        public SimulationActorState Target=>Context.GetActor("target")!;
        public AuraManager Auras { get; }=new();
        public AbilityExecutor Executor { get; }
        public RogueCombatProcessor Combat { get; }
        public RoguePoisonProcessor Poison { get; }
        public RogueThreatAndDefenseProcessor Defense { get; }
        public List<ICombatEventProcessor> Extra { get; }=[];
        private readonly Dictionary<string,Action> _actions=[];
        public Harness(RogueSimRequest r,decimal duration=30,double draw=.99)
        {
            Request=r;
            Context=new(new() { Seed=42,DurationSeconds=duration,CaptureTimeline=true,PrimaryActorKey="rogue",MaximumProcessedEvents=20000 });
            typeof(SimulationContext).GetField("<Random>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Context,new ConstantRandom(draw));
            foreach(var key in new[]{"rogue","target","target-2"})
            {
                var actor=new SimulationActorState { Key=key,Level=key=="rogue"?60:63,TeamKey=key=="rogue"?"raid":"enemy",MaximumHealth=10000000 };
                actor.InitializeHealth(actor.MaximumHealth);actor.Stats.Set("armor",r.TargetArmor);Context.AddActor(actor);
                if(key=="rogue")
                {
                    actor.AddResource(new ResourceState("energy",110,110*r.StartingEnergyPercent/100,r.Tuning.EnergyPerSecond));
                    foreach(var ability in Book(r))actor.AddAbility(ability);
                }
            }
            RogueCombatProcessor? combat=null;Func<RogueCombatProcessor?> getCombat=()=>combat;
            object Adapter(string name)=>Activator.CreateInstance(typeof(RogueSimRunner).GetNestedType(name,BindingFlags.NonPublic)!,
                BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[r,getCombat],null)!;
            Executor=new(Auras,(ICombatRollResolver)Adapter("RogueRollResolver"),(IDamageMitigationResolver)Adapter("RogueArmorMitigation"),resourceCostPolicy:()=>combat);
            Combat=combat=new(r,Actor,new RotationProfile(),Executor,Auras,new AutoAttackProcessor(Executor),[]);
            Poison=new(r,Actor,Auras,Executor);
            Defense=new("rogue",r,Auras,(ctx,target)=>Combat.ClassWide!.TrySetup(ctx,target));
        }
        public void At(decimal time,Action action)
        {
            var key="test-"+_actions.Count;_actions[key]=action;
            Context.ScheduleEvent(new() { Type=CombatEventType.RotationDecision,TimeSeconds=time,SourceActorKey="rogue",EffectKey=key,IsInternal=true });
        }
        public void Cast(string key,string target="target")
        {
            var result=Executor.TryStartAbility(Context,"rogue",target,key);
            Assert.True(result.Success,$"{key} failed at {Context.CurrentTimeSeconds}: {JsonSerializer.Serialize(result)}");
        }
        public void Process(SimulationContext c,CombatEvent ev)
        { if(ev.EffectKey is {} key && _actions.TryGetValue(key,out var action))action(); }
        public void Points(int count,decimal at=0)
        { for(var i=0;i<count;i++)At(at+i,()=>Cast("sinister-strike")); }
        public SimulationRunResult Run()=>new SimulationEngine([Executor,Auras,Combat,Poison,Defense,..Extra,this]).Run(Context);
        public CombatEvent[] Damage(string key)=>Context.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey==key).ToArray();
    }

    [Fact]
    public void CurrentTreeHas54ExplicitNodesAndRealChangedPrerequisites()
    {
        Assert.Equal(54,ForeverRogueClassTalents.Entries.Count);
        Assert.Equal(54,ForeverRogueClassTalents.CoverageStatuses.Count);
        Assert.Equal(("venom",1),(ForeverRogueClassTalents.Find("mutilate")!.ParentKey,ForeverRogueClassTalents.Find("mutilate")!.ParentRank));
        Assert.Equal(("precision",3),(ForeverRogueClassTalents.Find("weapon-expertise")!.ParentKey,ForeverRogueClassTalents.Find("weapon-expertise")!.ParentRank));
        Assert.Equal(("dirty-tricks",2),(ForeverRogueClassTalents.Find("quick-getaway")!.ParentKey,ForeverRogueClassTalents.Find("quick-getaway")!.ParentRank));
        foreach(var removed in new[]{"murder","quietus","cutthroat","thousand-cuts","improved-distract","improved-eviscerate","improved-sinister-strike","master-of-subtlety"})
            Assert.Null(ForeverRogueClassTalents.Find(removed));
        Assert.All(ForeverRogueClassTalents.Entries,n=>Assert.StartsWith("https://",n.SourceUrl));
    }
    [Theory][InlineData("assassination",31,20,0)][InlineData("combat",10,31,10)][InlineData("subtlety",10,10,31)]
    public void ReviewedStartersUse51NamedPointsAndAllTreesExecute(string tree,int a,int c,int s)
    {
        var r=Request();r.Talents=ForeverRogueClassTalents.Starter(tree);r.Tuning.EnergyPerSecond=10;
        Assert.Equal((a,c,s),(r.Talents.AssassinationPointsSpent,r.Talents.CombatPointsSpent,r.Talents.SubtletyPointsSpent));
        Assert.Equal(51,r.Talents.Ranks.Values.Sum());
        var result=new RogueSimRunner().Run(r);
        Assert.True(result.DamageDone>0);
        Assert.True(result.Run.CustomMetrics["rogue-shank-energy"]>0);
        if(tree=="assassination")Assert.True(result.DamageByAbility["mutilate"]>0);
        if(tree=="combat")
        {
            Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted && e.AbilityKey=="blade-flurry");
            Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted && e.AbilityKey=="adrenaline-rush");
        }
        if(tree=="subtlety")
        {
            Assert.True(result.Run.CustomMetrics["rogue-boot-knife-procs"]>0);
            Assert.True(result.DamageByAbility["hemorrhage"]>0);
            Assert.True(result.DamageByAbility["death-mark-damage"]>0);
        }
    }
    [Fact]
    public void PartialUnspentAndUnsupportedMechanicsAreLegalButCapsTiersAndRemovalAreNotIgnored()
    {
        ForeverRogueClassTalents.Validate(ForeverRogueClassTalents.FromRanks(new Dictionary<string,int> { ["master-of-deception"]=1,["malice"]=2 }));
        Assert.Throws<ArgumentException>(()=>ForeverRogueClassTalents.FromRanks(new Dictionary<string,int> { ["precision"]=4 }));
        Assert.Throws<ArgumentException>(()=>ForeverRogueClassTalents.FromRanks(new Dictionary<string,int> { ["mutilate"]=1 }));
        var ranks=ForeverRogueClassTalents.Starter("assassination").Ranks;
        ranks.Remove("venom");
        Assert.Throws<ArgumentException>(()=>ForeverRogueClassTalents.FromRanks(ranks));
        var b=ForeverRogueClassTalents.FromRanks(new Dictionary<string,int> { ["malice"]=1 });b.AssassinationPointsSpent=5;
        Assert.Throws<ArgumentException>(()=>ForeverRogueClassTalents.Validate(b));
    }
    [Fact]
    public void EquivalentClassWideBuildsIgnorePresetSpecializationAndReplayExactly()
    {
        var r=Request();r.Talents=ForeverRogueClassTalents.Starter("subtlety");r.StartInStealth=true;
        var original=JsonSerializer.Serialize(r);var expected=new RogueSimRunner().Run(r);
        foreach(var spec in new[]{"combat","subtlety","assassination"})
        {
            var copy=RogueSimulationRequestSnapshot.Create(r);copy.Specialization=copy.Character.SpecializationKey=spec;
            var result=new RogueSimRunner().Run(copy);
            Assert.Equal(expected.DamageDone,result.DamageDone);
            Assert.Equal(expected.EndingEnergy,result.EndingEnergy);
            Assert.Equal(expected.DamageByAbility.OrderBy(x=>x.Key),result.DamageByAbility.OrderBy(x=>x.Key));
            Assert.Equal(expected.Run.CustomMetrics.OrderBy(x=>x.Key),result.Run.CustomMetrics.OrderBy(x=>x.Key));
        }
        Assert.Equal(original,JsonSerializer.Serialize(r));
    }
    [Fact]
    public void ActualHandsGovernActionsAndCustomOrderIsPreservedOrRejected()
    {
        var r=Request();r.Talents=ForeverRogueClassTalents.Starter("assassination");r.BothWeaponsDaggers=true;r.OffHandWeaponType="sword";
        var preview=RogueSimRunner.Preview(r);
        Assert.DoesNotContain(preview.Rotation.Entries,e=>e.AbilityKey=="mutilate");
        Assert.Contains(preview.Rotation.Entries,e=>e.AbilityKey=="backstab");
        r.CustomPriority=["eviscerate","backstab"];
        Assert.Equal(r.CustomPriority,RogueSimRunner.Preview(r).Rotation.Entries.Select(e=>e.AbilityKey));
        r.CustomPriority=["mutilate"];
        Assert.Throws<ArgumentException>(()=>new RogueSimRunner().Run(r));
        Assert.Equal(new[]{"mutilate"},r.CustomPriority);
        r.MainHandWeaponType="sword";r.OffHandWeaponType="dagger";r.CustomPriority=["backstab"];
        Assert.Throws<ArgumentException>(()=>RogueSimRunner.Preview(r));
    }
    [Theory][InlineData(0,30)][InlineData(1,25)][InlineData(2,20)]
    public void PoisedAndComposureDiscountExactlyOneEnergyCast(int rank,int firstCost)
    {
        var ranks=Combat15();ranks["composure"]=rank;
        var h=new Harness(Request(ranks),2);h.At(0,()=>h.Cast("sinister-strike"));h.At(1,()=>h.Cast("sinister-strike"));h.Run();
        var spent=h.Context.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged && e.AbilityKey=="sinister-strike" && e.Amount<0).Select(e=>e.Amount).ToArray();
        Assert.Equal(new decimal?[]{-firstCost,-40m},spent);
        Assert.Equal(1,h.Combat.ClassWide!.PoisedUses);
    }
    [Theory][InlineData(0,25,10)][InlineData(1,28,9.6)][InlineData(5,40,8)]
    public void ShankFlashOfSteelChangesActualResourceGainAndCooldown(int rank,int gain,double cooldown)
    {
        var ranks=Combat25();ranks["flash-of-steel"]=rank;var r=Request(ranks);r.StartingEnergyPercent=40;
        var h=new Harness(r,.5m);h.At(0,()=>h.Cast("shank"));h.Run();
        Assert.Equal(gain,h.Combat.ClassWide!.ShankEnergy);
        Assert.Equal((decimal)cooldown,h.Actor.Abilities["shank"].Definition.CooldownSeconds);
        Assert.Single(h.Damage("shank"));Assert.Equal(WeaponHandKeys.OffHand,h.Damage("shank")[0].WeaponHandKey);
    }
    [Theory][InlineData(0,.0,0)][InlineData(1,.19,2)][InlineData(1,.20,0)][InlineData(5,.99,2)]
    public void BootKnifeUsesRankChanceAndAddsTwoPointsFromLandedShank(int rank,double draw,int expected)
    {
        var ranks=Subtlety25();ranks["boot-knife"]=rank;var r=Request(ranks);r.StartingEnergyPercent=40;
        var h=new Harness(r,.5m,draw);h.At(0,()=>h.Cast("shank"));h.Run();
        Assert.Equal(expected,h.Combat.ComboPointsFor("target"));
        Assert.Equal(expected/2,h.Combat.ClassWide!.BootKnifeProcs);
    }
    [Fact]
    public void ShankCannotMissButDoesNotBypassDodgeOrCreatePointsAfterAvoidance()
    {
        var ranks=Subtlety25();ranks["boot-knife"]=5;var r=Request(ranks);r.StartingEnergyPercent=40;
        r.Tuning.OffHandHitPercent=0;
        var h=new Harness(r,.5m);h.At(0,()=>h.Cast("shank"));h.Run();Assert.Equal(2,h.Combat.ComboPointsFor("target"));
        r.TargetDodgePercent=100;h=new(r,.5m);h.At(0,()=>h.Cast("shank"));h.Run();
        Assert.Equal(0,h.Combat.ComboPointsFor("target"));Assert.Equal(0,h.Combat.ClassWide!.ShankEnergy);
    }
    [Theory][InlineData(0,1)][InlineData(1,1.25)][InlineData(2,1.5)]
    public void OffHandRankChangesRealAutosAndReversalRestoresBaseline(int rank,double ratio)
    {
        var ranks=Combat25();ranks["dual-wield-specialization"]=rank;var r=Request(ranks);r.CustomPriority=[];r.DurationSeconds=10;
        var baseRanks=new Dictionary<string,int>(ranks);baseRanks["dual-wield-specialization"]=0;
        var baseline=Request(baseRanks);baseline.CustomPriority=[];baseline.DurationSeconds=10;
        var expected=new RogueSimRunner().Run(baseline).DamageByAbility["rogue-off-hand"];
        var result=new RogueSimRunner().Run(r);
        Assert.Equal(expected*(decimal)ratio,result.DamageByAbility["rogue-off-hand"]);
        r.Talents=ForeverRogueClassTalents.FromRanks(baseRanks);
        Assert.Equal(expected,new RogueSimRunner().Run(r).DamageByAbility["rogue-off-hand"]);
    }
    [Fact]
    public void GhostlyStrikeSuppliesRealDodgeAndNonStealthGarroteWithCurrentBleedMark()
    {
        var r=Request(Subtlety25());var h=new Harness(r,12);
        h.At(0,()=>h.Cast("ghostly-strike"));h.At(1,()=>h.Cast("hemorrhage"));h.At(2,()=>h.Cast("garrote"));
        h.At(.5m,()=>Assert.Contains(h.Actor.ActiveAuras,a=>a.Definition.Key=="rogue-ghostly-opener"));
        h.Run();Assert.Single(h.Context.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="garrote");
        Assert.DoesNotContain(h.Actor.ActiveAuras,a=>a.Definition.Key=="rogue-ghostly-opener");
        Assert.All(h.Damage("garrote").Where(e=>e.IsPeriodic),e=>Assert.Equal(65m*1.2m*1.15m,e.Amount));
        Assert.Equal(1,h.Combat.StealthOpeners);
    }
    [Theory][InlineData(false,0)][InlineData(true,1)]
    public void GhostlyDodgeWindowChangesActualIncomingResolution(bool ghost,int dodges)
    {
        var r=Request(Subtlety25());r.Tuning.BaseDodgePercent=0;
        var h=new Harness(r,1,.10);
        if(ghost)h.At(0,()=>h.Cast("ghostly-strike"));
        h.At(.5m,()=>h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100));
        h.Run();Assert.Equal(dodges,h.Defense.DodgedIncomingSwings);
    }
    [Theory][InlineData(0,.0,0)][InlineData(1,.14,1)][InlineData(1,.15,0)][InlineData(3,.44,1)]
    public void SetupConsumesRealDodgesAndFullResistsWithSourceOwnedPoints(int rank,double draw,int expected)
    {
        var r=Request(new Dictionary<string,int> { ["setup"]=rank });r.Tuning.BaseDodgePercent=100;
        var h=new Harness(r,1,draw);
        h.At(0,()=>h.Defense.ResolveIncomingPhysicalDamage(h.Context,h.Actor,new() { SourceActorKey="target",SchoolKey="physical" },100));
        h.At(.5m,()=>h.Context.EmitEvent(new() { Type=CombatEventType.Damage,TimeSeconds=h.Context.CurrentTimeSeconds,SourceActorKey="target-2",TargetActorKey="rogue",
            SchoolKey="frost",ResolutionType=CombatResolutionTypes.Spell,ResultKey="resist",Amount=0,RawAmount=100 }));
        h.Run();Assert.Equal(expected,h.Combat.ComboPointsFor("target"));Assert.Equal(expected,h.Combat.ComboPointsFor("target-2"));
    }
    [Theory][InlineData(false,0)][InlineData(true,3)]
    public void PremeditationExpiresUnlessItsPointsAreAddedTo(bool add,int expected)
    {
        var h=new Harness(Request(Subtlety25()),22);h.Request.StartInStealth=true;
        h.At(0,()=>h.Cast("premeditation"));if(add)h.At(1,()=>h.Cast("sinister-strike"));h.Run();
        Assert.Equal(expected,h.Combat.ComboPointsFor("target"));
    }
    [Theory][InlineData(0,0)][InlineData(1,0)][InlineData(5,25)]
    public void RelentlessStrikesUsesSpentPointsAndRankChance(int rank,int restored)
    {
        var ranks=Assassination25();ranks["relentless-strikes"]=rank;var r=Request(ranks);
        var h=new Harness(r,6,.99);h.Points(5);h.At(5,()=>h.Cast("eviscerate"));h.Run();
        Assert.Equal(restored,h.Combat.ClassWide!.RelentlessEnergy);Assert.Equal(1,h.Combat.FinishersUsed);
    }
    [Theory][InlineData(0,1)][InlineData(3,1.2)]
    public void DeathMarkUsesActualDamageDoubleBleedStorageAndCurrentSourceCap(int coup,double multiplier)
    {
        var ranks=Subtlety25();ranks["boot-knife"]=5;ranks["death-mark"]=1;ranks["coup-de-grace"]=coup;
        var h=new Harness(Request(ranks),12);h.Points(5);h.At(5,()=>h.Cast("death-mark"));
        h.At(6,()=>h.Cast("ghostly-strike"));h.At(7,()=>h.Cast("garrote"));h.At(8,()=>h.Cast("sinister-strike"));
        h.At(9,()=>h.Cast("sinister-strike"));h.Run();
        var during=h.Context.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.SourceActorKey=="rogue" &&
            e.TargetActorKey=="target" && e.TimeSeconds>5 && e.TimeSeconds<11 && e.AbilityKey!="death-mark-damage");
        var stored=during.Sum(e=>(e.Amount??0)*.6m*(decimal)multiplier*(e.AbilityKey is "garrote" or "rupture"?2:1));
        Assert.Equal(stored,h.Combat.ClassWide!.DeathMarkStored);
        Assert.Equal(Math.Min(stored,1381m)*(decimal)multiplier,Assert.Single(h.Damage("death-mark-damage")).Amount);
        Assert.Contains(during,e=>e.AbilityKey=="garrote" && e.IsPeriodic);
    }
    [Fact]
    public void DispelledDeathMarkCannotReleaseAndForeignDamageIsNotStored()
    {
        var ranks=Subtlety25();ranks["boot-knife"]=5;ranks["death-mark"]=1;
        var h=new Harness(Request(ranks),12);h.Points(5);h.At(5,()=>h.Cast("death-mark"));
        h.At(6,()=>h.Context.EmitEvent(new() { Type=CombatEventType.Damage,TimeSeconds=h.Context.CurrentTimeSeconds,SourceActorKey="other",TargetActorKey="target",AbilityKey="sinister-strike",SchoolKey="physical",Amount=99999 }));
        h.At(7,()=>h.Auras.RemoveAuras(h.Context,h.Target,"rogue-death-mark","rogue","test curse dispel"));h.Run();
        Assert.Equal(0,h.Combat.ClassWide!.DeathMarkStored);Assert.Empty(h.Damage("death-mark-damage"));
    }
    [Fact]
    public void SharedGearAndMeleeModifierApplyOnceAndRemovalReplaysOriginal()
    {
        var r=Request();r.CustomPriority=["sinister-strike"];r.DurationSeconds=10;
        var original=new RogueSimRunner().Run(r);var before=RogueSimRunner.Preview(r);
        r.Character.Equipment.Items.Add(new CharacterEquipmentItem { SlotKey="chest",ItemKey="manual-budget",Stats=new() { Values=new() { ["attack-power"]=100,["stamina"]=10 } } });
        r.Modifiers.Buffs=["battle-shout"];
        var snapshot=JsonSerializer.Serialize(r);var boosted=RogueSimRunner.Preview(r);
        Assert.Equal(before.AttackPower+239m,boosted.AttackPower);Assert.Equal(before.MaximumHealth+100m,boosted.MaximumHealth);
        Assert.True(new RogueSimRunner().Run(r).DamageDone>original.DamageDone);Assert.Equal(snapshot,JsonSerializer.Serialize(r));
        r.Character.Equipment.Items.Clear();r.Modifiers.Buffs.Clear();
        Assert.Equal(original.DamageDone,new RogueSimRunner().Run(r).DamageDone);
    }
    [Fact]
    public void FaerieFireUsesLiveArmorAndExpiresOrMaintainsOnThePrimaryTarget()
    {
        var r=Request();r.CustomPriority=[];r.DurationSeconds=85;r.TargetArmor=5500;r.Modifiers.TargetDebuffs=["faerie-fire-rank4"];
        var initial=new RogueSimRunner().Run(r);
        var hits=initial.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="rogue-main-hand").ToArray();
        Assert.True(hits.First(e=>e.TimeSeconds<40).Amount>hits.First(e=>e.TimeSeconds>40).Amount);
        r.Modifiers.TargetDebuffMode="maintained";
        var maintained=new RogueSimRunner().Run(r);
        Assert.True(maintained.DamageDone>initial.DamageDone);Assert.Equal(3,maintained.Run.CustomMetrics["rogue-external-debuff-applications"]);
    }
    [Fact]
    public void SharedInventoryActuallyHealsDuringAnIncomingDamageScenario()
    {
        var r=Request();r.CustomPriority=[];r.DurationSeconds=10;r.EnableBossAttacks=true;r.BossHitDamage=900;r.BossSwingSeconds=2;r.Tuning.BaseDodgePercent=0;
        r.CombatItems.MajorHealingPotionCharges=1;r.CombatItems.MajorHealingPotionBelowHealthPercent=60;
        var result=new RogueSimRunner().Run(r);
        Assert.Equal(1,result.Run.CustomMetrics["combat-item-uses-major-healing-potion"]);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Healing && e.AbilityKey=="major-healing-potion" && e.Amount>0);
    }
}
