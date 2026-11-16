using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarcraftSim.Api.Simulation;

/// <summary>A versioned class allocation around the existing shared character inputs.
/// Origin labels never select a combat implementation or an automatic priority.</summary>
public sealed class ForeverClassBuild
{
    public string Schema { get; set; } = "warcraftsim.class-build";
    public int Version { get; set; } = 1;
    public ForeverSharedCharacterConfiguration Character { get; set; } = new();
    public string? PresetOrigin { get; set; }
    public bool AcceptPartialCoverage { get; set; }
}

public sealed record ForeverClassTalentNode(string Key,string Name,string Tree,int MaxRank,
    int Row,int Column,string? PrerequisiteKey,int PrerequisiteRank,
    string SourceUrl,string SourceConfidence,string ReviewedAt,
    string ImplementationStatus,IReadOnlyList<string> ImplementedEffects,
    IReadOnlyList<string> MissingEffects,IReadOnlyList<string> ScenarioConditions)
{
    public string? IconKey { get; init; }
    public string IconSourceConfidence { get; init; } = "unresolved";
    // New class identities are additive and omitted on retained historical
    // nodes, so old Shaman/Rogue report snapshots keep their wire shape.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public int? TalentId { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public int? TalentSpellId { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public string? TalentSpellIdSourceUrl { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? RankSpellIds { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EffectComponents { get; init; }
}

public sealed record ForeverClassBuildValidation(bool IsLegal,bool CanSave,bool CanExecute,
    int AllocatedPoints,int UnspentPoints,IReadOnlyDictionary<string,int> TreePoints,
    IReadOnlyList<string> LegalityErrors,IReadOnlyList<string> ExecutionBlockers,
    IReadOnlyList<string> CoverageWarnings,object? Preparation);

public sealed record ForeverClassTalentPreset(string Key,string ClassKey,string Name,
    IReadOnlyDictionary<string,int> Ranks,string SourceUrl,string SourceBuild,
    string ReviewedAt,string Scenario,string Coverage,string Provenance);

public sealed class ForeverClassBuildComparisonRequest
{
    public ForeverClassBuild Baseline { get; set; } = new();
    public ForeverClassBuild Candidate { get; set; } = new();
    public int Iterations { get; set; } = 12;
    public int ValidationIterations { get; set; } = 8;
}

public sealed class ForeverClassBuildReport
{
    public string Schema { get; set; } = "";
    public int Version { get; set; }
    public ForeverClassBuild Build { get; set; } = new();
    public string SourceCommit { get; set; } = "";
    public string WorkflowCheckoutCommit { get; set; } = "";
    public ForeverClassBuildValidation? Validation { get; set; }
    public JsonElement Preparation { get; set; }
    public JsonElement OriginalSimulationRequest { get; set; }
    public IReadOnlyList<ForeverClassTalentNode> Coverage { get; set; } = [];
    public JsonElement Simulation { get; set; }
    public string[] Notes { get; set; } = [];
}

public static class ForeverClassBuildJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static T Clone<T>(T source) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(source,Options),Options) ??
        throw new ArgumentException("The submitted build could not be copied.");
    public static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value,Options);
}
