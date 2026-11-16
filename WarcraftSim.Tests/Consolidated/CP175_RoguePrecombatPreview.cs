using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175RoguePrecombatPreview(ITestOutputHelper output)
{
    private static string Fingerprint(RogueSimResponse r) => JsonSerializer.Serialize(new
    {
        r.Specialization, r.DamageDone, r.Dps, r.EndingEnergy, r.MaximumEnergy,
        r.MaximumHealth, r.EndingHealth, r.EndingComboPoints, r.PoisonProcs,
        r.FinishersUsed, r.StealthOpeners, r.DamageByAbility, r.Run.ProcessedEventCount,
        r.Run.CustomMetrics,
        Timeline = r.Run.Timeline.Select(e => new {
            e.TimeSeconds, e.Type, e.SourceActorKey, e.TargetActorKey, e.AbilityKey,
            e.EffectKey, e.Amount, e.RawAmount, e.ResultKey, e.ResourceKey, e.SchoolKey })
    });

    [Theory]
    [InlineData("assassination", "585EE6668BF114A5789517C2409E0E66E8453F17138FA993E73E0D8A3B3929ED")]
    [InlineData("combat", "2DF8B06C285F5A99095E5D938CD16B9ED88ED986F57EEBA397A7FA8231764651")]
    [InlineData("subtlety", "C3AAE253895B47D027782DEAD310E1D13F1CF4202E830B365CA91FDD679821DE")]
    public void LegacySpecCombatFingerprintIsUnchanged(string spec, string expected)
    {
        var r=RogueSimRequest.CreatePreset(spec); r.CaptureTimeline=true;
        // Retain the original golden: explicit legacy tuning must still replay
        // exactly after the historical Mutilate default correction to13.
        if(spec=="assassination")r.Tuning.MutilateBonusDamagePerHand=17.25m;
        var fingerprint=Fingerprint(new RogueSimRunner().Run(r));
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        output.WriteLine($"{spec}: {hash}\n{fingerprint}");
        // Captured from untouched production HEAD 0cdd7a8 before Prepare extraction.
        Assert.Equal(expected,hash);
    }

    [Fact]
    public void SourcedMutilateDefaultMatchesIndependentlyCapturedOldCodeWithExplicit13()
    {
        var r=RogueSimRequest.CreatePreset("assassination");r.CaptureTimeline=true;
        var sourced=new RogueSimRunner().Run(r);
        // Captured from preserved pre-edit DLLs with explicit13, before changing
        // production. Build-stamp provenance and causal receipts are documented
        // in docs/audits/CP175-rogue-mutilate-core.md.
        Assert.Equal("EE6D98EC2E9865F9BE6C5ED95D3B85C07813852DAB4D8BC8485EF21F11AE14CE",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint(sourced)))));
        r.Tuning.MutilateBonusDamagePerHand=17.25m;
        var legacy=new RogueSimRunner().Run(r);
        Assert.Equal(127.5m,legacy.DamageDone-sourced.DamageDone);
        Assert.Equal(legacy.FinishersUsed,sourced.FinishersUsed);
        Assert.Equal(legacy.PoisonProcs,sourced.PoisonProcs);
        Assert.Equal(legacy.EndingEnergy,sourced.EndingEnergy);
        Assert.Equal(legacy.Run.ProcessedEventCount,sourced.Run.ProcessedEventCount);
    }

    private static RogueSimRequest Input(string mode="observed", string spec="combat")
    {
        var r=RogueSimRequest.CreatePreset(spec); r.DurationSeconds=20m; r.CaptureTimeline=true;
        if(mode=="legacy")return r;
        r.Character.Race="human";
        if(mode=="provisional"){r.UseProvisionalLevel60Baseline=true;return r;}
        r.Level60Baseline=new() {Race="human",ClassKey="rogue",Strength=100m,Agility=200m,
            Stamina=100m,Intellect=40m,Spirit=40m,ObservedUnbuffedMeleeAttackPower=700m,
            ObservedUnbuffedMaximumHealth=3000m,ClientBuild="synthetic-test-build",
            SourceReference="controlled-test-observation",RecordedAtUtc="2026-11-10T00:00:00Z"};
        return r;
    }

    [Theory]
    [InlineData("legacy","assassination")][InlineData("legacy","combat")][InlineData("legacy","subtlety")]
    [InlineData("observed","assassination")][InlineData("observed","combat")][InlineData("observed","subtlety")]
    [InlineData("provisional","assassination")][InlineData("provisional","combat")][InlineData("provisional","subtlety")]
    public void PreviewAndExecutionShareResourcesWithoutMutatingInputs(string mode,string spec)
    {
        var r=Input(mode,spec);r.StartingEnergyPercent=25m;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored-budget",
            Stats=new(){Values=new(){["strength"]=50m,["agility"]=25m,["attack-power"]=125m,
                ["stamina"]=10m,["health"]=50m}}});
        var before=JsonSerializer.Serialize(r);
        var p=RogueSimRunner.Preview(r);var run=new RogueSimRunner().Run(r);
        Assert.Equal(run.MaximumEnergy,p.MaximumEnergy);Assert.Equal(run.MaximumHealth,p.MaximumHealth);
        Assert.Equal(p.MaximumEnergy*.25m,p.StartingEnergy);
        Assert.Equal(p.AttackPower,p.EffectiveStats.Get("attack-power"));
        Assert.Equal(p.MaximumEnergy,p.EffectiveStats.Get("energy"));
        Assert.Equal(p.MaximumHealth,p.EffectiveStats.Get("health"));
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(JsonSerializer.Serialize(p),JsonSerializer.Serialize(RogueSimRunner.Preview(r)));
        Assert.Equal(Fingerprint(run),Fingerprint(new RogueSimRunner().Run(r)));
        Assert.Contains(p.ModelingNotes,n=>n.Contains("PARTIALLY implemented"));
        Assert.Contains(p.ModelingNotes,n=>n.Contains("not server attestation"));
    }

    [Theory][InlineData(0,190)][InlineData(100,380)]
    public void ObservedApAndAuthoredDeltasHaveIndependentSwingOracle(int crit,int damage)
    {
        var r=Input();r.Talents=new();r.StartInStealth=false;r.UseInstantPoison=false;
        r.StartingEnergyPercent=0;r.Tuning.EnergyPerSecond=.01m;
        r.Tuning.MainHandWeaponDamage=100;r.Tuning.MainHandSpeedSeconds=1.4m;
        r.Tuning.MainHandHitPercent=100;r.Tuning.PhysicalCritPercent=crit;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="synthetic-budget",
            Stats=new(){Values=new(){["strength"]=50m,["agility"]=25m,["attack-power"]=125m,
                ["attack-critical-chance-percent"]=crit==0?-100m:0m}}});
        // Cancel derived Agility crit only in the guaranteed-normal-hit scenario.
        var p=RogueSimRunner.Preview(r);var run=new RogueSimRunner().Run(r);
        // Observed AP700 already contains naked Str100+Agi200. Added 50+25+125=200.
        Assert.Equal(900m,p.AttackPower);Assert.Equal(150m,p.EffectiveStats.Get("strength"));
        Assert.Equal(225m,p.EffectiveStats.Get("agility"));
        var swings=run.Run.Timeline.Where(e=>e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage &&
            e.AbilityKey=="rogue-main-hand").ToArray();
        Assert.NotEmpty(swings);
        // Independent weapon oracle: 100 + 900*1.4/14 = 190, doubled at 100% crit.
        Assert.All(swings,e=>Assert.Equal((decimal)damage,e.Amount));
    }

    [Theory][InlineData(false,4000,4251)][InlineData(true,4000,3251)]
    [InlineData(false,4800,5051)][InlineData(true,4800,3251)]
    public void HealthUsesObservedOrManualCapAndEveryAuthoredSourceExactlyOnce(bool observed,int manual,int expected)
    {
        var r=Input();r.RogueMaximumHealth=manual;
        if(!observed)r.Level60Baseline!.ObservedUnbuffedMaximumHealth=null;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",
            Stats=new(){Values=new(){["stamina"]=10m,["health"]=50m}},
            Modifiers=[new(){PlacementKey="authored-placement",ModifierKey="authored-modifier",ModifierType="enchant",Stats=new(){Values=new(){["stamina"]=2m,["health"]=3m}}}]});
        r.Character.Talents.Selections.Add(new(){TalentKey="authored",Rank=2,
            Stats=new(){Values=new(){["stamina"]=4m,["health"]=5m}}});
        r.Character.Buffs.Selections.Add(new(){BuffKey="authored",
            Stats=new(){Values=new(){["stamina"]=3m,["health"]=3m}}});
        // Equipment +10/+50, modifier +2/+3, authored talent budget +4/+5, buff +3/+3.
        // Character talent Stats is an authored total; Rank does not multiply this budget.
        var p=RogueSimRunner.Preview(r);var run=new RogueSimRunner().Run(r);
        Assert.Equal(expected,p.MaximumHealth);Assert.Equal(expected,run.MaximumHealth);
        Assert.Equal(expected,run.EndingHealth);Assert.Equal(119m,p.EffectiveStats.Get("stamina"));
        Assert.Contains(p.ModelingNotes,n=>n.Contains(observed?"Supplied naked Health":"manual scenario cap"));
    }

    [Theory][InlineData(0,0,100,0)][InlineData(0,25,100,25)][InlineData(0,100,100,100)]
    [InlineData(1,0,105,0)][InlineData(1,25,105,26.25)][InlineData(1,100,105,105)]
    [InlineData(2,0,110,0)][InlineData(2,25,110,27.5)][InlineData(2,100,110,110)]
    [InlineData(0,37.5,100,37.5)][InlineData(1,37.5,105,39.375)][InlineData(2,37.5,110,41.25)]
    public void VigorCapacityAndStartingPercentageAreSeparate(int vigor,double percent,int maximum,double starting)
    {
        var r=Input();r.Talents=new(){AssassinationPointsSpent=vigor,Ranks=new(){["vigor"]=vigor}};
        r.StartingEnergyPercent=(decimal)percent;r.StartInStealth=false;
        var p=RogueSimRunner.Preview(r);var run=new RogueSimRunner().Run(r);
        Assert.Equal(maximum,p.MaximumEnergy);Assert.Equal((decimal)starting,p.StartingEnergy);
        Assert.Equal(maximum,run.MaximumEnergy);
        var spent=run.Run.Timeline.First(e=>e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.ResourceChanged && e.ResourceKey=="energy" && e.Amount<0m);
        // Before its first spend, this Vigor-only build has only the fixed 10 Energy/s regeneration.
        Assert.Equal(Math.Min(maximum,(decimal)starting+10m*spent.TimeSeconds),spent.ResourceTotalAfter-spent.Amount);
    }

    [Theory][InlineData("legacy")][InlineData("observed")]
    public void NegativeAttackPowerIsClampedOnlyOnDisplayCopyAndSpellbookInput(string mode)
    {
        var r=Input(mode);r.Talents=new();r.StartInStealth=false;r.UseInstantPoison=false;
        r.StartingEnergyPercent=0;r.Tuning.EnergyPerSecond=.01m;
        r.Tuning.MainHandHitPercent=100;r.Tuning.PhysicalCritPercent=0;
        r.Tuning.MainHandSpeedSeconds=1.4m; // Land before the off-hand swing can trigger Vanish.
        r.Character.BaseStats=new();
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(){["attack-power"]=-10000m,["attack-critical-chance-percent"]=-100m}}});
        var p=RogueSimRunner.Preview(r);Assert.Equal(0m,p.AttackPower);
        Assert.Equal(0m,p.EffectiveStats.Get("attack-power"));
        var swings=new RogueSimRunner().Run(r).Run.Timeline.Where(e=>
            e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage && e.AbilityKey=="rogue-main-hand").ToArray();
        Assert.NotEmpty(swings);Assert.All(swings,e=>Assert.Equal(r.Tuning.MainHandWeaponDamage,e.Amount));
    }

    [Theory][InlineData("assassination")][InlineData("combat")][InlineData("subtlety")]
    public void PoisonOffPreviewDoesNotConsumeOrEraseCoatings(string spec)
    {
        var r=Input("legacy",spec);r.UseInstantPoison=false;
        var before=JsonSerializer.Serialize(r);var p=RogueSimRunner.Preview(r);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(0,new RogueSimRunner().Run(r).PoisonProcs);
        r.UseInstantPoison=true;
        var expected=Input("legacy",spec);
        Assert.Equal(Fingerprint(new RogueSimRunner().Run(expected)),Fingerprint(new RogueSimRunner().Run(r)));
        Assert.Equal(JsonSerializer.Serialize(p),JsonSerializer.Serialize(RogueSimRunner.Preview(r)));
    }

    [Theory][InlineData("legacy")][InlineData("observed")][InlineData("provisional")]
    public async Task MixedConcurrentPreviewsAndRunsOwnAllMutableState(string mode)
    {
        var r=Input(mode);r.UseInstantPoison=false;
        var before=JsonSerializer.Serialize(r);var expected=RogueSimRunner.Preview(r);
        var expectedRun=Fingerprint(new RogueSimRunner().Run(r));
        var previewTasks=Enumerable.Range(0,3).Select(_=>Task.Run(()=>RogueSimRunner.Preview(r))).ToArray();
        var runTasks=Enumerable.Range(0,3).Select(_=>Task.Run(()=>new RogueSimRunner().Run(r))).ToArray();
        var previews=await Task.WhenAll(previewTasks);var runs=await Task.WhenAll(runTasks);
        Assert.All(previews,t=>Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(t)));
        Assert.All(runs,t=>Assert.Equal(expectedRun,Fingerprint(t)));
        previews[0].EffectiveStats.Set("strength",99999);
        previews[0].Rotation.Entries[0].AbilityKey="changed";
        previews[0].Rotation.Entries[0].Conditions.Add(new(){Key="changed"});
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(previews[1]));
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(RogueSimRunner.Preview(r)));
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }

    [Theory][InlineData(false,100)][InlineData(true,110)]
    public void CaseSensitiveTalentLookupAndNestedStatComparersAreNotNormalized(bool ignoreCase,int maximum)
    {
        var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var r=Input();r.Talents=new(){AssassinationPointsSpent=2,Ranks=new(comparer){["VIGOR"]=2}};
        r.Character.BaseStats.Values=new(comparer){["STRENGTH"]=99};
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(comparer){["AGILITY"]=2}},
            Modifiers=[new(){PlacementKey="authored-placement",ModifierKey="authored-modifier",ModifierType="enchant",Stats=new(){Values=new(comparer){["STAMINA"]=3}}}]});
        r.Character.Talents.Selections.Add(new(){TalentKey="authored",Stats=new(){Values=new(comparer){["HEALTH"]=4}}});
        r.Character.Buffs.Selections.Add(new(){BuffKey="authored",Stats=new(){Values=new(comparer){["STRENGTH"]=5}}});
        var before=JsonSerializer.Serialize(r);var p=RogueSimRunner.Preview(r);
        Assert.Equal(maximum,p.MaximumEnergy);Assert.Equal(maximum,new RogueSimRunner().Run(r).MaximumEnergy);
        Assert.Same(comparer,r.Talents.Ranks.Comparer);Assert.Same(comparer,r.Character.BaseStats.Values.Comparer);
        Assert.Same(comparer,r.Character.Equipment.Items[0].Stats.Values.Comparer);
        Assert.Same(comparer,r.Character.Equipment.Items[0].Modifiers[0].Stats.Values.Comparer);
        Assert.Same(comparer,r.Character.Talents.Selections[0].Stats.Values.Comparer);
        Assert.Same(comparer,r.Character.Buffs.Selections[0].Stats.Values.Comparer);
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }

    [Theory][InlineData("legacy")][InlineData("observed")][InlineData("provisional")]
    public void PreviewProvenanceAndScopeDoNotClaimCurrentClientOrStrictBuildCertification(string mode)
    {
        var r=Input(mode);var p=RogueSimRunner.Preview(r);
        Assert.Equal("rogue-exact-precombat-display-only-not-current-client-certified",p.StatPreviewScope);
        Assert.Equal("fixed-configured-priority-no-custom-rotation",p.RotationPreviewScope);
        Assert.Equal(mode=="legacy"?"explicit-user-profile-not-client-verified":
            mode=="provisional"?"provisional-documented-proxy":"unverified-user-supplied",p.SourceStatus);
        Assert.Equal(mode=="legacy",p.Baseline is null);
        Assert.Contains(p.ModelingNotes,n=>n.Contains("strict talent allocation"));
        Assert.Contains(p.ModelingNotes,n=>n.Contains("dynamic")&&n.Contains("priority"));
        Assert.Contains(p.ModelingNotes,n=>n.Contains("catalog equipment"));
        if(mode=="provisional")Assert.Contains(p.ModelingNotes,n=>n.Contains("NOT verified for Forever"));
    }

    [Theory][InlineData("assassination")][InlineData("combat")][InlineData("subtlety")]
    public void PreviewUsesExistingConfiguredPriorityAndDoesNotPretendToExecuteIt(string spec)
    {
        var r=Input("legacy",spec);r.UseExposeArmor=true;r.UseUtilityAbilities=true;
        var p=RogueSimRunner.Preview(r);var keys=p.Rotation.Entries.Select(e=>e.AbilityKey).ToArray();
        Assert.Equal(new[]{"evasion","feint","kick","blind"},keys.Take(4));
        Assert.Contains("expose-armor",keys);Assert.Contains("vanish",keys);
        Assert.Contains("slice-and-dice",keys);
        if(spec=="assassination"){Assert.Contains("mutilate",keys);Assert.DoesNotContain("sinister-strike",keys);}
        else Assert.Contains("sinister-strike",keys);
        // Hand-type labels remain independent of the legacy eligibility flag.
        r.MainHandWeaponType="sword";r.OffHandWeaponType="sword";
        Assert.Equal(keys,RogueSimRunner.Preview(r).Rotation.Entries.Select(e=>e.AbilityKey));
        r.BothWeaponsDaggers=false;r.IsBehindTarget=false;
        var frontal=RogueSimRunner.Preview(r).Rotation.Entries.Select(e=>e.AbilityKey).ToArray();
        Assert.DoesNotContain("ambush",frontal);Assert.DoesNotContain("backstab",frontal);
        Assert.DoesNotContain("garrote",frontal);Assert.DoesNotContain("mutilate",frontal);
        Assert.Contains("sinister-strike",frontal);
        // Run still performs the actual eligibility/finisher gating during combat.
        Assert.True(new RogueSimRunner().Run(r).Run.ProcessedEventCount>0);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void LegacyCatalogMetadataAndRulesetAcceptanceRemainUnchanged(int kind)
    {
        var r=Input("legacy");r.Character.RulesetKey="legacy-author-label";
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="legacy-authored",
            ItemId=kind==0?19102:null,EnchantSpellId=kind==1?20034:null,
            TemporaryEnchantSpellId=kind==2?25122:null,
            Stats=new(){Values=new(){["attack-power"]=10m,["stamina"]=500m,["health"]=500m}}});
        var p=RogueSimRunner.Preview(r);var run=new RogueSimRunner().Run(r);
        // Legacy Health is always the scenario cap, not its authored stat budget.
        Assert.Equal(4000m,p.MaximumHealth);Assert.Equal(4000m,run.MaximumHealth);
        Assert.Equal(670m,p.AttackPower);
    }

    public static TheoryData<string> InvalidInputs=>new()
    {
        "character","talents","ranks","tuning","duration-zero","duration-long","target-health",
        "rogue-health","energy-negative","energy-over","target-armor","targets-negative","targets-over",
        "boss-swing","boss-damage","dodge","parry","main-weapon","off-weapon","level","class","spec",
        "unknown-spec","poison-main","poison-off","poison-charges","poison-disabled-invalid",
        "talent-unknown","talent-cap","talent-total","tuning-speed","tuning-energy","tuning-hit",
        "equipment","items","entry","catalog","enchant","temporary-enchant","baseline-race",
        "baseline-modes","baseline-source","prepared-health"
    };

    [Theory][MemberData(nameof(InvalidInputs))]
    public void MalformedRequestsRejectIdenticallyBeforeReturningPreview(string invalid)
    {
        var r=Input();
        switch(invalid)
        {
            case "character":r.Character=null!;break;
            case "talents":r.Talents=null!;break;
            case "ranks":r.Talents.Ranks=null!;break;
            case "tuning":r.Tuning=null!;break;
            case "duration-zero":r.DurationSeconds=0;break;
            case "duration-long":r.DurationSeconds=1201;break;
            case "target-health":r.TargetMaximumHealth=0;break;
            case "rogue-health":r.RogueMaximumHealth=0;break;
            case "energy-negative":r.StartingEnergyPercent=-1;break;
            case "energy-over":r.StartingEnergyPercent=101;break;
            case "target-armor":r.TargetArmor=-1;break;
            case "targets-negative":r.AdditionalTargets=-1;break;
            case "targets-over":r.AdditionalTargets=9;break;
            case "boss-swing":r.BossSwingSeconds=0;break;
            case "boss-damage":r.BossHitDamage=-1;break;
            case "dodge":r.TargetDodgePercent=101;break;
            case "parry":r.TargetParryPercent=-1;break;
            case "main-weapon":r.MainHandWeaponType="DAGGER";break;
            case "off-weapon":r.OffHandWeaponType="unknown";break;
            case "level":r.Character.Level=59;break;
            case "class":r.Character.ClassKey="warrior";break;
            case "spec":r.Character.SpecializationKey="subtlety";break;
            case "unknown-spec":r.Specialization=r.Character.SpecializationKey="unknown";break;
            case "poison-main":r.MainHandPoison="unknown";break;
            case "poison-off":r.OffHandPoison=null!;break;
            case "poison-charges":r.PoisonChargesPerWeapon=-1;break;
            case "poison-disabled-invalid":r.UseInstantPoison=false;r.MainHandPoison="unknown";break;
            case "talent-unknown":r.Talents.Ranks["unknown"]=1;break;
            case "talent-cap":r.Talents.Ranks["vigor"]=3;break;
            case "talent-total":r.Talents.CombatPointsSpent=42;break;
            case "tuning-speed":r.Tuning.MainHandSpeedSeconds=0;break;
            case "tuning-energy":r.Tuning.EnergyPerSecond=0;break;
            case "tuning-hit":r.Tuning.OffHandHitPercent=101;break;
            case "equipment":r.Character.Equipment=null!;break;
            case "items":r.Character.Equipment.Items=null!;break;
            case "entry":r.Character.Equipment.Items.Add(null!);break;
            case "catalog":r.Character.Equipment.Items.Add(new(){ItemId=19102});break;
            case "enchant":r.Character.Equipment.Items.Add(new(){EnchantSpellId=20034});break;
            case "temporary-enchant":r.Character.Equipment.Items.Add(new(){TemporaryEnchantSpellId=25122});break;
            case "baseline-race":r.Character.Race="orc";break;
            case "baseline-modes":r.UseReviewedLevel60Baseline=true;r.UseProvisionalLevel60Baseline=true;break;
            case "baseline-source":r.Level60Baseline!.SourceReference="";break;
            case "prepared-health":r.Character.Buffs.Selections.Add(new(){BuffKey="authored",Stats=new(){Values=new(){["health"]=-3000m}}});break;
        }
        var before=JsonSerializer.Serialize(r);
        var runtime=Assert.ThrowsAny<ArgumentException>(()=>new RogueSimRunner().Run(r));
        var preview=Assert.ThrowsAny<ArgumentException>(()=>RogueSimRunner.Preview(r));
        Assert.Equal(runtime.GetType(),preview.GetType());Assert.Equal(runtime.Message,preview.Message);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(preview.Message,Assert.ThrowsAny<ArgumentException>(()=>RogueSimRunner.Preview(r)).Message);
    }

    [Fact]
    public void NullRequestRejectsAtTheSameBoundary()
    {
        var run=Assert.Throws<ArgumentNullException>(()=>new RogueSimRunner().Run(null!));
        var preview=Assert.Throws<ArgumentNullException>(()=>RogueSimRunner.Preview(null!));
        Assert.Equal(run.ParamName,preview.ParamName);
    }
}
