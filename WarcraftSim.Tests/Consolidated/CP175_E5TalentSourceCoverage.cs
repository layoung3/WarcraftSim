using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E5TalentSourceCoverage
{
    [Fact]
    public void AllFourClassesHaveThreeHonestSourceCoverageRows()
    {
        var action=Assert.IsType<OkObjectResult>(
            new ForeverSoloTalentController().SourceCoverage());
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(action.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root=doc.RootElement;
        var rows=root.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(12,rows.Length);
        foreach(var cls in new[]{"warrior","mage","warlock","hunter"})
        {
            var byClass=rows.Where(x=>x.GetProperty("classKey").GetString()==cls).ToArray();
            Assert.Equal(3,byClass.Length);
            Assert.All(byClass,row=>{
                Assert.True(row.GetProperty("engineDeclaredConnectedKeys").GetInt32()>0);
                Assert.Equal(0,row.GetProperty("current70707TooltipVerifiedInRepository").GetInt32());
            });
        }
        Assert.Contains("historical",root.GetProperty("historicalVisualReferenceStatus").GetString());
    }
}
