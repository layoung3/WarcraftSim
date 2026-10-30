# CP173 — Character stats, racials and external modifiers

Starting point: merged main `32a74a1b334b5267cf09ed97bd6cb791a6d98cf7` (CP172 PR #25).

## Source and safety contract

- Retain all CP170–CP172 stat/gear/enchant behavior, class presets and previous tests.
- Implement opt-in source-identified modifiers through the existing character buff and simulation-event pipelines.
- No automatically active external buffs, no fabricated client progression/racial proc effects, and no double-use of CP172 weapon oil effects.
- Target debuffs are owned by the target aura/metadata, not inserted as attacker permanent stats.
- For current Forever data, directly established numeric values are usable **provisionally**; masked/hotfix fields and server-only mechanics are unmodeled.
- Prefer a shared collector/resolver plus thin Mage/Warlock/Hunter bridges. Preserve legacy explicit stats when no selections are made.

## Work plan

1. Add source-backed modifier catalog, validation, stacking/unique group policy, opt-in simulation selections, and a discovery API.
2. Connect approved self buffs to existing derived stat pipeline and actual Mage/Warlock/Hunter simulations. Integrate targeted vulnerability through the shared hit/damage pipeline with target ownership and nonstacking safeguards.
3. Add true build-pinned source census pipeline, reproducibility checks and integration tests. Broaden safe content where data allows.
4. Review race/class/level baseline sourcing and implement only verified/provisionally explicit ranges. Report unsupported combinations rather than inventing progression.
5. Strict .NET 10 Release build, full xUnit suite, Python import checks and exact-HEAD GitHub Actions validation.

**Draft PR only. Do not merge without explicit owner approval.**
