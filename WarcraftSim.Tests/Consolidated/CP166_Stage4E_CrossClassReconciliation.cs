using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Stage 4E cross-class reconciliations. Compares actual resolved Damage,
/// ThreatChanged and API responses; leaves unverifiable hit-table coefficients
/// and source-owned class rotation policy untouched.
/// </summary>
public sealed class Cp166Stage4ECrossClassReconciliationTests
{
    [Theory]
    [InlineData("elemental")]
    [InlineData("enhancement")]
    [InlineData("balance")]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void PreviouslySilentDpsSpecsGenerateExactlyOneThreatPerResolvedDamage(string spec)
    {
        SimulationRunResult run = spec switch
        {
            "elemental" => new ElementalShamanSimRunner().Run(new ElementalShamanSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true, UseLightningOverload = false
            }).Run,
            "enhancement" => new EnhancementShamanSimRunner().Run(new EnhancementShamanSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true,
                UseWindfuryWeapon = false, UseWindfuryTotem = false
            }).Run,
            "balance" => new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true
            }).Run,
            "marksmanship" => new MarksmanshipHunterSimRunner().Run(new MarksmanshipHunterSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true
            }).Run,
            "survival" => new SurvivalHunterSimRunner().Run(new SurvivalHunterSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true
            }).Run,
            _ => throw new ArgumentException(spec)
        };

        var owner = spec is "elemental" or "enhancement" ? "shaman"
            : spec == "balance" ? "druid" : "hunter";
        var raw = run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.SourceActorKey == owner &&
            e.Amount is > 0m && e.TargetActorKey is not null)
            .Sum(e => e.Amount.GetValueOrDefault());
        var threat = run.Timeline.Where(e =>
            e.Type == CombatEventType.ThreatChanged && e.SourceActorKey == owner)
            .Sum(e => e.Amount.GetValueOrDefault());
        Assert.True(raw > 0m, $"{spec} should resolve outgoing damage");
        Assert.InRange(Math.Abs(threat - raw), 0m, .000001m);
        Assert.Contains(run.Timeline, e =>
            e.Type == CombatEventType.ThreatChanged &&
            e.TargetActorKey == "target");
        Assert.Contains("ActorSummaries", JsonSerializer.Serialize(run));
    }

    [Fact]
    public void ElementalLightningOverloadDamageDoesNotGenerateThreatWhileOrdinarySpellsDo()
    {
        var request = new ElementalShamanSimRequest {
            DurationSeconds = 70m, CaptureTimeline = true,
            BaseSpellHitPercent = 100m, SpellCritPercent = 0m,
            UseLightningOverload = true
        };
        request.Tuning.LightningOverloadChancePercentRank3 = 100m;
        request.CustomPriority = ["lightning-bolt"];
        request.UseFlameShock = false;
        request.UseSearingTotem = false;
        request.UseLavaBurst = false;
        request.UseChainLightning = false;
        var sim = new ElementalShamanSimRunner().Run(request);
        Assert.True(sim.LightningOverloadProcs > 0);
        var normal = sim.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.SourceActorKey == "shaman" &&
            e.Amount is > 0m && e.AbilityKey is not
                ("lightning-overload-bolt" or "lightning-overload-chain"))
            .Sum(e => e.Amount.GetValueOrDefault());
        var overload = sim.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey is
                ("lightning-overload-bolt" or "lightning-overload-chain") &&
            e.Amount is > 0m).Sum(e => e.Amount.GetValueOrDefault());
        Assert.True(overload > 0m);
        var threat = sim.Run.Timeline.Where(e =>
            e.Type == CombatEventType.ThreatChanged && e.SourceActorKey == "shaman")
            .Sum(e => e.Amount.GetValueOrDefault());
        Assert.InRange(Math.Abs(threat - normal), 0m, .000001m);
        Assert.True(sim.DamageDone >= normal + overload - .000001m);
    }

    [Fact]
    public void NaturalGraceReducesElementalSpellThreatWithoutChangingDamage()
    {
        ElementalShamanSimResponse Sim(int rank)
        {
            var request = new ElementalShamanSimRequest {
                DurationSeconds = 50m, CaptureTimeline = true,
                BaseSpellHitPercent = 100m, SpellCritPercent = 0m,
                UseLightningOverload = false
            };
            // Keep both comparison builds valid: shift three filler points
            // from Totemic Focus to Natural Grace in the same Restoration tree.
            request.Talents.Ranks["totemic-focus"] = 2;
            request.Talents.Ranks["natural-grace"] = rank;
            request.UseSearingTotem = false;
            return new ElementalShamanSimRunner().Run(request);
        }
        var none = Sim(0);
        var three = Sim(3);
        Assert.Equal(none.DamageDone, three.DamageDone);
        var threat0 = none.Run.Timeline.Where(e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "shaman").Sum(e => e.Amount.GetValueOrDefault());
        var threat3 = three.Run.Timeline.Where(e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "shaman").Sum(e => e.Amount.GetValueOrDefault());
        Assert.True(threat0 > 0m);
        Assert.InRange(Math.Abs(threat3 - threat0 * .85m), 0m, .000001m);
    }

    [Fact]
    public void DruidSubtletyReducesNatureArcaneThreatWithoutAlteringSpellDamage()
    {
        BalanceDruidSimResponse Sim(int rank)
        {
            var request = new BalanceDruidSimRequest {
                DurationSeconds = 55m, CaptureTimeline = true,
                UseFaerieFire = false, UseThorns = false, UseHurricane = true
            };
            request.Talents.Ranks["subtlety"] = rank;
            return new BalanceDruidSimRunner().Run(request);
        }
        var none = Sim(0);
        var three = Sim(3);
        Assert.Equal(none.DamageDone, three.DamageDone);
        var threat0 = none.Run.Timeline.Where(e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "druid").Sum(e => e.Amount.GetValueOrDefault());
        var threat3 = three.Run.Timeline.Where(e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "druid").Sum(e => e.Amount.GetValueOrDefault());
        Assert.True(threat0 > 0m);
        Assert.InRange(Math.Abs(threat3 - threat0 * .70m), 0m, .000001m);
    }

    [Fact]
    public void RestorationShamanNaturalGraceMatchesElementalFivePercentPerRank()
    {
        RestorationShamanSimResponse Sim(int rank)
        {
            var request = new RestorationShamanSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true,
                IncomingDamage = 850m, AllyInitialHealthPercent = 45m,
                AdditionalAllies = 2
            };
            request.Talents.Ranks["natural-grace"] = rank;
            request.Tuning.HealingCritPercent = 0m;
            return new RestorationShamanSimRunner().Run(request);
        }
        var ordinary = Sim(0);
        var reduced = Sim(3);
        Assert.Equal(ordinary.HealingDone, reduced.HealingDone);
        Assert.Equal(ordinary.OverhealingDone, reduced.OverhealingDone);
        var baseline = ordinary.Run.Timeline.Where(e =>
            e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "shaman").Sum(e => e.Amount.GetValueOrDefault());
        var adjusted = reduced.Run.Timeline.Where(e =>
            e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "shaman").Sum(e => e.Amount.GetValueOrDefault());
        Assert.True(baseline > 0m);
        Assert.InRange(Math.Abs(adjusted - baseline * .85m), 0m, .000001m);
    }

    [Fact]
    public void AllEightImplementedClassDefaultsRunAndSerializeWithoutDamageDrift()
    {
        var warrior = new ForeverWarriorSimRunner().Run(new WarriorSimRequest {
            Preset = "arms", TalentMode = "legacy", Level = 60,
            Iterations = 1, DurationSeconds = 45m,
            ExecutePhaseDurationSeconds = 0m, TargetArmor = 0m,
            TargetLevel = 60, MeleeHitPercent = 100m,
            TargetDodgePercent = 0m, TargetParryPercent = 0m,
            StartingRage = 50m
        });
        Assert.True(warrior.MeanDps >= 0m);
        Assert.Contains("RepresentativeRun", JsonSerializer.Serialize(warrior));

        var rogueReq = RogueSimRequest.CreatePreset("combat");
        rogueReq.DurationSeconds = 45m;
        var rogue = new RogueSimRunner().Run(rogueReq);
        Assert.True(rogue.DamageDone > 0m);
        Assert.InRange(Math.Abs(rogue.DamageDone - rogue.DamageByAbility.Values.Sum()),
            0m, .000001m);
        Assert.Contains("DamageByAbility", JsonSerializer.Serialize(rogue));

        var hunter = new BeastMasteryHunterSimRunner().Run(
            new BeastMasteryHunterSimRequest {
                DurationSeconds = 45m, PetFamily = "bear"
            });
        Assert.True(hunter.DamageDone > 0m);
        Assert.InRange(Math.Abs(hunter.DamageDone -
            hunter.HunterDamage - hunter.PetDamage - hunter.HawkDamage), 0m, .000001m);
        Assert.Contains("PetDamage", JsonSerializer.Serialize(hunter));

        var warlockReq = WarlockSimRequest.CreatePreset("demonology");
        warlockReq.DurationSeconds = 45m;
        var warlock = new WarlockSimRunner().Run(warlockReq);
        Assert.True(warlock.DamageDone > 0m);
        Assert.InRange(Math.Abs(warlock.DamageDone -
            warlock.WarlockDamage - warlock.PetDamage), 0m, .000001m);
        Assert.Contains("PetDamage", JsonSerializer.Serialize(warlock));

        var mageReq = MageSimRequest.CreatePreset("fire");
        mageReq.DurationSeconds = 45m;
        var mage = new MageSimRunner().Run(mageReq);
        Assert.True(mage.DamageDone > 0m);
        Assert.InRange(Math.Abs(mage.DamageDone - mage.DamageByAbility.Values.Sum()),
            0m, .000001m);
        Assert.Contains("DamageByAbility", JsonSerializer.Serialize(mage));

        var priest = new ShadowPriestSimRunner().Run(new ShadowPriestSimRequest {
            DurationSeconds = 45m
        });
        Assert.True(priest.DamageDone > 0m);
        Assert.InRange(Math.Abs(priest.DamageDone - priest.DamageByAbility.Values.Sum()),
            0m, .000001m);
        Assert.Contains("DamageByAbility", JsonSerializer.Serialize(priest));

        var shaman = new ElementalShamanSimRunner().Run(new ElementalShamanSimRequest {
            DurationSeconds = 45m
        });
        Assert.True(shaman.DamageDone > 0m);
        Assert.InRange(Math.Abs(shaman.DamageDone - shaman.DamageByAbility.Values.Sum()),
            0m, .000001m);
        Assert.Contains("DamageByAbility", JsonSerializer.Serialize(shaman));

        var druid = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", DurationSeconds = 45m
        });
        Assert.True(druid.DamageDone > 0m);
        Assert.InRange(Math.Abs(druid.DamageDone - druid.DamageByAbility.Values.Sum()),
            0m, .000001m);
        Assert.Contains("DamageByAbility", JsonSerializer.Serialize(druid));
    }

    [Theory]
    [InlineData("warrior")]
    [InlineData("rogue")]
    [InlineData("hunter")]
    [InlineData("warlock")]
    [InlineData("mage")]
    [InlineData("priest")]
    [InlineData("shaman")]
    [InlineData("druid")]
    public void SourceOwnedSharedThreatIgnoresFriendlySelfAndUnsuccessfulDamage(string key)
    {
        var context = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 6m, PrimaryActorKey = key
        });
        var source = new SimulationActorState { Key = key, TeamKey = "raid" };
        source.InitializeHealth(1000m);
        var friend = new SimulationActorState { Key = "ally", TeamKey = "raid" };
        friend.InitializeHealth(1000m);
        var boss = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        boss.InitializeHealth(10000m);
        context.AddActor(source); context.AddActor(friend); context.AddActor(boss);
        var threat = new ThreatManager(new SoloDamageThreatResolver(key));
        void Record(string target, decimal amount, CombatEffectDeliveryType delivery)
            => threat.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = key,
                TargetActorKey = target, Amount = amount,
                AbilityKey = "comparative-ability", SchoolKey = "nature",
                EffectDeliveryType = delivery
            });
        Record("ally", 200m, CombatEffectDeliveryType.Direct);
        Record("target", 0m, CombatEffectDeliveryType.Direct);
        Record("target", 100m, CombatEffectDeliveryType.Direct);
        Record("target", 30m, CombatEffectDeliveryType.Periodic);
        Assert.Equal(130m, boss.ThreatTable.GetThreat(key));
        Assert.Equal(0m, friend.ThreatTable.GetThreat(key));
    }
}
