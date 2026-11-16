using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E3EquipmentCoverage
{
    [Fact]
    public void CoverageCountsAreDerivedFromTheVersionedOfflineCatalog()
    {
        var action=Assert.IsType<OkObjectResult>(new ForeverEquipmentController().Coverage());
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(action.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var data=doc.RootElement;
        var catalog=ForeverEquipmentCatalog.GetCatalog();
        Assert.Equal(catalog.Items.Count,data.GetProperty("totalItems").GetInt32());
        Assert.Equal(catalog.Enchants.Count,data.GetProperty("totalEnchants").GetInt32());
        Assert.Equal(catalog.Enchants.Count,data.GetProperty("permanentEnchants").GetInt32()+
            data.GetProperty("temporaryEnhancements").GetInt32());
        Assert.Equal(ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Count,
            data.GetProperty("totalEquipmentEffects").GetInt32());
        Assert.Equal(data.GetProperty("totalEquipmentEffects").GetInt32(),
            data.GetProperty("executableEquipmentEffects").GetInt32()+
            data.GetProperty("unsupportedEquipmentEffects").GetInt32());
        Assert.True(data.GetProperty("bySlot").GetProperty("main-hand").GetInt32()>0);
        Assert.True(data.GetProperty("byClass").GetProperty("warrior").GetInt32()>0);
    }
}
