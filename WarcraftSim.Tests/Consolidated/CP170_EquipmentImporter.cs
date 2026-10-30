using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp170EquipmentImporterContract
{
    private const string Sample=@"{
        ""item"": { ""id"": 19102, ""name"": ""Crackling Staff"",
            ""quality"": ""RARE"", ""requiredLevel"": 60 }
    }";

    [Fact]
    public void DocumentedAhledgerMetadataIsNormalizedWithoutInventingCombatStats()
    {
        var record=ForeverEquipmentMetadataImporter.ParseAhledger(
            Sample,19102,"https://api.ahledger.com/v1/items/19102?game=forever",
            "1.60.1.70685");
        Assert.Equal(19102,record.Id);
        Assert.Equal("Crackling Staff",record.Name);
        Assert.Equal("RARE",record.Quality);
        Assert.Equal("metadata-only",record.VerificationStatus);
        Assert.Equal("ahledger-forever-items-v1",record.Provider);
        Assert.Equal("1.60.1.70685",record.SourceBuild);
        Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentMetadataImporter.PromoteToSimulationItem(record));
    }

    private sealed class StubHandler:HttpMessageHandler
    {
        public Uri? Requested {get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requested=request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){
                Content=new StringContent(Sample)
            });
        }
    }

    [Fact]
    public async Task DocumentedPublicEndpointCanBeImportedForReviewWithoutLiveNetwork()
    {
        var handler=new StubHandler();
        using var client=new HttpClient(handler);
        var source=new AhledgerForeverItemMetadataSource(client);
        var result=await source.ReadAsync(19102);
        Assert.Equal("https://api.ahledger.com/v1/items/19102?game=forever",
            handler.Requested?.ToString());
        Assert.Equal(19102,result.Id);
        Assert.Equal("metadata-only",result.VerificationStatus);
        Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentMetadataImporter.PromoteToSimulationItem(result));
    }

    [Fact]
    public void WrongItemIdMissingSourceOrNonProviderUrlCannotEnterImportPipeline()
    {
        Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentMetadataImporter.ParseAhledger(Sample,19103,
                "https://api.ahledger.com/v1/items/19103?game=forever"));
        Assert.Throws<ArgumentException>(()=>
            ForeverEquipmentMetadataImporter.ParseAhledger(Sample,19102,
                "https://not-authorized.example/items/19102"));
        Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentMetadataImporter.ParseAhledger(
                @"{""item"":{""id"":19102}}",19102,
                "https://api.ahledger.com/v1/items/19102?game=forever"));
    }
}
