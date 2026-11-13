using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Rank and event witnesses exercise the actual shared runner. Deliberately
/// controlled manual scenarios isolate effects; these are not recommended builds.</summary>
public sealed partial class Cp175PaladinMechanics
{
    private static PaladinSimRequest Request(params (string Key,int Rank)[] talents)=>new()
    {
        Character=new CharacterProfile {Id=Guid.Empty,Name="Mechanic witness",ClassKey="paladin",SpecializationKey="retribution",
            RulesetKey="wow-forever",Race="human",Level=60,
            BaseStats=new StatCollection {Values=new() { ["strength"]=100m,["intellect"]=1000m,["stamina"]=100m,["spell-power"]=100m}}},
        Talents=Allocation(talents),UseManualWeaponInputs=true,ManualWeaponKind="two-hand",WeaponMinDamage=200m,WeaponMaxDamage=200m,
        WeaponSpeedSeconds=3.5m,UseManualCombatAssumptions=true,BaseMeleeHitPercent=100m,BaseRangedHitPercent=100m,BaseSpellHitPercent=100m,
        CriticalChancePercent=0m,IncomingBlockChancePercent=0m,UseAutoAttacks=false,Seal="none",Aura="none",
        MaximumHealth=1000000m,InitialHealthPercent=50m,TargetMaximumHealth=10000000m,TargetArmor=0m,
        DurationSeconds=16m,CustomPriority=["holy-strike"],Seed=31175
    };

    private static ForeverPaladinTalents Allocation(params (string Key,int Rank)[] selected)
    {
        var explicitKeys=selected.Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        var ranks=selected.ToDictionary(x=>x.Key,x=>x.Rank,StringComparer.Ordinal);
        for(var guard=0;guard<100;guard++)
        {
            var changed=false;
            foreach(var node in ForeverPaladinClassTalents.Entries.Where(x=>ranks.GetValueOrDefault(x.Key)>0).ToArray())
            {
                if(node.ParentKey is string parent&&ranks.GetValueOrDefault(parent)<node.ParentRank)
                {ranks[parent]=node.ParentRank;changed=true;}
                while(ForeverPaladinClassTalents.Entries.Where(x=>x.Tree==node.Tree&&x.Row<node.Row).Sum(x=>ranks.GetValueOrDefault(x.Key))<(node.Row-1)*5)
                {
                    var filler=ForeverPaladinClassTalents.Entries.FirstOrDefault(x=>x.Tree==node.Tree&&x.Row<node.Row&&
                        !explicitKeys.Contains(x.Key)&&ranks.GetValueOrDefault(x.Key)<x.MaxRank&&
                        (x.ParentKey is null||ranks.GetValueOrDefault(x.ParentKey)>=x.ParentRank)&&
                        ForeverPaladinClassTalents.Entries.Where(y=>y.Tree==x.Tree&&y.Row<x.Row).Sum(y=>ranks.GetValueOrDefault(y.Key))>=(x.Row-1)*5)
                        ??throw new InvalidOperationException("The test allocation cannot satisfy its sourced prerequisite.");
                    ranks[filler.Key]=ranks.GetValueOrDefault(filler.Key)+1;changed=true;
                }
            }
            if(!changed){ForeverPaladinClassTalents.Validate(ranks);return new(){Ranks=ranks};}
        }
        throw new InvalidOperationException("Test allocation did not converge.");
    }
    private static PaladinSimRequest Copy(PaladinSimRequest r)=>ForeverClassBuildJson.Clone(r);
    private static PaladinSimResponse Run(PaladinSimRequest r)=>new PaladinSimRunner().Run(r);
    private static CombatEvent[] Events(PaladinSimResponse r,string ability,CombatEventType type=CombatEventType.Damage)=>
        r.Run.Timeline.Where(e=>e.Type==type&&e.AbilityKey==ability).ToArray();
    private static decimal Metric(PaladinSimResponse r,string key)=>r.Run.CustomMetrics.GetValueOrDefault("paladin-"+key);
    private static decimal Spent(PaladinSimResponse r,string key)=>-Events(r,key,CombatEventType.ResourceChanged).Where(e=>e.Amount<0m).Sum(e=>e.Amount??0m);
    private static decimal HealRaw(PaladinSimResponse r,string key)=>Events(r,key,CombatEventType.Healing).Sum(e=>e.RawAmount??0m);
    private static decimal FirstDamage(PaladinSimResponse r,string key)=>Events(r,key).First(e=>e.Amount>0m).Amount!.Value;

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public void StrengthAndIntellectRanksComposeOnceBeforeActualActionsAndCaps(int rank)
    {
        var baseRequest=Request();var ranked=Copy(baseRequest);
        ranked.Talents=Allocation(("divine-strength",rank),("divine-intellect",rank));
        var a=PaladinSimRunner.Preview(baseRequest);var b=PaladinSimRunner.Preview(ranked);
        Assert.Equal(100m*.02m*rank,b.EffectiveStats.Get("strength")-a.EffectiveStats.Get("strength"));
        Assert.Equal(100m*.02m*rank*2m,b.AttackPower-a.AttackPower);
        Assert.Equal(1000m*.02m*rank*15m,b.MaximumMana-a.MaximumMana);
        var baseline=Run(baseRequest);var actual=Run(ranked);
        Assert.Equal(a.AttackPower,baseline.AttackPower);Assert.Equal(b.AttackPower,actual.AttackPower);
        Assert.Equal((b.AttackPower-a.AttackPower)*3.3m/14m*.5m,FirstDamage(actual,"holy-strike")-FirstDamage(baseline,"holy-strike"),10);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void HealingLightChangesTheActualQualifyingHealWithoutChangingItsCost(int rank)
    {
        var a=Request(("healing-light",3));a.CustomPriority=["flash-of-light"];a.DurationSeconds=5m;
        var b=Copy(a);a.Talents.Ranks["healing-light"]=0;b.Talents.Ranks["healing-light"]=rank;
        var x=Run(a);var y=Run(b);
        Assert.Equal(HealRaw(x,"flash-of-light")*(1m+.04m*rank),HealRaw(y,"flash-of-light"));
        Assert.Equal(Spent(x,"flash-of-light"),Spent(y,"flash-of-light"));Assert.True(y.HealingDone>0m);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public void HolyPowerMultipliesTheWholeHealingCriticalAndDoesNotAddCriticalChance(int rank)
    {
        var a=Request(("holy-power",5));a.CustomPriority=["flash-of-light"];a.DurationSeconds=3m;a.CriticalChancePercent=100m;
        var b=Copy(a);a.Talents.Ranks["holy-power"]=0;b.Talents.Ranks["holy-power"]=rank;
        Assert.Equal(HealRaw(Run(a),"flash-of-light")*(1m+.1m*rank),HealRaw(Run(b),"flash-of-light"));
        b.CriticalChancePercent=0m;
        Assert.All(Events(Run(b),"flash-of-light",CombatEventType.Healing),e=>Assert.False(e.IsCritical));
    }

    [Fact]
    public void IlluminationRefundsActualManaOncePerCriticalHealEvenWhenItOverheals()
    {
        var r=Request(("illumination",5));r.CustomPriority=["flash-of-light"];r.CriticalChancePercent=100m;
        r.InitialManaPercent=50m;r.InitialHealthPercent=100m;r.DurationSeconds=6m;
        var result=Run(r);var heals=Events(result,"flash-of-light",CombatEventType.Healing);
        Assert.NotEmpty(heals);Assert.All(heals,e=>{Assert.True(e.IsCritical);Assert.Equal(0m,e.Amount);Assert.True(e.OverhealingAmount>0m);});
        Assert.Equal(heals.Length*70m,Metric(result,"illumination-mana"));
        Assert.Equal(heals.Length,Metric(result,"illumination-procs"));
        var removed=Copy(r);removed.Talents.Ranks["illumination"]=0;
        Assert.Equal(0m,Metric(Run(removed),"illumination-mana"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ReverenceRetainsOnlyItsRankFractionOfExplicitSpiritRegenerationWhileCasting(int rank)
    {
        var r=Request(("reverence",2));r.Talents.Ranks["reverence"]=rank;r.ManaRegenPerSecond=40m;
        r.InitialManaPercent=50m;r.CustomPriority=["flash-of-light"];r.DurationSeconds=6m;
        var baseline=Copy(r);baseline.ManaRegenPerSecond=0m;
        var actual=Run(r);var firstPayment=Events(actual,"flash-of-light",CombatEventType.ResourceChanged).First(e=>e.Amount<0m).TimeSeconds;
        Assert.Equal(1.5m,firstPayment); // The five-second rule starts when the completed spell pays Mana.
        Assert.Equal(40m*(firstPayment+(6m-firstPayment)*.15m*rank),actual.EndingMana-Run(baseline).EndingMana);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void HolyConduitDiscountsActualSelectedSpellCosts(int rank)
    {
        var r=Request(("holy-conduit",2));r.Talents.Ranks["holy-conduit"]=rank;r.CustomPriority=["consecration"];r.DurationSeconds=7m;
        Assert.Equal(565m*(1m-.2m*rank),Spent(Run(r),"consecration"));
    }

    [Fact]
    public void DivineFavorIsConsumedAndInfusionShortensOnlyTheNextHolyLight()
    {
        var r=Request(("divine-favor",1),("infusion-of-light",2),("holy-shock",1));
        r.CustomPriority=["divine-favor","holy-shock-heal","holy-light"];r.DurationSeconds=7m;
        var run=Run(r);var shock=Assert.Single(Events(run,"holy-shock-heal",CombatEventType.Healing));Assert.True(shock.IsCritical);
        var starts=Events(run,"holy-light",CombatEventType.AbilityCastStarted);
        var ends=Events(run,"holy-light",CombatEventType.AbilityCastCompleted);
        Assert.True(starts.Length>=2);Assert.True(ends.Length>=2);
        Assert.Equal(1.5m,ends[0].TimeSeconds-starts[0].TimeSeconds);
        Assert.Equal(2.5m,ends[1].TimeSeconds-starts[1].TimeSeconds);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="divine-favor");
    }

    [Fact]
    public void OathUsesTheCurrentLevelSixtyBonusOnItsActualTargetAndReversesOnRemoval()
    {
        var a=Request(("oath-of-light",1));a.CustomPriority=["oath-of-light","flash-of-light"];a.DurationSeconds=3.1m;
        var b=Copy(a);b.CustomPriority=["flash-of-light"];
        var oathed=Events(Run(a),"flash-of-light",CombatEventType.Healing).First();
        var baseline=Events(Run(b),"flash-of-light",CombatEventType.Healing).First();
        Assert.Equal(ForeverPaladinSourceData.OathFlashOfLightAt60*(1m+.04m*a.Talents.Rank("healing-light")),oathed.RawAmount-baseline.RawAmount);
        a.FriendlyTargetEnabled=true;a.OathTarget="friendly";
        Assert.Equal(baseline.RawAmount,Events(Run(a),"flash-of-light",CombatEventType.Healing).First().RawAmount);
    }

    [Fact]
    public void VigilHealsBothLivingPartyTargetsAndResetsHolyShockWithoutReusingItsRefundIdentity()
    {
        var r=Request(("lights-vigil",1),("illumination",5));r.FriendlyTargetEnabled=true;r.HealTarget="friendly";
        r.FriendlyTargetMaximumHealth=100000m;r.FriendlyTargetInitialHealthPercent=50m;r.CriticalChancePercent=100m;
        r.InitialManaPercent=50m;r.CustomPriority=["lights-vigil","holy-shock-heal"];r.DurationSeconds=5m;
        var result=Run(r);var pulses=Events(result,"lights-vigil-heal",CombatEventType.Healing);
        Assert.Equal(2,pulses.Length);Assert.Equal(new[]{"friendly","paladin"},pulses.Select(e=>e.TargetActorKey).OrderBy(x=>x));
        Assert.All(pulses,e=>Assert.True(e.Amount>0m));Assert.Equal(1m,Metric(result,"lights-vigil-holy-shock-resets"));
        var shocks=Events(result,"holy-shock-heal",CombatEventType.Healing);Assert.True(shocks.Length>=2);
        Assert.Equal(670m+162.5m*shocks.Length,Metric(result,"illumination-mana"));
        Assert.Single(pulses.Select(x=>x.AbilityExecutionId).Distinct());
        Assert.DoesNotContain(pulses[0].AbilityExecutionId,shocks.Select(x=>x.AbilityExecutionId));
    }

    [Fact]
    public void ConsecrationExecutesOneHitRollPerEnemyPerTickWithFirstFourBonusAndCriticals()
    {
        var r=Request();r.CustomPriority=["consecration"];r.TargetCount=6;r.DurationSeconds=7.5m;r.CriticalChancePercent=100m;
        var result=Run(r);var ticks=Events(result,"consecration");Assert.Equal(42,ticks.Length);Assert.All(ticks,e=>Assert.True(e.IsPeriodic));
        Assert.All(ticks.Where(e=>e.TargetActorKey is "target" or "target-2" or "target-3" or "target-4"),
            e=>Assert.Equal((39m+.042m*100m)*1.5m,e.Amount));
        Assert.All(ticks.Where(e=>e.TargetActorKey is "target-5" or "target-6"),e=>Assert.Equal(18m,e.Amount));
        var misses=Copy(r);misses.BaseSpellHitPercent=0m;
        Assert.All(Events(Run(misses),"consecration"),e=>{Assert.Equal("miss",e.ResultKey);Assert.Equal(0m,e.Amount);});
    }

    [Fact]
    public void ConsecratedGroundIncreasesOnlyItsFirstFourMarkedTargets()
    {
        var r=Request(("consecrated-ground",2));r.CustomPriority=["consecration"];r.TargetCount=6;r.DurationSeconds=3m;
        var plain=Copy(r);plain.Talents.Ranks["consecrated-ground"]=0;
        var a=Events(Run(plain),"consecration");var b=Events(Run(r),"consecration");
        Assert.Equal(a.Length,b.Length);
        for(var i=0;i<a.Length;i++)Assert.Equal(a[i].Amount*(a[i].TargetActorKey is "target-5" or "target-6"?1m:1.1m),b[i].Amount);
    }

    [Fact]
    public void JudgmentUsesRangedHitAndDoesNotTriggerWeaponSealsOrWindfuryOrCapstone()
    {
        var r=Request(("unsealed-wrath",1));r.Seal="righteousness";r.BaseMeleeHitPercent=0m;r.BaseRangedHitPercent=100m;
        r.UseWindfuryTotem=true;r.WindfuryTotemProcChancePercent=100m;r.CustomPriority=["seal-of-righteousness","judgement"];r.DurationSeconds=24m;
        var result=Run(r);var judgments=Events(result,"judgement-righteousness");Assert.NotEmpty(judgments);
        Assert.All(judgments,e=>{Assert.Equal(CombatResolutionTypes.Ranged,e.ResolutionType);Assert.Null(e.WeaponHandKey);Assert.True(e.Amount>0m);});
        Assert.Empty(Events(result,"seal-proc-righteousness"));Assert.Equal(0m,Metric(result,"windfury-procs"));Assert.Equal(0m,Metric(result,"maximum-stored-seals"));
        r.BaseRangedHitPercent=0m;r.BaseMeleeHitPercent=100m;
        Assert.All(Events(Run(r),"judgement-righteousness"),e=>Assert.Equal("miss",e.ResultKey));
    }

    [Fact]
    public void HolyStrikeCannotTriggerWhiteOnlySealsOrStoreUnsealedEchoes()
    {
        var r=Request(("unsealed-wrath",1));r.Seal="righteousness";r.CustomPriority=["seal-of-righteousness","holy-strike"];
        var result=Run(r);Assert.NotEmpty(Events(result,"holy-strike"));Assert.Empty(Events(result,"seal-proc-righteousness"));
        Assert.Equal(0m,Metric(result,"maximum-stored-seals"));Assert.Equal(0m,Metric(result,"seal-echo-rolls"));
    }

    [Fact]
    public void SealSwitchConsumesUpToThreeStoredWhiteHitsAndCrusaderNeverStoresAnEcho()
    {
        var r=Request(("unsealed-wrath",1));r.UseAutoAttacks=true;r.Seal="righteousness";r.SecondarySeal="crusader";
        r.SealSwitchIntervalSeconds=12m;r.CustomPriority=["seal-of-righteousness","seal-of-crusader"];r.DurationSeconds=21m;
        var result=Run(r);Assert.Equal(3m,Metric(result,"maximum-stored-seals"));Assert.Equal(3m,Metric(result,"seal-echo-rolls"));
        Assert.Equal(3,Events(result,"unsealed-righteousness").Length);Assert.Equal(0m,Metric(result,"ending-stored-seal-stacks"));
        Assert.Empty(Events(result,"unsealed-crusader"));
        r.UseWindfuryTotem=true;r.WindfuryTotemProcChancePercent=100m;
        var windy=Run(r);Assert.True(Metric(windy,"windfury-extra-attacks")>0m);
        Assert.Equal(3m,Metric(windy,"seal-echo-rolls"));Assert.Equal(0m,Metric(windy,"ending-stored-seal-stacks"));
    }

    [Fact]
    public void ReckoningConsumesStoredAttacksOnAnAvoidedNormalSwingAndNeverRecurses()
    {
        var r=Request(("reckoning",5));r.UseAutoAttacks=true;r.WeaponSpeedSeconds=8m;r.BaseMeleeHitPercent=0m;
        r.IncomingBossHitDamage=10m;r.IncomingBossSwingSeconds=.5m;r.IncomingCriticalChancePercent=100m;r.DurationSeconds=10m;
        r.CustomPriority=["aura-devotion"];
        var result=Run(r);Assert.Equal(4m,Metric(result,"maximum-reckoning-stacks"));
        var extras=Events(result,"reckoning-extra");Assert.Equal(4,extras.Length);
        Assert.All(extras,e=>Assert.Equal(8m,e.TimeSeconds));Assert.Equal(4m,Metric(result,"reckoning-extra-attacks"));
        Assert.Contains(Events(result,"main-hand-auto"),e=>e.TimeSeconds==8m&&e.ResultKey=="miss");
    }

    [Fact]
    public void ShieldBlockUsesIntrinsicPlusBonusAndStrengthOnceAndHolyShieldChargesAreFinite()
    {
        var r=Request(("holy-shield",1),("shield-specialization",2));r.UseManualWeaponInputs=false;
        r.Character.Equipment.Items=[new(){SlotKey="main-hand",ItemId=21395},new(){SlotKey="off-hand",ItemId=21269}];
        r.IncomingBossHitDamage=1000m;r.IncomingBossSwingSeconds=1m;r.IncomingBlockChancePercent=100m;r.CustomPriority=["holy-shield"];r.DurationSeconds=6m;
        var preview=PaladinSimRunner.Preview(r);
        Assert.Equal(55m+15m+ForeverPhysicalStatConversions.StrengthToBlockValue(preview.EffectiveStats.Get("strength")),preview.EffectiveStats.Get("block-value"));
        Assert.Equal(308m+4m*r.Talents.Rank("anticipation"),preview.EffectiveStats.Get("defense-skill"));
        var result=Run(r);var blocked=Events(result,"encounter:paladin-incoming").Where(e=>e.ResultKey=="block").ToArray();Assert.NotEmpty(blocked);
        Assert.All(blocked,e=>Assert.Equal(preview.EffectiveStats.Get("block-value")*1.3m,e.BlockedAmount));
        Assert.Equal(4,Events(result,"holy-shield-retaliation").Length);
        Assert.Equal(4m,Metric(result,"holy-shield-charge-triggers"));Assert.Equal(0m,Metric(result,"ending-holy-shield-charges"));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="holy-shield");
    }

    [Fact]
    public void ShieldSpecializationReturnsFourPercentManaWithThreeSecondCooldown()
    {
        var r=Request(("shield-specialization",2));r.ManualWeaponKind="one-hand";r.ManualShield=true;r.InitialManaPercent=50m;
        r.IncomingBlockChancePercent=100m;r.IncomingBossHitDamage=1000m;r.IncomingBossSwingSeconds=.5m;
        r.CustomPriority=["aura-devotion"];r.DurationSeconds=9m;
        var result=Run(r);var gains=Events(result,"shield-specialization",CombatEventType.ResourceChanged);
        Assert.Equal(new[]{2m,5m,8m},gains.Select(e=>e.TimeSeconds));
        Assert.All(gains,e=>Assert.Equal(result.MaximumMana*.04m,e.Amount));
    }

    [Fact]
    public void ShieldswornAndIronCreedActivateOnMissButOnlyLandedStrikeGetsFreeBlockEffects()
    {
        var r=Request(("shieldsworn",1),("iron-creed",3));r.ManualWeaponKind="one-hand";r.ManualShield=true;
        r.BaseMeleeHitPercent=0m;r.IncomingBlockChancePercent=100m;r.IncomingBossHitDamage=1000m;
        r.IncomingAttackType="spell";r.IncomingDamageSchool="fire";r.IncomingBossSwingSeconds=1m;
        r.CustomPriority=["righteous-fury","holy-shield","holy-strike"];r.DurationSeconds=9m;
        var result=Run(r);Assert.All(Events(result,"holy-strike"),e=>Assert.Equal("miss",e.ResultKey));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="shieldsworn");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="iron-creed");
        Assert.Equal(0m,Metric(result,"shieldsworn-free-block-triggers"));
        var incoming=Events(result,"encounter:paladin-incoming");
        Assert.Contains(incoming,e=>e.TimeSeconds<3m&&e.ResultKey!="block");
        Assert.Contains(incoming,e=>e.TimeSeconds>3m&&e.ResultKey=="block");
        r.BaseMeleeHitPercent=100m;
        Assert.True(Metric(Run(r),"shieldsworn-free-block-triggers")>0m);
    }

    [Fact]
    public void BulwarkAbsorbsActualMaximumHealthAndForbearancePreventsOtherDefenses()
    {
        var r=Request(("templars-bulwark",1),("sacred-duty",2));r.MaximumHealth=1000m;r.InitialHealthPercent=100m;
        r.IncomingBossHitDamage=700m;r.IncomingAttackType="spell";r.IncomingDamageSchool="holy";r.DurationSeconds=4m;
        r.CustomPriority=["templars-bulwark","divine-shield","divine-protection"];
        var result=Run(r);Assert.Equal(700m,result.AbsorptionDone);Assert.Equal(0m,result.DamageTaken);
        Assert.Empty(Events(result,"divine-shield",CombatEventType.AbilityCastCompleted));
        Assert.Empty(Events(result,"divine-protection",CombatEventType.AbilityCastCompleted));
        var expiry=Copy(r);expiry.IncomingBossHitDamage=0m;expiry.DurationSeconds=51m;
        Assert.Equal(50m,Assert.Single(Events(Run(expiry),"divine-shield",CombatEventType.AbilityCastCompleted)).TimeSeconds);
    }

    [Theory]
    [InlineData("silence","divine-precision",2,8)]
    [InlineData("stun","stoicism",2,8)]
    [InlineData("charm","stoicism",2,8)]
    [InlineData("fear","unyielding-faith",2,7)]
    [InlineData("disorient","unyielding-faith",2,7)]
    public void UtilityTalentDurationsAffectRealControlAndCastAvailability(string control,string talent,int rank,int seconds)
    {
        var r=Request((talent,rank));r.CustomPriority=["flash-of-light"];
        r.UtilityScenario=new(){Effects=[new(){AtSeconds=1m,Kind=control,DurationSeconds=10m}]};
        var result=Run(r);Assert.Equal(seconds,Metric(result,"control-duration-"+control));
        Assert.DoesNotContain(Events(result,"flash-of-light",CombatEventType.AbilityCastStarted),e=>e.TimeSeconds>=1m&&e.TimeSeconds<1m+seconds);
        Assert.Contains(Events(result,"flash-of-light",CombatEventType.AbilityCastStarted),e=>e.TimeSeconds>=1m+seconds);
    }

    [Fact]
    public void FuryJudgementActuallyRedirectsFriendlyIncomingDamageForItsThreeSecondTaunt()
    {
        var r=Request();r.Seal="fury";r.FriendlyTargetEnabled=true;r.FriendlyTargetIncomingDamage=100m;
        r.FriendlyTargetIncomingIntervalSeconds=1m;r.CustomPriority=["seal-of-fury","judgement"];r.DurationSeconds=6m;
        var result=Run(r);var applied=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ForcedTargetApplied);
        var expectedHits=Enumerable.Range(2,4).Count(t=>t>=applied.TimeSeconds&&t<applied.TimeSeconds+3m);
        Assert.True(expectedHits>0);Assert.Equal(expectedHits,Metric(result,"fury-taunt-redirected-hits"));
        Assert.Equal(expectedHits*100m,result.DamageTaken);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ForcedTargetApplied&&e.ForcedTargetActorKey=="paladin");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ForcedTargetRemoved);
    }
}
