using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ElementalAlacrityTests
{
    private static readonly decimal[] BoltTimes=[2.50m,2.33m,2.17m,2m];
    private static readonly decimal[] ChainTimes=[2m,1.83m,1.67m,1.5m];
    private static readonly string[] Keys=["lightning-bolt","lightning-bolt-rank7","chain-lightning","lava-burst"];
    private static decimal Time(string key,int rank)=>key=="chain-lightning"?ChainTimes[rank]:BoltTimes[rank];
    private static decimal Damage(string key)=>key switch {"lightning-bolt"=>196m,"lightning-bolt-rank7"=>147m,"chain-lightning"=>123m,_=>220m};
    private static decimal Cost(string key)=>key switch {"lightning-bolt"=>198m,"lightning-bolt-rank7"=>121.5m,"chain-lightning"=>283.5m,_=>238.5m};
    private static ElementalShamanSimRequest Request(int rank,string key="lightning-bolt",bool prepared=false,bool mastery=false) {
        var r=new ElementalShamanSimRequest {
            DurationSeconds=25m,CaptureTimeline=true,TargetMaximumHealth=1000000000m,
            BaseSpellHitPercent=100m,SpellCritPercent=0m,MajorManaPotionCharges=0,
            UseLightningOverload=false,AllowDownranking=true,UseElementalMastery=mastery,
            CustomPriority=mastery?["elemental-mastery",key]:[key],Tuning=new(){ManaRegenPerSecond=0m},
            Talents=new(){ElementalPointsSpent=31,EnhancementPointsSpent=0,RestorationPointsSpent=0,
                Ranks=new(StringComparer.OrdinalIgnoreCase){["convection"]=5,["elemental-alacrity"]=rank,["lava-burst"]=1,["elemental-mastery"]=1}}
        };
        r.Character.Equipment.Items.Clear();r.Character.Buffs.Selections.Clear();
        r.Character.BaseStats.Values=new(){["intellect"]=1000m,["spell-power"]=0m,["spirit"]=0m,["mp5"]=0m};
        if(prepared){r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
            r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="alacrity-oracle",Stats=new(){Values=new(){["intellect"]=1000m,["haste-percent"]=25m}}});}
        return r;
    }
    private static ElementalShamanSimResponse Run(ElementalShamanSimRequest r)=>new ElementalShamanSimRunner().Run(r);
    private static CombatEvent[] Events(ElementalShamanSimResponse r,CombatEventType type,string key)=>r.Run.Timeline.Where(e=>e.Type==type&&e.AbilityKey==key).ToArray();
    public static IEnumerable<object[]> Cases(){for(var rank=0;rank<=3;rank++)foreach(var key in Keys)foreach(var prepared in new[]{false,true})foreach(var mastery in new[]{false,true})yield return [rank,key,prepared,mastery];}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void FourDefinitionsHaveLiteralRankTimesAndUnchangedTuning(int rank){
        var t=Request(rank).Talents;t.Validate();var spells=ForeverElementalShamanSpellbook.Create(t,new(),0m);
        foreach(var key in Keys){var a=spells.Single(a=>a.Key==key);Assert.Equal(Time(key,rank),a.CastTimeSeconds);Assert.Equal(1.5m,a.GlobalCooldownSeconds);Assert.Equal(Cost(key),Assert.Single(a.ResourceCosts).Amount);Assert.Equal(Damage(key),a.Effects[0].MinimumValue);Assert.Equal(Damage(key),a.Effects[0].MaximumValue);Assert.Equal(key=="chain-lightning"?6m:key=="lava-burst"?10m:0m,a.CooldownSeconds);}
    }
    [Theory][MemberData(nameof(Cases))]
    public void ActualCompletionSnapshotsHasteMasteryAndPaysExactUnchangedCost(int rank,string key,bool prepared,bool mastery){
        var r=Request(rank,key,prepared,mastery);var result=Run(r);var starts=Events(result,CombatEventType.AbilityCastStarted,key);var completed=Events(result,CombatEventType.AbilityCastCompleted,key);Assert.NotEmpty(completed);
        var removed=result.Run.Timeline.SingleOrDefault(e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="elemental-mastery");
        foreach(var end in completed){var start=Assert.Single(starts,s=>s.AbilityExecutionId==end.AbilityExecutionId);// The queued expiration wake can resolve a cast at exactly 15 before the removal callback. Preserve that existing snapshot.
            var reduced=mastery&&key!="chain-lightning"&&(start.TimeSeconds<15m || prepared&&start.TimeSeconds==15m);var expected=(Time(key,rank)-(reduced?.5m:0m))/(prepared?1.25m:1m);Assert.True(expected==end.TimeSeconds-start.TimeSeconds,$"rank {rank}, {key}, start {start.TimeSeconds}, end {end.TimeSeconds}, removed {removed?.TimeSeconds}, start index {result.Run.Timeline.IndexOf(start)}, removed index {result.Run.Timeline.IndexOf(removed!)}, expected {expected}");
            Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey==key&&e.TimeSeconds==end.TimeSeconds&&e.Amount==-Cost(key));
            Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage&&e.AbilityKey==key&&e.TimeSeconds==end.TimeSeconds&&e.Amount==Damage(key)*(e.IsCritical?1.5m:1m));}
        Assert.All(Events(result,CombatEventType.Damage,key),e=>Assert.Equal(Damage(key)*(e.IsCritical?1.5m:1m),e.Amount));
        Assert.Equal(completed.Length,Events(result,CombatEventType.Damage,key).Length);
        Assert.Equal(Events(result,CombatEventType.Damage,key).Sum(e=>Damage(key)*(e.IsCritical?1.5m:1m)),result.DamageDone);
        var debits=result.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey==key&&e.Amount<0).ToArray();Assert.Equal(completed.Length,debits.Length);Assert.All(debits,e=>Assert.Equal(-Cost(key),e.Amount));
        if(mastery){Assert.NotNull(removed);Assert.Equal(15m,removed.TimeSeconds);Assert.Contains(starts,e=>e.TimeSeconds>=15m);}
        for(var i=1;i<starts.Length;i++)Assert.True(starts[i].TimeSeconds-starts[i-1].TimeSeconds>=1.5m);
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void CompletionBoundaryOwnsDamageAndManaNotTheStart(int rank){
        foreach(var key in Keys){var r=Request(rank,key);r.DurationSeconds=Time(key,rank)-.001m;var before=Run(r);Assert.Single(Events(before,CombatEventType.AbilityCastStarted,key));Assert.Empty(Events(before,CombatEventType.AbilityCastCompleted,key));Assert.Equal(0m,before.DamageDone);Assert.Equal(before.MaximumMana,before.EndingMana);
            r.DurationSeconds=Time(key,rank)+.001m;var after=Run(r);Assert.Single(Events(after,CombatEventType.AbilityCastCompleted,key));Assert.Equal(Damage(key),after.DamageDone);Assert.Equal(after.MaximumMana-Cost(key),after.EndingMana);}
    }
    [Theory][InlineData(1)][InlineData(2)]
    public async Task SerializationPreviewAndRequestReuseDoNotLeakMasteryOrPreparation(int rank){
        var r=Request(rank,prepared:true,mastery:true);var before=JsonSerializer.Serialize(r);var p=ElementalShamanSimRunner.Preview(r);var a=Run(r);var replay=Run(JsonSerializer.Deserialize<ElementalShamanSimRequest>(before)!);
        Assert.Equal(a.DamageDone,replay.DamageDone);Assert.Equal(a.EndingMana,replay.EndingMana);Assert.Equal(Canonical(a.Run.Timeline),Canonical(replay.Run.Timeline));
        var repeats=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Task.Run(()=>Run(r))));Assert.All(repeats,b=>{Assert.Equal(a.DamageDone,b.DamageDone);Assert.Equal(Canonical(a.Run.Timeline),Canonical(b.Run.Timeline));});
        Assert.Equal(before,JsonSerializer.Serialize(r));var after=ElementalShamanSimRunner.Preview(r);Assert.Equal(Canonical(p),Canonical(after));p.EffectiveStats.Set("haste-percent",999m);Assert.Equal(25m,ElementalShamanSimRunner.Preview(r).EffectiveStats.Get("haste-percent"));
    }
    // Canonicalize only generated execution/aura/rotation IDs, preserving joins and all other fields.
    private static string Canonical(object? value){
        var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(value));var ids=new Dictionary<string,string>();
        void Visit(System.Text.Json.Nodes.JsonNode? n,string parent=""){if(n is System.Text.Json.Nodes.JsonObject o){foreach(var kv in o.ToArray()){if((kv.Key is "AbilityExecutionId" or "AuraInstanceId" || kv.Key=="Id" && parent is "Rotation" or "configuredRotation") && kv.Value is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var id)&&Guid.TryParse(id,out _)){if(!ids.TryGetValue(id,out var mapped)){mapped="generated-"+ids.Count;ids[id]=mapped;}o[kv.Key]=mapped;}else Visit(kv.Value,kv.Key);}}else if(n is System.Text.Json.Nodes.JsonArray a)foreach(var child in a)Visit(child);}
        Visit(node);return node!.ToJsonString();
    }
    private static ForeverLevel60SimulationRequest Strict(int rank){var r=Request(rank,prepared:true);r.CustomPriority=["lightning-bolt"];r.Talents=new(){ElementalPointsSpent=5+rank,EnhancementPointsSpent=0,RestorationPointsSpent=0,Ranks=new(){["convection"]=5,["elemental-alacrity"]=rank}};return new(){ClassKey="shaman",Specialization="elemental",ItemizedTalentMode=true,ItemizedTalentRanks=new(r.Talents.Ranks),Elemental=r};}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task StrictLegalPartialRanksSerializeAndReplayWithoutAdmissionChanges(int rank){
        var q=Strict(rank);var before=JsonSerializer.Serialize(q);var controller=new ForeverLevel60SimulationController();var ok=Assert.IsType<OkObjectResult>(await controller.Simulate(q,default));var replay=Assert.IsType<OkObjectResult>(await controller.Simulate(JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(before)!,default));
        Assert.Equal(Canonical(ok.Value),Canonical(replay.Value));Assert.Equal(before,JsonSerializer.Serialize(q));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));var events=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().ToArray();var start=events.First(e=>e.GetProperty("type").GetInt32()==3);var end=events.First(e=>e.GetProperty("type").GetInt32()==4);Assert.Equal(BoltTimes[rank]/1.25m,end.GetProperty("timeSeconds").GetDecimal()-start.GetProperty("timeSeconds").GetDecimal());
    }
    [Theory][InlineData("mismatch")][InlineData("overrank")][InlineData("prerequisite")][InlineData("total")]
    public async Task InvalidPartialMapsRemainRejected(string mode){var q=Strict(2);if(mode=="mismatch")q.ItemizedTalentRanks!["elemental-alacrity"]=1;if(mode=="overrank"){q.Elemental!.Talents.Ranks["elemental-alacrity"]=4;q.ItemizedTalentRanks!["elemental-alacrity"]=4;q.Elemental.Talents.ElementalPointsSpent=9;}if(mode=="prerequisite"){q.Elemental!.Talents.Ranks["lightning-overload"]=1;q.ItemizedTalentRanks!["lightning-overload"]=1;q.Elemental.Talents.ElementalPointsSpent++;}if(mode=="total")q.Elemental!.Talents.ElementalPointsSpent++;Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));}
}
