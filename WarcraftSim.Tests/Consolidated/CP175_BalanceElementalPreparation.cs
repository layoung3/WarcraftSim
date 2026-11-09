using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175BalanceElementalPreparation
{
    private sealed record Result(decimal Mana,decimal Health,decimal EndingHealth,
        int Potions,Dictionary<string,int> Items,SimulationRunResult Run,List<string> Notes);

    private static void AssertReplay(Result first,Result replay)
    {
        Assert.Equal(first.Mana,replay.Mana);Assert.Equal(first.Health,replay.Health);
        Assert.Equal(first.EndingHealth,replay.EndingHealth);Assert.Equal(first.Potions,replay.Potions);
        Assert.Equal(first.Items,replay.Items);Assert.Equal(first.Notes,replay.Notes);
        Assert.Equal(JsonSerializer.Serialize(first.Run.Summary),JsonSerializer.Serialize(replay.Run.Summary));
        Assert.Equal(first.Run.ProcessedEventCount,replay.Run.ProcessedEventCount);
        Assert.Equal(first.Run.CustomMetrics,replay.Run.CustomMetrics);
        // Event identity GUIDs are intentionally fresh for each run. Compare
        // actual timing, damage, casts and resource outcomes rather than IDs.
        object[] Events(Result r)=>r.Run.Timeline.Select(e=>(object)new {
            e.TimeSeconds,e.Type,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,
            e.EffectKey,e.ResourceKey,e.Amount,e.RawAmount,e.ResultKey,e.IsPeriodic
        }).ToArray();
        Assert.Equal(JsonSerializer.Serialize(Events(first)),JsonSerializer.Serialize(Events(replay)));
    }

    private sealed class Fixture
    {
        public bool IsShaman {get;}
        public string ClassKey=>IsShaman?"shaman":"druid";
        public BalanceDruidSimRequest Druid {get;}=new();
        public ElementalShamanSimRequest Shaman {get;}=new();
        public object Request=>IsShaman?(object)Shaman:Druid;
        public CharacterProfile Character=>IsShaman?Shaman.Character:Druid.Character;
        public ForeverLevel60BaselineObservation Baseline=>
            (IsShaman?Shaman.Level60Baseline:Druid.Level60Baseline)!;
        public CombatUseItemsOptions Items=>IsShaman?Shaman.CombatItems:Druid.CombatItems;
        public Fixture(bool shaman)
        {
            IsShaman=shaman;
            var baseline=new ForeverLevel60BaselineObservation {
                Race=shaman?"orc":"night-elf",ClassKey=ClassKey,
                Strength=40m,Agility=40m,Stamina=100m,Intellect=200m,Spirit=100m,
                ObservedUnbuffedMaximumHealth=3000m,ClientBuild="1.60.1.70707",
                SourceReference="test-controlled-observation",RecordedAtUtc="2026-11-09T00:00:00Z"
            };
            Character.Race=baseline.Race;
            Character.Equipment=new();Character.Buffs=new();Character.Talents=new();
            Character.BaseStats=new() {Values=new() { ["intellect"]=999m,["health"]=999m }};
            if(shaman)
            {
                Shaman.Level60Baseline=baseline;Shaman.Talents.Ranks.Clear();
                Shaman.DurationSeconds=12m;Shaman.StartingManaPercent=40m;
                Shaman.MajorManaPotionCharges=0;Shaman.MaximumHealth=4000m;
                Shaman.CustomPriority=["lightning-bolt"];Shaman.UseElementalMastery=false;
                Shaman.Tuning.ManaRegenPerSecond=0m;
            }
            else
            {
                Druid.Level60Baseline=baseline;Druid.Talents.Ranks.Clear();
                Druid.DurationSeconds=12m;Druid.StartingManaPercent=40m;
                Druid.MajorManaPotionCharges=0;Druid.MaximumHealth=4000m;
                Druid.CustomPriority=["wrath"];Druid.UseInnervate=false;Druid.UseOmenOfClarity=false;
                Druid.Tuning.ManaRegenPerSecond=0m;
            }
        }
        public void LegacyCharges(int charges)
        {
            if(IsShaman)Shaman.MajorManaPotionCharges=charges;
            else Druid.MajorManaPotionCharges=charges;
        }
        public void Start(decimal percent)
        {
            if(IsShaman)Shaman.StartingManaPercent=percent;
            else Druid.StartingManaPercent=percent;
        }
        public Result Run()
        {
            if(IsShaman)
            {
                var r=new ElementalShamanSimRunner().Run(Shaman);
                return new(r.MaximumMana,r.MaximumHealth,r.EndingHealth,r.MajorManaPotionUses,
                    r.CombatItemUses,r.Run,r.ModelingNotes);
            }
            var d=new BalanceDruidSimRunner().Run(Druid);
            return new(d.MaximumMana,d.MaximumHealth,d.EndingHealth,d.MajorManaPotionUses,
                d.CombatItemUses,d.Run,d.ModelingNotes);
        }
    }

    [Theory]
    [InlineData(false,3944)]
    [InlineData(true,4220)]
    public void SourcedBaseManaUsesPreparedIntellectAndConsistentStartingReport(bool shaman,int mana)
    {
        var f=new Fixture(shaman);
        var result=f.Run();
        Assert.Equal(mana,result.Mana);
        Assert.Equal(mana*.4m,result.Run.Summary.ActorSummaries[f.ClassKey].StartingResources["mana"]);
        Assert.Equal(3000m,result.Health);
        Assert.Contains(result.Notes,n=>n.Contains("ContentSetID=0")&&
            n.Contains("1.60.1.70707")&&n.Contains(ForeverClientBaseManaCatalog.SourceSha256));
        Assert.Contains(result.Notes,n=>n.Contains("test-controlled-observation")&&n.Contains("unverified-user-supplied"));
        Assert.Contains(result.Notes,n=>n.Contains("conversions remain provisional"));
    }

    [Fact]
    public void AncestralKnowledgeScalesTotalIntellectOnceBeforeResourceInitialization()
    {
        var f=new Fixture(true);
        f.Baseline.Intellect=180m;
        f.Character.Equipment.Items=[new() {SlotKey="head",ItemKey="test-head",
            Stats=new() {Values=new() { ["intellect"]=20m }} }];
        f.Shaman.Talents.Ranks=new(StringComparer.OrdinalIgnoreCase) { ["ANCESTRAL-KNOWLEDGE"]=5 };
        var before=JsonSerializer.Serialize(f.Request);
        var first=f.Run();var replay=f.Run();
        Assert.Equal(4520m,first.Mana);
        Assert.Equal(1808m,first.Run.Summary.ActorSummaries["shaman"].StartingResources["mana"]);
        AssertReplay(first,replay);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData(false,false,4069,3350)]
    [InlineData(true,false,4345,3350)]
    [InlineData(false,true,4069,4350)]
    [InlineData(true,true,4345,4350)]
    public void FlatResourcesAndStaminaDeltaApplyOnceWithoutChangingCaller(
        bool shaman,bool manualHealth,int mana,int health)
    {
        var f=new Fixture(shaman);
        f.Baseline.Intellect=180m;
        if(manualHealth)f.Baseline.ObservedUnbuffedMaximumHealth=null;
        f.Character.Equipment.Items=[new() {SlotKey="head",ItemKey="test-head",
            Stats=new() {Values=new() { ["intellect"]=20m,["stamina"]=10m,["mana"]=125m,["health"]=250m }} }];
        var before=JsonSerializer.Serialize(f.Request);
        var first=f.Run();var replay=f.Run();
        Assert.Equal(mana,first.Mana);Assert.Equal(health,first.Health);
        Assert.Equal(health,first.EndingHealth);
        Assert.Equal(mana*.4m,first.Run.Summary.ActorSummaries[f.ClassKey].StartingResources["mana"]);
        AssertReplay(first,replay);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
        if(manualHealth)Assert.Contains(first.Notes,n=>n.Contains("manual scenario cap")&&n.Contains("not observed or source-verified"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservedManaUsesOnlyPostBaselineDeltaAndFlatMana(bool shaman)
    {
        var f=new Fixture(shaman);
        f.Baseline.ObservedUnbuffedMaximumMana=5000m;
        f.Character.Buffs.Selections=[new() {BuffKey="test-resource-buff",
            Stats=new() {Values=new() { ["intellect"]=10m,["mana"]=100m }} }];
        if(shaman)f.Shaman.Talents.Ranks["ancestral-knowledge"]=5;
        var result=f.Run();
        Assert.Equal(shaman?5565m:5250m,result.Mana);
        Assert.Contains(result.Notes,n=>n.Contains("Maximum Mana uses the supplied naked observation"));
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void NonpositivePreparedCapsFailClearly(bool shaman,bool mana)
    {
        var f=new Fixture(shaman);
        f.Baseline.ObservedUnbuffedMaximumHealth=null;
        f.Character.Buffs.Selections=[new() {BuffKey="test-negative-cap",
            Stats=new() {Values=new() { [mana?"mana":"health"]=-10000m }} }];
        var before=JsonSerializer.Serialize(f.Request);
        var error=Assert.Throws<ArgumentException>(()=>f.Run());
        Assert.Contains("maximum Mana and Health must be positive",error.Message);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData(false,0,1)]
    [InlineData(true,0,1)]
    [InlineData(false,1,0)]
    [InlineData(true,1,0)]
    [InlineData(false,1,1)]
    [InlineData(true,1,1)]
    public void PreparedPotionInventoryUsesOneSchedulerAndReplaysWithoutMutation(
        bool shaman,int legacy,int shared)
    {
        var f=new Fixture(shaman);f.Start(0m);f.LegacyCharges(legacy);
        f.Items.MajorManaPotionCharges=shared;
        var before=JsonSerializer.Serialize(f.Request);
        var first=f.Run();var replay=f.Run();
        Assert.Equal(1,first.Potions);
        Assert.Equal(1,first.Items["major-mana-potion"]);
        Assert.Single(first.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="major-mana-potion");
        Assert.Single(first.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey=="major-mana-potion"&&e.Amount>0m);
        AssertReplay(first,replay);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedThresholdWinsEqualInventoryAndLegacyOnlyCopiesItsThreshold(bool shaman)
    {
        var f=new Fixture(shaman);f.Start(30m);f.LegacyCharges(1);
        // End before the first damaging cast completes, so the initial Mana
        // percentage remains the threshold discriminator and the deficit fits.
        if(shaman){f.Shaman.DurationSeconds=1.5m;f.Shaman.MajorManaPotionBelowManaPercent=20m;}
        else {f.Druid.DurationSeconds=1.5m;f.Druid.MajorManaPotionBelowManaPercent=20m;}
        f.Items.MajorManaPotionCharges=1;
        f.Items.MajorManaPotionBelowManaPercent=55m;
        var before=JsonSerializer.Serialize(f.Request);
        var shared=f.Run();
        Assert.Equal(1,shared.Potions);
        Assert.Equal(1,shared.Items["major-mana-potion"]);
        var gain=Assert.Single(shared.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&
            e.AbilityKey=="major-mana-potion"&&e.Amount>0m);
        Assert.Equal(0m,gain.TimeSeconds);
        Assert.DoesNotContain(shared.Run.Timeline,e=>e.Type==CombatEventType.Damage);
        Assert.InRange(gain.Amount!.Value,1350m,2250m);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));

        f.Items.MajorManaPotionCharges=0;
        before=JsonSerializer.Serialize(f.Request);
        var legacyOnly=f.Run();
        Assert.Equal(0,legacyOnly.Potions);Assert.Empty(legacyOnly.Items);
        Assert.DoesNotContain(legacyOnly.Run.Timeline,e=>e.AbilityKey=="major-mana-potion");
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));

        f.Start(10m);
        var belowLegacy=f.Run();
        Assert.Equal(1,belowLegacy.Potions);
        Assert.Single(belowLegacy.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&
            e.AbilityKey=="major-mana-potion"&&e.Amount>0m);
        Assert.Equal(0,f.Items.MajorManaPotionCharges);
        Assert.Equal(55m,f.Items.MajorManaPotionBelowManaPercent);
    }

    [Theory]
    [InlineData(false,1)]
    [InlineData(true,0)]
    public void PreparedBalancePotionIsInstantAndDoesNotDelayFillerWhileLegacyTimingIsPreserved(
        bool prepared,int potionCompletion)
    {
        var f=new Fixture(false);f.Start(0m);f.LegacyCharges(1);
        f.Druid.DurationSeconds=5m;
        if(!prepared)f.Druid.Level60Baseline=null;
        f.Character.BaseStats.Values=new() { ["intellect"]=200m };
        var result=f.Run();
        var start=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted&&
            e.AbilityKey=="major-mana-potion");
        var complete=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted&&
            e.AbilityKey=="major-mana-potion");
        var gain=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&
            e.AbilityKey=="major-mana-potion"&&e.Amount>0m);
        Assert.Equal(0m,start.TimeSeconds);
        Assert.Equal(potionCompletion,complete.TimeSeconds);Assert.Equal(complete.TimeSeconds,gain.TimeSeconds);
        var wrathStart=result.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastStarted&&e.AbilityKey=="wrath");
        var wrathComplete=result.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="wrath");
        // The common executor reserves one microsecond after instant off-GCD actions.
        Assert.Equal(prepared?0.000001m:potionCompletion,wrathStart.TimeSeconds);
        Assert.Equal(wrathStart.TimeSeconds+2m,wrathComplete.TimeSeconds);
    }

    [Fact]
    public void PreparedBalancePotionIgnoresActiveNaturesGraceAndLeavesNextCastAvailable()
    {
        var f=new Fixture(false);f.Start(45m);
        f.Druid.DurationSeconds=6m;
        f.Druid.BaseSpellHitPercent=100m;f.Druid.SpellCritPercent=100m;
        f.Druid.Talents.Ranks["natures-grace"]=1;
        f.Items.MajorManaPotionCharges=1;
        // At pull the deficit is below the item's maximum restore. Wrath's first
        // cost at 2s opens enough room; its guaranteed crit activates Grace.
        var result=new BalanceDruidSimRunner().Run(f.Druid);
        Assert.True(result.NaturesGraceProcs>0);
        var firstCrit=result.Run.Timeline.First(e=>e.Type==CombatEventType.Damage&&
            e.AbilityKey=="wrath"&&e.ResultKey=="critical");
        Assert.Equal(2m,firstCrit.TimeSeconds);
        var itemStart=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted&&
            e.AbilityKey=="major-mana-potion");
        var itemComplete=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted&&
            e.AbilityKey=="major-mana-potion");
        var gain=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&
            e.AbilityKey=="major-mana-potion"&&e.Amount>0m);
        Assert.Equal(2m,itemStart.TimeSeconds);Assert.Equal(itemStart.TimeSeconds,itemComplete.TimeSeconds);
        Assert.Equal(itemStart.TimeSeconds,gain.TimeSeconds);
        var starts=result.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted&&e.AbilityKey=="wrath").ToArray();
        var completes=result.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="wrath").ToArray();
        Assert.Equal(0m,starts[0].TimeSeconds);Assert.Equal(2.000001m,starts[1].TimeSeconds);
        Assert.Equal(starts[1].TimeSeconds+1.8m,completes[1].TimeSeconds);
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void ZeroOrDisabledPreparedPotionHasNoEvents(bool shaman,bool disabled)
    {
        var f=new Fixture(shaman);f.Start(0m);
        if(disabled)
        {
            f.LegacyCharges(1);f.Items.MajorManaPotionCharges=1;
            f.Items.DisabledItemKeys=["major-mana-potion"];
        }
        var result=f.Run();
        Assert.Equal(0,result.Potions);Assert.Empty(result.Items);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="major-mana-potion");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingInventoriesRejectInsteadOfAddingCharges(bool shaman)
    {
        var f=new Fixture(shaman);f.LegacyCharges(1);f.Items.MajorManaPotionCharges=2;
        var before=JsonSerializer.Serialize(f.Request);
        var error=Assert.Throws<ArgumentException>(()=>f.Run());
        Assert.Equal("Conflicting Major Mana Potion inventories.",error.Message);
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyAliasesCannotLeakIntoCallerSharedInventory(bool shaman)
    {
        var f=new Fixture(shaman);f.Start(0m);f.LegacyCharges(1);
        Assert.Equal(1,f.Run().Potions);
        Assert.Equal(0,f.Items.MajorManaPotionCharges);
        f.LegacyCharges(0);
        Assert.Equal(0,f.Run().Potions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoBaselinePreservesHistoricalManaAndManualHealth(bool shaman)
    {
        var f=new Fixture(shaman);
        if(shaman)f.Shaman.Level60Baseline=null;else f.Druid.Level60Baseline=null;
        f.Character.BaseStats.Values=new() { ["intellect"]=200m,["mana"]=125m,["health"]=250m,["stamina"]=100m };
        var result=f.Run();
        Assert.Equal(4076m,result.Mana);Assert.Equal(4000m,result.Health);
        Assert.Equal(1630.4m,result.Run.Summary.ActorSummaries[f.ClassKey].StartingResources["mana"]);
        Assert.DoesNotContain(result.Notes,n=>n.StartsWith("Prepared level-60 baseline"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BaselinesRemainExplicitAndStrictModeFailsClosed(bool shaman)
    {
        var f=new Fixture(shaman);
        f.Baseline.ClassKey=shaman?"druid":"shaman";
        Assert.Throws<ArgumentException>(()=>f.Run());
        f=new Fixture(shaman);
        if(shaman){f.Shaman.Level60Baseline=null;f.Shaman.UseReviewedLevel60Baseline=true;}
        else {f.Druid.Level60Baseline=null;f.Druid.UseReviewedLevel60Baseline=true;}
        Assert.Throws<ArgumentException>(()=>f.Run());
        f=new Fixture(shaman);
        f.Baseline.VerificationStatus="provisional-documented-proxy";
        Assert.Throws<ArgumentException>(()=>f.Run());
        if(shaman)f.Shaman.UseProvisionalLevel60Baseline=true;else f.Druid.UseProvisionalLevel60Baseline=true;
        Assert.Contains(f.Run().Notes,n=>n.Contains("provisional-documented-proxy"));
    }

    private static StatCollection Stats(StringComparer comparer)=>new() {
        Values=new(comparer) { ["INTELLECT"]=123m }
    };
    private static StatCollection[] AllStats(CharacterProfile p)=>[
        p.BaseStats,p.Equipment.Items[0].Stats,p.Equipment.Items[0].Modifiers[0].Stats,
        p.Talents.Selections[0].Stats,p.Buffs.Selections[0].Stats
    ];

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void RequestSnapshotsIsolateMutableGraphsAndPreserveComparers(bool shaman,bool ignoreCase)
    {
        var f=new Fixture(shaman);
        var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var original=f.Character;
        original.BaseStats=Stats(comparer);
        original.Equipment.Items=[new() {Stats=Stats(comparer),Modifiers=[new() {Stats=Stats(comparer)}]}];
        original.Talents.Selections=[new() {Stats=Stats(comparer)}];
        original.Buffs.Selections=[new() {Stats=Stats(comparer)}];
        CharacterProfile copy;
        if(shaman)
        {
            f.Shaman.Talents.Ranks=new(comparer) { ["ANCESTRAL-KNOWLEDGE"]=5 };
            var snapshot=CasterSimulationRequestSnapshot.Create(f.Shaman);
            copy=snapshot.Character;
            Assert.Same(comparer,snapshot.Talents.Ranks.Comparer);
            Assert.Equal(ignoreCase?5:0,snapshot.Talents.Rank("ancestral-knowledge"));
            snapshot.Talents.Ranks.Clear();snapshot.CustomPriority.Clear();
            snapshot.CallOfElementsTotems.Clear();snapshot.CallOfAncestorsTotems.Clear();snapshot.CallOfSpiritsTotems.Clear();
            Assert.Single(f.Shaman.Talents.Ranks);Assert.Single(f.Shaman.CustomPriority);
            Assert.NotEmpty(f.Shaman.CallOfElementsTotems);Assert.NotEmpty(f.Shaman.CallOfAncestorsTotems);Assert.NotEmpty(f.Shaman.CallOfSpiritsTotems);
            snapshot.Level60Baseline!.Intellect=1m;
        }
        else
        {
            f.Druid.Talents.Ranks=new(comparer) { ["IMPROVED-WRATH"]=5 };
            var snapshot=CasterSimulationRequestSnapshot.Create(f.Druid);
            copy=snapshot.Character;
            Assert.Same(comparer,snapshot.Talents.Ranks.Comparer);
            Assert.Equal(ignoreCase?5:0,snapshot.Talents.Rank("improved-wrath"));
            snapshot.Talents.Ranks.Clear();snapshot.CustomPriority.Clear();
            Assert.Single(f.Druid.Talents.Ranks);Assert.Single(f.Druid.CustomPriority);
            snapshot.Level60Baseline!.Intellect=1m;
        }
        Assert.Equal(200m,f.Baseline.Intellect);
        var sources=AllStats(original);var copies=AllStats(copy);
        for(var i=0;i<sources.Length;i++)
        {
            Assert.NotSame(sources[i],copies[i]);Assert.NotSame(sources[i].Values,copies[i].Values);
            Assert.Same(sources[i].Values.Comparer,copies[i].Values.Comparer);
            Assert.Equal(ignoreCase?123m:0m,copies[i].Get("intellect"));
            copies[i].Values.Clear();Assert.Single(sources[i].Values);
        }
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void BaselineGenerationPreservesEveryContributorComparer(bool shaman,bool ignoreCase)
    {
        var f=new Fixture(shaman);
        var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        f.Character.Equipment.Items=[new() {Stats=Stats(comparer),Modifiers=[new() {Stats=Stats(comparer)}]}];
        f.Character.Talents.Selections=[new() {Stats=Stats(comparer)}];
        f.Character.Buffs.Selections=[new() {Stats=Stats(comparer)}];
        // Inspect the internal preparation boundary without widening the public API.
        var method=typeof(CasterSimulationRequestSnapshot).GetMethod("GenerateBaseline",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        var copy=Assert.IsType<CharacterProfile>(method.Invoke(null,[f.Character,f.Baseline]));
        Assert.Equal(200m,copy.BaseStats.Get("intellect"));
        Assert.Equal(999m,f.Character.BaseStats.Get("intellect"));
        var sources=AllStats(f.Character);var copies=AllStats(copy);
        for(var i=1;i<sources.Length;i++)
        {
            Assert.Same(comparer,copies[i].Values.Comparer);
            Assert.Equal(ignoreCase?123m:0m,copies[i].Get("intellect"));
            copies[i].Values.Clear();Assert.Single(sources[i].Values);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalCatalogBaselineRequiresExplicitOptInAndStaysProvisional(bool shaman)
    {
        var f=new Fixture(shaman);
        if(shaman){f.Shaman.Level60Baseline=null;f.Shaman.UseProvisionalLevel60Baseline=true;}
        else {f.Druid.Level60Baseline=null;f.Druid.UseProvisionalLevel60Baseline=true;}
        var result=f.Run();
        Assert.Contains(result.Notes,n=>n.Contains("provisional-documented-proxy"));
        Assert.Contains(result.Notes,n=>n.Contains("not Forever-verified"));
    }

    [Fact]
    public void NullMembersRemainNullAndInvalidInputsStillReject()
    {
        var d=new BalanceDruidSimRequest {Talents=null!,CustomPriority=null!};
        var ds=CasterSimulationRequestSnapshot.Create(d);
        Assert.Null(ds.Talents);Assert.Null(ds.CustomPriority);
        Assert.Throws<ArgumentException>(()=>new BalanceDruidSimRunner().Run(d));
        var s=new ElementalShamanSimRequest {CombatItems=null!,CallOfElementsTotems=null!};
        var ss=CasterSimulationRequestSnapshot.Create(s);
        Assert.Null(ss.CombatItems);Assert.Null(ss.CallOfElementsTotems);
        Assert.Throws<ArgumentException>(()=>new ElementalShamanSimRunner().Run(s));
        d=new();d.Character.BaseStats.Values=null!;d.Character.Equipment.Items=[null!];
        d.Character.Talents.Selections=[null!];d.Character.Buffs.Selections=null!;
        ds=CasterSimulationRequestSnapshot.Create(d);
        Assert.Null(ds.Character.BaseStats.Values);Assert.Null(ds.Character.Equipment.Items[0]);
        Assert.Null(ds.Character.Talents.Selections[0]);Assert.Null(ds.Character.Buffs.Selections);
        Assert.Throws<ArgumentNullException>(()=>new BalanceDruidSimRunner().Run(null!));
        Assert.Throws<ArgumentNullException>(()=>new ElementalShamanSimRunner().Run(null!));
    }
}
