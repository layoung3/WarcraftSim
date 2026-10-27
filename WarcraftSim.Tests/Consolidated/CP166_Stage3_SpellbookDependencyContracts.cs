using WarcraftSim.Core.Abilities;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Direct-impact spells cannot apply their damage-over-time component after
/// the impact has missed. Independent, aura-only DoTs retain their own
/// application roll rather than depending on an unrelated spell impact.
/// </summary>
public sealed class Cp166Stage3SpellbookDependencyContractsTests
{
    [Fact]
    public void ImmolateTickRequiresLandedDirectImpact()
    {
        var spells=ForeverWarlockDpsSpellbook.Create("destruction",
            new ForeverWarlockTalentBuild(),new ForeverWarlockTuning(),0m,0m);
        var immolate=Assert.Single(spells,spell=>spell.Key=="immolate");
        Assert.Equal("immolate-impact",Assert.Single(immolate.Effects,
            effect=>effect.Key=="immolate-tick").DependsOnEffectKey);
        var corruption=Assert.Single(spells,spell=>spell.Key=="corruption");
        Assert.Null(Assert.Single(corruption.Effects).DependsOnEffectKey);
    }

    [Fact]
    public void FrostfireTickRequiresLandedFrostfireImpact()
    {
        var spells=ForeverMageDpsSpellbook.Create(
            new ForeverMageTalentBuild(),new ForeverMageTuning(),0m);
        var frostfire=Assert.Single(spells,spell=>spell.Key=="frostfire-bolt");
        Assert.Equal("frostfire-impact",Assert.Single(frostfire.Effects,
            effect=>effect.Key=="frostfire-tick").DependsOnEffectKey);
    }

    [Fact]
    public void FlameShockTickRequiresLandedShockImpact()
    {
        var spells=ForeverElementalShamanSpellbook.Create(
            new ForeverElementalShamanTalents(),
            new ForeverElementalShamanTuning(),0m);
        var shock=Assert.Single(spells,spell=>spell.Key=="flame-shock");
        Assert.Equal("flame-shock-impact",Assert.Single(shock.Effects,
            effect=>effect.Key=="flame-shock-tick").DependsOnEffectKey);
    }
}
