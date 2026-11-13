using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class Cp175PaladinMechanics
{
    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(5)]
    public void ImprovedSealsChangesActualSealAndJudgementDamageAtPartialRanks(int rank)
    {
        var r=Request(("improved-seals",5));r.Talents.Ranks["improved-seals"]=rank;r.Seal="righteousness";
        r.UseAutoAttacks=true;r.CustomPriority=["seal-of-righteousness","judgement"];r.DurationSeconds=8m;
        var plain=Copy(r);plain.Talents.Ranks["improved-seals"]=0;
        var a=Run(plain);var b=Run(r);
        foreach(var ability in new[]{"seal-proc-righteousness","judgement-righteousness"})
            Assert.Equal(a.DamageByAbility[ability]*(1m+.03m*rank),b.DamageByAbility[ability],10);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public void BenedictionChangesInstantPaymentAndMeasuredSanctifiedRefundUsesBasePlusReduction(int rank)
    {
        var r=Request(("benediction",5),("sanctified-judgement",3));r.Talents.Ranks["benediction"]=rank;
        // Retain the independent first-row points when removing Benediction.
        r.Talents.Ranks["deflection"]=5;r.Seal="righteousness";r.InitialManaPercent=50m;r.BaseRangedHitPercent=0m;
        r.CustomPriority=["seal-of-righteousness","judgement"];r.DurationSeconds=2m;
        var result=Run(r);Assert.Equal(200m*(1m-.02m*rank),Spent(result,"seal-of-righteousness"));
        Assert.Equal(200m*(1m+.02m*rank)*.6m,Metric(result,"sanctified-judgement-mana"));
        Assert.All(Events(result,"judgement-righteousness"),e=>Assert.Equal("miss",e.ResultKey));
        var healing=Copy(r);healing.CustomPriority=["holy-light"];healing.DurationSeconds=3m;
        Assert.Equal(660m,Spent(Run(healing),"holy-light"));
    }

    [Fact]
    public void ImprovedAndSwiftJudgementsHaveActualIndependentCooldownsAndNoGlobalLock()
    {
        var r=Request(("improved-judgement",2),("swift-judgement",1));r.Seal="righteousness";
        r.CustomPriority=["seal-of-righteousness","swift-judgement","judgement","holy-strike"];r.DurationSeconds=18m;
        var result=Run(r);var judges=Events(result,"judgement",CombatEventType.AbilityCastCompleted);
        Assert.True(judges.Length>=3);Assert.Equal(8m,judges[1].TimeSeconds-judges[0].TimeSeconds);
        var swift=Assert.Single(Events(result,"swift-judgement",CombatEventType.AbilityCastCompleted));
        Assert.True(swift.TimeSeconds<1.5m);Assert.True(judges[0].TimeSeconds<1.5m);
        Assert.Equal(0m,Spent(result,"swift-judgement"));
        Assert.Equal(1.5m,Events(result,"holy-strike",CombatEventType.AbilityCastCompleted)[0].TimeSeconds);
    }

    [Fact]
    public void PurifyingPowerDiscountsCleansesAndShortensActualUndeadSpellCooldowns()
    {
        var r=Request(("purifying-power",2));r.CustomPriority=["cleanse"];r.DurationSeconds=1m;
        var plain=Copy(r);plain.Talents.Ranks["purifying-power"]=0;
        Assert.Equal(Spent(Run(plain),"cleanse")*.8m,Spent(Run(r),"cleanse"));
        r.TargetCreatureType="undead";r.CustomPriority=["exorcism"];r.DurationSeconds=23m;
        var casts=Events(Run(r),"exorcism",CombatEventType.AbilityCastCompleted);
        Assert.True(casts.Length>=3);Assert.Equal(10.05m,casts[1].TimeSeconds-casts[0].TimeSeconds);
        r.CustomPriority=["holy-wrath"];r.DurationSeconds=48m;
        casts=Events(Run(r),"holy-wrath",CombatEventType.AbilityCastCompleted);
        Assert.Equal(2,casts.Length);Assert.Equal(40.2m,casts[1].TimeSeconds-casts[0].TimeSeconds);
    }

    [Fact]
    public void SpiritualFocusAndConcentrationPreventRealCastPushbackTogether()
    {
        var r=Request(("spiritual-focus",5));r.CustomPriority=["aura-concentration","holy-light"];
        r.IncomingBossHitDamage=10m;r.IncomingBossSwingSeconds=.25m;r.IncomingAttackType="spell";
        r.IncomingDamageSchool="holy";r.DurationSeconds=10m;
        var result=Run(r);Assert.True(Metric(result,"pushback-resisted")>0m);Assert.Equal(0m,Metric(result,"pushback-seconds"));
        var plain=Copy(r);plain.Talents.Ranks["spiritual-focus"]=0;
        Assert.True(Metric(Run(plain),"pushback-seconds")>0m);
    }

    [Fact]
    public void CancelledHolyLightRetainsInfusionUntilAnActualCastCompletes()
    {
        var r=Request(("divine-favor",1),("infusion-of-light",2),("holy-shock",1));
        r.CustomPriority=["divine-favor","holy-shock-heal","holy-light"];r.DurationSeconds=7m;
        r.UtilityScenario=new(){Effects=[new(){AtSeconds=2m,Kind="silence",DurationSeconds=1m}]};
        var result=Run(r);Assert.Single(Events(result,"holy-light",CombatEventType.AbilityCastCancelled));
        var complete=Events(result,"holy-light",CombatEventType.AbilityCastCompleted).First();
        var start=Events(result,"holy-light",CombatEventType.AbilityCastStarted).Single(e=>e.AbilityExecutionId==complete.AbilityExecutionId);
        Assert.Equal(1.5m,complete.TimeSeconds-start.TimeSeconds);
        var consumed=result.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="infusion-of-light").ToArray();
        Assert.NotEmpty(consumed);Assert.Equal(complete.TimeSeconds,consumed[0].TimeSeconds);
    }

    [Fact]
    public void BoundlessLightChangesOnlyNonOathFriendlyHolyShockCriticalsAcrossFixedSeeds()
    {
        var r=Request(("boundless-light",2),("oath-of-light",1));r.FriendlyTargetEnabled=true;r.HealTarget="friendly";
        r.CustomPriority=["holy-shock-heal"];r.DurationSeconds=2m;
        var plain=Copy(r);plain.Talents.Ranks["boundless-light"]=0;
        var oathed=Copy(r);oathed.CustomPriority=["oath-of-light","holy-shock-heal"];
        var crits=0;
        for(var seed=1;seed<=100;seed++)
        {
            r.Seed=plain.Seed=oathed.Seed=seed;
            crits+=Events(Run(r),"holy-shock-heal",CombatEventType.Healing).Count(e=>e.IsCritical);
            Assert.All(Events(Run(plain),"holy-shock-heal",CombatEventType.Healing),e=>Assert.False(e.IsCritical));
            Assert.All(Events(Run(oathed),"holy-shock-heal",CombatEventType.Healing),e=>Assert.False(e.IsCritical));
        }
        Assert.InRange(crits,15,45); // Probability witness, not a claim about an arbitrary gameplay build.
    }

    [Fact]
    public void DivinePrecisionAndPrecisionFeedTheCorrectActualHitTables()
    {
        var holy=Request(("divine-precision",2));holy.CustomPriority=["consecration"];holy.BaseSpellHitPercent=92m;
        holy.TargetCount=6;holy.DurationSeconds=60m;
        Assert.All(Events(Run(holy),"consecration"),e=>Assert.NotEqual("miss",e.ResultKey));
        holy.Talents.Ranks["divine-precision"]=0;
        Assert.Contains(Events(Run(holy),"consecration"),e=>e.ResultKey=="miss");
        var precision=Request(("precision",3));precision.CustomPriority=["holy-strike","consecration"];
        precision.BaseMeleeHitPercent=precision.BaseSpellHitPercent=0m;precision.UseAutoAttacks=true;
        precision.WeaponSpeedSeconds=.25m;precision.DurationSeconds=60m;precision.TargetCount=6;
        var result=Run(precision);Assert.Contains(Events(result,"main-hand-auto"),e=>e.Amount>0m);
        Assert.Contains(Events(result,"consecration"),e=>e.Amount>0m);
        precision.Talents.Ranks["precision"]=0;
        Assert.All(Run(precision).Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.SourceActorKey=="paladin"),e=>Assert.Equal(0m,e.Amount));
    }

    [Fact]
    public void RedoubtSeesAvoidedIncomingMeleeAttemptsButNeverRangedAttacks()
    {
        var r=Request(("redoubt",5));r.ManualWeaponKind="one-hand";r.ManualShield=true;
        r.CustomPriority=["divine-shield"];r.IncomingBossHitDamage=10m;r.IncomingBossSwingSeconds=.25m;r.DurationSeconds=11.75m;
        var result=Run(r);Assert.All(Events(result,"encounter:paladin-incoming"),e=>Assert.Equal(0m,e.Amount));
        Assert.True(Metric(result,"redoubt-procs")>0m);
        r.IncomingAttackType="ranged";Assert.Equal(0m,Metric(Run(r),"redoubt-procs"));
        r.IncomingAttackType="melee";r.Talents.Ranks["redoubt"]=0;Assert.Equal(0m,Metric(Run(r),"redoubt-procs"));
    }

    [Fact]
    public void GuardianFavorChangesFreedomImmunityAndProtectionCooldown()
    {
        var r=Request(("guardians-favor",2));r.CustomPriority=["blessing-of-freedom"];r.DurationSeconds=17m;
        r.UtilityScenario=new(){Effects=[new(){AtSeconds=12m,Kind="root",DurationSeconds=2m}]};
        Assert.Equal(1m,Metric(Run(r),"control-effects-prevented"));
        var plain=Copy(r);plain.Talents.Ranks["guardians-favor"]=0;
        Assert.Equal(0m,Metric(Run(plain),"control-effects-prevented"));
        r.CustomPriority=["blessing-of-protection"];r.UtilityScenario=null;r.DurationSeconds=185m;
        var casts=Events(Run(r),"blessing-of-protection",CombatEventType.AbilityCastCompleted);
        Assert.Equal(new[]{0m,180m},casts.Select(e=>e.TimeSeconds));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void ImprovedHammerJusticeChangesActualStunCooldown(int rank)
    {
        var r=Request(("improved-hammer-of-justice",3));r.Talents.Ranks["improved-hammer-of-justice"]=rank;
        r.TargetControlImmune=false;r.CustomPriority=["hammer-of-justice"];r.DurationSeconds=125m;
        var casts=Events(Run(r),"hammer-of-justice",CombatEventType.AbilityCastCompleted);
        Assert.True(casts.Length>=2);Assert.Equal(60m-5m*rank,casts[1].TimeSeconds-casts[0].TimeSeconds);
    }

    [Fact]
    public void ToughnessAppliesOnlyEquipmentArmorAndRighteousFuryReductionRequiresTheActiveAura()
    {
        var r=Request(("toughness",5),("improved-righteous-fury",3));r.UseManualWeaponInputs=false;
        r.Character.Equipment.Items=[new(){SlotKey="main-hand",ItemId=21395},new(){SlotKey="off-hand",ItemId=21269}];
        r.Character.BaseStats.Set("agility",100m);r.Character.BaseStats.Set("armor",300m);
        var plain=Copy(r);plain.Talents.Ranks["toughness"]=0;
        // Removing these tier points also removes the later talent. The armor
        // comparison remains isolated because Righteous Fury is not active.
        plain.Talents.Ranks["improved-righteous-fury"]=0;
        Assert.Equal(2964m*.1m,PaladinSimRunner.Preview(r).EffectiveStats.Get("armor")-PaladinSimRunner.Preview(plain).EffectiveStats.Get("armor"));
        r.CustomPriority=["righteous-fury"];r.IncomingBossHitDamage=100m;r.IncomingAttackType="spell";r.IncomingDamageSchool="holy";
        var active=Run(r);Assert.All(Events(active,"encounter:paladin-incoming"),e=>Assert.Equal(91m,e.Amount));
        r.CustomPriority=["aura-devotion"];
        Assert.All(Events(Run(r),"encounter:paladin-incoming"),e=>Assert.Equal(100m,e.Amount));
    }

    [Fact]
    public void AnticipationAndDeflectionAffectIncomingMeleeWithoutParryingSpells()
    {
        var r=Request(("anticipation",5),("deflection",5));r.CustomPriority=["aura-devotion"];
        r.IncomingBossHitDamage=1m;r.IncomingBossSwingSeconds=.25m;r.DurationSeconds=120m;
        Assert.Equal(320m,PaladinSimRunner.Preview(r).EffectiveStats.Get("defense-skill"));
        var melee=Run(r);Assert.Contains(Events(melee,"encounter:paladin-incoming"),e=>e.ResultKey=="parry");
        Assert.Contains(Events(melee,"encounter:paladin-incoming"),e=>e.ResultKey=="dodge");
        r.IncomingAttackType="spell";r.IncomingDamageSchool="holy";
        Assert.DoesNotContain(Events(Run(r),"encounter:paladin-incoming"),e=>e.ResultKey is "parry" or "dodge");
    }

    [Theory]
    [InlineData(0,0)] [InlineData(1,8)] [InlineData(2,15)]
    public void PursuitUsesRealMovementAndDoesNotStackWithALargerMovementBonus(int rank,int percent)
    {
        var r=Request(("pursuit-of-justice",2));r.Talents.Ranks["pursuit-of-justice"]=rank;r.DurationSeconds=1m;
        r.UtilityScenario=new(){InitialTargetDistanceYards=100m,MoveTowardTarget=true};
        Assert.Equal(.75m*7m*(1m+percent/100m),Metric(Run(r),"distance-travelled"));
        r.UtilityScenario.OtherMovementSpeedBonusPercent=20m;
        Assert.Equal(.75m*7m*1.2m,Metric(Run(r),"distance-travelled"));
        r.UtilityScenario.Mounted=true;
        var mounted=Run(r);Assert.Equal(.75m*7m*1.2m,Metric(mounted,"distance-travelled"));
        Assert.DoesNotContain(mounted.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void VindicationNeedsItsIndependentSpellHitAndReducesRealEnemyAttackPowerDamage()
    {
        var r=Request(("vindication",3));r.UseAutoAttacks=true;r.WeaponSpeedSeconds=12m;r.CustomPriority=["aura-devotion"];
        r.IncomingBossHitDamage=0m;r.IncomingAttackPower=1000m;r.IncomingBossSwingSeconds=1m;r.IncomingDamageSchool="holy";r.DurationSeconds=20m;
        var landed=Run(r);Assert.True(Metric(landed,"vindication-procs")>0m);
        Assert.All(Events(landed,"encounter:paladin-incoming").Where(e=>e.Amount>0m),e=>Assert.Equal((1000m-204m)/14m,e.Amount!.Value,10));
        r.BaseSpellHitPercent=0m;var missed=Run(r);Assert.Equal(0m,Metric(missed,"vindication-procs"));Assert.True(Metric(missed,"vindication-misses")>0m);
        Assert.All(Events(missed,"encounter:paladin-incoming").Where(e=>e.Amount>0m),e=>Assert.Equal(1000m/14m,e.Amount!.Value,10));
    }

    [Fact]
    public void EyeForAnEyeReflectsExactReceivedDamageWithoutTwoHandOrVengeanceAmplification()
    {
        var r=Request(("eye-for-an-eye",2),("two-handed-weapon-specialization",3),("vengeance",3));
        r.CriticalChancePercent=100m;r.IncomingCriticalChancePercent=100m;r.IncomingBossHitDamage=100m;
        r.IncomingAttackType="spell";r.IncomingDamageSchool="holy";r.DurationSeconds=16m;
        var result=Run(r);var incoming=Events(result,"encounter:paladin-incoming").Where(e=>e.IsCritical).ToArray();
        var reflected=Events(result,"eye-for-an-eye");Assert.Equal(incoming.Length,reflected.Length);
        for(var i=0;i<incoming.Length;i++)
        {Assert.Equal(incoming[i].Amount*.1m,reflected[i].Amount);Assert.True(reflected[i].Amount<=result.MaximumHealth*.5m);}
        Assert.True(Metric(result,"vengeance-triggers")>0m);
    }

    [Fact]
    public void VengeanceStacksToFiveAndExpiresDuringARealControlWindow()
    {
        var r=Request(("vengeance",3));r.CriticalChancePercent=100m;r.DurationSeconds=65m;
        var plain=Copy(r);plain.Talents.Ranks["vengeance"]=0;
        var a=Events(Run(plain),"holy-strike");var b=Events(Run(r),"holy-strike");Assert.Equal(a.Length,b.Length);
        for(var i=0;i<a.Length;i++)Assert.Equal(a[i].Amount*(1m+.03m*Math.Min(5,i)),b[i].Amount);
        r.UtilityScenario=new(){Effects=[new(){AtSeconds=20m,Kind="fear",DurationSeconds=35m}]};
        var controlled=Run(r);
        Assert.Contains(controlled.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="vengeance"&&e.TimeSeconds==40m);
        Assert.DoesNotContain(Events(controlled,"holy-strike"),e=>e.TimeSeconds>=20m&&e.TimeSeconds<55m);
    }

    [Fact]
    public void SacredArbiterChangesStrikeDamageWhileTwoHandSpecializationChangesDamageAndThreat()
    {
        var r=Request(("sacred-arbiter",1),("two-handed-weapon-specialization",3));r.DurationSeconds=2m;
        var plain=Copy(r);plain.Talents.Ranks["sacred-arbiter"]=0;plain.Talents.Ranks["two-handed-weapon-specialization"]=0;
        var result=Run(r);Assert.Equal(FirstDamage(Run(plain),"holy-strike")*1.2m*1.06m,FirstDamage(result,"holy-strike"),10);
        Assert.Equal(FirstDamage(result,"holy-strike")*.8m,Assert.Single(Events(result,"holy-strike",CombatEventType.ThreatChanged)).Amount);
        r.ManualWeaponKind="one-hand";plain.ManualWeaponKind="one-hand";
        Assert.Equal(FirstDamage(Run(plain),"holy-strike")*1.2m,FirstDamage(Run(r),"holy-strike"),10);
    }

    [Theory]
    [InlineData("exorcism")] [InlineData("hammer-of-wrath")] [InlineData("retribution-aura")]
    public void LatestMeasuredTwoHandExceptionsDoNotReceiveTheDamageBonus(string ability)
    {
        var r=Request(("two-handed-weapon-specialization",3));r.TargetCreatureType="undead";r.TargetInitialHealthPercent=20m;
        r.CustomPriority=[ability=="retribution-aura"?"aura-retribution":ability];r.IncomingBossHitDamage=10m;r.DurationSeconds=5m;
        var plain=Copy(r);plain.Talents.Ranks["two-handed-weapon-specialization"]=0;
        Assert.Equal(Run(plain).DamageByAbility[ability],Run(r).DamageByAbility[ability]);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public void ChampionUsesActualIntellectForDamageOnlyAndChangesExecuteCastTime(int rank)
    {
        var r=Request(("champion-of-the-light",5));r.Talents.Ranks["champion-of-the-light"]=rank;
        r.TargetInitialHealthPercent=20m;r.CustomPriority=["hammer-of-wrath"];r.DurationSeconds=2m;
        var p=PaladinSimRunner.Preview(r);Assert.Equal(100m+1000m*.12m*rank,p.SpellPower);Assert.Equal(100m,p.HealingPower);
        var result=Run(r);var start=Assert.Single(Events(result,"hammer-of-wrath",CombatEventType.AbilityCastStarted));
        var end=Assert.Single(Events(result,"hammer-of-wrath",CombatEventType.AbilityCastCompleted));
        Assert.Equal(1m-.2m*rank,end.TimeSeconds-start.TimeSeconds);
        r.TargetInitialHealthPercent=21m;Assert.Empty(Events(Run(r),"hammer-of-wrath"));
    }

    [Theory]
    [InlineData("holy-shock",20,"humanoid")]
    [InlineData("holy-wrath",20,"undead")]
    [InlineData("hammer-of-justice",10,"humanoid")]
    [InlineData("repentance",20,"humanoid")]
    [InlineData("turn-undead",20,"demon")]
    public void SourcedHostileRangeGatesUseActualDistance(string action,int yards,string creature)
    {
        var r=action switch {"holy-shock"=>Request(("holy-shock",1)),"repentance"=>Request(("repentance",1)),
            "turn-undead"=>Request(("turn-evil",2)),_=>Request()};
        r.CustomPriority=[action];r.TargetCreatureType=creature;r.TargetControlImmune=false;r.DurationSeconds=4m;
        r.UtilityScenario=new(){InitialTargetDistanceYards=yards};Assert.NotEmpty(Events(Run(r),action,CombatEventType.AbilityCastCompleted));
        r.UtilityScenario.InitialTargetDistanceYards=yards+.25m;Assert.Empty(Events(Run(r),action,CombatEventType.AbilityCastCompleted));
    }

    [Fact]
    public void RepentanceBreaksOnDamageAndTurnEvilEnablesActualDemonControl()
    {
        var r=Request(("repentance",1));r.TargetControlImmune=false;r.CustomPriority=["repentance","holy-strike"];r.DurationSeconds=4m;
        var result=Run(r);Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="repentance"&&e.TimeSeconds==1.5m);
        var demon=Request(("turn-evil",2));demon.TargetControlImmune=false;demon.TargetCreatureType="demon";
        demon.CustomPriority=["turn-undead"];demon.IncomingBossHitDamage=100m;demon.DurationSeconds=22m;
        var turned=Run(demon);Assert.Contains(turned.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="turn-undead");
        Assert.DoesNotContain(Events(turned,"encounter:paladin-incoming"),e=>e.TimeSeconds<20.5m);
        demon.Talents.Ranks["turn-evil"]=0;Assert.Throws<ArgumentException>(()=>Run(demon));
    }

    [Theory]
    [InlineData(20,20)] [InlineData(100,55)]
    public void SacrificeTransfersRealFriendlyDamageCappedPerHitAndUsesOneBlessingOwnership(int hit,int transfer)
    {
        var r=Request(("improved-righteous-fury",3));r.FriendlyTargetEnabled=true;r.HealTarget="friendly";
        r.FriendlyTargetIncomingDamage=hit;r.FriendlyTargetIncomingIntervalSeconds=1m;r.CustomPriority=["blessing-of-sacrifice","righteous-fury"];
        r.DurationSeconds=6m;var result=Run(r);
        Assert.Equal(4m*transfer,Metric(result,"sacrifice-redirected-damage"));Assert.Equal(4m*transfer,result.DamageTaken);
        Assert.Equal(2500m-4m*(hit-transfer),result.FriendlyEndingHealth);Assert.Equal(0m,result.HealingDone);
        r.CustomPriority=["blessing-of-sacrifice","blessing-of-protection"];r.DurationSeconds=2.5m;
        var replaced=Run(r);Assert.Equal(0m,Metric(replaced,"sacrifice-redirected-damage"));
        Assert.Contains(replaced.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="blessing-of-sacrifice");
    }

    [Fact]
    public void LayOnHandsCanCritDrainsManaAndNeverTriggersIlluminationOrAFalseHealingWorkflow()
    {
        var r=Request(("holy-power",5),("illumination",5));r.CustomPriority=["lay-on-hands"];r.CriticalChancePercent=100m;
        r.InitialManaPercent=50m;r.DurationSeconds=1m;
        var result=Run(r);var heal=Assert.Single(Events(result,"lay-on-hands",CombatEventType.Healing));
        Assert.Equal(result.MaximumHealth*2.25m,heal.RawAmount);Assert.True(heal.OverhealingAmount>0m);
        Assert.Equal(550m,result.EndingMana);Assert.Equal(0m,Metric(result,"illumination-procs"));
    }

    [Fact]
    public void SelectedItemsRespectChargesDisabledStateCapsAndClassRestrictions()
    {
        var r=Request();r.InitialManaPercent=10m;r.DurationSeconds=8m;r.CombatItems.MajorManaPotionCharges=1;
        var selected=Run(r);Assert.Equal(1,selected.CombatItemUses["major-mana-potion"]);
        Assert.InRange(selected.EndingMana,0m,selected.MaximumMana);Assert.Equal(1,r.CombatItems.MajorManaPotionCharges);
        var empty=Copy(r);empty.CombatItems.MajorManaPotionCharges=0;var noItem=Run(empty);
        Assert.False(noItem.CombatItemUses.ContainsKey("major-mana-potion"));Assert.True(selected.EndingMana>noItem.EndingMana);
        r.CombatItems.DisabledItemKeys=["major-mana-potion"];Assert.False(Run(r).CombatItemUses.ContainsKey("major-mana-potion"));
        r.CombatItems.DisabledItemKeys=[];r.InitialManaPercent=100m;Assert.False(Run(r).CombatItemUses.ContainsKey("major-mana-potion"));
        r.CombatItems.ManaRubyCharges=1;Assert.Throws<ArgumentException>(()=>Run(r));
    }

    [Fact]
    public void ActualCatalogFieryEnchantTriggersFromHolyStrikeButNotRangedJudgement()
    {
        var r=Request();r.UseManualWeaponInputs=false;r.DurationSeconds=180m;
        var fiery=ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Single(e=>e.Key=="fiery-weapon");
        r.Character.Equipment.Items=[new(){SlotKey="main-hand",ItemId=21395,EnchantSpellId=fiery.EnchantSpellId}];
        var result=Run(r);Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage&&e.AbilityKey=="equipment-fiery-weapon@main-hand");
        r.Seal="righteousness";r.CustomPriority=["seal-of-righteousness","judgement"];
        Assert.DoesNotContain(Run(r).Run.Timeline,e=>e.Type==CombatEventType.Damage&&e.AbilityKey=="equipment-fiery-weapon@main-hand");
    }

    [Fact]
    public void KingsUsesCurrentPrimariesOnceChangesLiveCapsAndReversesOnBlessingReplacement()
    {
        var r=Request(("divine-strength",5),("divine-intellect",5));r.CustomPriority=["blessing-of-kings","holy-strike"];r.DurationSeconds=4m;
        var preview=PaladinSimRunner.Preview(r);var result=Run(r);
        Assert.Equal(preview.MaximumMana,result.MaximumMana);Assert.Equal(preview.MaximumHealth,result.MaximumHealth);
        Assert.Equal(preview.MaximumMana+preview.EffectiveStats.Get("intellect")*.1m*15m,result.EndingMaximumMana);
        Assert.Equal(preview.MaximumHealth+preview.EffectiveStats.Get("stamina")*.1m*10m,result.EndingMaximumHealth);
        Assert.True(result.EndingMana<result.MaximumMana); // Increased capacity never grants free Mana.
        var plain=Copy(r);plain.CustomPriority=["holy-strike"];
        Assert.Equal(preview.EffectiveStats.Get("strength")*.1m*2m*3.3m/14m*.5m,
            FirstDamage(result,"holy-strike")-FirstDamage(Run(plain),"holy-strike"),10);
        r.CustomPriority=["blessing-of-kings","blessing-of-might"];r.DurationSeconds=2.5m;
        var replaced=Run(r);Assert.Equal(preview.MaximumMana,replaced.EndingMaximumMana);Assert.Equal(preview.MaximumHealth,replaced.EndingMaximumHealth);
        Assert.Contains(replaced.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="blessing-of-kings");
        r.DurationSeconds=14.5m;var repeated=Run(r); // Repeated replacement must not accumulate a percentage bonus.
        Assert.InRange(repeated.EndingMaximumMana,preview.MaximumMana,result.EndingMaximumMana);
        Assert.InRange(repeated.EndingMaximumHealth,preview.MaximumHealth,result.EndingMaximumHealth);
    }

    [Fact]
    public void ActiveMightDoesNotDoubleAnExplicitExternalMightOrTreatManualAttackPowerAsABuff()
    {
        var r=Request();r.ExternalModifiers.Buffs=["blessing-of-might"];r.DurationSeconds=3m;
        var baseline=Run(r);r.CustomPriority=["blessing-of-might","holy-strike"];
        Assert.Equal(FirstDamage(baseline,"holy-strike"),FirstDamage(Run(r),"holy-strike"));
        r.ExternalModifiers.Buffs=[];r.Character.BaseStats.Set("attack-power",133m);
        var manual=Copy(r);manual.CustomPriority=["holy-strike"];
        Assert.Equal(133m*3.3m/14m*.5m,FirstDamage(Run(r),"holy-strike")-FirstDamage(Run(manual),"holy-strike"),10);
    }

    [Fact]
    public void ConvictionRanksChangeActualPhysicalAndSpellCriticalsWithoutPresetSelection()
    {
        var r=Request(("conviction",3));r.UseAutoAttacks=true;r.WeaponSpeedSeconds=.25m;r.DurationSeconds=120m;
        r.CustomPriority=["consecration"];r.TargetCount=6;
        var counts=new List<(int Melee,int Spell)>();
        foreach(var rank in new[]{0,1,3})
        {
            r.Talents.Ranks["conviction"]=rank;var result=Run(r);
            counts.Add((Events(result,"main-hand-auto").Count(e=>e.IsCritical),Events(result,"consecration").Count(e=>e.IsCritical)));
        }
        Assert.Equal((0,0),counts[0]);Assert.True(counts[1].Melee>0&&counts[1].Spell>0);
        Assert.True(counts[2].Melee>counts[1].Melee&&counts[2].Spell>counts[1].Spell);
    }

    [Fact]
    public void SealCommandMakesItsOwnHitRollAndControlledJudgementDoublesOnlyTheBaseDamage()
    {
        var r=Request(("seal-of-command",1));r.Seal="command";r.UseAutoAttacks=true;
        r.BaseMeleeHitPercent=50m;r.DurationSeconds=180m;r.CustomPriority=["seal-of-command"];
        var procs=Events(Run(r),"seal-proc-command");Assert.Contains(procs,e=>e.ResultKey=="miss");Assert.Contains(procs,e=>e.Amount>0m);
        r.UseAutoAttacks=false;r.BaseMeleeHitPercent=100m;r.DurationSeconds=3m;r.TargetControlImmune=false;
        r.CustomPriority=["seal-of-command","judgement"];var plain=FirstDamage(Run(r),"judgement-command");
        r.CustomPriority=["hammer-of-justice","seal-of-command","judgement"];
        Assert.Equal(2m*plain-.429m*PaladinSimRunner.Preview(r).SpellPower,FirstDamage(Run(r),"judgement-command"),10);
        r.Talents.Ranks["seal-of-command"]=0;Assert.Throws<ArgumentException>(()=>Run(r));
    }

    [Theory]
    [InlineData("templars-bulwark")] [InlineData("divine-shield")] [InlineData("divine-protection")]
    public void SacredDutyChangesEachActualDefensiveCooldown(string action)
    {
        var r=Request(("sacred-duty",2),("templars-bulwark",1));r.CustomPriority=[action];r.DurationSeconds=245m;
        var casts=Events(Run(r),action,CombatEventType.AbilityCastCompleted);
        Assert.Equal(new[]{0m,240m},casts.Select(e=>e.TimeSeconds));
    }

    [Fact]
    public void IronCreedChangesHolyStrikeThreatIndependentlyOfItsRighteousFuryMitigationGate()
    {
        var r=Request(("iron-creed",3));r.DurationSeconds=2m;
        var result=Run(r);Assert.Equal(FirstDamage(result,"holy-strike")*1.3m,
            Assert.Single(Events(result,"holy-strike",CombatEventType.ThreatChanged)).Amount);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="iron-creed");
    }

    private sealed class NoMitigation:IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext c,SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount)=>DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class DynamicObserver(SimulationActorState actor):ICombatEventProcessor
    {
        public StatCollection? Applied {get;private set;} public StatCollection? Expired {get;private set;}
        public decimal AppliedManaCap {get;private set;} public decimal AppliedMana {get;private set;}
        public decimal AppliedHealthCap {get;private set;}
        public void Process(SimulationContext c,CombatEvent e)
        {
            if(e.EffectKey!="controlled-paladin-stat-effect@trinket1")return;
            if(e.Type==CombatEventType.AuraApplied)
            {Applied=new(){Values=new(actor.Stats.Values)};AppliedManaCap=actor.Resources["mana"].Maximum;AppliedMana=actor.Resources["mana"].Current;AppliedHealthCap=actor.MaximumHealth;}
            if(e.Type==CombatEventType.AuraRemoved)Expired=new(){Values=new(actor.Stats.Values)};
        }
    }

    [Fact]
    public void SharedTemporaryPrimaryStatsApplyTalentsAndCapsOnceAndRestoreOnExpiration()
    {
        var r=Request(("divine-strength",5),("divine-intellect",5),("champion-of-the-light",5));
        var prepared=PaladinSimRunner.Preview(r);var stats=new StatCollection {Values=new(prepared.EffectiveStats.Values)};
        var c=new SimulationContext(new(){Seed=175,DurationSeconds=8m,PrimaryActorKey="paladin",CaptureTimeline=true});
        var actor=new SimulationActorState {Key="paladin",Name="Controlled equipment witness",TeamKey="raid",Level=60,Stats=stats,MaximumHealth=1000m};
        actor.InitializeHealth(1000m,500m);actor.AddResource(new("mana",prepared.MaximumMana,1000m));c.AddActor(actor);
        var target=new SimulationActorState {Key="target",Name="Target",TeamKey="enemy",Level=63,MaximumHealth=10000m};target.InitializeHealth(10000m);c.AddActor(target);
        var effect=new ForeverExecutableEquipmentEffect {Key="controlled-paladin-stat-effect",Kind="on-use",Trigger="manual",Modeled=true,
            AutoUse=true,DurationSeconds=2m,CooldownSeconds=30m,SourceBuild="controlled-engine-test",
            StatBonuses=new(){["strength"]=100m,["intellect"]=100m,["stamina"]=10m,["spell-power"]=30m,["healing-power"]=40m}};
        var aura=new AuraManager();var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new NoMitigation());
        var effects=new ForeverEquipmentEffectProcessor(new CharacterEquipmentLoadout(),actor.Key,target.Key,aura,executor,175,
            explicitBindings:[new("trinket1",effect)],classKey:"paladin",
            onStatDelta:(ctx,a,delta)=>PaladinSimRunner.ApplyEquipmentStatDelta(ctx,a,delta,r.Talents));
        var observer=new DynamicObserver(actor);new SimulationEngine([executor,aura,effects,observer]).Run(c);
        var applied=Assert.IsType<StatCollection>(observer.Applied);var expired=Assert.IsType<StatCollection>(observer.Expired);
        Assert.Equal(110m,applied.Get("strength")-prepared.EffectiveStats.Get("strength"));
        Assert.Equal(220m,applied.Get("attack-power")-prepared.AttackPower);
        Assert.Equal(110m,applied.Get("intellect")-prepared.EffectiveStats.Get("intellect"));
        Assert.Equal(30m+110m*.6m,applied.Get("spell-power")-prepared.SpellPower);
        Assert.Equal(70m,applied.Get("healing-power")-prepared.HealingPower);
        Assert.Equal(prepared.MaximumMana+1650m,observer.AppliedManaCap);Assert.Equal(1000m,observer.AppliedMana);
        Assert.Equal(1100m,observer.AppliedHealthCap);Assert.Equal(1000m,actor.MaximumHealth);
        foreach(var key in new[]{"strength","intellect","attack-power","block-value","spell-power","healing-power"})
            Assert.Equal(prepared.EffectiveStats.Get(key),expired.Get(key));
        Assert.Equal(prepared.MaximumMana,actor.Resources["mana"].Maximum);Assert.Equal(1000m,actor.Resources["mana"].Current);
    }

    [Fact]
    public void BulwarkSnapshotsLiveMaximumHealthAtTheActualSharedAbsorbExecution()
    {
        var r=Request(("templars-bulwark",1));var c=new SimulationContext(new(){Seed=1,DurationSeconds=2m,PrimaryActorKey="paladin",CaptureTimeline=true});
        var actor=new SimulationActorState {Key="paladin",Name="Live cap witness",TeamKey="raid",Level=60,MaximumHealth=1000m};
        actor.InitializeHealth(1000m);actor.AddResource(new("mana",1000m,1000m));c.AddActor(actor);
        actor.AddAbility(PaladinSpellbook.Create(r,"none",false,1512m,1000m).Single(a=>a.Key=="templars-bulwark"));
        var aura=new AuraManager();var executor=new AbilityExecutor(aura,new PaladinCombatRolls(r,false),new NoMitigation());
        var run=new SimulationEngine([executor,aura]).Run(c,ctx=>
        {
            ForeverEquipmentDynamicResourceCaps.Apply(ctx,actor,new Dictionary<string,decimal>{["stamina"]=100m});
            Assert.Equal(2000m,actor.MaximumHealth);
            Assert.True(executor.TryStartAbility(ctx,actor.Key,actor.Key,"templars-bulwark").Success);
        });
        Assert.Equal(2000m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied).Amount);
    }
}
