using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical decoded Serrated Blades tables. Multiplicative composition
/// and inherited cleave topology are provisional simulator conventions.</summary>
public sealed class Cp175RogueSerratedBladesSourceCorrections
{
    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    private static RogueSimRequest Request(int? rank = 3, string spec = "subtlety")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { CombatPointsSpent = 21, SubtletyPointsSpent = 30,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["hemorrhage"] = 1 } };
        if (rank.HasValue) r.Talents.Ranks["serrated-blades"] = rank.Value;
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.MainHandWeaponType = r.OffHandWeaponType = "sword";
        r.BothWeaponsDaggers = false; r.IsBehindTarget = true;
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 200;
        r.Tuning.SinisterStrikeBonusDamage = 0;
        r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.EnergyPerSecond = 60;
        r.TargetArmor = r.TargetDodgePercent = r.TargetParryPercent = 0;
        r.TargetMaximumHealth = 10000000; r.DurationSeconds = 10; r.CaptureTimeline = true;
        return r;
    }
    // Admission-only bypass for pre-product numerical proof. All other Prepare
    // validation, definitions, real rolls, mitigation and scheduling are retained.
    private static IReadOnlyList<AbilityDefinition> PrepareWithoutSerratedAdmission(RogueSimRequest r)
    {
        var present = r.Talents.Ranks.Remove("serrated-blades", out var rank);
        try
        {
            var p = typeof(RogueSimRunner).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [r])!;
            return (IReadOnlyList<AbilityDefinition>)p.GetType().GetProperty("Abilities")!.GetValue(p)!;
        }
        finally { if (present) r.Talents.Ranks["serrated-blades"] = rank; }
    }
    private sealed class CountingRandom() : Random(1)
    { public int Draws { get; private set; } public override double NextDouble() { Draws++; return base.NextDouble(); } }
    private sealed class Harness : ICombatEventProcessor
    {
        public CountingRandom Random { get; } = new();
        public RogueSimRequest Request { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor => Context.GetActor("rogue")!;
        public SimulationActorState Target => Context.GetActor("target")!;
        public RogueCombatProcessor Combat { get; }
        public RoguePoisonProcessor Poisons { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public IDamageMitigationResolver Mitigation { get; }
        public AutoAttackProcessor Autos { get; }
        public AutoAttackDefinition[] Weapons { get; }
        private readonly Dictionary<string, Action> _actions = new();
        public Harness(RogueSimRequest r, decimal duration = 1)
        {
            Request = r;
            Context = new(new() { Seed = 1, DurationSeconds = duration,
                CaptureTimeline = true, PrimaryActorKey = "rogue" });
            typeof(SimulationContext).GetField("<Random>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Context, Random);
            foreach (var key in new[] { "rogue", "other-rogue", "target", "target-2" })
            {
                var friendly = key.Contains("rogue", StringComparison.Ordinal);
                var actor = new SimulationActorState { Key = key, Name = key,
                    TeamKey = friendly ? "raid" : "enemy", Level = friendly ? 60 : 63,
                    MaximumHealth = 1000000 };
                actor.InitializeHealth(1000000);
                if (friendly)
                {
                    actor.AddResource(new ResourceState("energy", 100, 100, 60));
                    foreach (var ability in PrepareWithoutSerratedAdmission(r)) actor.AddAbility(ability);
                }
                Context.AddActor(actor);
            }
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Mitigation = (IDamageMitigationResolver)Adapter("RogueArmorMitigation");
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"), Mitigation);
            Autos = new(Executor);
            Weapons = (AutoAttackDefinition[])typeof(RogueSimRunner).GetMethod("CreateWeapons", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [r.Tuning, 0m, r.Talents])!;
            Combat = combat = new(r, Actor, new RotationProfile(), Executor, Auras,
                Autos, Weapons);
            Poisons = new(r, Actor, Auras, Executor);
        }
        public void At(decimal time, Action action)
        {
            var key = "test-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                SourceActorKey = "rogue", EffectKey = key, IsInternal = true });
        }
        public void Process(SimulationContext context, CombatEvent ev)
        { if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action(); }
        public void Cast(string ability, string target = "target", string source = "rogue") =>
            Assert.True(Executor.TryStartAbility(Context, source, target, ability).Success,
                $"{source} could not cast {ability} at {Context.CurrentTimeSeconds}");
        public void Points(int count, decimal start = 0, string target = "target")
        { for (var i = 0; i < count; i++) At(start + i, () => Cast("sinister-strike", target)); }
        public void Mark(string source = "rogue", string target = "target") => Cast("hemorrhage", target, source);
        public CombatEvent[] Damage(string ability) => Context.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == ability).ToArray();
        public CombatEvent[] Ticks() => Damage("rupture").Where(e => e.IsPeriodic).ToArray();
        public AuraInstance MarkAura(string source = "rogue", string target = "target") =>
            Assert.Single(Context.GetActor(target)!.ActiveAuras, a =>
                a.Definition.Key == "hemorrhage-rupture" &&
                string.Equals(a.SourceActorKey, source, StringComparison.OrdinalIgnoreCase));
        public void Run() => new SimulationEngine([Executor, Auras, Autos, Combat, Poisons, this]).Run(Context);
    }

    public static IEnumerable<object[]> AdmissionRanks()
    { foreach (var spec in new[] { "assassination", "combat", "subtlety" }) for (var rank = 0; rank <= 3; rank++) yield return [spec, rank]; }
    [Theory][MemberData(nameof(AdmissionRanks))]
    public void Admission_LegacyRanksAndPreview(string spec, int rank)
    { var r = Request(rank, spec); r.Talents.Validate(60, spec); Assert.NotNull(RogueSimRunner.Preview(r)); }
    [Theory][InlineData(-1)][InlineData(4)]
    public void Control_InvalidRankRejected(int rank) => Assert.Throws<ArgumentException>(() => Request(rank).Talents.Validate(60, "subtlety"));
    [Fact]
    public void Control_NullMapAndOverbudgetReject()
    {
        var r = Request(); r.Talents.Ranks = null!;
        Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, "subtlety"));
        r = Request(); r.Talents.AssassinationPointsSpent = 1;
        Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, "subtlety"));
        r = Request(); r.Talents.SubtletyPointsSpent = 2;
        Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, "subtlety"));
    }
    private static AbilityEffectDefinition Hit(string? hand = WeaponHandKeys.MainHand) => new() {
        Key = "weapon-hit", EffectType = AbilityEffectTypes.DirectDamage, SchoolKey = "physical",
        TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
        CanMiss = true, CanCrit = true, WeaponHandKey = hand, MinimumValue = 100, MaximumValue = 100 };
    private static DamageMitigationResult Resolve(Harness h, string? hand = WeaponHandKeys.MainHand, decimal raw = 100,
        string ability = "sinister-strike", AbilityEffectDefinition? effect = null, string target = "target") =>
        h.Mitigation.Resolve(h.Context, h.Actor, h.Context.GetActor(target)!, new() { Key = ability }, effect ?? Hit(hand), raw);

    [Theory][InlineData(null, "100")][InlineData(0, "100")][InlineData(1, "97")][InlineData(2, "94")][InlineData(3, "91")]
    public void Numerical_ExactRankArmorAndRealSpecial(int? rank, string armor)
    {
        var r = Request(rank); r.TargetArmor = 100; r.Tuning.ArmorDamageReductionConstant = 91;
        var h = new Harness(r); var actual = Resolve(h);
        var expected = rank switch { 1 => 100m - 100m * (97m / 188m), 2 => 100m - 100m * (94m / 185m),
            3 => 50m, _ => 100m - 100m * (100m / 191m) };
        Assert.Equal(D(armor), actual.DefenseValue); Assert.Equal(expected, actual.FinalAmount);
        Assert.Equal(100m - expected, actual.MitigatedAmount);
        h.At(0, () => h.Cast("sinister-strike")); h.Run();
        var hit = Assert.Single(h.Damage("sinister-strike")); Assert.Equal(100m, hit.RawAmount); Assert.Equal(expected, hit.Amount);
    }

    public static IEnumerable<object[]> RuptureCases()
    {
        string[,] plain = {{"24","26.4","28.8","31.2"},{"120","132","144","156"}};
        string[,] marked = {{"27.6","30.36","33.12","35.88"},{"138","151.8","165.6","179.4"}};
        foreach(var points in new[]{1,5}) foreach(var rank in new[]{0,1,2,3})
        foreach(var mark in new[]{false,true}) foreach(var crit in new[]{false,true})
            yield return [points,rank,mark,crit,(mark?marked:plain)[points==1?0:1,rank]];
    }
    [Theory][MemberData(nameof(RuptureCases))]
    public void Numerical_RealRuptureCpRankMarkCritTicks(int points, int rank, bool mark, bool crit, string tick)
    {
        var r = Request(rank); r.TargetArmor = 100000; r.Tuning.PhysicalCritPercent = crit ? 100 : 0;
        var h = new Harness(r, points + 4);
        if(mark) {h.At(0,()=>h.Mark());h.Points(points-1,1);} else h.Points(points);
        h.At(points,()=>h.Cast("rupture"));h.Run();
        Assert.Equal(points,h.Combat.RuptureSnapshotPoints);Assert.Equal(0,h.Combat.ComboPointsFor("target"));
        Assert.NotEmpty(h.Ticks()); Assert.All(h.Ticks(),e=> { Assert.Equal(D(tick)*(crit?2m:1m),e.Amount);
            Assert.Equal(e.RawAmount,e.Amount);Assert.Equal(crit,e.IsCritical);Assert.Equal("rogue",e.SourceActorKey); });
        Assert.Equal(0,h.Poisons.Applications);
    }
    [Theory][InlineData("foreign","156")][InlineData("wrong-target","156")]
    [InlineData("own","179.4")][InlineData("both","179.4")][InlineData("removed","156")]
    [InlineData("future","156")][InlineData("at-expiry","156")][InlineData("just-before-expiry","179.4")]
    public void Numerical_LiveOwnedMarkOnly(string mode,string expected)
    {
        var h=new Harness(Request(),9); h.Points(5);
        if(mode is "foreign" or "both" or "wrong-target") h.At(0,()=>h.Mark("other-rogue",mode=="wrong-target"?"target-2":"target"));
        if(mode is not ("foreign" or "wrong-target")) h.At(5,()=>h.Mark());
        h.At(6,()=> { if(mode=="removed") h.Auras.RemoveAuras(h.Context,h.Target,"hemorrhage-rupture","rogue");
            if(mode=="future") h.MarkAura().AppliedAtSeconds=8.001m;
            if(mode=="at-expiry") h.MarkAura().ExpiresAtSeconds=8m;
            if(mode=="just-before-expiry") h.MarkAura().ExpiresAtSeconds=8.001m;
            h.Cast("rupture"); });
        h.Run(); Assert.Equal(D(expected),Assert.Single(h.Ticks()).Amount);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Numerical_ExpiredMarkDoesNotStaySnapshotted(bool retainExpired)
    {
        var h=new Harness(Request(),22);h.At(0,()=>h.Mark());h.Points(4,1);h.At(5,()=>h.Cast("rupture"));
        if(retainExpired) h.At(1,()=>h.MarkAura().InstanceId=Guid.NewGuid());
        h.Run();Assert.Equal(new[]{7m,9m,11m,13m,15m,17m,19m,21m},h.Ticks().Select(e=>e.TimeSeconds));
        Assert.Equal(new[]{179.4m,179.4m,179.4m,179.4m,156m,156m,156m,156m},h.Ticks().Select(e=>e.Amount!.Value));
    }
    [Fact]
    public void Numerical_TargetCpSnapshotsStaySeparate()
    {
        var h=new Harness(Request(),14);h.Points(5);h.Points(1,5,"target-2");
        h.At(6,()=>h.Cast("rupture"));h.At(7,()=>h.Cast("rupture","target-2"));h.At(9.5m,()=>h.Mark());h.Run();
        Assert.Equal(new[]{156m,179.4m,179.4m},h.Ticks().Where(e=>e.TargetActorKey=="target").Select(e=>e.Amount!.Value));
        Assert.All(h.Ticks().Where(e=>e.TargetActorKey=="target-2"),e=>Assert.Equal(31.2m,e.Amount));
    }

    public static IEnumerable<object[]> CompositionCases()
    {
        foreach(var hand in new[]{WeaponHandKeys.MainHand,WeaponHandKeys.OffHand,"fallback"})
        foreach(var mace in new[]{false,true}) foreach(var mode in new[]{"own","foreign","expired","removed","none","floor"})
            yield return [hand,mace,mode];
    }
    [Theory][MemberData(nameof(CompositionCases))]
    public void Numerical_ExposeThenHandMaceThenSerrated(string hand,bool mace,string mode)
    {
        var r=Request();r.TargetArmor=200;r.Tuning.ArmorDamageReductionConstant=81.9m;
        r.Tuning.ArmorReductionPerExposeComboPoint=mode=="floor"?100:25;
        r.Talents.Ranks["hack-and-slash"]=5;r.Tuning.HackAndSlashMaceArmorIgnorePercentPerRank=2;
        r.MainHandWeaponType=mace?"mace":"sword";r.OffHandWeaponType=mace?"sword":"mace";
        var h=new Harness(r,6);h.Points(4);h.At(4,()=>h.Cast("expose-armor"));
        h.At(5,()=> {
            var aura=Assert.Single(h.Target.ActiveAuras,a=>a.Definition.Key=="expose-armor");
            if(mode=="foreign") aura.SourceActorKey="other-rogue";
            if(mode=="expired") aura.ExpiresAtSeconds=5;
            if(mode is "removed" or "none") h.Auras.RemoveAuras(h.Context,h.Target,"expose-armor","rogue");
            var actual=Resolve(h,hand=="fallback"?null:hand);
            bool isMace=hand==WeaponHandKeys.OffHand?!mace:mace;
            decimal expected=mode=="floor"?0:mode=="own"?(isMace?81.9m:91m):(isMace?163.8m:182m);
            Assert.Equal(4,h.Combat.ExposeArmorPoints);Assert.Equal(expected,actual.DefenseValue);
            if(expected==81.9m)Assert.Equal(50m,actual.FinalAmount);
            if(expected==0)Assert.Equal(100m,actual.FinalAmount);
        });h.Run();
    }
    [Theory][InlineData("299","272.09","272.09")][InlineData("300","273","273")]
    [InlineData("301","273.91","273.91")][InlineData("0","0","0")]
    [InlineData("100000000000000000000","91000000000000000000","91000000000000000000")]
    public void Numerical_FiniteLargeArmorEffectiveValue(string input,string expected,string denominator)
    {
        var r=Request();r.TargetArmor=D(input);r.Tuning.ArmorDamageReductionConstant=D(denominator)==0?91:D(denominator);
        var h=new Harness(r);var result=Resolve(h);Assert.Equal(D(expected),result.DefenseValue);
        Assert.Equal(input=="0"?100m:50m,result.FinalAmount);
    }
    [Theory][InlineData("200","33.333333333333333333333333330")][InlineData("300","25")][InlineData("400","25")]
    public void Numerical_CapAppliesAfterIgnore(string armor,string amount)
    {
        var r=Request();r.TargetArmor=D(armor);r.Tuning.ArmorDamageReductionConstant=91;
        var h=new Harness(r);Assert.Equal(D(amount),Resolve(h).FinalAmount);
    }
    [Theory][InlineData(0)][InlineData(3)]
    public void Control_ZeroRawZeroArmorAndFullMaceIgnore(int rank)
    {
        var r=Request(rank);var h=new Harness(r);Assert.Equal(100m,Resolve(h).FinalAmount);
        r.TargetArmor=100;Assert.Equal(0m,Resolve(h,raw:0).FinalAmount);
        r.MainHandWeaponType="mace";r.Talents.Ranks["hack-and-slash"]=5;r.Tuning.HackAndSlashMaceArmorIgnorePercentPerRank=20;
        r.Tuning.Validate();Assert.Equal(100m,Resolve(h).FinalAmount);Assert.Equal(0m,Resolve(h).DefenseValue);
    }
    [Theory][InlineData("garrote")][InlineData("rupture")][InlineData("instant-poison")][InlineData("deadly-poison")][InlineData("other-periodic")]
    public void Control_ArmorExcludesBleedsPoisonsPeriodic(string ability)
    {
        var r=Request();r.TargetArmor=100000;var h=new Harness(r);var e=Hit();
        if(ability.Contains("poison",StringComparison.Ordinal))e.SchoolKey="nature";
        if(ability is "deadly-poison" or "other-periodic")e.EffectType=AbilityEffectTypes.PeriodicDamage;
        Assert.Equal(100m,Resolve(h,ability:ability,effect:e).FinalAmount);
    }
    [Theory][InlineData("garrote")][InlineData("eviscerate")][InlineData("instant-poison")][InlineData("deadly-poison")][InlineData("other-periodic")]
    public void Control_RuptureMultiplierExcludesOtherAbilities(string ability)
    {
        var h=new Harness(Request());Assert.Equal(1m,h.Combat.DamageMultiplier(ability,CombatEffectDeliveryType.Periodic,h.Context,h.Target));
    }
    [Theory][InlineData("miss")][InlineData("dodge")][InlineData("parry")]
    public void Control_ActualAvoidanceKeepsCostsAndNoPoints(string outcome)
    {
        var r=Request();r.IsBehindTarget=false;r.TargetArmor=100;
        r.Tuning.MainHandHitPercent=outcome=="miss"?0:100;r.TargetDodgePercent=outcome=="dodge"?100:0;r.TargetParryPercent=outcome=="parry"?100:0;
        var h=new Harness(r);h.At(0,()=>h.Cast("sinister-strike"));h.Run();var hit=Assert.Single(h.Damage("sinister-strike"));
        Assert.Equal(outcome,hit.ResultKey);Assert.Equal(0m,hit.Amount);Assert.Equal(0,h.Combat.ComboPointsFor("target"));
        Assert.Contains(h.Context.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&e.Amount==-45);
    }
    [Theory][InlineData(0,"100","50","25")][InlineData(3,"91","50","25")]
    public void Numerical_ActualCleavePreservesTwoArmorResolutions(int rank,string denominator,string primary,string secondary)
    {
        var r=Request(rank);r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=D(denominator);
        r.Talents.Ranks["blade-flurry"]=1;var h=new Harness(r,2);h.At(0,()=>h.Cast("blade-flurry"));h.At(1,()=>h.Cast("sinister-strike"));h.Run();
        var main=Assert.Single(h.Damage("sinister-strike"));var cleave=Assert.Single(h.Damage("blade-flurry-cleave"));
        Assert.Equal(100m,main.RawAmount);Assert.Equal(D(primary),main.Amount);Assert.Equal(main.Amount,cleave.RawAmount);
        Assert.Equal(D(secondary),cleave.Amount);Assert.Equal("rogue",cleave.SourceActorKey);Assert.Equal("target-2",cleave.TargetActorKey);
        Assert.Null(cleave.WeaponHandKey);Assert.False(cleave.IsCritical);Assert.Equal(75m,main.Amount+cleave.Amount);
    }
    [Theory][InlineData("main-hand")][InlineData("off-hand")]
    public void Numerical_ActualAutoAndHackExtraResolveArmor(string hand)
    {
        var r=Request();r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=91;
        r.Talents.Ranks["hack-and-slash"]=5;r.Tuning.HackAndSlashExtraAttackPercentPerRank=20;
        var h=new Harness(r,1);h.At(0,()=>Assert.True(h.Autos.Start(h.Context,"rogue","target",h.Weapons.Single(w=>w.WeaponHandKey==hand),0)));
        h.Run();var auto=Assert.Single(h.Damage(hand==WeaponHandKeys.MainHand?"rogue-main-hand":"rogue-off-hand"));
        var extra=Assert.Single(h.Damage("hack-and-slash-extra"));Assert.Equal(100m,auto.RawAmount);Assert.Equal(50m,auto.Amount);
        Assert.Equal(100m,extra.RawAmount);Assert.Equal(50m,extra.Amount);Assert.Equal(hand,extra.WeaponHandKey);Assert.Equal(1,h.Combat.HackAndSlashAttacks);
    }

    [Fact]
    public void Numerical_CleaveHandlessFallbackUsesMainMaceAfterOffHandPrimary()
    {
        var r=Request();r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=91;
        r.MainHandWeaponType="mace";r.OffHandWeaponType="sword";r.Talents.Ranks["hack-and-slash"]=5;
        r.Tuning.HackAndSlashMaceArmorIgnorePercentPerRank=2;r.Tuning.HackAndSlashExtraAttackPercentPerRank=0;
        r.Talents.Ranks["blade-flurry"]=1;var h=new Harness(r,2);
        h.At(0,()=>h.Cast("blade-flurry"));h.At(1,()=>h.Executor.ExecuteBackgroundDirectDamage(h.Context,h.Actor,h.Target,"rogue-off-hand","off-hand",h.Weapons[1].DamageEffect));h.Run();
        var primary=Assert.Single(h.Damage("rogue-off-hand"));var secondary=Assert.Single(h.Damage("blade-flurry-cleave"));
        Assert.Equal(50m,primary.Amount);Assert.Equal(50m,secondary.RawAmount);
        Assert.Equal(50m-50m*(81.9m/172.9m),secondary.Amount);Assert.Null(secondary.WeaponHandKey);
    }
    [Fact]
    public void Numerical_CleaveChecksSecondaryAuraSeparatelyButKeepsProcessorWideExposePoints()
    {
        var r=Request();r.TargetArmor=200;r.Tuning.ArmorDamageReductionConstant=91;
        r.Tuning.ArmorReductionPerExposeComboPoint=25;r.Talents.Ranks["blade-flurry"]=1;
        var h=new Harness(r,8);h.Points(4);h.At(4,()=>h.Cast("expose-armor"));
        h.At(5,()=>h.Cast("blade-flurry"));h.At(6,()=>h.Cast("sinister-strike"));h.Run();
        var primary=h.Damage("sinister-strike").Last();var secondary=Assert.Single(h.Damage("blade-flurry-cleave"));
        Assert.Equal(4,h.Combat.ExposeArmorPoints);Assert.Equal(50m,primary.Amount);
        Assert.Equal(50m-50m*(182m/273m),secondary.Amount);
        Assert.DoesNotContain(h.Context.GetActor("target-2")!.ActiveAuras,a=>a.Definition.Key=="expose-armor");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Numerical_DictionaryLookupComparerSemanticsRemainUnchanged(bool insensitive)
    {
        var r=Request(null);r.Talents.Ranks=new(r.Talents.Ranks,insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        r.Talents.Ranks["SERRATED-BLADES"]=3;r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=91;
        // Prepare only recognizes its explicit lowercase bypass key. Remove the
        // supplied spelling while preparing, then restore without normalizing.
        r.Talents.Ranks.Remove("SERRATED-BLADES");var h=new Harness(r);r.Talents.Ranks["SERRATED-BLADES"]=3;
        Assert.Equal(insensitive?91m:100m,Resolve(h).DefenseValue);
        Assert.Same(insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal,r.Talents.Ranks.Comparer);
    }

    private static object[] DamageTrace(IEnumerable<CombatEvent> events) => events.Where(e=>e.Type==CombatEventType.Damage)
        .Select(e=>(object)new { e.TimeSeconds,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,
            e.RawAmount,e.Amount,e.MitigatedAmount,e.IsCritical,e.IsPeriodic,e.ResultKey,e.WeaponHandKey }).ToArray();
    [Fact]
    public void Control_MissingZeroReplayAndIndependentRunsPreserveDraws()
    {
        Harness Execute(int? rank) { var r=Request(rank);r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=91;
            var h=new Harness(r,4);h.Points(4);h.Run();return h; }
        var missing=Execute(null);var zero=Execute(0);var positive=Execute(3);var repeat=Execute(3);
        Assert.Equal(JsonSerializer.Serialize(DamageTrace(missing.Context.Timeline)),JsonSerializer.Serialize(DamageTrace(zero.Context.Timeline)));
        Assert.Equal(JsonSerializer.Serialize(DamageTrace(positive.Context.Timeline)),JsonSerializer.Serialize(DamageTrace(repeat.Context.Timeline)));
        Assert.Equal(missing.Random.Draws,zero.Random.Draws);Assert.Equal(zero.Random.Draws,positive.Random.Draws);
        Assert.Equal(positive.Random.Draws,repeat.Random.Draws);
        Assert.Equal(4,positive.Combat.ComboPointsFor("target"));Assert.Equal(0,positive.Combat.ComboPointsFor("target-2"));
    }
    [Theory][InlineData("assassination")][InlineData("combat")][InlineData("subtlety")]
    public void Admission_PublicRunnerReplayDpsAndPreviewStatIsolation(string spec)
    {
        var absent=Request(null,spec);absent.TargetArmor=100;absent.Tuning.ArmorDamageReductionConstant=91;
        absent.EnableBossAttacks=true;absent.BossHitDamage=10;absent.DurationSeconds=10;
        var zero=RogueSimulationRequestSnapshot.Create(absent);zero.Talents.Ranks["serrated-blades"]=0;
        var positive=RogueSimulationRequestSnapshot.Create(absent);positive.Talents.Ranks["serrated-blades"]=3;
        var runner=new RogueSimRunner();var a=runner.Run(absent);var z=runner.Run(zero);var p=runner.Run(positive);var repeat=runner.Run(positive);
        Assert.Equal(JsonSerializer.Serialize(DamageTrace(a.Run.Timeline)),JsonSerializer.Serialize(DamageTrace(z.Run.Timeline)));
        Assert.Equal(JsonSerializer.Serialize(DamageTrace(p.Run.Timeline)),JsonSerializer.Serialize(DamageTrace(repeat.Run.Timeline)));
        Assert.True(p.DamageDone>a.DamageDone);Assert.True(p.Dps>a.Dps);Assert.Equal(a.EndingHealth,p.EndingHealth);
        Assert.Equal(JsonSerializer.Serialize(RogueSimRunner.Preview(absent).EffectiveStats),JsonSerializer.Serialize(RogueSimRunner.Preview(positive).EffectiveStats));
        Assert.Equal(p.DamageDone,p.DamageByAbility.Values.Sum());Assert.Equal(p.DamageDone,p.Dps*10m);
    }

    [Fact]
    public void Numerical_LargeRepresentableRawAndArmorRemainFinite()
    {
        var r=Request();r.TargetArmor=100000000000000000000m;r.Tuning.ArmorDamageReductionConstant=91000000000000000000m;
        var h=new Harness(r);var actual=Resolve(h,raw:100000000000000000000m);
        Assert.Equal(50000000000000000000m,actual.FinalAmount);Assert.Equal(50000000000000000000m,actual.MitigatedAmount);
        // This bounded finite witness is not an arbitrary Decimal.MaxValue safety claim.
    }

    [Theory][InlineData("special")][InlineData("main-hand")][InlineData("off-hand")]
    public void Numerical_ActualCritArmorAfterRoll(string path)
    {
        var r=Request();r.TargetArmor=100;r.Tuning.ArmorDamageReductionConstant=91;r.Tuning.PhysicalCritPercent=100;
        var h=new Harness(r,1);
        h.At(0,()=> { if(path=="special")h.Cast("sinister-strike");
            else Assert.True(h.Autos.Start(h.Context,"rogue","target",h.Weapons.Single(w=>w.WeaponHandKey==path),0)); });
        h.Run();var hit=Assert.Single(h.Context.Timeline,e=>e.Type==CombatEventType.Damage);
        Assert.True(hit.IsCritical);Assert.Equal(200m,hit.RawAmount);Assert.Equal(100m,hit.Amount);Assert.Equal(100m,hit.MitigatedAmount);
    }
}
