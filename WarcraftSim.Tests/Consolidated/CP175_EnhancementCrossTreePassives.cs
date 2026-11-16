using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

// Historical shaman.json @ 1cc5ae1e9abdbf941b7326d2d90421822dce31ba.
// Independent literal rank oracles; no current-server or full-model claim.
public sealed class Cp175EnhancementCrossTreePassives
{
    public static IEnumerable<object[]> RankCases()
    {
        decimal[] bolt = [540m,545.4m,550.8m,556.2m,561.6m,567m];
        decimal[] shock = [410m,414.1m,418.2m,422.3m,426.4m,430.5m];
        decimal[] boltMana = [320m,313.6m,307.2m,300.8m,294.4m,288m];
        decimal[] shockMana = [430m,421.4m,412.8m,404.2m,395.6m,387m];
        decimal[] cooldown = [6m,5.8m,5.6m,5.4m,5.2m,5m];
        for(int rank=0;rank<=5;rank++) yield return [rank,bolt[rank],shock[rank],boltMana[rank],shockMana[rank],cooldown[rank]];
    }
    [Theory, MemberData(nameof(RankCases))]
    public void ExactRanksKeepOtherDefinitionsUnchanged(int rank, decimal bolt, decimal shock, decimal boltMana, decimal shockMana, decimal cooldown)
    {
        var talents = new ForeverEnhancementShamanTalents { Ranks = new() { ["maelstrom-weapon"]=5,
            ["convection"]=rank,["concussion"]=rank,["reverberation"]=rank } };
        var spells=ForeverEnhancementShamanSpellbook.Create(new(),talents,0m,2.8m,100m,100m);
        var baseline=ForeverEnhancementShamanSpellbook.Create(new(),new(){Ranks=new(){["maelstrom-weapon"]=5}},0m,2.8m,100m,100m);
        foreach(var key in new[]{"lightning-bolt","maelstrom-lightning-bolt"})
            Assert.Equal(bolt,spells.Single(s=>s.Key==key).Effects.Single().MinimumValue);
        Assert.Equal(shock,spells.Single(s=>s.Key=="earth-shock").Effects.Single().MinimumValue);
        Assert.Equal(boltMana,spells.Single(s=>s.Key=="lightning-bolt").ResourceCosts.Single().Amount);
        Assert.Equal(shockMana,spells.Single(s=>s.Key=="earth-shock").ResourceCosts.Single().Amount);
        Assert.Equal(cooldown,spells.Single(s=>s.Key=="earth-shock").CooldownSeconds);
        Assert.All(spells.Where(s=>s.Key is not ("lightning-bolt" or "maelstrom-lightning-bolt" or "earth-shock")),
            spell=>Assert.Equal(JsonSerializer.Serialize(baseline.Single(s=>s.Key==spell.Key)),JsonSerializer.Serialize(spell)));
    }
    [Theory, MemberData(nameof(RankCases))]
    public void RealShockSpendsRankCostAndOnlyReopensAtExactBoundary(int rank, decimal bolt, decimal shock, decimal boltMana, decimal shockMana, decimal cooldown)
    {
        var h=new Harness(0,8m,convection:rank,concussion:rank,reverberation:rank);
        Assert.Equal(bolt,h.Actor.Abilities["lightning-bolt"].Definition.Effects.Single().MinimumValue);
        Assert.Equal(boltMana,h.BoltCost);
        h.At(0m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","earth-shock").Success));
        h.At(cooldown-.001m,()=>Assert.False(h.Executor.TryStartAbility(h.Context,"shaman","target","earth-shock").Success));
        h.At(cooldown,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","earth-shock").Success));
        var run=h.Run();
        var hits=run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="earth-shock").ToArray();
        Assert.Equal(2,hits.Length); Assert.Equal(new[]{0m,cooldown},hits.Select(e=>e.TimeSeconds));
        Assert.All(hits,e=>Assert.Equal(shock,e.Amount));
        Assert.Equal(1000m-2m*shockMana,h.Actor.Resources["mana"].Current);
        Assert.All(run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged),e=>Assert.Equal(-shockMana,e.Amount));
    }
    [Theory]
    [InlineData(5,1,230.4,2)] [InlineData(5,4,57.6,.5)] [InlineData(5,5,0,0)]
    [InlineData(1,5,230.4,2)]
    public void PassiveCostIsCapturedOnceUnderPartialAndFullMaelstrom(int rank,int stacks,double cost,double seconds)
    {
        var h=new Harness(rank,5m); h.Hits(0m,stacks);
        h.At(.5m,()=>Assert.Equal((decimal)cost,h.BoltCost));
        h.At(1m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","lightning-bolt").Success));
        var run=h.Run();
        Assert.Equal(1000m-(decimal)cost,h.Actor.Resources["mana"].Current);
        Assert.Equal(288m,h.BoltCost);
        Assert.Equal(0,h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(1m+(decimal)seconds,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted).TimeSeconds);
        Assert.Equal(567m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="lightning-bolt").Amount);
    }
    [Fact]
    public void FocusAndConvectionComposeOnlyForShockWhileCoefficientRemainsEnhancement()
    {
        var h=new Harness(0,2m,focus:1,spellPower:100m);
        h.At(0m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","earth-shock").Success));
        var run=h.Run();
        Assert.Equal(-212.85m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.ResourceChanged).Amount);
        Assert.Equal(475.545m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage).Amount);
        Assert.Equal(203.5m,h.Actor.Abilities["lightning-shield"].Definition.ResourceCosts.Single().Amount);
        Assert.Equal(641.97m,h.Actor.Abilities["lightning-bolt"].Definition.Effects.Single().MinimumValue);
        Assert.Equal(1.5m,h.Actor.Abilities["earth-shock"].Definition.GlobalCooldownSeconds);
    }
    [Fact]
    public void ExplicitTuningAndZeroValuesRemainBaseInputs()
    {
        var talents=new ForeverEnhancementShamanTalents {Ranks=new(){["convection"]=5,["concussion"]=5,["reverberation"]=5}};
        var spells=ForeverEnhancementShamanSpellbook.Create(new(){EarthShockDamage=100m,EarthShockMana=200m,LightningBoltDamage=300m,LightningBoltMana=400m},talents,100m,2m,0m,0m);
        Assert.Equal(150.045m,spells.Single(s=>s.Key=="earth-shock").Effects.Single().MinimumValue);
        Assert.Equal(389.97m,spells.Single(s=>s.Key=="lightning-bolt").Effects.Single().MinimumValue);
        Assert.Equal(180m,spells.Single(s=>s.Key=="earth-shock").ResourceCosts.Single().Amount);
        Assert.Equal(360m,spells.Single(s=>s.Key=="lightning-bolt").ResourceCosts.Single().Amount);
        spells=ForeverEnhancementShamanSpellbook.Create(new(){EarthShockDamage=0m,EarthShockMana=0m,LightningBoltDamage=0m,LightningBoltMana=0m},talents,0m,2m,0m,0m);
        Assert.All(spells.Where(s=>s.Key is "earth-shock" or "lightning-bolt"),s=>{Assert.Equal(0m,s.Effects.Single().MinimumValue);Assert.Equal(0m,s.ResourceCosts.Single().Amount);});
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExpiryCancellationAndFreshStacksNeverCompoundPassive(bool cancel)
    {
        var h=new Harness(1,36m); h.Hits(0m,3);
        h.At(29.9m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","lightning-bolt").Success));
        h.Hits(30.1m,1);
        if(cancel) h.At(30.2m,()=>Assert.True(h.Executor.TryCancelCurrentCast(h.Context,"shaman")));
        h.At(32.2m,()=>{Assert.Equal(1,h.Reactive.CurrentMaelstromStacks);Assert.Equal(276.48m,h.BoltCost);});
        h.At(33m,()=>Assert.True(h.Executor.TryStartAbility(h.Context,"shaman","target","lightning-bolt").Success));
        var run=h.Run();
        Assert.Equal(cancel?723.52m:470.08m,h.Actor.Resources["mana"].Current);
        Assert.Equal(288m,h.BoltCost); Assert.Equal(0,h.Reactive.CurrentMaelstromStacks);
        var costs=run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged).Select(e=>e.Amount).ToArray();
        Assert.Equal(cancel?new decimal?[]{-276.48m}:new decimal?[]{-253.44m,-276.48m},costs);
    }
    [Theory]
    [InlineData("windfury","windfury-weapon-extra")]
    [InlineData("flametongue","flametongue")]
    [InlineData("frostbrand","frostbrand")]
    public void RealExcludedProcPeriodicAndShieldTimelineIsUnchanged(string imbue,string expectedProc)
    {
        var request=new EnhancementShamanSimRequest { DurationSeconds=40m,
            MainHandImbue=imbue,UseWindfuryTotem=false,UseStormstrike=false,
            UseEarthShock=false,UseLightningBoltFiller=false,UseMaelstromLightningBolt=false,
            UseRageOfTheFarseer=false,UseLightningShield=true,IncomingBossHitDamage=10m,
            UseCallOfElements=true,CallOfElementsTotems=["strength-of-earth-totem","searing-totem"],
            BaseMeleeHitPercent=100m,BaseSpellHitPercent=100m,CriticalChancePercent=0m,
            ProvisionalGlancingChancePercent=0m,FrostbrandProcChancePercent=100m,
            Talents=new(){ElementalPointsSpent=15,EnhancementPointsSpent=36,Ranks=new()}
        };
        request.Tuning.WindfuryWeaponProcChancePercent=100m;
        var before=new EnhancementShamanSimRunner().Run(request);
        foreach(var key in new[]{"convection","concussion","reverberation"}) request.Talents.Ranks[key]=5;
        var after=new EnhancementShamanSimRunner().Run(request);
        Assert.Contains(before.DamageByAbility,k=>k.Key.Contains(expectedProc) && k.Value>0m);
        Assert.True(before.DamageByAbility["call-of-elements"]>0m); // Searing ticks retain the parent call ability.
        Assert.True(before.DamageByAbility["lightning-shield"]>0m);
        Assert.Equal(before.EndingMana,after.EndingMana);
        Assert.Equal(JsonSerializer.Serialize(before.DamageByAbility),JsonSerializer.Serialize(after.DamageByAbility));
        object[] Events(EnhancementShamanSimResponse result)=>result.Run.Timeline.Select(e=>(object)new {e.TimeSeconds,e.Type,e.AbilityKey,e.Amount,e.ResultKey}).ToArray();
        Assert.Equal(JsonSerializer.Serialize(Events(before)),JsonSerializer.Serialize(Events(after)));
    }

    [Theory]
    [InlineData("dead-target")]
    [InlineData("missing-target")]
    [InlineData("missing-ability")]
    public void CrossTreeInvalidBoltCompletionPreservesPassiveAndStackOwnership(string failure)
    {
        var h = new Harness(1, 7m);
        var replacement = new SimulationActorState { Key = "replacement", TeamKey = "enemy", Level = 60 };
        replacement.InitializeHealth(100000m);
        h.Context.AddActor(replacement);
        var bolt = h.Actor.Abilities["lightning-bolt"].Definition;
        Guid? firstId = null;
        h.Hits(0m, 3);
        h.At(1m, () =>
        {
            Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success);
            firstId = h.Actor.CurrentCastExecutionId;
        });
        h.At(2m, () =>
        {
            if (failure == "dead-target") h.Context.GetActor("target")!.TakeDamage(100000m);
            else if (failure == "missing-target") h.Context.Actors.Remove("target");
            else h.Actor.Abilities.Remove("lightning-bolt");
        });
        h.At(3.25m, () =>
        {
            if (failure == "missing-ability") h.Actor.AddAbility(bolt);
        });
        h.Hits(3.3m, 1);
        h.At(3.5m, () =>
        {
            var retry = h.Executor.TryStartAbility(h.Context, "shaman", "replacement", "lightning-bolt");
            Assert.True(retry.Success, retry.FailureReason);
            Assert.True(h.Context.GetAbilityExecution(firstId!.Value)!.IsCancelled);
            Assert.Equal(4, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(1000m, h.Actor.Resources["mana"].Current);
        });
        h.Hits(6m, 1);
        var run = h.Run();
        Assert.Equal(-241.92m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(567m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "lightning-bolt").Amount);
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(276.48m, h.BoltCost); // No stale execution re-latches after the unsuccessful cast.
    }

    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public AbilityExecutor Executor { get; }
        public ForeverEnhancementShamanReactiveProcessor Reactive { get; }
        private readonly AuraManager _auras = new();
        private readonly Dictionary<string, Action> _steps = new();
        public decimal BoltCost => Actor.Abilities["lightning-bolt"].Definition.ResourceCosts.Single().Amount;
        public Harness(int rank, decimal duration, IReadOnlySet<string>? completionOrderedKeys = null,
            bool useDefaultOrdering = false, int convection = 5, int concussion = 5, int reverberation = 5, int focus = 0, decimal spellPower = 0m)
        {
            Context = new(new() { DurationSeconds = duration, CaptureTimeline = true, PrimaryActorKey = "shaman" });
            Actor = new() { Key = "shaman", TeamKey = "raid", Level = 60 };
            Actor.InitializeHealth(1000m);
            Actor.AddResource(new("mana", 1000m, 1000m));
            var target = new SimulationActorState { Key = "target", TeamKey = "enemy", Level = 60 };
            target.InitializeHealth(100000m);
            Context.AddActor(Actor);
            Context.AddActor(target);
            var talents = new ForeverEnhancementShamanTalents { Ranks = new() { ["maelstrom-weapon"] = rank, ["convection"] = convection, ["concussion"] = concussion, ["reverberation"] = reverberation, ["shamanistic-focus"] = focus } };
            var tuning = new ForeverEnhancementShamanTuning { MaelstromChancePerMeleePercentPerRank = 100m };
            foreach (var spell in ForeverEnhancementShamanSpellbook.Create(tuning, talents, spellPower, 2.8m, 100m, 100m))
                Actor.AddAbility(spell);
            ForeverEnhancementShamanReactiveProcessor? state = null;
            Executor = new(_auras, new SimpleCombatRollResolver(), new NoMitigation(),
                abilityTimingProvider: new EnhancementMaelstromTimingProvider(() => state?.CurrentMaelstromStacks ?? 0, rank),
                completionOrderedAbilityKeys: useDefaultOrdering ? null :
                    completionOrderedKeys ?? new HashSet<string> { "lightning-bolt" });
            Reactive = new("shaman", new AutoAttackProcessor(Executor), _auras, talents, tuning);
            state = Reactive;
        }
        public void Hits(decimal time, int count)
        {
            for (var i = 0; i < count; i++)
                Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.Damage,
                    SourceActorKey = "shaman", TargetActorKey = "target", AbilityKey = "main-hand-auto",
                    SchoolKey = "physical", WeaponHandKey = "main-hand", Amount = 1m });
        }
        public void At(decimal time, Action action)
        {
            var key = $"step-{_steps.Count}";
            _steps.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                EffectKey = key, IsInternal = true });
        }
        public SimulationRunResult Run() => new SimulationEngine([
            Executor, _auras, Reactive, new Steps(_steps)]).Run(Context);
    }
    private sealed class Steps(Dictionary<string, Action> actions) : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent e)
        {
            if (e.EffectKey is not null && actions.TryGetValue(e.EffectKey, out var action)) action();
        }
    }
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect, decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }
}
