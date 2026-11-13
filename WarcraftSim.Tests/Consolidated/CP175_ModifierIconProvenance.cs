using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ModifierIconProvenance
{
    [Fact]
    public void SourceBackedArtworkHasSeparateProvisionalProvenance()
    {
        var entries = ForeverExternalModifierCatalog.Entries;
        var icons = entries.Where(x => x.IconKey is not null).ToArray();
        Assert.Equal(25, icons.Length);
        Assert.Equal(46, entries.Count);
        foreach (var entry in icons)
        {
            Assert.Matches("^[a-z0-9_]+$", entry.IconKey!);
            Assert.Equal("provisional", entry.DataConfidence);
            Assert.Equal(ForeverExternalModifierIconCatalog.VerificationStatus, entry.IconSourceStatus);
            Assert.Contains(ForeverExternalModifierIconCatalog.SourceCommit, entry.IconSourceUrl!);
        }
        Assert.Equal("spell_holy_magicalsentry", ForeverExternalModifierCatalog.Find("arcane-intellect").IconKey);
        Assert.Equal("inv_potion_41", ForeverExternalModifierCatalog.Find("flask-of-supreme-power").IconKey);
        Assert.Equal("inv_misc_fish_13", ForeverExternalModifierCatalog.Find("grilled-squid").IconKey);
    }

    [Fact]
    public void ExactCatalogIdentityIsRequiredAndConflictingNamesRemainNeutral()
    {
        var entry = ForeverExternalModifierCatalog.Find("grilled-squid");
        Assert.NotNull(entry.IconKey);
        Assert.Null((entry with { ItemId = 1 }).IconKey);
        Assert.Null((entry with { SpellId = 1 }).IconKey);
        Assert.Null((entry with { Name = "Renamed Squid (Well Fed)" }).IconKey);
        Assert.Null((entry with { Key = "different-key" }).IconKey);
        Assert.Null((entry with { Category = "external-buff" }).IconKey);
        foreach (var key in new[] { "elixir-of-greater-strength", "elixir-of-lesser-fortitude", "savory-turtle-stew" })
        {
            var unresolved = ForeverExternalModifierCatalog.Find(key);
            Assert.Null(unresolved.IconKey);
            Assert.Equal("unresolved", unresolved.IconSourceStatus);
            Assert.Null(unresolved.IconSourceUrl);
        }
    }

    [Fact]
    public void ArtworkMetadataIsSerializedWithoutChangingMechanics()
    {
        var entry = ForeverExternalModifierCatalog.Find("elixir-of-the-mongoose");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(entry,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("inv_potion_32", json.RootElement.GetProperty("iconKey").GetString());
        Assert.Equal("provisional", json.RootElement.GetProperty("dataConfidence").GetString());
        Assert.Equal(25m, entry.Stats["agility"]);
        Assert.Equal("elixir-agility", entry.ExclusiveGroup);
        Assert.Equal(1800m, entry.DurationSeconds);
        Assert.Throws<ArgumentException>(() => ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection { Consumables = [entry.Key, "elixir-of-greater-agility"] }, 60));
    }
}
