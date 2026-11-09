using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PreparedCasterStaticCatalog
{
    private static readonly Type Support=typeof(BalanceDruidSimRunner).Assembly
        .GetType("WarcraftSim.Api.Simulation.ForeverCasterSoloSupport")!;
    private static object Invoke(string name,params object?[] args)
    {
        try { return Support.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args)!; }
        catch(TargetInvocationException e) when(e.InnerException is not null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw; }
    }
    private static ForeverLevel60BaselineObservation Baseline(bool shaman)=>new() {
        Race=shaman?"orc":"night-elf",ClassKey=shaman?"shaman":"druid",
        Strength=40m,Agility=40m,Stamina=100m,Intellect=200m,Spirit=100m,
        ObservedUnbuffedMaximumMana=5000m,ObservedUnbuffedMaximumHealth=3000m,
        ClientBuild="1.60.1.70707",SourceReference="controlled-test-observation",
        RecordedAtUtc="2026-11-09T00:00:00Z"
    };
    private sealed class Fixture
    {
        public bool Shaman {get;}
        public BalanceDruidSimRequest D {get;}=new();
        public ElementalShamanSimRequest S {get;}=new();
        public CharacterProfile Profile=>Shaman?S.Character:D.Character;
        public ForeverExternalModifierSelection External=>Shaman?S.ExternalModifiers:D.ExternalModifiers;
        public object Request=>Shaman?(object)S:D;
        public ForeverLevel60BaselineObservation Observation=>Shaman?S.Level60Baseline!:D.Level60Baseline!;
        public Fixture(bool shaman)
        {
            Shaman=shaman;
            Profile.Race=shaman?"orc":"night-elf";
            Profile.Equipment=new();Profile.Buffs=new();Profile.Talents=new();Profile.BaseStats=new();
            D.Level60Baseline=Baseline(false);S.Level60Baseline=Baseline(true);
            D.Talents.Ranks.Clear();S.Talents.Ranks.Clear();
            D.DurationSeconds=S.DurationSeconds=20m;
            D.StartingManaPercent=S.StartingManaPercent=40m;
            D.MajorManaPotionCharges=S.MajorManaPotionCharges=0;
            D.BaseSpellHitPercent=S.BaseSpellHitPercent=100m;
            D.SpellCritPercent=S.SpellCritPercent=0m;
            D.CustomPriority=["wrath"];S.CustomPriority=["lightning-bolt"];
            D.UseOmenOfClarity=false;D.UseInnervate=false;S.UseElementalMastery=false;
            D.Tuning.ManaRegenPerSecond=S.Tuning.ManaRegenPerSecond=0m;
        }
        public (decimal Mana,decimal Health,decimal Power,decimal EndingMana,SimulationRunResult Run,List<string> Notes) Run()
        {
            if(Shaman) {var r=new ElementalShamanSimRunner().Run(S);return(r.MaximumMana,r.MaximumHealth,r.SpellPower,r.EndingMana,r.Run,r.ModelingNotes);}
            var d=new BalanceDruidSimRunner().Run(D);return(d.MaximumMana,d.MaximumHealth,d.SpellPower,d.EndingMana,d.Run,d.ModelingNotes);
        }
        public SimulationActorState Actor(decimal multiplier=1m)=>
            ((SimulationContext,SimulationActorState,SimulationActorState,decimal,decimal))Invoke("Create",
                Profile,Shaman?"shaman":"druid",Shaman?"elemental":"balance",1,20m,true,
                4000m,200000m,40m,CancellationToken.None,Observation,multiplier,External,new List<string>()) is var x ? x.Item2 : throw new InvalidOperationException();
    }
    private static CharacterEquipmentItem Item(int id,string slot="chest",int? enchant=null,int? temporary=null)=>
        new() {ItemId=id,SlotKey=slot,EnchantSpellId=enchant,TemporaryEnchantSpellId=temporary};

    [Theory]
    [InlineData(false,10,29,0)]
    [InlineData(true,4,33,0)]
    [InlineData(true,15,27,100)]
    public void AuthoredBudgetsAndSelectedExternalModifiersApplyOnce(bool shaman,int intellect,int stamina,int health)
    {
        var f=new Fixture(shaman);var naked=f.Run();
        f.Profile.Equipment.Items=[new() {SlotKey="chest",ItemKey="authored-test-budget",
            Stats=new() {Values=new() {["intellect"]=intellect,["stamina"]=stamina,["health"]=health}}}];
        f.External.Buffs=["arcane-intellect","power-word-fortitude"];
        f.External.Consumables=["flask-of-distilled-wisdom","greater-arcane-elixir"];
        var before=JsonSerializer.Serialize(f.Request);var equipped=f.Run();
        Assert.Equal(naked.Mana+15m*(intellect+31m)+2000m,equipped.Mana);
        Assert.Equal(naked.Health+10m*(stamina+70m)+health,equipped.Health);
        Assert.Equal(35m,equipped.Power);
        Assert.Equal(equipped.Mana*.4m,equipped.Run.Summary.ActorSummaries[shaman?"shaman":"druid"].StartingResources["mana"]);
        Assert.DoesNotContain(equipped.Notes,x=>x.StartsWith("Prepared equipment")||x.StartsWith("Prepared enchant"));
        Assert.Contains(equipped.Notes,x=>x.Contains("Prepared modifier greater-arcane-elixir")&&x.Contains("provisional"));
        Assert.Contains(equipped.Notes,x=>x.Contains("Unsupported set bonuses remain inert"));
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
        Assert.Equal(JsonSerializer.Serialize(equipped.Run.Summary),JsonSerializer.Serialize(f.Run().Run.Summary));
    }

    [Theory]
    [InlineData(22749,30,0)]
    [InlineData(1219580,65,0)]
    [InlineData(1248607,55,0)]
    [InlineData(23804,0,22)]
    public void StaticEnchantDefinitionsDoNotGrantUnknownPreparedItemEligibility(int enchant,int power,int intellect)
    {
        var staff=ForeverEquipmentCatalog.FindItem(19102);
        var permanent=ForeverEquipmentCatalog.FindEnchant(enchant);
        var temporary=ForeverEquipmentCatalog.FindEnchant(25122);
        ForeverEquipmentCatalog.ValidateEnchantTarget(staff,permanent,"main-hand");
        ForeverEquipmentCatalog.ValidateEnchantTarget(staff,temporary,"main-hand");
        Assert.Equal((decimal)power,permanent.Stats.GetValueOrDefault("spell-power"));
        Assert.Equal((decimal)intellect,permanent.Stats.GetValueOrDefault("intellect"));
        Assert.Equal(1800m,temporary.DurationSeconds);
        var f=new Fixture(false);f.Profile.Equipment.Items=[Item(19102,"main-hand",enchant,25122)];
        Assert.Contains("unknown/pending",Assert.Throws<ArgumentException>(()=>f.Run()).Message);
        Assert.Null(staff.ClassRestrictionEvidence);
    }

    [Fact]
    public void EmbeddedCatalogRemainsPendingEvidenceWithoutChangingLegacyRecords()
    {
        foreach(var classKey in new[]{"druid","shaman"})
            foreach(var item in ForeverEquipmentCatalog.GetCatalog().Items)
            {
                Assert.Null(item.ClassRestrictionEvidence);
                Assert.Contains("unknown/pending",Assert.Throws<ArgumentException>(()=>
                    Invoke("ValidatePreparedItem",item,classKey)).Message);
            }
        Assert.Equal("1.60.1.70532",ForeverEquipmentCatalog.FindItem(19102).SourceBuild);
        Assert.Equal("1.60.1.70450",ForeverEquipmentCatalog.FindItem(22476).SourceBuild);
        Assert.Equal("1.60.1.70291",ForeverEquipmentCatalog.FindItem(22436).SourceBuild);
        Assert.All(ForeverEquipmentCatalog.GetCatalog().Sets.Where(x=>x.Key=="bonescythe-armor"),x=>Assert.False(x.Modeled));
        Assert.All(ForeverEquipmentCatalog.GetCatalog().Sets.Where(x=>x.Key=="cryptstalker-armor"),x=>Assert.False(x.Modeled));
    }

    [Theory]
    [InlineData(false,19102,"main-hand")]
    [InlineData(false,22476,"chest")]
    [InlineData(true,22476,"chest")]
    [InlineData(true,22436,"chest")]
    [InlineData(false,19682,"chest")]
    [InlineData(true,19682,"chest")]
    [InlineData(false,21712,"neck")]
    [InlineData(false,18820,"trinket1")]
    [InlineData(true,18820,"trinket1")]
    [InlineData(false,22954,"trinket1")]
    [InlineData(true,22954,"trinket1")]
    [InlineData(true,19102,"main-hand")]
    [InlineData(false,12602,"off-hand")]
    [InlineData(true,12602,"off-hand")]
    [InlineData(false,22436,"chest")]
    public void UnsupportedItemsAndAmbiguousClassEligibilityFailClosed(bool shaman,int id,string slot)
    {
        var f=new Fixture(shaman);f.Profile.Equipment.Items=[Item(id,slot)];
        var before=JsonSerializer.Serialize(f.Request);
        Assert.Throws<ArgumentException>(()=>f.Run());Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData("classes")][InlineData("classification")][InlineData("subclass")]
    [InlineData("source")][InlineData("build")][InlineData("url")][InlineData("verification")]
    [InlineData("hand")][InlineData("effects")][InlineData("unknown-weapon")]
    public void GuardRejectsIncompleteOrUnsupportedMetadataWithoutMutatingCatalog(string missing)
    {
        var item=SyntheticEquipmentRestrictionFixture.Item();
        switch(missing)
        {
            case "classes":item.AllowedClasses=[];break;
            case "classification":item.ItemClass="";break;
            case "subclass":item.ItemSubclass="";break;
            case "source":item.SourceStatus="classic-proxy-not-verified-forever";break;
            case "build":item.SourceBuild="";break;
            case "url":item.SourceUrl="";break;
            case "verification":item.VerificationStatus="metadata-only";break;
            case "hand":item.ItemClass="weapon";item.Weapon=new() {Type="staff",Hand="",SpeedSeconds=3m};break;
            case "unknown-weapon":item.ItemClass="weapon";item.ItemSubclass="unknown";item.Weapon=new() {Type="unknown",Hand="two-hand",SpeedSeconds=3m};break;
            case "effects":item.Effects.Add(new() {Kind="proc"});break;
        }
        Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,"druid"));
        Invoke("ValidatePreparedItem",SyntheticEquipmentRestrictionFixture.Item(),"druid");
    }

    [Theory]
    [InlineData("forged")][InlineData("modifier")][InlineData("duplicate-slot")]
    [InlineData("wrong-slot")][InlineData("off-hand")][InlineData("raw-mix")]
    [InlineData("wrong-enchant")][InlineData("proc-enchant")][InlineData("temporary-placement")]
    public void UnknownCatalogEligibilityNeverAllowsInvalidPreparedSelections(string kind)
    {
        var f=new Fixture(false);f.Profile.Equipment.Items=[Item(19102,"main-hand")];
        var item=f.Profile.Equipment.Items[0];
        switch(kind)
        {
            case "forged":item.Stats.Set("spell-power",999m);break;
            case "modifier":item.Modifiers.Add(new());break;
            case "duplicate-slot":f.Profile.Equipment.Items.Add(Item(19102,"MAIN-HAND"));break;
            case "wrong-slot":item.SlotKey="chest";break;
            case "off-hand":item.SlotKey="off-hand";break;
            case "raw-mix":f.Profile.Equipment.Items.Add(new() {SlotKey="head",ItemKey="raw",Stats=new()});break;
            case "wrong-enchant":item.EnchantSpellId=20028;break;
            case "proc-enchant":item.EnchantSpellId=20034;break;
            case "temporary-placement":item.EnchantSpellId=25122;break;
        }
        Assert.Throws<ArgumentException>(()=>f.Run());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void DuplicateExternalContributorsAndUnsupportedTargetEffectsReject(bool shaman)
    {
        var f=new Fixture(shaman);f.External.Buffs=["arcane-intellect","arcane-intellect"];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.External.Buffs=["arcane-intellect"];
        f.Profile.Buffs.Selections=[new() {BuffKey="cp173-arcane-intellect",Stats=new() {Values=new() {["intellect"]=31m}}}];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.Profile.Buffs.Selections.Clear();f.External.Buffs.Clear();
        f.External.TargetDebuffs=["curse-of-elements"];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.External.TargetDebuffs.Clear();f.External.Consumables=["baked-salmon"];
        f.D.DurationSeconds=f.S.DurationSeconds=901m;
        Assert.Throws<ArgumentException>(()=>f.Run());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Mp5AliasesInOneSourceCountOnceWhileIndependentSourcesStack(bool shaman)
    {
        var f=new Fixture(shaman);var naked=f.Run();
        f.Profile.Buffs.Selections=[new() {BuffKey="mp5-test",Stats=new() {Values=new() {["mana-per-5"]=20m}}}];
        var canonical=f.Run();
        f.Profile.Buffs.Selections[0].Stats.Values=new() {["mp5"]=20m};var alias=f.Run();
        f.Profile.Buffs.Selections[0].Stats.Set("mana-per-5",20m);var both=f.Run();
        Assert.Equal(naked.EndingMana+80m,canonical.EndingMana);
        Assert.Equal(canonical.EndingMana,alias.EndingMana);Assert.Equal(alias.EndingMana,both.EndingMana);
        f.External.Consumables=["greater-mageblood-elixir"];
        Assert.Equal(40m,f.Actor().Stats.Get("mp5"));
        Assert.Equal(naked.EndingMana+160m,f.Run().EndingMana);
        f.Profile.Buffs.Selections[0].Stats.Set("mana-per-5",21m);
        Assert.Throws<ArgumentException>(()=>f.Run());
    }

    [Fact]
    public void AuthoredMp5AndAncestralKnowledgeDeriveAfterAllStaticContributions()
    {
        var f=new Fixture(true);f.Profile.Equipment.Items=[new() {SlotKey="chest",ItemKey="authored-test-budget",
            Stats=new() {Values=new() {["intellect"]=25m,["stamina"]=27m,["mana-per-5"]=4m}}}];
        f.External.Buffs=["arcane-intellect"];f.External.Consumables=["elixir-of-the-owl"];
        f.S.Talents.Ranks["ancestral-knowledge"]=5;
        var actor=f.Actor(1.1m);
        Assert.Equal(309.1m,actor.Stats.Get("intellect"));
        Assert.Equal(4m,actor.Stats.Get("mp5"));Assert.Equal(0m,actor.Stats.Get("mana-per-5"));
        Assert.Equal(309.1m/ForeverPrimarySpellStatProfiles.GetIntellectPerSpellCriticalPercent("shaman",60)+2m,
            actor.Stats.Get("spell-critical-chance-percent"));
        Assert.Equal(5000m+(309.1m-200m)*15m,f.Run().Mana);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void AdditionalProfileClonesPreserveComparersAndCallerIsolation(bool shaman)
    {
        var f=new Fixture(shaman);f.Profile.Equipment.Items=[new() {SlotKey="chest",ItemKey="authored-test-budget"}];
        f.External.Buffs=["arcane-intellect"];
        var comparer=StringComparer.Ordinal;
        f.Profile.Talents.Selections=[new() {TalentKey="authored-talent",Stats=new() {Values=new(comparer) {["INTELLECT"]=12m}}}];
        f.Profile.Buffs.Selections=[new() {BuffKey="authored-buff",Stats=new() {Values=new(comparer) {["SPELL-POWER"]=15m}}}];
        var before=JsonSerializer.Serialize(f.Request);
        var copy=(CharacterProfile)Invoke("PrepareProfile",f.Profile,f.Observation,f.External,20m,new List<string>());
        Assert.Same(comparer,copy.Talents.Selections[0].Stats.Values.Comparer);
        Assert.Same(comparer,copy.Buffs.Selections[0].Stats.Values.Comparer);
        Assert.Equal(0m,copy.Talents.Selections[0].Stats.Get("intellect"));
        Assert.Equal(0m,copy.Buffs.Selections[0].Stats.Get("spell-power"));
        copy.Talents.Selections[0].Stats.Values.Clear();copy.Buffs.Selections[0].Stats.Values.Clear();
        copy.Equipment.Items[0].Stats.Values.Clear();
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
    }

    [Theory]
    [InlineData("moonfire")][InlineData("insect-swarm")][InlineData("faerie-fire")]
    [InlineData("barkskin")][InlineData("innervate")]
    public void PreparedDeclaredInstantClassSpellsRemainInstantWithClassGcd(string key)
    {
        var f=new Fixture(false);f.D.StartingManaPercent=30m;f.D.CustomPriority=[key,"wrath"];
        f.D.Talents.Ranks["insect-swarm"]=1;
        f.Profile.Buffs.Selections=[new() {BuffKey="haste",Stats=new() {Values=new() {["haste-percent"]=100m}}}];
        var r=f.Run();
        var start=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey==key);
        var complete=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted&&x.AbilityKey==key);
        var wrath=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="wrath");
        Assert.Equal(0m,start.TimeSeconds);Assert.Equal(start.TimeSeconds,complete.TimeSeconds);
        Assert.Equal(1.5m,wrath.TimeSeconds);
        f.D.Level60Baseline=null;f.Profile.BaseStats.Set("intellect",200m);
        r=f.Run();complete=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted&&x.AbilityKey==key);
        Assert.Equal(1m,complete.TimeSeconds);
    }

    [Fact]
    public void PreparedHurricaneStartsInstantlyAndStaticHasteDoesNotChangeChannelCadence()
    {
        var f=new Fixture(false);f.D.StartingManaPercent=100m;f.D.DurationSeconds=11m;
        f.D.CustomPriority=["hurricane"];f.D.AdditionalTargets=1;
        f.Profile.Equipment.Items=[new() {SlotKey="main-hand",ItemKey="authored-channel-budget",
            Stats=new() {Values=new() {["spell-power"]=91m,["intellect"]=25m,["spell-critical-chance-percent"]=1m}}}];
        f.Profile.Buffs.Selections=[new() {BuffKey="haste",Stats=new() {Values=new() {["cast-haste-percent"]=100m}}}];
        var result=f.Run();
        var ticks=result.Run.Timeline.Where(x=>x.Type==CombatEventType.Damage&&x.AbilityKey=="hurricane"&&x.TargetActorKey=="target").ToArray();
        Assert.Equal(new[]{2m,4m,6m,8m,10m},ticks.Select(x=>x.TimeSeconds).ToArray());
        Assert.Equal(5m,result.Run.CustomMetrics["balance-hurricane-secondary-hits"]);
    }

    [Theory]
    [InlineData(false,false)][InlineData(false,true)]
    [InlineData(true,false)][InlineData(true,true)]
    public void EnchantIdsWithoutCatalogItemRejectOnlyOnPreparedPath(bool shaman,bool temporary)
    {
        var f=new Fixture(shaman);
        f.Profile.Equipment.Items=[new() {SlotKey="main-hand",ItemKey="raw",
            EnchantSpellId=temporary?null:20034,TemporaryEnchantSpellId=temporary?25122:null}];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.D.Level60Baseline=f.S.Level60Baseline=null;
        f.Profile.BaseStats.Set("intellect",200m);
        Assert.Equal(4076m,f.Run().Mana);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void AuthoredAndSelectedStatsMatchEquivalentBudgetIncludingRolls(bool shaman)
    {
        var f=new Fixture(shaman);var authored=new Fixture(shaman);
        f.D.BaseSpellHitPercent=f.S.BaseSpellHitPercent=70m;
        authored.D.BaseSpellHitPercent=authored.S.BaseSpellHitPercent=70m;
        f.External.Buffs=["arcane-intellect"];f.External.Consumables=["elixir-of-the-owl"];
        f.Profile.Equipment.Items=[new() {SlotKey="chest",ItemKey="authored-roll-budget",Stats=new() {Values=new() {
            ["intellect"]=25m,["stamina"]=shaman?27m:0m,["mana-per-5"]=shaman?4m:0m,
            ["spell-power"]=shaman?0m:91m,["spell-critical-chance-percent"]=shaman?0m:1m}}}];
        // Both paths have the same independent authored hit/rating source.
        f.Profile.Buffs.Selections=[new() {BuffKey="roll-budget",Stats=new() {Values=new() {
            ["hit-rating"]=50m,["spell-hit-chance-percent"]=3m,["spell-hit-percent"]=2m}}}];
        authored.Profile.Buffs.Selections=[new() {BuffKey="equivalent-budget",Stats=new() {Values=new() {
            ["intellect"]=shaman?109.1m:81m,["spell-power"]=shaman?0m:91m,
            ["spell-critical-chance-percent"]=shaman?2m:3m,
            ["mp5"]=shaman?4m:0m,["stamina"]=shaman?27m:0m,
            ["hit-rating"]=50m,["spell-hit-chance-percent"]=3m,["spell-hit-percent"]=2m}}}];
        if(shaman)f.S.Talents.Ranks["ancestral-knowledge"]=5;
        // AK's effective Intellect and derived crit match an identical raw
        // budget without AK. Selected stats were captured before spell rolls.
        var actual=f.Run();var expected=authored.Run();
        Assert.Equal(expected.Mana,actual.Mana);Assert.Equal(expected.Health,actual.Health);
        Assert.Equal(expected.Power,actual.Power);Assert.Equal(expected.EndingMana,actual.EndingMana);
        Assert.Equal(expected.Run.Summary.DamageDone,actual.Run.Summary.DamageDone);
        Assert.Equal(expected.Run.Summary.DamageDoneByAbility,actual.Run.Summary.DamageDoneByAbility);
        Assert.Equal(expected.Run.ProcessedEventCount,actual.Run.ProcessedEventCount);
        object[] Events(SimulationRunResult r)=>r.Timeline.Select(e=>(object)new {
            e.TimeSeconds,e.Type,e.AbilityKey,e.EffectKey,e.Amount,e.ResultKey,e.TargetActorKey}).ToArray();
        Assert.Equal(Events(expected.Run),Events(actual.Run));
        Assert.Equal(8m,f.Actor(shaman?1.1m:1m).Stats.Get("spell-hit-chance-percent"));
    }

    [Fact]
    public void PreparedStaticHasteComposesWithGraceAndEclipseWithoutChangingClassGcd()
    {
        var f=new Fixture(false);f.D.StartingManaPercent=100m;f.D.DurationSeconds=8m;
        f.D.CustomPriority=[];f.D.UseMoonfire=false;f.D.UseInsectSwarm=false;
        f.D.Talents.Ranks["eclipse"]=3;f.D.Talents.Ranks["natures-grace"]=1;
        f.D.SpellCritPercent=100m;f.D.ConserveManaForEncounter=false;
        f.Profile.Buffs.Selections=[new() {BuffKey="haste",Stats=new() {Values=new() {["haste-percent"]=100m}}}];
        var r=new BalanceDruidSimRunner().Run(f.D);
        var wrath=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted&&x.AbilityKey=="wrath");
        var starStart=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="starfire");
        var starEnd=r.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted&&x.AbilityKey=="starfire");
        Assert.Equal(1m,wrath.TimeSeconds);Assert.Equal(1.5m,starStart.TimeSeconds);
        Assert.Equal(1.35m,starEnd.TimeSeconds-starStart.TimeSeconds);
        Assert.True(r.EclipseEmpoweredStarfires>0);Assert.True(r.NaturesGraceProcs>0);
    }

    [Theory]
    [InlineData(false,"MANA-PER-5","mp5")]
    [InlineData(true,"MANA-PER-5","mp5")]
    [InlineData(false," mana-per-5 "," MP5 ")]
    [InlineData(true," mana-per-5 "," MP5 ")]
    public void Mp5CanonicalizationMatchesCoreAndPreservesCallerComparer(bool ignoreCase,string first,string second)
    {
        var f=new Fixture(false);var comparer=ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        f.Profile.Buffs.Selections=[new() {BuffKey="raw-regen",Stats=new() {
            Values=new(comparer) {[first]=20m,[second]=20m}}}];
        var before=JsonSerializer.Serialize(f.Request);
        var copy=(CharacterProfile)Invoke("PrepareProfile",f.Profile,f.Observation,f.External,20m,new List<string>());
        Assert.Same(comparer,copy.Buffs.Selections[0].Stats.Values.Comparer);
        Assert.Single(copy.Buffs.Selections[0].Stats.Values);Assert.Equal(20m,copy.Buffs.Selections[0].Stats.Get("mp5"));
        Assert.Equal(20m,f.Actor().Stats.Get("mp5"));
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
        Assert.Same(comparer,f.Profile.Buffs.Selections[0].Stats.Values.Comparer);
    }

    [Theory]
    [InlineData("mp5","MP5")][InlineData("mp5"," mp5 ")]
    [InlineData("mana-per-5"," MANA-PER-5 ")]
    public void CanonicalRegenKeyCollisionsRemainInvalid(string first,string second)
    {
        var f=new Fixture(false);
        f.Profile.Buffs.Selections=[new() {BuffKey="invalid-regen",Stats=new() {
            Values=new(StringComparer.Ordinal) {[first]=20m,[second]=20m}}}];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.D.Level60Baseline=null;f.Profile.BaseStats.Set("intellect",200m);
        Assert.Throws<ArgumentException>(()=>f.Run());
    }

    [Theory]
    [InlineData(" arcane-intellect ","arcane-intellect",false)]
    [InlineData(" CP173-ARCANE-INTELLECT ","arcane-intellect",false)]
    [InlineData(" flask-of-the-titans ","flask-of-supreme-power",true)]
    [InlineData(" cp173-elixir-of-greater-intellect ","elixir-of-the-owl",true)]
    public void KnownRawBuffAliasesAndExclusiveGroupsCannotDoubleContribute(string raw,string selected,bool consumable)
    {
        var f=new Fixture(false);
        f.Profile.Buffs.Selections=[new() {BuffKey=raw,Stats=new() {Values=new() {["intellect"]=31m}}}];
        if(consumable)f.External.Consumables=[selected];else f.External.Buffs=[selected];
        Assert.Throws<ArgumentException>(()=>f.Run());
        f.Profile.Buffs.Selections[0].IsEnabled=false;
        f.Run();
        f.Profile.Buffs.Selections[0].IsEnabled=true;
        f.External.Consumables.Clear();f.External.Buffs.Clear();
        f.Run(); // A raw-only authored budget remains supported.
        f.D.Level60Baseline=null;f.Profile.BaseStats.Set("intellect",200m);
        Assert.Equal(4541m,f.Run().Mana);
    }

    [Theory]
    [InlineData(0L)][InlineData(-1L)][InlineData(-2L)][InlineData(65535L)]
    [InlineData(4294967295L)][InlineData(32L)]
    public void UnattestedMasksStayPendingUntilValidSyntheticEvidenceArrives(long mask)
    {
        var item=SyntheticEquipmentRestrictionFixture.Item();
        item.ClassRestrictionEvidence!.RawMask=mask;
        item.ClassRestrictionEvidence.Status="unknown";
        item.ClassRestrictionEvidence.DataConfidence="unknown";
        Assert.Contains("unknown/pending",Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,"druid")).Message);
        item.ClassRestrictionEvidence=SyntheticEquipmentRestrictionFixture.Evidence(item.Id);
        Invoke("ValidatePreparedItem",item,"druid"); // Contract only; never installed in the live catalog.
    }

    [Theory]
    [InlineData("missing")][InlineData("null-mask")][InlineData("item")][InlineData("build")]
    [InlineData("digest")][InlineData("source")][InlineData("confidence")][InlineData("source-classes")]
    [InlineData("effective-classes")][InlineData("duplicate-source")][InlineData("status")]
    public void InconsistentSyntheticRestrictionEvidenceCannotAuthorizePreparedGear(string conflict)
    {
        var item=SyntheticEquipmentRestrictionFixture.Item();var e=item.ClassRestrictionEvidence!;
        switch(conflict)
        {
            case "missing":item.ClassRestrictionEvidence=null;break;
            case "null-mask":e.RawMask=null;break;
            case "item":e.ItemId++;break;
            case "build":e.SourceBuild="different-build";break;
            case "digest":e.SourceSha256="not-a-digest";break;
            case "source":e.SourceKind="proficiency-list";break;
            case "confidence":e.DataConfidence="verified";break;
            case "source-classes":e.SourceAllowedClasses=["mage"];break;
            case "effective-classes":item.AllowedClasses=[];break;
            case "duplicate-source":e.SourceAllowedClasses=["druid","DRUID"];break;
            case "status":e.Status="unrestricted";break;
        }
        Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,"druid"));
    }

    [Theory]
    [InlineData("druid",1024L)][InlineData("shaman",64L)]
    public void SourceRestrictionsDoNotInventPreparedWeaponProficiency(string classKey,long mask)
    {
        var item=SyntheticEquipmentRestrictionFixture.Item(weapon:true);
        item.AllowedClasses=[classKey];item.ClassRestrictionEvidence!.RawMask=mask;
        item.ClassRestrictionEvidence.SourceAllowedClasses=[classKey];
        var error=Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,classKey));
        Assert.Contains("proficiency is pending separate evidence",error.Message);
    }

    [Fact]
    public void SourceMaskAndProficiencyListsRemainSeparateOnSyntheticReceipt()
    {
        var item=SyntheticEquipmentRestrictionFixture.Item();
        item.ItemClass="armor";item.ItemSubclass="leather";item.Weapon=null;item.AllowedSlots=["chest"];
        item.ClassRestrictionEvidence!.RawMask=1152L;item.ClassRestrictionEvidence.SourceAllowedClasses=["mage","druid"];
        Invoke("ValidatePreparedItem",item,"druid");
        Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,"mage"));
        item.AllowedClasses=["mage","druid"];
        Assert.Throws<ArgumentException>(()=>Invoke("ValidatePreparedItem",item,"druid"));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void NoBaselineAuthoredLegacyInputsReplayAndSelectedModifiersRequirePreparation(bool shaman)
    {
        var f=new Fixture(shaman);f.D.Level60Baseline=f.S.Level60Baseline=null;
        f.Profile.BaseStats.Values=new() {["intellect"]=200m,["mp5"]=20m};
        f.Profile.Equipment.Items=[new() {SlotKey="head",ItemKey="raw",Stats=new() {Values=new() {["spell-power"]=50m}}}];
        var before=JsonSerializer.Serialize(f.Request);var first=f.Run();var second=f.Run();
        Assert.Equal(4076m,first.Mana);Assert.Equal(50m,first.Power);
        Assert.Equal(JsonSerializer.Serialize(first.Run.Summary),JsonSerializer.Serialize(second.Run.Summary));
        Assert.Equal(before,JsonSerializer.Serialize(f.Request));
        f.External.Buffs=["arcane-intellect"];
        Assert.Throws<ArgumentException>(()=>f.Run());
    }
}
