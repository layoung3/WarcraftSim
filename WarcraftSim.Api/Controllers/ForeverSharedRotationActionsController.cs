using Microsoft.AspNetCore.Mvc;

namespace WarcraftSim.Api.Controllers;

/// <summary>Server-owned action discovery for the four Solo DPS editors.
/// Availability of learned/cooldown-gated actions is still validated by
/// the real runner. Lists never imply unmodeled ability support.</summary>
[ApiController]
[Route("api/forever/shared/rotation-actions")]
public sealed class ForeverSharedRotationActionsController : ControllerBase
{
    [HttpGet("{classKey}/{spec}")]
    public IActionResult Get(string classKey,string spec)
    {
        string[]? actions=classKey switch {
            "mage" => ["arcane-power","presence-of-mind","arcane-blast",
                "arcane-wrath","arcane-missiles","mana-shield","fireball","frostbolt",
                "frostfire-bolt","fire-blast","scorch","pyroblast",
                "combustion","ice-lance","cold-snap","ice-barrier","ice-block","frost-nova","cone-of-cold","blizzard","mage-armor"],
            "warlock" => ["corruption","bane-of-agony","immolate","siphon-life",
                "wrack","shadow-bolt","incinerate","conflagrate","shadowburn",
                "searing-pain","soul-fire","drain-life","amplify-curse"],
            "hunter" when spec=="marksmanship" => ["serpent-sting","aimed-shot",
                "multi-shot","arcane-shot","sniper-shot","scatter-shot","rapid-fire","volley",
                "mongoose-bite","wing-clip","counterattack","strider-kick","deterrence",
                "explosive-trap","immolation-trap","frost-trap","freezing-trap",
                "aspect-of-the-hawk","aspect-of-the-beast","summon-hawk","intimidation","bestial-wrath"],
            "hunter" when spec=="survival" => ["raptor-strike","wing-clip",
                "mongoose-bite","counterattack","explosive-trap","immolation-trap",
                "frost-trap","freezing-trap","multi-shot","aimed-shot","arcane-shot","lacerate",
                "strider-kick","scatter-shot","sniper-shot","rapid-fire","volley",
                "aspect-of-the-hawk","aspect-of-the-beast","summon-hawk","intimidation","bestial-wrath"],
            "hunter" when spec=="beast-mastery" => ["serpent-sting","aimed-shot",
                "multi-shot","arcane-shot","rapid-fire","sniper-shot","scatter-shot","volley",
                "mongoose-bite","wing-clip","counterattack","strider-kick","deterrence",
                "explosive-trap","immolation-trap","frost-trap","freezing-trap",
                "aspect-of-the-hawk","aspect-of-the-beast","summon-hawk","intimidation","bestial-wrath"],
            "warrior" => ["bloodrage","mortal-strike","bloodthirst",
                "whirlwind","overpower","rend","slam","execute","heroic-strike",
                "sweeping-strikes","spearing-strike","shield-slam",
                "concussion-blow","revenge","shield-block"],
            _ => null
        };
        if(actions is null || (classKey=="mage"&&spec is not ("arcane" or "fire" or "frost")) ||
           (classKey=="warlock"&&spec is not ("affliction" or "demonology" or "destruction")) ||
           (classKey=="warrior"&&spec is not ("arms" or "fury" or "protection")))
            return BadRequest(new{error="Unsupported class or specialization."});
        var defaults=classKey switch {
            "hunter" when spec=="marksmanship" => new[]{"serpent-sting","aimed-shot","arcane-shot"},
            "hunter" when spec=="survival" => new[]{"raptor-strike","arcane-shot"},
            "hunter" when spec=="beast-mastery" => new[]{"serpent-sting","arcane-shot"},
            _ => Array.Empty<string>()
        };
        return Ok(new {classKey,spec,actions,defaultActions=defaults,
            note="The typed engine validates learned abilities and action prerequisites at execution."});
    }
}
