using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using System.Reflection;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HistoricalCombatRogueTests
{
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount)
            => DamageMitigationResult.Unmitigated(rawAmount);
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static Dictionary<string,int> Ranks() => new(StringComparer.Ordinal) {
        ["improved-eviscerate"]=3,["lightning-reflexes"]=5,["puncturing-wounds"]=3,
        ["precision"]=3,["endurance"]=2,["flawless-execution"]=1,
        ["dual-wield-specialization"]=5,["blade-flurry"]=1,["hack-and-slash"]=5,
        ["weapon-expertise"]=2,["adrenaline-rush"]=1
    };
    private static RogueSimRequest Input(int points=31)
    {
        var input=RogueSimRequest.CreatePreset("combat");
        input.DurationSeconds=20;input.CaptureTimeline=true;
        input.Character.Race="human";input.UseProvisionalLevel60Baseline=true;
        input.Talents=new(){CombatPointsSpent=points,Ranks=Ranks()};
        if(points==30)input.Talents.Ranks.Remove("adrenaline-rush");
        return input;
    }
    private static ForeverLevel60SimulationRequest Envelope(RogueSimRequest input,
        Dictionary<string,int>? full=null) => new() {
        ClassKey="rogue",Specialization="combat",ItemizedTalentMode=true,
        ItemizedTalentRanks=full??new(input.Talents.Ranks,input.Talents.Ranks.Comparer),Rogue=input
    };
    private static JsonElement Json(object? value)=>JsonSerializer.SerializeToElement(value,Web);
    private static IActionResult Validate(object? talents)=>new ForeverSoloTalentController()
        .ValidateItemized("rogue","combat",Json(talents));
    private static async Task<JsonElement> Run(ForeverLevel60SimulationRequest request)
    {
        var before=JsonSerializer.Serialize(request,Web);
        var result=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
        return Json(result.Value);
    }
    private static async Task Reject(ForeverLevel60SimulationRequest request)
    {
        var before=JsonSerializer.Serialize(request,Web);
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(request,default));
        Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }
    private static string Stable(JsonElement value)
    {
        var ids=new Dictionary<string,string>();
        var fields=new HashSet<string>{"abilityExecutionId","autoAttackInstanceId","auraInstanceId","absorbInstanceId","forcedTargetInstanceId"};
        JsonNode? Visit(JsonElement item,bool timeline=false)=>item.ValueKind switch {
            JsonValueKind.Object => new JsonObject(item.EnumerateObject().OrderBy(x=>x.Name).Select(p=>
                new KeyValuePair<string,JsonNode?>(p.Name,timeline&&fields.Contains(p.Name)&&p.Value.ValueKind==JsonValueKind.String&&p.Value.GetString()!=Guid.Empty.ToString()
                    ? JsonValue.Create(ids.TryGetValue(p.Value.GetString()!,out var id)?id:ids[p.Value.GetString()!]="id-"+ids.Count)
                    : Visit(p.Value,timeline||p.Name=="timeline")))),
            JsonValueKind.Array => new JsonArray(item.EnumerateArray().Select(x=>Visit(x,timeline)).ToArray()),
            _=>JsonNode.Parse(item.GetRawText())
        };
        return Visit(value)!.ToJsonString();
    }

    [Fact]
    public void ExactlyElevenCombatNodesStayIsolatedFromTwelveAssassinationNodes()
    {
        Assert.True(ForeverHistoricalCasterTalentCatalog.Supports("rogue","combat"));
        var combat=ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue","combat");
        Assert.Equal(11,combat.Count);Assert.All(combat,x=>Assert.Equal("combat",x.Tree));
        Assert.Equal(Ranks().OrderBy(x=>x.Key),combat.ToDictionary(x=>x.Key,x=>x.MaxRank).OrderBy(x=>x.Key));
        var assassination=ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue","assassination");
        Assert.Equal(12,assassination.Count);Assert.All(assassination,x=>Assert.Equal("assassination",x.Tree));
        Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForClass("rogue"));
        Assert.Empty(ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue","subtlety"));
        var preset=Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().DefaultItemized("rogue","combat")).Value);
        Assert.Equal(31,preset.TotalRankedPoints);Assert.Equal(20,preset.UnspentPoints);
        Assert.Equal(Ranks().OrderBy(x=>x.Key),preset.Ranks.OrderBy(x=>x.Key));
        Assert.DoesNotContain("rogue",Json(Assert.IsType<OkObjectResult>(new ForeverLevel60SimulationController().Supported()).Value).GetProperty("classes").GetRawText());
        var rows=Json(Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().Catalog("rogue","combat")).Value).GetProperty("talents");
        Assert.Equal(11,rows.GetArrayLength());
        foreach(var row in rows.EnumerateArray())
        {
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceCommit,row.GetProperty("historicalValidationSourceCommit").GetString());
            Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,row.GetProperty("historicalValidationSourceStatus").GetString());
            if(new[]{"lightning-reflexes","endurance","puncturing-wounds"}.Contains(row.GetProperty("key").GetString()))
                Assert.Equal("inactive-in-fixed-sword-combat",row.GetProperty("simulationStatus").GetString());
        }
    }
    [Theory]
    [InlineData("improved-eviscerate",3,1,1,null,0)][InlineData("lightning-reflexes",5,1,3,null,0)]
    [InlineData("puncturing-wounds",3,2,1,null,0)][InlineData("precision",3,2,3,null,0)]
    [InlineData("endurance",2,3,1,null,0)][InlineData("flawless-execution",1,4,2,null,0)]
    [InlineData("dual-wield-specialization",5,4,3,"precision",3)][InlineData("blade-flurry",1,5,2,null,0)]
    [InlineData("hack-and-slash",5,5,3,null,0)][InlineData("weapon-expertise",2,6,2,"blade-flurry",1)]
    [InlineData("adrenaline-rush",1,7,2,null,0)]
    public void ExactHistoricalPositionsAndLocalParentConvention(string key,int cap,int row,int column,string? parent,int parentRank)
    {
        var e=Assert.IsType<ForeverHistoricalCasterTalentCatalog.Entry>(ForeverHistoricalCasterTalentCatalog.Find("rogue","combat",key));
        Assert.Equal((cap,row,column,parent,parentRank),(e.MaxRank,e.Row,e.Column,e.PrerequisiteKey,e.PrerequisiteRank));
    }
    [Theory][InlineData(31)][InlineData(30)]
    public async Task LegalPartialAllocationUsesExactExistingRunnerAndOwnedPreparation(int points)
    {
        var input=Input(points);var request=Envelope(input);var ranks=input.Talents.Ranks;
        var full=request.ItemizedTalentRanks;var before=JsonSerializer.Serialize(request,Web);
        var valid=Json(Assert.IsType<OkObjectResult>(Validate(input.Talents)).Value);
        Assert.Equal(points,valid.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(51-points,valid.GetProperty("remaining").GetInt32());
        var response=await Run(request);var direct=Json(new RogueSimRunner().Run(input));
        Assert.Equal(Stable(direct),Stable(response.GetProperty("sim")));
        Assert.Equal(Stable(response),Stable(await Run(request)));
        var directPreview=JsonNode.Parse(Json(RogueSimRunner.Preview(input)).GetRawText())!.AsObject();
        var strictPreview=JsonNode.Parse(response.GetProperty("preparation").GetProperty("preview").GetRawText())!.AsObject();
        directPreview.Remove("modelingNotes");strictPreview.Remove("modelingNotes");
        Assert.Equal(directPreview.ToJsonString(),strictPreview.ToJsonString());
        var preview=Json(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewRogue(input)).Value);
        Assert.Equal(100,preview.GetProperty("maximumEnergy").GetDecimal());
        Assert.Equal(response.GetProperty("preparation").GetProperty("preview").GetRawText(),preview.GetRawText());
        Assert.Equal(0,response.GetProperty("inputTiming").GetProperty("additionalInputLatencyMilliseconds").GetDecimal());
        var notes=response.GetProperty("talentModelingNotes").GetRawText();
        foreach(var phrase in new[]{"partial","unspent","not optimized","Precision","30/60","10/20","off-hand","full-parent","decoded","not current-client","discard"})Assert.Contains(phrase,notes);
        Assert.Same(ranks,input.Talents.Ranks);Assert.Same(full,request.ItemizedTalentRanks);
        Assert.Same(StringComparer.Ordinal,ranks.Comparer);Assert.Equal(before,JsonSerializer.Serialize(request,Web));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task PrecisionHistoricalCompatibilityAcceptsOnlyItsSourceRange(int rank)
    {
        var input=Input();input.Talents=new(){CombatPointsSpent=8+rank,Ranks=new() {
            ["improved-eviscerate"]=3,["lightning-reflexes"]=5,["precision"]=rank}};
        Assert.IsType<OkObjectResult>(Validate(input.Talents));await Run(Envelope(input));
        Assert.Equal(5,ForeverRogueTalentBuild.SupportedTalentMetadata.Single(x=>x.Key=="precision").MaxRank);
        Assert.Equal(5,ForeverRogueTalentBuild.Default("combat").Rank("precision"));
    }
    [Theory][InlineData(4)][InlineData(5)]
    public async Task PrecisionDoesNotClampLegacyRanksIntoHistoricalAcceptance(int rank)
    {
        var input=Input();input.Talents.Ranks["precision"]=rank;input.Talents.CombatPointsSpent+=rank-3;
        Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
        Assert.True(new RogueSimRunner().Run(input).DamageDone>0);
    }
    [Theory]
    [InlineData("aggression")][InlineData("deflection")][InlineData("riposte")][InlineData("improved-sinister-strike")]
    [InlineData("malice")][InlineData("mutilate")][InlineData("hemorrhage")][InlineData("unknown")]
    [InlineData("PRECISION")][InlineData(" precision ")]
    public async Task UnsupportedZeroAndNonzeroKeysRejectInEitherMap(string key)
    {
        foreach(var rank in new[]{0,1})
        {
            var input=Input();input.Talents.Ranks[key]=rank;
            Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
            input=Input();var full=Ranks();full[key]=rank;await Reject(Envelope(input,full));
        }
    }
    [Theory]
    [InlineData("rank")][InlineData("missing-full")][InlineData("missing-typed")][InlineData("full-zero")]
    [InlineData("typed-zero")][InlineData("aggregate")][InlineData("assassination-total")][InlineData("subtlety-total")]
    public async Task ExactMapAndDeclaredTotalsAreNotSynthetic(string fault)
    {
        var input=Input(30);var full=new Dictionary<string,int>(input.Talents.Ranks);
        switch(fault){
            case "rank":full["precision"]=2;break;
            case "missing-full":full.Remove("precision");break;
            case "missing-typed":input.Talents.Ranks.Remove("precision");break;
            case "full-zero":full["adrenaline-rush"]=0;break;
            case "typed-zero":input.Talents.Ranks["adrenaline-rush"]=0;break;
            case "aggregate":input.Talents.CombatPointsSpent=41;break;
            case "assassination-total":input.Talents.AssassinationPointsSpent=1;break;
            case "subtlety-total":input.Talents.SubtletyPointsSpent=1;break;
        }await Reject(Envelope(input,full));
    }
    [Fact]
    public async Task LaterRowsAndParentsNeedActualEarlierAllocations()
    {
        foreach(var key in Ranks().Keys.Except(new[]{"improved-eviscerate","lightning-reflexes"}))
        {
            var input=Input();input.Talents=new(){CombatPointsSpent=1,Ranks=new(){[key]=1}};
            Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
        }
        foreach(var key in new[]{"precision","blade-flurry"})
        {
            var input=Input(30);input.Talents.Ranks[key]--;input.Talents.CombatPointsSpent--;
            Assert.IsType<BadRequestObjectResult>(Validate(input.Talents));await Reject(Envelope(input));
        }
    }
    [Theory]
    [InlineData("{\"ranks\":{},\"ranks\":{}}")][InlineData("{\"ranks\":{},\"Ranks\":{}}")]
    [InlineData("{\"ranks\":{},\"totalPoints\":1}")][InlineData("{\"ranks\":{},\"unspentPoints\":50}")]
    [InlineData("{\"ranks\":{},\"totalPoints\":0,\"totalPoints\":0}")]
    [InlineData("{\"ranks\":{},\"totalPoints\":null}")][InlineData("{\"ranks\":{},\"totalPoints\":0.5}")]
    [InlineData("{\"ranks\":{},\"unspentPoints\":\"51\"}")][InlineData("{\"ranks\":{},\"inputLatencyMs\":0}")]
    [InlineData("{\"ranks\":{\"precision\":0,\"precision\":0}}")]
    [InlineData("{\"ranks\":{\"precision\":-1}}")][InlineData("{\"ranks\":{\"precision\":1.5}}")]
    [InlineData("{\"ranks\":{\"precision\":2147483648}}")][InlineData("{\"ranks\":null}")][InlineData("null")]
    public void RawCombatValidationRejectsDuplicateAndFalseDeclarations(string raw)
    {
        using var doc=JsonDocument.Parse(raw);var c=new ForeverSoloTalentController();
        Assert.IsType<BadRequestObjectResult>(c.ValidateItemized("rogue","combat",doc.RootElement));
        Assert.IsType<BadRequestObjectResult>(c.Validate("rogue","combat",doc.RootElement));
    }
    [Fact]
    public void RawCombatValidationAllowsTrueOptionalDeclarations()
    {
        using var doc=JsonDocument.Parse("{\"ranks\":{},\"totalPoints\":0,\"unspentPoints\":51}");
        Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().ValidateItemized("rogue","combat",doc.RootElement));
    }
    [Theory]
    [InlineData("wrapper-class")][InlineData("wrapper-spec")][InlineData("typed-spec")][InlineData("character-class")]
    [InlineData("character-spec")][InlineData("level")][InlineData("ruleset")][InlineData("character")]
    [InlineData("talents")][InlineData("ranks")][InlineData("tuning")][InlineData("baseline")][InlineData("baseline-modes")]
    [InlineData("equipment")][InlineData("items")][InlineData("entry")][InlineData("catalog-item")][InlineData("enchant")]
    [InlineData("buffs")][InlineData("generic-talents")][InlineData("modifiers")][InlineData("incoming")][InlineData("utility")]
    [InlineData("adds")][InlineData("dagger-flag")][InlineData("main-weapon")][InlineData("off-weapon")][InlineData("latency-envelope")]
    public async Task FixedSwordBoundaryRejectsContradictoryOrUnsupportedInputs(string fault)
    {
        var input=Input();var request=Envelope(input);
        switch(fault){
            case "wrapper-class":request.ClassKey="Rogue";break;
            case "wrapper-spec":request.Specialization="Combat";break;
            case "typed-spec":input.Specialization="assassination";break;
            case "character-class":input.Character.ClassKey="Rogue";break;
            case "character-spec":input.Character.SpecializationKey="Combat";break;
            case "level":input.Character.Level=59;break;
            case "ruleset":input.Character.RulesetKey="classic";break;
            case "character":input.Character=null!;break;
            case "talents":input.Talents=null!;break;
            case "ranks":input.Talents.Ranks=null!;break;
            case "tuning":input.Tuning=null!;break;
            case "baseline":input.UseProvisionalLevel60Baseline=false;break;
            case "baseline-modes":input.UseReviewedLevel60Baseline=true;break;
            case "equipment":input.Character.Equipment=null!;break;
            case "items":input.Character.Equipment.Items=null!;break;
            case "entry":input.Character.Equipment.Items.Add(null!);break;
            case "catalog-item":input.Character.Equipment.Items.Add(new(){ItemId=19102});break;
            case "enchant":input.Character.Equipment.Items.Add(new(){EnchantSpellId=1});break;
            case "buffs":input.Character.Buffs.Selections.Add(new(){BuffKey="unsupported"});break;
            case "generic-talents":input.Character.Talents.Selections.Add(new(){TalentKey="unsupported"});break;
            case "modifiers":input.Character.Equipment.Items.Add(new(){Modifiers=[new(){Stats=new(){Values=new(){["attack-power"]=1}}}]});break;
            case "incoming":input.EnableBossAttacks=true;break;
            case "utility":input.UseUtilityAbilities=true;break;
            case "adds":input.AdditionalTargets=1;break;
            case "dagger-flag":input.BothWeaponsDaggers=true;break;
            case "main-weapon":input.MainHandWeaponType="dagger";break;
            case "off-weapon":input.OffHandWeaponType="axe";break;
            case "latency-envelope":request.UnsupportedInputs=new(){["inputLatencyMs"]=Json(80)};break;
        }
        await Reject(request);
        if(fault is not ("wrapper-class" or "wrapper-spec" or "latency-envelope"))
            Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewRogue(input));
    }
    [Theory][InlineData(false,0)][InlineData(false,25)][InlineData(false,100)]
    [InlineData(true,0)][InlineData(true,25)][InlineData(true,100)]
    public async Task InitialResourcesAuthoredStatsAndOwnedPreparationRetainExistingPrecedence(bool observed,int percent)
    {
        var input=Input();input.StartingEnergyPercent=percent;input.RogueMaximumHealth=9999;
        input.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored-test-only",
            Stats=new(){Values=new(){["stamina"]=10,["health"]=50,["attack-power"]=100}}});
        if(observed){input.UseProvisionalLevel60Baseline=false;input.Level60Baseline=new(){
            ClassKey="rogue",Race="human",Strength=40,Agility=100,Stamina=100,Intellect=30,Spirit=40,
            ObservedUnbuffedMaximumHealth=3000,ClientBuild="controlled-test-build",
            SourceReference="synthetic-unit-test-not-client-data",RecordedAtUtc="2026-11-11T00:00:00Z"};}
        var prepared=HistoricalCombatRogueInputAdapter.Prepare(input);
        Assert.Equal(observed?3150:2423,prepared.Preview.MaximumHealth);
        Assert.Equal(100,prepared.Preview.MaximumEnergy);Assert.Equal(percent,prepared.Preview.StartingEnergy);
        Assert.Equal(RogueSimRunner.Preview(input).AttackPower,prepared.Preview.AttackPower);
        var result=await Run(Envelope(input));var sim=result.GetProperty("sim");
        Assert.Equal(prepared.Preview.MaximumHealth,sim.GetProperty("maximumHealth").GetDecimal());
        Assert.Equal(percent,sim.GetProperty("run").GetProperty("summary").GetProperty("actorSummaries").GetProperty("rogue").GetProperty("startingResources").GetProperty("energy").GetDecimal());
        input.Talents.Ranks["precision"]=5;input.Tuning.MainHandWeaponDamage=999;input.Character.Equipment.Items[0].Stats.Values["attack-power"]=999;
        Assert.Equal(3,prepared.Request.Talents.Rank("precision"));Assert.Equal(125,prepared.Request.Tuning.MainHandWeaponDamage);
        Assert.Equal(100,prepared.Request.Character.Equipment.Items[0].Stats.Get("attack-power"));
    }
    [Theory][InlineData("endurance")][InlineData("lightning-reflexes")][InlineData("puncturing-wounds")]
    public async Task InactiveLegalRankDifferencesCannotAffectFixedSwordCombat(string key)
    {
        var full=Input(30);var reduced=Input(30);reduced.Talents.Ranks[key]--;reduced.Talents.CombatPointsSpent--;
        var a=await Run(Envelope(full));var b=await Run(Envelope(reduced));
        Assert.Equal(Stable(a.GetProperty("sim")),Stable(b.GetProperty("sim")));
        Assert.DoesNotContain("backstab",a.GetProperty("sim").GetProperty("damageByAbility").GetRawText());
        Assert.DoesNotContain("mutilate",a.GetProperty("sim").GetProperty("damageByAbility").GetRawText());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void PrecisionSourceRanksAddExactlyOneHitPercentagePointPerRank(int rank)
    {
        var request=Input();request.Talents.Ranks["precision"]=rank;
        Func<RogueCombatProcessor?> combat=()=>null;
        var resolver=(ICombatRollResolver)Activator.CreateInstance(typeof(RogueSimRunner).GetNestedType("RogueRollResolver",BindingFlags.NonPublic)!,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[request,combat],null)!;
        foreach(var hand in new[]{WeaponHandKeys.MainHand,WeaponHandKeys.OffHand})
            for(var seed=1;seed<=200;seed++)
            {
                var context=new SimulationContext(new(){Seed=seed});
                var expected=(decimal)new Random(seed).NextDouble()*100m<
                    (hand==WeaponHandKeys.MainHand?request.Tuning.MainHandHitPercent:request.Tuning.OffHandHitPercent)+rank+4;
                var actor=new SimulationActorState{Key="rogue",TeamKey="raid"};actor.Stats.Set("hit-chance-percent",4);
                var target=new SimulationActorState{Key="target",TeamKey="enemy"};
                var result=resolver.Resolve(context,actor,target,new(){Key="test"},new(){
                    ResolutionType=CombatResolutionTypes.Melee,WeaponHandKey=hand,CanMiss=true,CanCrit=false});
                Assert.Equal(expected,result.Landed);
            }
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void ExistingDualWieldPenaltyAndApCompositionIsRetainedNotReinterpreted(int rank)
    {
        var request=Input();request.Talents.Ranks["dual-wield-specialization"]=rank;
        var weapons=(AutoAttackDefinition[])typeof(RogueSimRunner).GetMethod("CreateWeapons",BindingFlags.Static|BindingFlags.NonPublic)!
            .Invoke(null,[request.Tuning,140m,request.Talents])!;
        var off=Assert.Single(weapons,w=>w.WeaponHandKey==WeaponHandKeys.OffHand);
        Assert.Equal((80m+140m*1.8m/14m)*(.5m+rank*.05m),off.DamageEffect.MinimumValue);
        Assert.Equal(off.DamageEffect.MinimumValue,off.DamageEffect.MaximumValue);
    }
    [Fact]
    public async Task SwordHackAndSlashUsesSameHandPreparedApWithoutRecursiveProc()
    {
        var request=Input();request.DurationSeconds=60;request.StartInStealth=false;request.UseInstantPoison=false;
        request.Tuning.HackAndSlashExtraAttackPercentPerRank=20;request.Tuning.PhysicalCritPercent=0;
        request.Tuning.MainHandHitPercent=100;request.Tuning.OffHandHitPercent=100;
        var prepared=HistoricalCombatRogueInputAdapter.Prepare(request);
        var result=await Run(Envelope(request));var sim=result.GetProperty("sim");
        var damage=sim.GetProperty("run").GetProperty("timeline").EnumerateArray()
            .Where(x=>x.GetProperty("type").GetInt32()==(int)CombatEventType.Damage).ToArray();
        var extra=damage.Where(x=>x.GetProperty("abilityKey").GetString()=="hack-and-slash-extra").ToArray();
        Assert.NotEmpty(extra);
        foreach(var ev in extra)
        {
            var off=ev.GetProperty("weaponHandKey").GetString()==WeaponHandKeys.OffHand;
            var expected=off?(80m+prepared.Preview.AttackPower*1.8m/14m)*.75m:125m+prepared.Preview.AttackPower*2.6m/14m;
            Assert.Equal(expected*(ev.GetProperty("isCritical").GetBoolean()?2m:1m),ev.GetProperty("amount").GetDecimal());
        }
        Assert.Equal(extra.Length,sim.GetProperty("run").GetProperty("customMetrics").GetProperty("rogue-hack-and-slash-attacks").GetInt32());
        Assert.True(extra.Length<=damage.Count(x=>x.GetProperty("abilityKey").GetString()!="hack-and-slash-extra"));
        Assert.DoesNotContain(damage,x=>x.GetProperty("abilityKey").GetString()=="blade-flurry-cleave");
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void AdrenalineRushSettlesRealRegenerationAtTheFifteenSecondBoundary(bool rush)
    {
        var request=Input();request.StartInStealth=false;
        var context=new SimulationContext(new(){Seed=1,DurationSeconds=20,CaptureTimeline=true,PrimaryActorKey="rogue"});
        var actor=new SimulationActorState{Key="rogue",TeamKey="raid",MaximumHealth=1000};actor.InitializeHealth(1000);
        actor.AddResource(new ResourceState("energy",1000,0,10));context.AddActor(actor);
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),new NoMitigation());
        var combat=new RogueCombatProcessor(request,actor,new RotationProfile(),executor,auras,new AutoAttackProcessor(executor),[]);
        var definition=ForeverRogueSpellbook.Create("combat",request.Talents,request.Tuning,0,false).Single(x=>x.Key=="adrenaline-rush");
        Assert.Equal(15,definition.Effects.Single().DurationSeconds);
        var result=new SimulationEngine([auras,combat]).Run(context,ctx=>{
            if(rush)auras.ApplyAura(ctx,actor,new AuraDefinition{Key="adrenaline-rush",DurationSeconds=15},"rogue","adrenaline-rush","rush-aura");
        });
        Assert.Equal(rush?350:200,actor.Resources["energy"].Current);
        Assert.Equal(10,actor.Resources["energy"].RegenerationPerSecond);
        if(rush)Assert.Contains(result.Timeline,x=>x.Type==CombatEventType.AuraRemoved&&x.AbilityKey=="adrenaline-rush"&&x.TimeSeconds==15);
    }
    [Fact]
    public void BladeFlurryUsesExistingTwentyPercentHasteWithoutInventingCleave()
    {
        var request=Input();var context=new SimulationContext(new(){Seed=1});
        var actor=new SimulationActorState{Key="rogue"};actor.Stats.Set("haste-percent",10);
        var provider=(IAutoAttackTimingProvider)Activator.CreateInstance(typeof(RogueSimRunner).GetNestedType("RogueAutoAttackTimingProvider",BindingFlags.NonPublic)!,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[request.Tuning],null)!;
        var weapon=new AutoAttackDefinition{SwingIntervalSeconds=2.6m};
        Assert.Equal(2.6m/1.1m,provider.Resolve(context,actor,weapon).SwingIntervalSeconds);
        new AuraManager().ApplyAura(context,actor,new AuraDefinition{Key="blade-flurry",DurationSeconds=15},"rogue","blade-flurry","test");
        Assert.Equal(2.6m/1.3m,provider.Resolve(context,actor,weapon).SwingIntervalSeconds);
        var spell=ForeverRogueSpellbook.Create("combat",request.Talents,request.Tuning,0,false).Single(x=>x.Key=="blade-flurry");
        Assert.Equal(15,spell.Effects.Single().DurationSeconds);Assert.Equal(25,spell.ResourceCosts.Single().Amount);
    }
    [Theory][InlineData(0,35)][InlineData(1,25)]
    public void FlawlessExecutionChangesActualDynamicFinisherCost(int rank,int expected)
    {
        var request=Input();request.Talents.Ranks["flawless-execution"]=rank;
        var context=new SimulationContext(new(){Seed=1});var actor=new SimulationActorState{Key="rogue",TeamKey="raid"};
        actor.AddResource(new ResourceState("energy",100,100));context.AddActor(actor);
        foreach(var ability in ForeverRogueSpellbook.Create("combat",request.Talents,request.Tuning,0,false))actor.AddAbility(ability);
        Assert.Equal(35,actor.Abilities["eviscerate"].Definition.ResourceCosts.Single().Amount);
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),new NoMitigation());
        var combat=new RogueCombatProcessor(request,actor,new RotationProfile(),executor,auras,new AutoAttackProcessor(executor),[]);
        combat.Process(context,new(){Type=CombatEventType.RotationDecision,SourceActorKey="rogue",TargetActorKey="target"});
        Assert.Equal(expected,actor.Abilities["eviscerate"].Definition.ResourceCosts.Single().Amount);
    }
    [Theory][InlineData(0,false)][InlineData(1,false)][InlineData(2,false)]
    [InlineData(0,true)][InlineData(1,true)][InlineData(2,true)]
    public void WeaponExpertiseUsesSourceOnePointPerRankAgainstExplicitAvoidance(int rank,bool behind)
    {
        var request=Input();request.Talents.Ranks["weapon-expertise"]=rank;request.IsBehindTarget=behind;
        request.Tuning.MainHandHitPercent=100;request.TargetDodgePercent=7;request.TargetParryPercent=9;
        Func<RogueCombatProcessor?> combat=()=>null;
        var resolver=(ICombatRollResolver)Activator.CreateInstance(typeof(RogueSimRunner).GetNestedType("RogueRollResolver",BindingFlags.NonPublic)!,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[request,combat],null)!;
        for(var seed=1;seed<=200;seed++)
        {
            var random=new Random(seed);random.NextDouble();var avoidance=(decimal)random.NextDouble()*100m;
            var expected=avoidance<7-rank?CombatResultTypes.Dodge:!behind&&avoidance<7-rank+9-rank?CombatResultTypes.Parry:CombatResultTypes.Hit;
            var result=resolver.Resolve(new(new(){Seed=seed}),new(){Key="rogue",TeamKey="raid"},new(){Key="target",TeamKey="enemy"},
                new(){Key="test"},new(){ResolutionType=CombatResolutionTypes.Melee,CanMiss=true,CanCrit=false});
            Assert.Equal(expected,result.ResultKey);
        }
    }

}
