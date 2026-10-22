using System.Text.Json;
using WarcraftSim.Core.Simulation.Engine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();
app.MapGet("/", () => Results.Redirect("/warrior.html"));
app.UseHttpsRedirection();

// Shared input latency for ALL API-backed class/specialization simulations.
// Read an optional top-level inputLatencyMs (0..1000) from the existing JSON
// request body. The typed specialization contracts can safely ignore that field.
// Scoping via AsyncLocal keeps simultaneous HTTP simulations independent.
app.Use(async (http, next) =>
{
    if (HttpMethods.IsPost(http.Request.Method) &&
        http.Request.Path.Value?.StartsWith("/api/sim/", StringComparison.OrdinalIgnoreCase) == true)
    {
        decimal latencyMs = 80m;
        if (http.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            http.Request.EnableBuffering();
            try
            {
                using var json = await JsonDocument.ParseAsync(http.Request.Body);
                if (json.RootElement.ValueKind == JsonValueKind.Object &&
                    json.RootElement.TryGetProperty("inputLatencyMs", out var value) &&
                    value.ValueKind == JsonValueKind.Number &&
                    value.TryGetDecimal(out var specified))
                    latencyMs = specified;
            }
            catch (JsonException)
            {
                // Leave invalid JSON handling to the API endpoint as before.
            }
            finally
            {
                http.Request.Body.Position = 0;
            }
        }

        var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        SimulationInputTimingScope.AdditionalDelaySeconds =
            Math.Clamp(latencyMs, 0m, 1000m) / 1000m;
        try
        {
            await next(http);
        }
        finally
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = previous;
        }
        return;
    }

    await next(http);
});

app.UseAuthorization();
app.MapControllers();
app.Run();
