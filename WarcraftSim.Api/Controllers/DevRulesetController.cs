using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/dev/ruleset")]
public sealed class DevRulesetController :
    ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var metadata =
            new RulesetFreshnessMetadata
            {
                RulesetKey =
                    "forever-beta",

                RulesetVersion =
                    "development",

                LastVerifiedUtc =
                    new DateTimeOffset(
                        2026,
                        9,
                        18,
                        0,
                        0,
                        0,
                        TimeSpan.Zero
                    ),

                Mechanics =
                [
                    new RulesetMechanicStatus
                    {
                        MechanicKey =
                            "spell-hit",

                        DisplayName =
                            "Spell Hit",

                        Status =
                            RulesetMechanicStatuses.Investigating,

                        Confidence =
                            RulesetConfidenceLevels.Medium,

                        ImplementedRevision =
                            "development-v1",

                        LatestKnownRevision =
                            "development-v1",

                        HasKnownUpdate =
                            false,

                        Notes =
                            "Development values are still in use until Forever beta values are sufficiently established."
                    },

                    new RulesetMechanicStatus
                    {
                        MechanicKey =
                            "armor-mitigation",

                        DisplayName =
                            "Armor Mitigation",

                        Status =
                            RulesetMechanicStatuses.Provisional,

                        Confidence =
                            RulesetConfidenceLevels.Medium,

                        ImplementedRevision =
                            "development-v1",

                        LatestKnownRevision =
                            "development-v2",

                        // Intentionally true so the dev endpoint proves
                        // that the user-facing update notice path works.
                        HasKnownUpdate =
                            true,

                        Notes =
                            "Example update notice only. This is not claiming a real Forever formula change."
                    },

                    new RulesetMechanicStatus
                    {
                        MechanicKey =
                            "partial-resistance",

                        DisplayName =
                            "Partial Resistance",

                        Status =
                            RulesetMechanicStatuses.Investigating,

                        Confidence =
                            RulesetConfidenceLevels.Low,

                        ImplementedRevision =
                            "average-reduction-dev",

                        LatestKnownRevision =
                            "average-reduction-dev",

                        HasKnownUpdate =
                            false,

                        Notes =
                            "Discrete partial-resist behavior is not implemented yet."
                    }
                ]
            };

        var notices =
            RulesetFreshnessEvaluator
                .GetUpdateNotices(
                    metadata
                );

        return Ok(
            new
            {
                metadata,
                notices
            }
        );
    }
}
