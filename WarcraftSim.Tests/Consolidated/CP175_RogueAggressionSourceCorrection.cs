using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

// Historical simulator pin 1cc5ae1: eviscerate.go:65-69 adds the two rank tables.
// This isolates the sourced modifier, not live-server or base-coefficient certification.
public sealed class Cp175RogueAggressionSourceCorrection
{
    private static RogueSimRequest Request(int? aggression = 3, int improved = 3, string spec = "combat")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { CombatPointsSpent = 21, AssassinationPointsSpent = 15,
            SubtletyPointsSpent = 15, Ranks = new() { ["improved-eviscerate"] = improved } };
        if (aggression.HasValue) r.Talents.Ranks["aggression"] = aggression.Value;
        r.StartInStealth = false; r.UseInstantPoison = false;
        r.MainHandPoison = r.OffHandPoison = "none";
        r.Character.BaseStats = new(); r.Tuning.PhysicalCritPercent = 0;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 50;
        r.Tuning.SinisterStrikeBonusDamage = r.Tuning.BackstabBonusDamage = 0;
        r.Tuning.EviscerateDamagePerPoint = 100;
        r.TargetArmor = r.TargetDodgePercent = r.TargetParryPercent = 0;
        r.TargetMaximumHealth = 10000000; r.DurationSeconds = 120; r.CaptureTimeline = true;
        return r;
    }
    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public RogueCombatProcessor Combat { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public RoguePoisonProcessor Poisons { get; }
        public Harness(RogueSimRequest r, int seed = 1, decimal duration = .5m)
        {
            Context = new(new() { Seed = seed, DurationSeconds = duration, CaptureTimeline = true,
                PrimaryActorKey = "rogue" });
            Actor = new() { Key = "rogue", TeamKey = "raid", Level = 60, MaximumHealth = 4000 };
            Actor.InitializeHealth(4000); Actor.AddResource(new ResourceState("energy", 100, 100));
            Context.AddActor(Actor);
            foreach (var key in new[] { "target", "target-2" })
            {
                var target = new SimulationActorState { Key = key, TeamKey = "enemy", Level = 63,
                    MaximumHealth = 100000 };
                target.InitializeHealth(100000); Context.AddActor(target);
            }
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"));
            Combat = combat = new(r, Actor, new RotationProfile(), Executor, Auras,
                new AutoAttackProcessor(Executor), (AutoAttackDefinition[])typeof(RogueSimRunner)
                    .GetMethod("CreateWeapons", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [r.Tuning, 0m, r.Talents])!);
            Poisons = new(r, Actor, Auras, Executor);
            foreach (var ability in ForeverRogueSpellbook.Create(r.Specialization, r.Talents,
                r.Tuning, 0, true)) Actor.AddAbility(ability);
        }
        public void Hit(string key = "sinister-strike", string target = "target", string source = "rogue",
            bool critical = false, bool periodic = false, decimal amount = 100, Guid? id = null)
            => Combat.Process(Context, new() { Type = CombatEventType.Damage,
                SourceActorKey = source, TargetActorKey = target, AbilityKey = key,
                AbilityExecutionId = id ?? Guid.NewGuid(), Amount = amount,
                ResultKey = amount == 0 ? CombatResultTypes.Miss : critical ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                IsCritical = critical, EffectDeliveryType = periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct });
        public CombatEvent[] Cast(string key, string target = "target")
        {
            var result = new SimulationEngine([Executor, Auras, Combat, Poisons]).Run(Context, ctx =>
                Assert.True(Executor.TryStartAbility(ctx, "rogue", target, key).Success));
            return result.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == key).ToArray();
        }
    }


    public static IEnumerable<object[]> RankCases()
    {
        decimal[,] literal = { {100,102,104,106}, {107,109,111,113},
            {113,115,117,119}, {120,122,124,126} };
        for (var ie=0; ie<4; ie++) for (var a=0; a<4; a++)
        foreach (var cp in new[]{1,5}) foreach (var crit in new[]{false,true})
        foreach (var armor in new[]{false,true}) yield return [ie,a,cp,crit,armor,literal[ie,a]];
    }
    [Theory][MemberData(nameof(RankCases))]
    public void Numerical_AllRankCompositionsThroughExecutor(int improved, int aggression,
        int cp, bool crit, bool armor, decimal perPoint)
    {
        var r=Request(aggression,improved); r.Tuning.PhysicalCritPercent=crit?100:0;
        r.TargetArmor=armor?r.Tuning.ArmorDamageReductionConstant:0;
        var h=new Harness(r);
        for(var i=0;i<cp;i++) h.Hit();
        h.Hit(target:"target-2"); h.Hit(source:"foreign");
        var damage=Assert.Single(h.Cast("eviscerate"));
        var expected=perPoint*cp*(crit?2:1);
        Assert.Equal(expected,damage.RawAmount); Assert.Equal(expected*(armor?.5m:1m),damage.Amount);
        Assert.Equal(crit,damage.IsCritical); Assert.Equal("rogue",damage.SourceActorKey);
        Assert.Equal("target",damage.TargetActorKey); Assert.Equal("eviscerate-hit",damage.EffectKey);
        Assert.Equal(0,h.Combat.ComboPointsFor("target")); Assert.Equal(1,h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(1,h.Combat.FinishersUsed); Assert.Equal(70m,h.Actor.Resources["energy"].Current);
        Assert.Single(h.Context.Timeline,e=>e.Type==CombatEventType.ResourceChanged && e.Amount==-35);
        Assert.Equal(1m,h.Actor.Abilities["eviscerate"].Definition.GlobalCooldownSeconds);
    }
    [Theory][InlineData("0","0")][InlineData("37.5","236.25")][InlineData("100","630")]
    public void Numerical_ExplicitTuningAndCpCap(string tuning,string amount)
    {
        var r=Request(); r.Tuning.EviscerateDamagePerPoint=decimal.Parse(tuning,System.Globalization.CultureInfo.InvariantCulture);
        var h=new Harness(r);for(var i=0;i<8;i++)h.Hit();Assert.Equal(5,h.Combat.ComboPointsFor("target"));
        Assert.Equal(decimal.Parse(amount,System.Globalization.CultureInfo.InvariantCulture),Assert.Single(h.Cast("eviscerate")).Amount);
        Assert.Equal(0,h.Combat.ComboPointsFor("target"));Assert.Equal(1,h.Combat.FinishersUsed);
    }
    [Theory][InlineData("miss")][InlineData("dodge")][InlineData("parry")]
    public void Control_AvoidancePreservesPointsCostsAndRng(string result)
    {
        double? next=null;
        foreach(var rank in new[]{0,3})
        {
            var r=Request(rank);r.IsBehindTarget=false;
            if(result=="miss")r.Tuning.MainHandHitPercent=0;
            if(result=="dodge")r.TargetDodgePercent=100;
            if(result=="parry")r.TargetParryPercent=100;
            var h=new Harness(r);for(var i=0;i<5;i++)h.Hit();
            var hit=Assert.Single(h.Cast("eviscerate"));Assert.Equal(result,hit.ResultKey);
            Assert.Equal(0m,hit.Amount);Assert.Equal(5,h.Combat.ComboPointsFor("target"));
            Assert.Equal(0,h.Combat.FinishersUsed);Assert.Equal(70m,h.Actor.Resources["energy"].Current);
            var draw=h.Context.Random.NextDouble();if(next.HasValue)Assert.Equal(next,draw);else next=draw;
        }
    }
    [Theory][InlineData(0,100,150)][InlineData(1,102,153)][InlineData(2,104,156)][InlineData(3,106,159)]
    public void Control_ExistingBuilderScalingAndOtherDefinitions(int rank,int sinister,int backstab)
    {
        var r=Request(rank);var h=new Harness(r);
        Assert.Equal(sinister,Assert.Single(h.Cast("sinister-strike")).Amount);
        h=new Harness(r);Assert.Equal(backstab,Assert.Single(h.Cast("backstab")).Amount);
        var ranked=ForeverRogueSpellbook.Create(r.Specialization,r.Talents,r.Tuning,0,true);
        r.Talents.Ranks.Remove("aggression");
        var absent=ForeverRogueSpellbook.Create(r.Specialization,r.Talents,r.Tuning,0,true);
        static bool Other(AbilityDefinition a)=>a.Key is not("eviscerate" or "sinister-strike" or "backstab");
        Assert.Equal(JsonSerializer.Serialize(absent.Where(Other)),JsonSerializer.Serialize(ranked.Where(Other)));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Numerical_CleaveUsesCorrectedLandedAmountOnceWithExistingMitigation(bool armor)
    {
        var r=Request();r.TargetArmor=armor?r.Tuning.ArmorDamageReductionConstant:0;
        var h=new Harness(r);for(var i=0;i<5;i++)h.Hit();
        h.Auras.ApplyAura(h.Context,h.Actor,new AuraDefinition{Key="blade-flurry",DurationSeconds=10},"rogue",null,null);
        var hit=Assert.Single(h.Cast("eviscerate"));Assert.Equal(armor?315m:630m,hit.Amount);
        var cleave=Assert.Single(h.Context.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="blade-flurry-cleave");
        Assert.Equal(hit.Amount,cleave.RawAmount);Assert.Equal(armor?157.5m:630m,cleave.Amount);
        Assert.Equal("target-2",cleave.TargetActorKey);Assert.Equal("rogue",cleave.SourceActorKey);
        Assert.NotEqual(hit.AbilityExecutionId,cleave.AbilityExecutionId);Assert.False(cleave.IsCritical);
        Assert.Equal(1,h.Combat.FinishersUsed);
    }
    [Theory][InlineData("instant")][InlineData("deadly")]
    public void Control_ProcsAndDelayedDamageKeepTheirOwnAmountsTargetsAndRng(string poison)
    {
        string? previous=null;double? next=null;
        foreach(var rank in new[]{0,3})
        {
            var r=Request(rank);r.MainHandWeaponType="sword";r.Talents.Ranks["hack-and-slash"]=5;
            r.Tuning.HackAndSlashExtraAttackPercentPerRank=20;
            r.MainHandPoison=poison;r.Tuning.InstantPoisonChancePercent=100;
            var h=new Harness(r,duration:7);for(var i=0;i<5;i++)h.Hit();h.Cast("eviscerate");
            Assert.Equal(1,h.Combat.HackAndSlashAttacks);
            var extra=Assert.Single(h.Context.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="hack-and-slash-extra");
            Assert.Equal(100m,extra.RawAmount);Assert.Equal("target",extra.TargetActorKey);
            var hits=h.Context.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey==poison+"-poison").ToArray();
            Assert.NotEmpty(hits);Assert.All(hits,e=>{Assert.Equal("target",e.TargetActorKey);Assert.Equal("rogue",e.SourceActorKey);});
            if(poison=="deadly")Assert.All(hits,e=>Assert.True(e.IsPeriodic));
            var stable=JsonSerializer.Serialize(new {h.Combat.HackAndSlashAttacks,h.Poisons.Applications,h.Poisons.DeadlyTicks,
                Charges=h.Poisons.ChargesRemaining(WeaponHandKeys.MainHand),Hits=hits.Select(e=>new{e.TimeSeconds,e.Amount,e.RawAmount,e.IsPeriodic,e.TargetActorKey,e.SourceActorKey,e.IsCritical})});
            if(previous is not null)Assert.Equal(previous,stable);previous=stable;
            var draw=h.Context.Random.NextDouble();if(next.HasValue)Assert.Equal(next,draw);next=draw;
        }
    }
    [Fact]
    public void Control_ZeroDamageSpendsPointsWithoutSecondaryEffects()
    {
        var r=Request();r.Tuning.EviscerateDamagePerPoint=0;r.MainHandWeaponType="sword";
        r.Talents.Ranks["hack-and-slash"]=5;r.Tuning.HackAndSlashExtraAttackPercentPerRank=20;
        r.MainHandPoison="deadly";r.Tuning.InstantPoisonChancePercent=100;
        var h=new Harness(r,duration:7);for(var i=0;i<5;i++)h.Hit();
        h.Auras.ApplyAura(h.Context,h.Actor,new AuraDefinition{Key="blade-flurry",DurationSeconds=10},"rogue",null,null);
        Assert.Equal(0m,Assert.Single(h.Cast("eviscerate")).Amount);
        Assert.Equal(1,h.Combat.FinishersUsed);Assert.Equal(0,h.Combat.ComboPointsFor("target"));
        Assert.Equal(0,h.Combat.HackAndSlashAttacks);Assert.Equal(0,h.Poisons.Applications);Assert.Equal(0,h.Poisons.DeadlyTicks);
        Assert.DoesNotContain(h.Context.Timeline,e=>e.AbilityKey=="blade-flurry-cleave");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Numerical_LiveConditionalModifiersRemainSeparate(bool owned)
    {
        var r=Request();r.Talents.Ranks["master-of-subtlety"]=3;r.Talents.Ranks["improved-kidney-shot"]=3;
        r.Tuning.MasterOfSubtletyDamagePercentPerRank=10;r.Tuning.ImprovedKidneyShotDamagePerRank=.05m;
        var h=new Harness(r);for(var i=0;i<5;i++)h.Hit();
        h.Auras.ApplyAura(h.Context,h.Actor,new AuraDefinition{Key="master-of-subtlety-window",DurationSeconds=10},"rogue",null,null);
        h.Auras.ApplyAura(h.Context,h.Context.GetActor("target")!,new AuraDefinition{Key="kidney-shot-stun",DurationSeconds=10},owned?"rogue":"foreign",null,null);
        Assert.Equal(owned?913.5m:819m,Assert.Single(h.Cast("eviscerate")).Amount);
    }
    [Theory][InlineData(false,"miss")][InlineData(true,"miss")]
    [InlineData(false,"dodge")][InlineData(true,"dodge")][InlineData(false,"parry")][InlineData(true,"parry")]
    public void Control_QuickRecoveryUsesActualDiscountedCostOnlyOnMiss(bool discount,string result)
    {
        foreach(var rank in new[]{0,3})
        {
            var r=Request(rank);r.Talents.Ranks["quick-recovery"]=2;
            r.Tuning.QuickRecoveryEnergyRefundPercentPerRank=20;
            if(discount)r.Talents.Ranks["flawless-execution"]=1;
            r.IsBehindTarget=false;
            if(result=="miss")r.Tuning.MainHandHitPercent=0;
            if(result=="dodge")r.TargetDodgePercent=100;
            if(result=="parry")r.TargetParryPercent=100;
            var h=new Harness(r);for(var i=0;i<5;i++)h.Hit();
            h.Combat.Process(h.Context,new(){Type=CombatEventType.RotationDecision,SourceActorKey="rogue"});
            var damage=Assert.Single(h.Cast("eviscerate"));Assert.Equal(result,damage.ResultKey);
            var cost=discount?25m:35m;var refund=result=="miss"?cost*.4m:0;
            Assert.Equal(refund,h.Combat.QuickRecoveryEnergyRestored);
            Assert.Equal(105m-cost+refund,h.Actor.Resources["energy"].Current);
            Assert.Equal(5,h.Combat.ComboPointsFor("target"));Assert.Equal(0,h.Combat.FinishersUsed);
        }
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void Control_RuthlessnessPreservesExactDrawOrderAndTargetOwnedRefund(int seed)
    {
        foreach(var rank in new[]{0,3})
        {
            var r=Request(rank);r.Talents.Ranks["ruthlessness"]=3;
            var h=new Harness(r,seed);for(var i=0;i<5;i++)h.Hit();h.Hit(target:"target-2");
            h.Cast("eviscerate");var expected=new Random(seed);
            expected.NextDouble();expected.NextDouble();expected.NextDouble(); // hit, avoidance, ordinary critical
            var refund=(decimal)expected.NextDouble()*100m<60m?1:0;
            Assert.Equal(refund,h.Combat.ComboPointsFor("target"));Assert.Equal(1,h.Combat.ComboPointsFor("target-2"));
            Assert.Equal(expected.NextDouble(),h.Context.Random.NextDouble());Assert.Equal(1,h.Combat.FinishersUsed);
        }
    }
    // Complete response normalization only renames the five generated timeline identity fields.
    // All scalar fields, array ordering and joins are retained; fixed request identity stays exact.
    private static string Stable(RogueSimResponse response)
    {
        var root=JsonSerializer.SerializeToNode(response)!;var identities=new Dictionary<string,string>();
        foreach(var ev in root["Run"]!["Timeline"]!.AsArray())
        foreach(var field in new[]{"AbilityExecutionId","AutoAttackInstanceId","AuraInstanceId","AbsorbInstanceId","ForcedTargetInstanceId"})
        {
            var value=ev![field]?.GetValue<string>();
            if(value is not null && Guid.TryParse(value,out var id) && id!=Guid.Empty)
            {if(!identities.ContainsKey(value))identities[value]="generated-"+identities.Count;ev[field]=identities[value];}
        }
        return root.ToJsonString();
    }
    private static RogueSimRequest Integration(string spec,int? rank=3)
    {var r=Request(rank,spec:spec);r.Tuning.EnergyPerSecond=60;return r;}
    [Theory][InlineData("combat")][InlineData("assassination")][InlineData("subtlety")]
    public void Integration_ActualEvisceratesUseLiteralPerPointAmounts(string spec)
    {
        var r=Integration(spec);var original=JsonSerializer.Serialize(r);var runner=new RogueSimRunner();
        var corrected=runner.Run(r);Assert.Equal(original,JsonSerializer.Serialize(r));
        Assert.Contains(corrected.Run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="eviscerate" && e.Amount>0);
        Assert.Equal(Stable(corrected),Stable(runner.Run(r)));
        Assert.All(corrected.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="eviscerate"),
            e=>Assert.Contains(e.RawAmount!.Value,new[]{126m,252m,378m,504m,630m}));
        Assert.Equal(original,JsonSerializer.Serialize(r));
    }
    [Theory][InlineData("combat")][InlineData("assassination")][InlineData("subtlety")]
    public void Control_AbsentZeroRemovalAndRepeatedPreparation(string spec)
    {
        var r=Integration(spec,null);var runner=new RogueSimRunner();var missing=Stable(runner.Run(r));
        r.Talents.Ranks["aggression"]=0;Assert.Equal(missing,Stable(runner.Run(r)));
        r.Talents.Ranks["aggression"]=3;runner.Run(r);r.Talents.Ranks.Remove("aggression");
        Assert.Equal(missing,Stable(runner.Run(r)));
    }
    [Fact]
    public void Capture_CompleteRunnerEnvelopes()
    {
        var directory=Environment.GetEnvironmentVariable("CP175_AGGRESSION_CAPTURE");if(directory is null)return;
        Directory.CreateDirectory(directory);
        foreach(var spec in new[]{"combat","assassination","subtlety"})
        foreach(var rank in new int?[]{null,0,1,2,3})
        {
            var r=Integration(spec,rank);var runner=new RogueSimRunner();var response=runner.Run(r);
            File.WriteAllText(Path.Combine(directory,$"{spec}-{rank?.ToString()??"absent"}.json"),JsonSerializer.Serialize(new{request=r,response,preview=RogueSimRunner.Preview(r)}));
        }
        foreach(var spec in new[]{"combat","assassination","subtlety"})
        {
            var r=Integration(spec);r.Tuning.EviscerateDamagePerPoint=105;
            File.WriteAllText(Path.Combine(directory,$"oracle105-{spec}.json"),JsonSerializer.Serialize(new{request=r,response=new RogueSimRunner().Run(r),preview=RogueSimRunner.Preview(r)}));
        }
        foreach(var spec in new[]{"combat","assassination","subtlety"})
        {
            var r=RogueSimRequest.CreatePreset(spec);r.DurationSeconds=120;r.CaptureTimeline=true;
            File.WriteAllText(Path.Combine(directory,$"default-{spec}.json"),JsonSerializer.Serialize(new{request=r,response=new RogueSimRunner().Run(r),preview=RogueSimRunner.Preview(r)}));
        }
    }
}
