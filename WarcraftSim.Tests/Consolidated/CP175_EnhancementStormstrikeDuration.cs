using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

// Historical-provisional oracle: gunba/wow-forever-sim @ 1cc5ae1e9abdbf941b7326d2d90421822dce31ba,
// ui/core/talents/trees/shaman.json, Stormstrike [20, 12]. No charges are specified.
// Damage constants below are independent arithmetic, not the production aura or stat calculation.
public sealed class Cp175EnhancementStormstrikeDuration
{
    [Fact]
    public void EveryNatureHitReceivesTwentyPercentWithoutConsumingTheTwelveSecondAura()
    {
        var f = new Fixture(14m);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(1m, () => f.Hit("earth-shock"));
        f.At(2m, () => f.Hit("lightning-bolt"));
        f.At(3m, () => f.Hit("maelstrom-lightning-bolt"));
        f.At(4m, () => f.Hit("earth-shock"));
        foreach (var school in new[] { "physical", "fire", "frost" })
            f.At(5m, () => f.Probe(school));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 492m, 648m, 648m, 492m },
            Damage(run).Where(e => e.SchoolKey == "nature").Select(e => e.Amount));
        Assert.All(Damage(run).Where(e => e.SchoolKey != "nature"), e => Assert.Equal(100m, e.Amount));
        Assert.Equal(2680m, run.Summary.ActorSummaries["shaman"].DamageDone);
        Assert.Single(run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.EffectKey == "stormstrike-nature");
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.EffectKey == "stormstrike-nature" && e.TimeSeconds == 12m);
        Assert.Equal(0m, f.Owner.Stats.Get("shaman-stormstrike-nature-percent"));
    }

    [Fact]
    public void ExpiryIsHalfOpenEvenWhenDamageWasQueuedBeforeTheExpirationEvent()
    {
        var f = new Fixture(13m);
        f.At(0m, () => f.Hit("stormstrike"));
        foreach (var at in new[] { 11.999m, 12m, 12.001m })
            f.At(at, () => f.Hit("earth-shock"));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 492m, 410m, 410m },
            Damage(run, "earth-shock").Select(e => e.Amount));
        Assert.DoesNotContain(f.Target.ActiveAuras, a => a.Definition.Key == "shaman-stormstrike-nature");
    }

    [Theory]
    [InlineData("9.499", 648)]
    [InlineData("9.500", 540)]
    [InlineData("9.501", 540)]
    public void LightningBoltChecksTheAuraAtImpactRatherThanCastStart(string start, int expected)
    {
        var f = new Fixture(14m);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(decimal.Parse(start, System.Globalization.CultureInfo.InvariantCulture), () =>
            Assert.True(f.Executor.TryStartAbility(f.Context, "shaman", "target", "lightning-bolt").Success));
        var run = f.Run();
        var bolt = Assert.Single(Damage(run, "lightning-bolt"));
        Assert.Equal((decimal)expected, bolt.Amount);
        Assert.Equal(decimal.Parse(start, System.Globalization.CultureInfo.InvariantCulture) + 2.5m,
            bolt.TimeSeconds);
    }

    [Fact]
    public void RefreshExtendsOnlyTheCurrentAuraAndIgnoresItsOldExpiration()
    {
        var f = new Fixture(21m);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(8m, () => f.Hit("stormstrike"));
        foreach (var at in new[] { 11.999m, 12m, 19.999m, 20m, 20.001m })
            f.At(at, () => f.Hit("earth-shock"));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 492m, 492m, 492m, 410m, 410m },
            Damage(run, "earth-shock").Select(e => e.Amount));
        Assert.Equal(2, run.Timeline.Count(e => e.Type == CombatEventType.AuraApplied && e.EffectKey == "stormstrike-nature"));
        var removed = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AuraRemoved && e.EffectKey == "stormstrike-nature");
        Assert.Equal(20m, removed.TimeSeconds);
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("dodge")]
    [InlineData("parry")]
    [InlineData("zero-damage")]
    public void AvoidedOrZeroDamageStormstrikeCannotApplyTheBonus(string outcome)
    {
        var f = new Fixture(2m);
        f.Rolls.StrikeOutcome = _ => outcome;
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(1m, () => f.Hit("earth-shock"));
        var run = f.Run();
        Assert.Equal(0m, Assert.Single(Damage(run, "stormstrike")).Amount);
        Assert.Equal(410m, Assert.Single(Damage(run, "earth-shock")).Amount);
        Assert.DoesNotContain(run.Timeline, e => e.EffectKey == "stormstrike-nature");
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("dodge")]
    [InlineData("parry")]
    [InlineData("zero-damage")]
    public void FailedRefreshDoesNotExtendThePreviousSuccessfulStrike(string outcome)
    {
        var f = new Fixture(14m);
        f.Rolls.StrikeOutcome = at => at == 8m ? outcome : "hit";
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(8m, () => f.Hit("stormstrike"));
        f.At(11.999m, () => f.Hit("earth-shock"));
        f.At(12m, () => f.Hit("earth-shock"));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 492m, 410m }, Damage(run, "earth-shock").Select(e => e.Amount));
        Assert.Single(run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.EffectKey == "stormstrike-nature");
    }

    [Fact]
    public void OverlappingSourcesAndTargetsKeepIndependentDurationsWithoutStacking()
    {
        var f = new Fixture(17m);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(1m, () => f.Hit("earth-shock", source: "other-shaman"));
        f.At(2m, () => f.Hit("stormstrike", source: "other-shaman"));
        f.At(3m, () => f.Hit("earth-shock", target: "other-target"));
        f.At(4m, () => f.Hit("stormstrike", target: "other-target"));
        f.At(5m, () => f.Hit("earth-shock"));
        f.At(5m, () => f.Hit("earth-shock", source: "other-shaman"));
        f.At(5m, () => f.Hit("earth-shock", target: "other-target"));
        f.At(5m, () => f.Hit("earth-shock", source: "other-shaman", target: "other-target"));
        f.At(12m, () => f.Hit("earth-shock"));
        f.At(12m, () => f.Hit("earth-shock", source: "other-shaman"));
        f.At(14m, () => f.Hit("earth-shock", source: "other-shaman"));
        f.At(14m, () => f.Hit("earth-shock", target: "other-target"));
        f.At(16m, () => f.Hit("earth-shock", target: "other-target"));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 410m, 410m, 492m, 492m, 492m, 410m,
            410m, 492m, 410m, 492m, 410m }, Damage(run, "earth-shock").Select(e => e.Amount));
        Assert.Equal(new[] { 12m, 14m, 16m }, run.Timeline.Where(e =>
            e.Type == CombatEventType.AuraRemoved && e.EffectKey == "stormstrike-nature").Select(e => e.TimeSeconds));
    }

    [Theory]
    [InlineData(0, "213.6", "178")]
    [InlineData(3, "245.64", "204.7")]
    public void LightningShieldUsesTheSameLiveTargetAuraAndPreservesItsOwnTalentMultiplier(
        int improvedShieldRank, string boosted, string normal)
    {
        var f = new Fixture(14m, shieldMultiplier: 1m + .05m * improvedShieldRank);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(.1m, f.ApplyShield);
        foreach (var at in new[] { 11.999m, 12m, 12.001m })
            f.At(at, () => f.Incoming("target"));
        var run = f.Run();
        var orbs = Damage(run, "lightning-shield").ToArray();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(new decimal?[] { decimal.Parse(boosted, culture), decimal.Parse(normal, culture),
            decimal.Parse(normal, culture) }, orbs.Select(e => e.Amount));
        Assert.All(orbs, e => Assert.False(e.IsCritical));
        Assert.Equal(3, f.Shield.Triggers);
        Assert.Equal(0, f.Shield.Charges);
    }

    [Fact]
    public void ShieldRetaliationDoesNotTransferTheBonusToAnUnmarkedAttacker()
    {
        var f = new Fixture(5m);
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(.1m, f.ApplyShield);
        f.At(1m, () => f.Incoming("target"));
        f.At(2m, () => f.Incoming("other-target"));
        f.At(3m, () => f.Incoming("target"));
        var run = f.Run();
        Assert.Equal(new decimal?[] { 213.6m, 178m, 213.6m },
            Damage(run, "lightning-shield").Select(e => e.Amount));
    }

    [Fact]
    public void ZeroRankAndNeutralOrExpiredGenericAurasDoNotChangeDamage()
    {
        var f = new Fixture(2m, stormstrikeRank: 0);
        f.At(0m, () =>
        {
            // The synthetic strike is deliberate: zero rank must remain inert even
            // if an external caller provides an otherwise eligible damage event.
            f.Hit("stormstrike");
            foreach (var definition in new[]
            {
                new AuraDefinition { Key = "default", DurationSeconds = 10m },
                new AuraDefinition { Key = "neutral", DurationSeconds = 10m,
                    SourceOwnedDamageSchoolKey = "nature" },
                new AuraDefinition { Key = "unspecified-school", DurationSeconds = 10m,
                    SourceOwnedDamageMultiplier = 2m },
                new AuraDefinition { Key = "expired", DurationSeconds = 0m,
                    SourceOwnedDamageSchoolKey = "nature", SourceOwnedDamageMultiplier = 2m }
            }) f.Auras.ApplyAura(f.Context, f.Target, definition, "shaman", null, null);
        });
        f.At(1m, () => f.Hit("earth-shock"));
        var run = f.Run();
        Assert.Equal(410m, Assert.Single(Damage(run, "earth-shock")).Amount);
        Assert.DoesNotContain(run.Timeline, e => e.EffectKey == "stormstrike-nature");
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void SchoolMultiplierPrecedesCriticalMitigationAndAbsorbForEveryDelivery(
        CombatEffectDeliveryType delivery)
    {
        var f = new Fixture(14m, mitigation: new QuarterNatureMitigation());
        f.Rolls.NatureCritical = true;
        f.At(0m, () => f.Hit("stormstrike"));
        f.At(.1m, () => f.Absorb(50m));
        f.At(1m, () => f.Probe("nature", delivery, 1.5m));
        f.At(11m, () => f.Absorb(50m));
        f.At(12m, () => f.Probe("nature", delivery, 1.5m));
        var run = f.Run();
        var hits = Damage(run, "probe-nature").ToArray();
        Assert.Equal(2, hits.Length);
        // Before expiry: 100 * 1.5 * 1.2 * 2 = 360 raw;
        // 25% mitigation removes 90, then 50 absorption leaves 220 health damage.
        Assert.Equal(360m, hits[0].RawAmount);
        Assert.Equal(90m, hits[0].MitigatedAmount);
        Assert.Equal(50m, hits[0].AbsorbedAmount);
        Assert.Equal(220m, hits[0].Amount);
        // At expiry: 100 * 1.5 * 2 = 300 raw, minus 75 mitigation and 50 absorb.
        Assert.Equal(300m, hits[1].RawAmount);
        Assert.Equal(75m, hits[1].MitigatedAmount);
        Assert.Equal(50m, hits[1].AbsorbedAmount);
        Assert.Equal(175m, hits[1].Amount);
        Assert.All(hits, e => { Assert.True(e.IsCritical); Assert.Equal(delivery, e.EffectDeliveryType); });
    }

    [Fact]
    public void FullyAbsorbedStormstrikePreservesTheExistingPositiveDamageGate()
    {
        var f = new Fixture(3m);
        f.At(0m, () => f.Absorb(100m));
        f.At(1m, () => f.Hit("stormstrike"));
        f.At(2m, () => f.Hit("earth-shock"));
        var run = f.Run();
        var strike = Assert.Single(Damage(run, "stormstrike"));
        Assert.Equal(100m, strike.AbsorbedAmount);
        Assert.Equal(0m, strike.Amount);
        Assert.Equal(410m, Assert.Single(Damage(run, "earth-shock")).Amount);
        Assert.DoesNotContain(run.Timeline, e => e.EffectKey == "stormstrike-nature");
    }

    [Fact]
    public void ActualEnhancementRotationMatchesTheIndependentPerImpactOracle()
    {
        var request = new EnhancementShamanSimRequest
        {
            DurationSeconds = 24m, CaptureTimeline = true,
            BaseMeleeHitPercent = 100m, BaseSpellHitPercent = 100m,
            CriticalChancePercent = 0m, ProvisionalGlancingChancePercent = 0m,
            TargetArmor = 0m, WeaponMinDamage = 100m, WeaponMaxDamage = 100m,
            UseWindfuryWeapon = false, UseWindfuryTotem = false,
            UseRageOfTheFarseer = false, UseMaelstromLightningBolt = false,
            UseLightningBoltFiller = true, UseLightningShield = true,
            IncomingBossHitDamage = 10m, IncomingBossSwingSeconds = 4m,
            Talents = new() { Ranks = new() { ["stormstrike"] = 1 } }
        };
        request.Character.BaseStats.Values = new() { ["intellect"] = 500m };
        var run = new EnhancementShamanSimRunner().Run(request);
        var strikes = Damage(run.Run, "stormstrike").Where(e => e.Amount > 0m).ToArray();
        Assert.True(strikes.Length >= 2);
        foreach (var key in new[] { "earth-shock", "lightning-bolt", "lightning-shield" })
            Assert.True(Damage(run.Run, key).Count() >= 2, $"Missing repeated real {key} hits.");
        foreach (var hit in Damage(run.Run).Where(e => e.SourceActorKey == "shaman" && e.SchoolKey == "nature"))
        {
            // Fixed unscaled base values, independently multiplied by 6/5.
            var baseDamage = hit.AbilityKey switch
            {
                "earth-shock" => 410m, "lightning-bolt" => 540m,
                "lightning-shield" => 178m, _ => throw new InvalidOperationException(hit.AbilityKey)
            };
            var active = strikes.Any(s => s.SourceActorKey == hit.SourceActorKey &&
                s.TargetActorKey == hit.TargetActorKey && s.TimeSeconds <= hit.TimeSeconds &&
                hit.TimeSeconds < s.TimeSeconds + 12m);
            Assert.Equal(active ? baseDamage * 6m / 5m : baseDamage, hit.Amount);
        }
        Assert.All(Damage(run.Run).Where(e => e.SourceActorKey == "shaman" && e.SchoolKey == "physical"),
            e => Assert.Equal(100m, e.Amount));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CrossTreeConcussionComposesOnlyWithOwnLiveStormstrikeAtImpact(bool critical)
    {
        var f=new Fixture(14m,concussion:5,spellPower:100m);
        f.Rolls.NatureCritical=critical;
        f.At(0m,()=>f.Hit("stormstrike",source:"other-shaman"));
        f.At(1m,()=>{f.Hit("earth-shock");f.Hit("lightning-bolt");});
        f.At(2m,()=>f.Hit("stormstrike"));
        f.At(3m,()=>{f.Hit("earth-shock");f.Hit("lightning-bolt");f.Hit("maelstrom-lightning-bolt");});
        f.At(4m,()=>f.Auras.RemoveAuras(f.Context,f.Target,"shaman-stormstrike-nature","shaman"));
        f.At(5m,()=>f.Hit("earth-shock"));
        var run=f.Run(); var crit=critical?2m:1m;
        Assert.Equal(new decimal?[]{475.545m*crit,570.654m*crit,475.545m*crit},Damage(run,"earth-shock").Select(e=>e.Amount));
        Assert.Equal(new decimal?[]{641.97m*crit,770.364m*crit},Damage(run,"lightning-bolt").Select(e=>e.Amount));
        Assert.Equal(770.364m*crit,Assert.Single(Damage(run,"maelstrom-lightning-bolt")).Amount);
    }

    [Fact]
    public void CrossTreeConcussionDoesNotKeepExpiredStormstrikeOrBuffShieldAndProbes()
    {
        var f=new Fixture(14m,concussion:5);
        f.At(0m,()=>{f.Hit("stormstrike");f.ApplyShield();});
        f.At(1m,()=>{f.Incoming("target");f.Probe("nature",CombatEffectDeliveryType.Periodic);f.Probe("fire");f.Probe("physical");});
        f.At(11.999m,()=>f.Hit("earth-shock"));
        f.At(12m,()=>f.Hit("earth-shock"));
        var run=f.Run();
        Assert.Equal(new decimal?[]{516.6m,430.5m},Damage(run,"earth-shock").Select(e=>e.Amount));
        Assert.Equal(213.6m,Assert.Single(Damage(run,"lightning-shield")).Amount);
        Assert.Equal(120m,Assert.Single(Damage(run,"probe-nature")).Amount); // Existing Stormstrike only, no Concussion.
        Assert.Equal(100m,Assert.Single(Damage(run,"probe-fire")).Amount);
        Assert.Equal(100m,Assert.Single(Damage(run,"probe-physical")).Amount);
        Assert.Equal(100m,Assert.Single(Damage(run,"stormstrike")).Amount);
    }

    private static IEnumerable<CombatEvent> Damage(SimulationRunResult run, string? ability = null) =>
        run.Timeline.Where(e => e.Type == CombatEventType.Damage && (ability is null || e.AbilityKey == ability));

    private sealed class Fixture
    {
        public SimulationContext Context { get; }
        public AuraManager Auras { get; } = new();
        public ControlledRolls Rolls { get; } = new();
        public AbilityExecutor Executor { get; }
        public SimulationActorState Owner => Context.GetActor("shaman")!;
        public SimulationActorState Target => Context.GetActor("target")!;
        public ShamanLightningShieldProcessor Shield { get; }
        private readonly List<(decimal At, Action Action)> _actions = [];
        private readonly List<ICombatEventProcessor> _processors = [];

        public Fixture(decimal duration, decimal shieldMultiplier = 1m, int stormstrikeRank = 1,
            IDamageMitigationResolver? mitigation = null, int concussion = 0, decimal spellPower = 0m)
        {
            Context = new(new SimulationRunOptions { Seed = 175120, DurationSeconds = duration,
                PrimaryActorKey = "shaman", CaptureTimeline = true });
            Executor = new(Auras, Rolls, mitigation ?? new NoMitigation());
            var tuning = new ForeverEnhancementShamanTuning { MaelstromChancePerMeleePercentPerRank = 0m };
            foreach (var key in new[] { "shaman", "other-shaman", "target", "other-target" })
            {
                var actor = new SimulationActorState { Key = key, Name = key, Level = 60,
                    TeamKey = key.Contains("shaman") ? "raid" : "enemy", MaximumHealth = 1000000m };
                actor.InitializeHealth(actor.MaximumHealth);
                Context.AddActor(actor);
                if (!key.Contains("shaman")) continue;
                actor.AddResource(new ResourceState("mana", 10000m, 10000m));
                var talents = new ForeverEnhancementShamanTalents { Ranks = new()
                    { ["stormstrike"] = 1, ["maelstrom-weapon"] = 5, ["concussion"] = concussion } };
                foreach (var ability in ForeverEnhancementShamanSpellbook.Create(tuning, talents, spellPower, 3m, 100m, 100m))
                    actor.AddAbility(ability);
                talents.Ranks["stormstrike"] = stormstrikeRank;
                _processors.Add(new ForeverEnhancementShamanReactiveProcessor(key,
                    new AutoAttackProcessor(Executor), Auras, talents, tuning));
            }
            Shield = new(Owner, Auras, Executor, internalCooldownSeconds: 0m, damageMultiplier: shieldMultiplier);
        }

        public void At(decimal at, Action action) => _actions.Add((at, action));
        public void Hit(string ability, string source = "shaman", string target = "target")
        {
            var actor = Context.GetActor(source)!;
            var definition = actor.Abilities[ability].Definition;
            Executor.ExecuteBackgroundDirectDamage(Context, actor, Context.GetActor(target)!,
                ability, definition.Name, definition.Effects.Single());
        }
        public void Probe(string school, CombatEffectDeliveryType delivery = CombatEffectDeliveryType.Direct,
            decimal multiplier = 1m) => Deal(Owner, Target, "probe-" + school, school, 100m, delivery, multiplier);
        public void Absorb(decimal amount) => new AbsorbManager().ApplyAbsorb(Context, Target,
            "fixture-absorb", "Fixture absorb", amount, 30m, AbsorbStackingMode.Refresh, 1,
            Target.Key, "fixture-absorb", "fixture-absorb");
        public void Incoming(string source) => Deal(Context.GetActor(source)!, Owner, "incoming", "physical", 10m);
        private void Deal(SimulationActorState source, SimulationActorState target, string key, string school, decimal amount,
            CombatEffectDeliveryType delivery = CombatEffectDeliveryType.Direct, decimal multiplier = 1m) =>
            Executor.ExecuteBackgroundDirectDamage(Context, source, target, key, key, new AbilityEffectDefinition
            {
                Key = key, EffectType = AbilityEffectTypes.DirectDamage, SchoolKey = school,
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false,
                CanCrit = Rolls.NatureCritical && school == "nature",
                MinimumValue = amount, MaximumValue = amount, DamageMultiplier = multiplier
            }, deliveryType: delivery);
        public void ApplyShield() => Auras.ApplyAura(Context, Owner, new AuraDefinition
        {
            Key = "shaman-lightning-shield", DurationSeconds = 600m
        }, Owner.Key, "lightning-shield", "lightning-shield");
        public SimulationRunResult Run() => new SimulationEngine([
            Executor, Auras, Shield, .._processors,
            new Driver((_, e) =>
            {
                if (e.Type == CombatEventType.RotationDecision && e.EffectKey == "fixture-action")
                    _actions[int.Parse(e.EncounterEventKey!)].Action();
            })]).Run(Context, context =>
            {
                for (var i = 0; i < _actions.Count; i++)
                    context.ScheduleEvent(new CombatEvent { TimeSeconds = _actions[i].At,
                        Type = CombatEventType.RotationDecision, EffectKey = "fixture-action",
                        EncounterEventKey = i.ToString(), IsInternal = true });
            });
    }

    private sealed class ControlledRolls : ICombatRollResolver
    {
        public Func<decimal, string> StrikeOutcome { get; set; } = _ => "hit";
        public bool NatureCritical { get; set; }
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            (ability.Key == "stormstrike" ? StrikeOutcome(context.CurrentTimeSeconds) : "hit") switch
            {
                "miss" => CombatRollResult.Miss(), "dodge" => CombatRollResult.Dodge(),
                "parry" => CombatRollResult.Parry(),
                "zero-damage" => new() { Landed = true, ResultKey = "hit", AmountMultiplier = 0m },
                _ => NatureCritical && effect.SchoolKey == "nature" ? CombatRollResult.Critical(2m) : CombatRollResult.Hit()
            };
    }
    private sealed class QuarterNatureMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect, decimal amount) =>
            effect.SchoolKey == "nature" ? new DamageMitigationResult
            {
                RawAmount = amount, FinalAmount = amount * .75m,
                MitigatedAmount = amount * .25m, ReductionPercent = 25m
            } : DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect, decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class Driver(Action<SimulationContext, CombatEvent> action) : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent e) => action(context, e);
    }
}
