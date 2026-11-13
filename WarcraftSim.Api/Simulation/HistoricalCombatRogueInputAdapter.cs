using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;

namespace WarcraftSim.Api.Simulation;

public sealed record HistoricalCombatRoguePreparation(RogueSimRequest Request, RoguePrecombatPreview Preview);

/// <summary>Admission only: reuse the existing isolated Rogue preparation and runner.
/// No legacy DTO, preset, numerical formula or general talent cap changes.</summary>
public static class HistoricalCombatRogueInputAdapter
{
    private static readonly JsonSerializerOptions Web=new(JsonSerializerDefaults.Web);

    public static HistoricalCombatRoguePreparation Prepare(RogueSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var copy=RogueSimulationRequestSnapshot.Create(request);
        if(copy.Specialization!="combat" || copy.Character is not { ClassKey:"rogue", SpecializationKey:"combat",
            Level:60, RulesetKey:"wow-forever" } character || copy.Talents?.Ranks is null || copy.Tuning is null)
            throw new ArgumentException("Historical Combat requires matching canonical level-60 Rogue/Combat identities, ruleset, talents and tuning.");
        if(copy.MainHandWeaponType!="sword" || copy.OffHandWeaponType!="sword" || copy.BothWeaponsDaggers ||
           copy.AdditionalTargets!=0 || copy.EnableBossAttacks || copy.UseUtilityAbilities)
            throw new ArgumentException("Historical Combat requires fixed sword/sword weapons, BothWeaponsDaggers=false, one target, no incoming attacks and no utility rotation.");
        if(character.Buffs?.Selections is null || character.Buffs.Selections.Count!=0 ||
           character.Talents?.Selections is null || character.Talents.Selections.Count!=0 ||
           character.Equipment?.Items is null || character.Equipment.Items.Any(x=>x is null ||
               x.ItemId.HasValue || x.EnchantSpellId.HasValue || x.TemporaryEnchantSpellId.HasValue ||
               x.Stats?.Values is null || x.Modifiers is null || x.Modifiers.Count!=0))
            throw new ArgumentException("Historical Combat permits authored equipment stat budgets only, not catalog items/enchants, generic talents, buffs or equipment modifiers.");
        var baseline=ForeverLevel60Baseline.Resolve(copy.Level60Baseline,copy.UseReviewedLevel60Baseline,
            character.Race??"","rogue",copy.UseProvisionalLevel60Baseline) ??
            throw new ArgumentException("Historical Combat requires an explicitly selected level-60 baseline.");
        ForeverLevel60Baseline.Validate(baseline);
        if(baseline.ClassKey!=character.ClassKey || baseline.Race!=character.Race || baseline.Level!=character.Level)
            throw new ArgumentException("Historical Combat baseline must match the selected class, race and level.");
        var valid=new ForeverSoloTalentController().ValidateItemized("rogue","combat",
            JsonSerializer.SerializeToElement(copy.Talents,Web));
        if(valid is not OkObjectResult)
            throw new ArgumentException("Historical Combat requires a valid strict historical itemized allocation.");
        // Preview and execution use this same owned request; both delegate all
        // numerical preparation to the unchanged legacy Rogue runner.
        var preview=RogueSimRunner.Preview(copy);
        return new(copy,preview with { ModelingNotes=[
            ..preview.ModelingNotes.Where(note=>!note.StartsWith("Preview validates the existing legacy runner preparation",StringComparison.Ordinal)),
            "This Combat endpoint additionally validates the strict historical allocation and fixed-sword boundary before using the existing exact runner preparation. Configured priority is initial state; dynamic costs and combat gating have not executed.",
            ..ForeverHistoricalCasterTalentCatalog.CombatRogueModelingNotes] });
    }
}
