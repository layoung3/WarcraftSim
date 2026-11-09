using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PreparedEnhancementInputs
{
    private static EnhancementShamanSimRequest Input(int scenario = 0)
    {
        var r = new EnhancementShamanSimRequest { DurationSeconds = 30m, CaptureTimeline = true };
        if (scenario > 0) { r.DualWield = true; r.WeaponSpeedSeconds = 2.6m; }
        if (scenario == 2) { r.MainHandImbue = "rockbiter"; r.OffHandImbue = "flametongue"; }
        if (scenario == 3) { r.MainHandImbue = "frostbrand"; r.UseCallOfElements = true; r.UseTotemicRecall = true; r.UseLightningShield = true; r.IncomingBossHitDamage = 10m; }
        return r;
    }
    // Combat-only fingerprints captured before preparation changes at bac22a34.
    [Theory][InlineData(0,"6C321A144430DA80423221B3B5754F903A5B27D245F23548461D7611B6242094")][InlineData(1,"AE923187B63B1FEA00011BA9C0BFF19E1F3119E22E2BCE0CF83BFB25ACDA8EFF")][InlineData(2,"0608A6B8D74914EC8D622A7FC6F23BFCCC9A29E6B817D580EAE2061A9DC9E798")][InlineData(3,"772E541B312CCD67B43FACCA6A47C997B2A672F8DC85D5CCC8A0A2A51F50DB3F")]
    public void LegacySeededCombatRemainsUnchanged(int scenario, string expected)
    {
        var result = new EnhancementShamanSimRunner().Run(Input(scenario));
        var combat = JsonSerializer.Serialize(new { result.DamageDone, result.EndingMana, result.MaximumMana,
            result.AttackPower, result.SpellPower, result.WindfuryWeaponProcs, result.WindfuryTotemProcs,
            result.DamageByAbility, Timeline = result.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.AbilityKey, e.Amount, e.ResultKey }) });
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combat))); Assert.True(expected == actual, $"Scenario {scenario}: {actual}");
    }
    private static EnhancementShamanSimRequest Prepared()
    {
        var r = Input(); r.Character.Race = "orc";
        r.Level60Baseline = new() { Race = "orc", ClassKey = "shaman", Strength = 100m,
            Agility = 100m, Stamina = 100m, Intellect = 100m, Spirit = 100m,
            ClientBuild = "synthetic", SourceReference = "controlled-test-observation",
            RecordedAtUtc = "2026-11-09T00:00:00Z" };
        return r;
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void PreparedResourcesAndTalentsApplyIndependentBudgetsOnce(bool observed)
    {
        var r = Prepared();
        r.Level60Baseline!.ObservedUnbuffedMeleeAttackPower = 700m;
        if (observed) { r.Level60Baseline.ObservedUnbuffedMaximumHealth = 3000m; r.Level60Baseline.ObservedUnbuffedMaximumMana = 2000m; }
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "authored", Stats = new() { Values = new() { ["strength"] = 10m, ["intellect"] = 2m, ["stamina"] = 3m, ["mana"] = 4m, ["health"] = 5m, ["spell-power"] = 6m } } });
        r.Character.Talents.Selections.Add(new() { TalentKey = "authored", Stats = new() { Values = new() { ["intellect"] = 4m, ["mana"] = 5m } } });
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored", Stats = new() { Values = new() { ["intellect"] = 6m, ["mana"] = 7m } } });
        var before = JsonSerializer.Serialize(r);
        var result = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(832m, result.AttackPower); // 700 observed + 10 Strength*2 + 112 Intellect*3/3.
        Assert.Equal(39.6m, result.SpellPower); // 6 flat + 112 Intellect*.15*2.
        Assert.Equal(observed ? 3035m : 3535m, result.MaximumHealth);
        Assert.Equal(result.MaximumHealth, result.EndingHealth);
        Assert.Equal(observed ? 2196m : ForeverClientBaseManaCatalog.GetBaseMana("shaman",60) + 92m*15m+16m, result.MaximumMana);
        Assert.Contains(result.ModelingNotes, n => n.Contains(observed ? "Supplied naked Health" : "manual scenario cap"));
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }
    [Fact]
    public void UnobservedApUsesPrimaryStrengthAndTalentsOnce()
    {
        var r = Prepared();
        Assert.Equal(300m, new EnhancementShamanSimRunner().Run(r).AttackPower);
        r.Talents.Ranks["mental-dexterity"] = 0;
        Assert.Equal(200m, new EnhancementShamanSimRunner().Run(r).AttackPower);
    }
    [Fact]
    public async Task SnapshotPreservesComparersAndIsolatesAllMutableInputs()
    {
        var r = Prepared();
        r.Talents.Ranks = new(r.Talents.Ranks, StringComparer.Ordinal);
        r.Character.BaseStats.Values = new(r.Character.BaseStats.Values, StringComparer.Ordinal);
        r.Character.Equipment.Items.Add(new() { SlotKey="chest", ItemKey="authored", Stats=new() { Values=new(StringComparer.OrdinalIgnoreCase) { ["STAMINA"]=4m } } });
        var before = JsonSerializer.Serialize(r);
        var copy = EnhancementShamanSimulationRequestSnapshot.Create(r);
        Assert.Same(r.Talents.Ranks.Comparer, copy.Talents.Ranks.Comparer);
        Assert.Same(r.Character.BaseStats.Values.Comparer, copy.Character.BaseStats.Values.Comparer);
        Assert.Same(r.Character.Equipment.Items[0].Stats.Values.Comparer, copy.Character.Equipment.Items[0].Stats.Values.Comparer);
        copy.Talents.Ranks.Clear(); copy.Tuning.EarthShockMana=0;
        copy.Character.Equipment.Items[0].Stats.Set("stamina",999m);
        copy.CombatItems.DisabledItemKeys.Add("major-mana-potion");
        copy.CallOfElementsTotems.Clear(); copy.CallOfAncestorsTotems.Clear(); copy.CallOfSpiritsTotems.Clear();
        copy.Level60Baseline!.Intellect=999;
        Assert.Equal(before, JsonSerializer.Serialize(r));
        var results = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Task.Run(() => new EnhancementShamanSimRunner().Run(r))));
        Assert.All(results, x => { Assert.Equal(results[0].DamageDone,x.DamageDone); Assert.Equal(results[0].EndingMana,x.EndingMana); });
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void PreparedCatalogIdsAreRejected(int kind)
    {
        var r=Prepared(); r.Character.Equipment.Items.Add(new() { SlotKey="chest", ItemKey="unsupported", ItemId=kind==0?19102:null, EnchantSpellId=kind==1?20034:null, TemporaryEnchantSpellId=kind==2?25122:null });
        Assert.Throws<ArgumentException>(()=>new EnhancementShamanSimRunner().Run(r));
    }
    [Theory][InlineData("race")][InlineData("class")][InlineData("modes")][InlineData("health")][InlineData("mana")]
    public void PreparedInvalidBaselineOrResourceCapFailsClosed(string kind)
    {
        var r=Prepared();
        switch(kind) {
            case "race": r.Character.Race="tauren";break;
            case "class": r.Level60Baseline!.ClassKey="warrior";break;
            case "modes": r.Level60Baseline=null;r.UseReviewedLevel60Baseline=true;r.UseProvisionalLevel60Baseline=true;break;
            case "health": r.Character.Buffs.Selections.Add(new(){BuffKey="authored",Stats=new(){Values=new(){["health"]=-3500m}}});break;
            case "mana": r.Level60Baseline!.ObservedUnbuffedMaximumMana=0m;break;
        }
        Assert.Throws<ArgumentException>(()=>new EnhancementShamanSimRunner().Run(r));
    }
    [Fact]
    public void ExplicitProvisionalBaselineKeepsWarning()
    {
        var r=Prepared();r.Level60Baseline=null;r.UseProvisionalLevel60Baseline=true;
        Assert.Contains(new EnhancementShamanSimRunner().Run(r).ModelingNotes,n=>n.Contains("NOT verified for Forever"));
    }
    [Theory][InlineData("base")][InlineData("map")][InlineData("equipment")][InlineData("items")]
    [InlineData("item")][InlineData("modifiers")][InlineData("modifier")][InlineData("talents")]
    [InlineData("talent")][InlineData("buffs")][InlineData("buff")][InlineData("ranks")]
    [InlineData("calls")][InlineData("disabled-items")]
    public async Task MalformedNestedJsonReturnsHttp400(string missing)
    {
        var r=Prepared();
        switch(missing) {
            case "base": r.Character.BaseStats=null!;break;
            case "map": r.Character.BaseStats.Values=null!;break;
            case "equipment": r.Character.Equipment=null!;break;
            case "items": r.Character.Equipment.Items=null!;break;
            case "item": r.Character.Equipment.Items.Add(null!);break;
            case "modifiers": r.Character.Equipment.Items.Add(new(){Modifiers=null!});break;
            case "modifier": r.Character.Equipment.Items.Add(new(){Modifiers=[null!]});break;
            case "talents": r.Character.Talents.Selections=null!;break;
            case "talent": r.Character.Talents.Selections.Add(null!);break;
            case "buffs": r.Character.Buffs.Selections=null!;break;
            case "buff": r.Character.Buffs.Selections.Add(null!);break;
            case "ranks": r.Talents.Ranks=null!;break;
            case "calls": r.CallOfSpiritsTotems=null!;break;
            case "disabled-items": r.CombatItems.DisabledItemKeys=null!;break;
        }
        var json=JsonSerializer.Serialize(r);var request=JsonSerializer.Deserialize<EnhancementShamanSimRequest>(json)!;
        var response=await new WarcraftSim.Api.Controllers.EnhancementShamanSimController().Run(request,default);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(response.Result);
        Assert.Equal(json,JsonSerializer.Serialize(request));
    }
    [Theory]
    [InlineData(10, 5, 2000)]
    [InlineData(10, 15, 2075)]
    [InlineData(30, -15, 1850)]
    [InlineData(30, -40, 1850)]
    [InlineData(100, -10, 1850)]
    public void ObservedManaUsesTheSameClampedIntellectConversionAsFallback(
        int nakedIntellect, int intellectDelta, int expectedObservedMana)
    {
        var r = Prepared();
        r.Level60Baseline!.Intellect = nakedIntellect;
        r.Level60Baseline.ObservedUnbuffedMaximumMana = 2000m;
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored-intellect-delta",
            Stats = new() { Values = new() { ["intellect"] = intellectDelta } } });
        var observed = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(expectedObservedMana, observed.MaximumMana);
        // The same contribution must change fallback and observed caps equally,
        // including negative authored contributions and either side of 20 Intellect.
        r.Level60Baseline.ObservedUnbuffedMaximumMana = null;
        var fallbackWithDelta = new EnhancementShamanSimRunner().Run(r);
        r.Character.Buffs.Selections.Clear();
        var fallbackWithoutDelta = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(expectedObservedMana - 2000m,
            fallbackWithDelta.MaximumMana - fallbackWithoutDelta.MaximumMana);
    }
}
