using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

/// <summary>Owns the shared boundary, not combat formulas. Each supported class
/// has one class-wide runner; origin labels never select its mechanics.</summary>
public static class ForeverClassBuildService
{
    private static readonly JsonSerializerOptions PaladinSettingsJson=new(ForeverClassBuildJson.Options) {
        NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.Strict};
    private static readonly HashSet<string> SharedSettings = new(StringComparer.OrdinalIgnoreCase) {
        "character","talents","modifiers","externalModifiers","combatItems","seed","durationSeconds",
        "captureTimeline","targetMaximumHealth","targetArmor","useReviewedLevel60Baseline",
        "useProvisionalLevel60Baseline","level60Baseline","specialization","useClassWideTalents",
        "customPriority","classWideRotationPolicy"
    };

    public static ForeverClassBuildValidation Validate(ForeverClassBuild source,bool prepare=true)
    {
        var errors=new List<string>();var blockers=new List<string>();var warnings=new List<string>();
        var totals=new Dictionary<string,int>(StringComparer.Ordinal);
        if(source is null || source.Schema!="warcraftsim.class-build" || source.Version!=1 || source.Character is null)
            return new(false,false,false,0,0,totals,["A supported class-build schema/version and character are required."],[],[],null);
        var c=source.Character;
        if(!ForeverClassBuildCatalog.Classes.Contains(c.ClassKey,StringComparer.Ordinal))
            return new(false,false,false,0,0,totals,["Choose a canonical class key."],[],[],null);
        var nodes=ForeverClassBuildCatalog.Talents(c.ClassKey);
        var byKey=nodes.ToDictionary(x=>x.Key,StringComparer.Ordinal);
        foreach(var tree in ForeverClassBuildCatalog.Trees(c.ClassKey))totals[tree]=0;
        if(c.Level!=60)errors.Add("This shared execution adapter supports level 60; the talent budget is 51.");
        if(c.ItemizedTalents is null)errors.Add("A rank map is required, including for an empty allocation.");
        else foreach(var (key,rank) in c.ItemizedTalents)
        {
            if(!byKey.TryGetValue(key,out var node))errors.Add($"Unknown or obsolete talent key: {key}.");
            else if(rank<0 || rank>node.MaxRank)errors.Add($"{key} allows ranks 0–{node.MaxRank}.");
            else totals[node.Tree]+=rank;
        }
        var allocated=totals.Values.Sum();
        if(allocated>51)errors.Add("Talent allocation exceeds the 51-point level-60 budget.");
        if(c.ItemizedTalents is not null)
        foreach(var node in nodes.Where(x=>c.ItemizedTalents.GetValueOrDefault(x.Key)>0))
        {
            var earlier=nodes.Where(x=>x.Tree==node.Tree&&x.Row<node.Row)
                .Sum(x=>Math.Clamp(c.ItemizedTalents.GetValueOrDefault(x.Key),0,x.MaxRank));
            if(earlier<(node.Row-1)*5)errors.Add($"{node.Key} needs {(node.Row-1)*5} points in earlier {node.Tree} rows, found {earlier}.");
            if(node.PrerequisiteKey is { } parent && c.ItemizedTalents.GetValueOrDefault(parent)<node.PrerequisiteRank)
                errors.Add($"{node.Key} requires {parent} rank {node.PrerequisiteRank}.");
            if(node.ImplementationStatus!="implemented")
                warnings.Add($"{node.Name}: {node.ImplementationStatus}. {string.Join(" ",node.MissingEffects)}");
        }
        if(!ForeverClassBuildCatalog.CanExecuteClassBuild(c.ClassKey))
            blockers.Add("This class has a coverage inventory but its class-wide shared execution adapter is not implemented yet.");
        if(c.Rotation is null || c.Rotation.Mode is not ("automatic" or "custom") || c.Rotation.Priority is null)
            errors.Add("Rotation must be automatic or custom with an explicit priority list.");
        if(c.Rotation?.Mode=="custom" && c.Rotation.Priority?.Count==0)
            blockers.Add("Custom priority is empty; choose actions or select automatic policy.");
        if(c.Encounter is null || c.Encounter.DurationSeconds is <=0m or >600m ||
            c.Encounter.TargetMaximumHealth<=0m || c.Encounter.TargetArmor<0m)
            errors.Add("Encounter duration must be greater than zero and at most 600 seconds, target Health positive and Armor nonnegative.");
        if(c.Encounter?.SurvivalPolicy!="existing-runner-behavior")
            blockers.Add("The requested survival policy has no class-wide adapter. Existing runners retain actual death semantics.");
        if(c.Equipment?.Items is null || c.Modifiers is null || c.CombatItems is null)
            errors.Add("Gear, modifiers and combat inventory cannot be null.");
        if(c.Equipment?.Items is not null && c.Equipment.Items.Any(x=>x is null || x.Stats?.Values is null ||
            x.Modifiers is null || x.Modifiers.Any(m=>m is null || m.Stats?.Values is null)))
            errors.Add("Gear entries and their stat/modifier collections cannot be null.");
        if(c.Modifiers is not null && (c.Modifiers.Buffs is null || c.Modifiers.Consumables is null ||
            c.Modifiers.TargetDebuffs is null || c.Modifiers.Buffs.Any(string.IsNullOrWhiteSpace) ||
            c.Modifiers.Consumables.Any(string.IsNullOrWhiteSpace) || c.Modifiers.TargetDebuffs.Any(string.IsNullOrWhiteSpace)))
            errors.Add("Modifier lists must be explicit arrays of named effects.");
        if(c.ClassSettings.ValueKind!=JsonValueKind.Object)
            errors.Add("Class settings must be an object.");
        if(c.ClassSettings.ValueKind==JsonValueKind.Object)
        {
            foreach(var property in c.ClassSettings.EnumerateObject())
                if(SharedSettings.Contains(property.Name))errors.Add($"{property.Name} belongs in shared character inputs, not classSettings.");
            // Check class-specific wire types even when a pending baseline or
            // equipment selection prevents preparation. An unknown setting is
            // malformed transport, not an unsupported talent component.
            if(ForeverClassBuildCatalog.CanExecuteClassBuild(c.ClassKey))
                try { _=ReadSettings(c.ClassKey,c.ClassSettings.GetRawText()); }
                catch(JsonException e){errors.Add("Unsupported class setting: "+e.Message);}
        }
        if(c.UseRacialActive)blockers.Add("Racial active execution is not connected to these class-wide adapters yet.");
        try {ForeverPlayableRaceCatalog.Validate(c.Race,c.ClassKey,c.Level);}
        catch(ArgumentException e){errors.Add(e.Message);}
        if(c.BaselineMode is not ("historical-provisional" or "strict-reviewed" or "explicit-observation"))
            errors.Add("Choose a supported baseline mode.");
        if(c.BaselineMode=="explicit-observation" && c.Baseline is null)
            blockers.Add("An explicit observation requires its baseline evidence record.");
        if(c.BaselineMode!="explicit-observation" && c.Baseline is not null)
            errors.Add("An inline baseline requires explicit-observation mode.");
        object? preview=null;
        if(prepare && errors.Count==0 && blockers.Count==0)
        {
            try {preview=Prepare(source).Preview;}
            catch(ArgumentException e){blockers.Add(e.Message);}
            catch(InvalidOperationException e){blockers.Add(e.Message);}
            catch(JsonException e){blockers.Add("Unsupported class setting: "+e.Message);}
        }
        if(warnings.Count>0 && !source.AcceptPartialCoverage)
            blockers.Add("Review the listed missing or partial effects and explicitly acknowledge partial coverage before execution.");
        return new(errors.Count==0,errors.Count==0,errors.Count==0&&blockers.Count==0,
            allocated,51-allocated,totals,errors,blockers,warnings,preview);
    }

    public static ForeverClassBuild ApplyPreset(ForeverClassBuild original,string presetKey)
    {
        ArgumentNullException.ThrowIfNull(original);
        if(original.Character is null)throw new ArgumentException("A character is required before applying a talent preset.");
        var copy=ForeverClassBuildJson.Clone(original);
        var preset=ForeverClassBuildCatalog.Presets(copy.Character.ClassKey).SingleOrDefault(x=>x.Key==presetKey) ??
            throw new ArgumentException("Unknown talent preset for this class.");
        // This is intentionally the only gameplay input modified by Apply.
        copy.Character.ItemizedTalents=new(preset.Ranks,StringComparer.Ordinal);
        copy.PresetOrigin=preset.Key;
        var validation=Validate(copy,false);
        if(!validation.IsLegal)throw new ArgumentException(string.Join(" ",validation.LegalityErrors));
        return copy;
    }

    public static ForeverClassBuild Create(string cls)
    {
        if(!ForeverClassBuildCatalog.CanExecuteClassBuild(cls))
            throw new ArgumentException("This class-wide adapter is not executable yet.");
        var (specialization,race,baseline)=cls switch {
            "shaman"=>("elemental","orc","historical-provisional"),
            "rogue"=>("assassination","human","historical-provisional"),
            // Paladin has no verified live level-60 primary-stat record.
            // The user must explicitly choose a proxy or provide observations.
            "paladin"=>("retribution","human","strict-reviewed"),
            _=>throw new ArgumentException("No class-wide execution adapter for this class.")};
        var build=new ForeverClassBuild {Character=new() {
            ClassKey=cls,Specialization=specialization,
            Race=race,BaselineMode=baseline,
            ItemizedTalents=new(StringComparer.Ordinal),
            ClassSettings=ForeverClassBuildJson.Element(new {})
        }};
        var preset=ForeverClassBuildCatalog.Presets(cls).FirstOrDefault();
        return preset is null?build:ApplyPreset(build,preset.Key);
    }

    internal sealed record Prepared(object Request,object Preview,Func<CancellationToken,object> Run);

    private static object ReadSettings(string cls,string settings) => cls switch {
        "shaman"=>JsonSerializer.Deserialize<EnhancementShamanSimRequest>(settings,ForeverClassBuildJson.Options)!,
        "rogue"=>JsonSerializer.Deserialize<RogueSimRequest>(settings,ForeverClassBuildJson.Options)!,
        "paladin"=>JsonSerializer.Deserialize<PaladinSimRequest>(settings,PaladinSettingsJson)!,
        _=>throw new ArgumentException("No class-wide execution adapter for this class.")};

    internal static Prepared Prepare(ForeverClassBuild input)
    {
        var build=ForeverClassBuildJson.Clone(input);var c=build.Character;
        var settings=c.ClassSettings.ValueKind==JsonValueKind.Object?c.ClassSettings.GetRawText():"{}";
        var profile=new CharacterProfile {Name="Shared "+c.ClassKey,ClassKey=c.ClassKey,
            SpecializationKey=c.ClassKey switch {
                "shaman"=>"enhancement","rogue"=>"assassination","paladin"=>"retribution",
                _=>throw new ArgumentException("No class-wide execution adapter for this class.")},RulesetKey="wow-forever",
            Race=c.Race,Level=c.Level,BaseStats=new StatCollection(),Equipment=c.Equipment};
        var totals=ForeverClassBuildCatalog.Talents(c.ClassKey).GroupBy(x=>x.Tree)
            .ToDictionary(g=>g.Key,g=>g.Sum(x=>c.ItemizedTalents.GetValueOrDefault(x.Key)));
        if(c.ClassKey=="shaman")
        {
            var r=(EnhancementShamanSimRequest)ReadSettings(c.ClassKey,settings);
            r.UseClassWideTalents=true;r.Character=profile;
            r.UseReviewedLevel60Baseline=c.BaselineMode=="strict-reviewed";
            r.UseProvisionalLevel60Baseline=c.BaselineMode=="historical-provisional";
            r.Level60Baseline=c.Baseline;r.Seed=c.Encounter.Seed;r.DurationSeconds=c.Encounter.DurationSeconds;
            r.TargetMaximumHealth=c.Encounter.TargetMaximumHealth;r.TargetArmor=c.Encounter.TargetArmor;
            r.CaptureTimeline=c.Encounter.CaptureTimeline;r.ExternalModifiers=c.Modifiers;r.CombatItems=c.CombatItems;
            r.Talents=new ForeverEnhancementShamanTalents {Ranks=new(c.ItemizedTalents,StringComparer.Ordinal),
                ElementalPointsSpent=totals["elemental"],EnhancementPointsSpent=totals["enhancement"],RestorationPointsSpent=totals["restoration"]};
            r.ClassWideRotationPolicy=c.Rotation.Mode;
            r.CustomPriority=c.Rotation.Mode=="custom"?new(c.Rotation.Priority):null;
            return new(r,EnhancementShamanSimRunner.Preview(r),token=>new EnhancementShamanSimRunner().Run(r,token));
        }
        if(c.ClassKey=="rogue")
        {
            var r=(RogueSimRequest)ReadSettings(c.ClassKey,settings);
            r.UseClassWideTalents=true;r.Character=profile;r.Specialization="assassination";
            r.UseReviewedLevel60Baseline=c.BaselineMode=="strict-reviewed";
            r.UseProvisionalLevel60Baseline=c.BaselineMode=="historical-provisional";
            r.Level60Baseline=c.Baseline;r.Seed=c.Encounter.Seed;r.DurationSeconds=c.Encounter.DurationSeconds;
            r.TargetMaximumHealth=c.Encounter.TargetMaximumHealth;r.TargetArmor=c.Encounter.TargetArmor;
            r.CaptureTimeline=c.Encounter.CaptureTimeline;r.Modifiers=c.Modifiers;r.CombatItems=c.CombatItems;
            r.Talents=new ForeverRogueTalentBuild {Ranks=new(c.ItemizedTalents,StringComparer.Ordinal),
                AssassinationPointsSpent=totals["assassination"],CombatPointsSpent=totals["combat"],SubtletyPointsSpent=totals["subtlety"]};
            r.CustomPriority=c.Rotation.Mode=="custom"?new(c.Rotation.Priority):null;
            return new(r,RogueSimRunner.Preview(r),token=>new RogueSimRunner().Run(r,token));
        }
        if(c.ClassKey=="paladin")
        {
            // This server-owned profile identity is not an actor or execution
            // identifier. Keeping it stable makes repeated preparation exact.
            profile.Id=Guid.Empty;
            var r=(PaladinSimRequest)ReadSettings(c.ClassKey,settings);
            r.Character=profile;r.Talents=ForeverPaladinClassTalents.FromRanks(c.ItemizedTalents);
            r.UseReviewedLevel60Baseline=c.BaselineMode=="strict-reviewed";
            r.UseProvisionalLevel60Baseline=c.BaselineMode=="historical-provisional";
            r.Level60Baseline=c.Baseline;r.Seed=c.Encounter.Seed;r.DurationSeconds=c.Encounter.DurationSeconds;
            r.TargetMaximumHealth=c.Encounter.TargetMaximumHealth;r.TargetArmor=c.Encounter.TargetArmor;
            r.CaptureTimeline=c.Encounter.CaptureTimeline;r.ExternalModifiers=c.Modifiers;r.CombatItems=c.CombatItems;
            r.CustomPriority=c.Rotation.Mode=="custom"?new(c.Rotation.Priority):null;
            return new(r,PaladinSimRunner.Preview(r),token=>new PaladinSimRunner().Run(r,token));
        }
        throw new ArgumentException("No class-wide execution adapter for this class.");
    }

    public static object Run(ForeverClassBuild build,CancellationToken token)
    {
        var copy=ForeverClassBuildJson.Clone(build);
        var validation=Validate(copy);
        if(!validation.CanExecute)throw new ArgumentException(string.Join(" ",validation.LegalityErrors.Concat(validation.ExecutionBlockers)));
        token.ThrowIfCancellationRequested();
        var prepared=Prepare(copy);
        return new {schema="warcraftsim.class-build-report",version=1,
            build=copy,sourceCommit=Environment.GetEnvironmentVariable("CP174_SOURCE_COMMIT")??Environment.GetEnvironmentVariable("GITHUB_SHA")??"not-stamped-local-build",
            workflowCheckoutCommit=Environment.GetEnvironmentVariable("GITHUB_SHA")??"not-stamped-local-build",
            validation,preparation=prepared.Preview,originalSimulationRequest=prepared.Request,
            coverage=ForeverClassBuildCatalog.Talents(copy.Character.ClassKey),
            simulation=prepared.Run(token),
            notes=new[]{"Preset origin is provenance only and does not choose actions, formulas or a runner.",
                "Selected ranks, actual weapons, resources, auras and cooldowns govern the common live priority.",
                "Manual class assumptions, source uncertainty and missing mechanics remain separate from allocation legality."}};
    }
}
