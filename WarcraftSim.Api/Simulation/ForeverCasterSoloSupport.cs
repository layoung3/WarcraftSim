using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>Shared class-neutral scaffolding for level-60 solo caster DPS previews.
/// Keeps new class runners on the same core event queue, player-profile stat pipeline,
/// combat-use items and reporting model as Mage/Warlock.</summary>
internal static class ForeverCasterSoloSupport
{
    public static (SimulationContext Context, SimulationActorState Actor, SimulationActorState Target,
        decimal SpellPower, decimal Mana) Create(CharacterProfile profile, string classKey,
        string specialization, int seed, decimal duration, bool capture, decimal health,
        decimal targetHealth, decimal startingMana, CancellationToken token,
        ForeverLevel60BaselineObservation? baseline=null,decimal intellectMultiplier=1m,
        ForeverExternalModifierSelection? externalModifiers=null,List<string>? preparationNotes=null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Level != 60 || !string.Equals(profile.ClassKey,classKey,StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profile.SpecializationKey,specialization,StringComparison.OrdinalIgnoreCase) ||
            duration is <= 0m or > 1200m || health <= 0m || targetHealth <= 0m ||
            startingMana is < 0m or > 100m)
            throw new ArgumentException($"Invalid level-60 {classKey} {specialization} preview configuration.");
        if(baseline is not null)
            profile=PrepareProfile(profile,baseline,externalModifiers??new(),duration,preparationNotes);
        else if(externalModifiers is not null &&
            ForeverExternalModifierCatalog.Resolve(externalModifiers,profile.Level,duration).Count>0)
            throw new ArgumentException("Selected external modifiers require an explicit level-60 baseline.");
        var stats=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>health,
            additionalStatContributors:baseline is null ? null : [new PreparedIntellectMultiplier(intellectMultiplier)])
            .Calculate(profile).EffectiveStats;
        var intellect=Math.Max(0m,stats.Get("intellect"));
        var mana=1376m+Math.Max(0m,intellect-20m)*15m;
        if(baseline is not null)
        {
            // The class multiplier already ran after raw contributions and before
            // the shared Intellect-to-spell-crit derivation, exactly once.
            // Observed deltas use the same clamped provisional conversion as
            // fallback Mana, including contributions that cross 20 Intellect.
            mana=(baseline.ObservedUnbuffedMaximumMana is { } observedMana
                ? observedMana+15m*(Math.Max(0m,intellect-20m)-Math.Max(0m,baseline.Intellect-20m))
                : ForeverClientBaseManaCatalog.GetBaseMana(classKey,60)+
                    Math.Max(0m,intellect-20m)*15m)+stats.Get("mana");
            // Without observed Health, health remains the manual scenario cap,
            // not a sourced naked HP value. Only post-baseline deltas are added.
            health=(baseline.ObservedUnbuffedMaximumHealth??health)+
                10m*(stats.Get("stamina")-baseline.Stamina)+stats.Get("health");
            if(mana<=0m || health<=0m)
                throw new ArgumentException("Prepared caster maximum Mana and Health must be positive.");
        }
        var context=new SimulationContext(new SimulationRunOptions
        {
            Seed=seed,DurationSeconds=duration,PrimaryActorKey=classKey,
            CaptureTimeline=capture,MaximumProcessedEvents=60000,CancellationToken=token
        });
        var actor=new SimulationActorState
        {
            Key=classKey,Name=profile.Name,TeamKey="raid",Level=60,MaximumHealth=health,Stats=stats
        };
        actor.InitializeHealth(health);
        actor.AddResource(new ResourceState("mana",mana,mana*startingMana/100m));
        var boss=new SimulationActorState
        {
            Key="target",Name=$"Level 63 {classKey} training boss",TeamKey="enemy",
            Level=63,MaximumHealth=targetHealth
        };
        boss.InitializeHealth(targetHealth);
        context.AddActor(actor);context.AddActor(boss);
        return (context,actor,boss,Math.Max(0m,stats.Get("spell-power")),mana);
    }

    // Prepared-only boundary. Raw authored legacy equipment remains supported,
    // but it must not be mixed with catalog selections whose stats are server-owned.
    internal static CharacterProfile PrepareProfile(CharacterProfile source,
        ForeverLevel60BaselineObservation baseline,ForeverExternalModifierSelection selection,
        decimal duration,List<string>? notes=null)
    {
        var profile=CasterSimulationRequestSnapshot.GenerateBaseline(source,baseline);
        var modifiers=ForeverExternalModifierCatalog.Resolve(selection,profile.Level,duration);
        if(modifiers.Any(x=>x.Category=="target-debuff"))
            throw new ArgumentException("Prepared casters do not yet execute selected target debuffs; damage remains unmitigated.");
        if(modifiers.Any(x=>!x.Modeled || x.Stats.Count==0))
            throw new ArgumentException("Prepared casters support only modeled static external modifiers.");
        // Core trims contributor keys. Canonicalize recognized raw buff keys
        // before checking selected aliases and their shared exclusivity groups.
        foreach(var buff in profile.Buffs.Selections.Where(x=>x is not null && x.IsEnabled))
        {
            var key=(buff.BuffKey??"").Trim();
            if(key.StartsWith("cp173-",StringComparison.OrdinalIgnoreCase))key=key[6..];
            var known=ForeverExternalModifierCatalog.Entries.FirstOrDefault(x=>
                x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
            if(known is not null && modifiers.Any(x=>x.Key==known.Key ||
                !string.IsNullOrEmpty(known.ExclusiveGroup) && x.ExclusiveGroup==known.ExclusiveGroup))
                throw new ArgumentException($"Modifier '{known.Key}' already contributes or conflicts with a selected modifier group.");
        }
        // An explicitly selected effect replaces an inactive entry with its
        // exact normalized contributor key on this isolated profile only.
        profile.Buffs.Selections.RemoveAll(x=>x is not null && !x.IsEnabled &&
            modifiers.Any(m=>string.Equals((x.BuffKey??"").Trim(),"cp173-"+m.Key,
                StringComparison.OrdinalIgnoreCase)));
        var withModifiers=ForeverExternalModifierCatalog.ApplyToProfile(profile,selection,duration);
        CasterSimulationRequestSnapshot.PreserveStatComparers(profile,withModifiers);
        profile=withModifiers;
        if(profile.Equipment?.Items is null)
            throw new ArgumentException("Character equipment items cannot be null.");
        if(profile.Equipment.Items.Any(x=>x is null))
            throw new ArgumentException("Equipment contains a null item.");
        if(profile.Equipment.Items.Any(x=>x.ItemId is null &&
            (x.EnchantSpellId is not null || x.TemporaryEnchantSpellId is not null)))
            throw new ArgumentException("Catalog enchant requires a catalog item ID.");
        var catalogSelected=profile.Equipment.Items.Any(x=>x.ItemId is not null);
        if(catalogSelected)
        {
            if(profile.Equipment.Items.Any(x=>x.ItemId is null))
                throw new ArgumentException("Prepared catalog equipment cannot be mixed with raw authored equipment.");
            foreach(var entry in profile.Equipment.Items)
            {
                var item=ForeverEquipmentCatalog.FindItem(entry.ItemId!.Value);
                ValidatePreparedItem(item,profile.ClassKey);
                foreach(var id in new[]{entry.EnchantSpellId,entry.TemporaryEnchantSpellId})
                {
                    if(id is not int spellId)continue;
                    var enchant=ForeverEquipmentCatalog.FindEnchant(spellId);
                    if(!enchant.Modeled || enchant.Stats.Count==0 ||
                       !IsSourced(enchant.SourceUrl,enchant.SourceBuild) ||
                       string.IsNullOrWhiteSpace(enchant.SourceStatus) ||
                       enchant.SourceStatus.Contains("classic",StringComparison.OrdinalIgnoreCase) ||
                       enchant.IsTemporary && enchant.DurationSeconds<duration)
                        throw new ArgumentException($"Enchant {spellId} lacks supported full-duration static caster data.");
                }
            }
            if(ForeverEquipmentEffectsCatalog.Select(profile.Equipment,includeUnmodeled:true).Count>0)
                throw new ArgumentException("Prepared casters do not support dynamic equipment or enchant effects.");
            // Reuse the existing slot/level/faction/unique/armor/hand/enchant checks.
            // Only the normalized loadout contributes; its intermediate derived
            // equipment-only snapshot is never added to this character's stats.
            var resolved=ForeverEquipmentStatBridge.ResolveProfile(profile);
            CasterSimulationRequestSnapshot.PreserveStatComparers(profile,resolved,includeEquipment:false);
            profile=resolved;
            foreach(var entry in profile.Equipment.Items)
            {
                var item=ForeverEquipmentCatalog.FindItem(entry.ItemId!.Value);
                notes?.Add($"Prepared equipment {item.Id} ({item.Name}): {item.SourceStatus}; {item.VerificationStatus}; build {item.SourceBuild}; source {item.SourceUrl}. Restriction evidence {item.ClassRestrictionEvidence!.Status}, confidence {item.ClassRestrictionEvidence.DataConfidence}, raw mask {item.ClassRestrictionEvidence.RawMask}, source {item.ClassRestrictionEvidence.SourceUrl}, SHA-256 {item.ClassRestrictionEvidence.SourceSha256}. Catalog confidence is not independent in-game certification.");
                foreach(var id in new[]{entry.EnchantSpellId,entry.TemporaryEnchantSpellId})
                {
                    if(id is not int spellId)continue;
                    var enchant=ForeverEquipmentCatalog.FindEnchant(spellId);
                    notes?.Add($"Prepared enchant {spellId} ({enchant.Name}): {enchant.SourceStatus}; {enchant.VerificationStatus}; build {enchant.SourceBuild}; source {enchant.SourceUrl}.");
                }
            }
        }
        foreach(var effect in modifiers)
            notes?.Add($"Prepared modifier {effect.Key}: {effect.VerificationStatus}; {effect.DataConfidence}; {effect.ImplementationStatus}; build {effect.SourceBuild}; source {effect.SourceUrl}.");
        // Normalize aliases per contributor, not after summing: independent
        // sources stack, while the same contributor cannot count two aliases.
        NormalizeManaRegen(profile.BaseStats);
        foreach(var item in profile.Equipment.Items)
        {
            NormalizeManaRegen(item.Stats);
            foreach(var modifier in item.Modifiers)NormalizeManaRegen(modifier.Stats);
        }
        foreach(var talent in profile.Talents.Selections)NormalizeManaRegen(talent.Stats);
        foreach(var buff in profile.Buffs.Selections)NormalizeManaRegen(buff.Stats);
        notes?.Add("Prepared static catalog stats, enchants and selected self modifiers apply once before spell construction. Historical catalog class lists do not establish restriction evidence; legacy items remain pending unless consistent pinned restriction evidence accompanies their record. The small reviewed published-client subset is explicitly provisional. Unsupported set bonuses remain inert. Dynamic equipment effects and selected target debuffs are rejected. Damage is unmitigated; armor/resistance budgets do not certify mitigation, and Spirit-based regeneration remains manually tuned. Shared cast haste affects newly started hardcasts only, not class GCD rules, channels or in-flight casts.");
        return profile;
    }

    internal static void ValidatePreparedItem(ForeverEquipmentItem item,string classKey)
    {
        ForeverPaladinEquipmentCatalog.ValidateReviewedExecutionScope(item,classKey);
        if(item.ClassRestrictionEvidence?.IsConsistentWith(item)!=true)
            throw new ArgumentException($"Item {item.Id} has unknown/pending or inconsistent class-restriction evidence; historical class lists alone do not authorize prepared caster gear.");
        if(item.SourceStatus is not ("forever-client-verified-tooltip" or
               "forever-client-db2-reviewed" or "reviewed-published-forever-tooltip" or "reviewed-published-forever-client-record") ||
           item.VerificationStatus is not ("client-tooltip" or "client-db2-reviewed" or "published-client-tooltip" or "published-client-record") ||
           !IsSourced(item.SourceUrl,item.SourceBuild) ||
           item.AllowedClasses is null || item.AllowedClasses.Count==0 ||
           !item.AllowedClasses.Contains(classKey,StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Item {item.Id} lacks sourced explicit {classKey} eligibility for prepared casters.");
        var classified=item.ItemClass switch {
            "armor" => item.ItemSubclass is "cloth" or "leather" or "mail" or "plate" or "shield" or "miscellaneous",
            "weapon" => item.Weapon is { } weapon &&
                item.ItemSubclass==weapon.Type &&
                weapon.Type is "axe" or "mace" or "sword" or "dagger" or "fist" or "staff" or
                    "polearm" or "bow" or "gun" or "crossbow" or "wand" or "thrown" &&
                weapon.Hand is "one-hand" or "two-hand" or "ranged" &&
                weapon.SpeedSeconds>0m && weapon.MinimumDamage>=0m && weapon.MaximumDamage>=weapon.MinimumDamage,
            _ => false
        };
        if(!classified || item.AllowedSlots is null || item.AllowedSlots.Count==0)
            throw new ArgumentException($"Item {item.Id} lacks complete equipment classification for prepared casters.");
        if(item.ItemClass=="weapon")
            throw new ArgumentException($"Item {item.Id} prepared weapon proficiency is pending separate evidence; an item class-restriction mask alone is insufficient.");
        if(item.Effects.Count>0)
            throw new ArgumentException($"Item {item.Id} has unsupported equipment effects for prepared casters.");
    }

    private static bool IsSourced(string url,string build)=>
        !string.IsNullOrWhiteSpace(build) && Uri.TryCreate(url,UriKind.Absolute,out var uri) &&
        uri.Scheme==Uri.UriSchemeHttps;

    private static void NormalizeManaRegen(StatCollection stats)
    {
        // Stat-source adapters trim keys and resolve them case-insensitively,
        // even when a caller's source dictionary itself is ordinal-sensitive.
        var aliases=stats.Values.Where(x=>
            x.Key.Trim().Equals("mana-per-5",StringComparison.OrdinalIgnoreCase) ||
            x.Key.Trim().Equals("mp5",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(aliases.Length==0)return;
        if(aliases.GroupBy(x=>x.Key.Trim(),StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1))
            throw new ArgumentException("Duplicate canonical Mana regeneration stat key.");
        if(aliases.Any(x=>x.Value!=aliases[0].Value))
            throw new ArgumentException("Conflicting mana-per-5 and mp5 aliases in one stat contributor.");
        foreach(var entry in aliases)stats.Values.Remove(entry.Key);
        stats.Set("mp5",aliases[0].Value);
    }

    // Run before the existing derived pipeline, so its class-specific client
    // Intellect conversion owns spell crit as well as the resource calculation.
    private sealed class PreparedIntellectMultiplier(decimal multiplier):ICharacterSimulationStatContributor
    {
        public string Key=>"prepared-caster-intellect-multiplier";
        public int Order=>CharacterSimulationStatContributorOrders.Derived-1;
        public void Contribute(CharacterProfile profile,StatCollection stats)=>
            stats.Set("intellect",Math.Max(0m,stats.Get("intellect"))*multiplier);
    }

    // Normalize only the isolated prepared request. Never sum duplicate inventory
    // aliases, and never instantiate the legacy potion processor on this path.
    // Positive shared inventory keeps its threshold, including equal aliases.
    // Copy the legacy threshold only when adapting legacy-only inventory.
    public static void PrepareCombatItems(CombatUseItemsOptions items,int legacyCharges,
        decimal legacyThreshold)
    {
        if(legacyCharges is <0 or >20 || legacyThreshold is <0m or >100m)
            throw new ArgumentException("Invalid Mana Potion charges or threshold.");
        if(legacyCharges<=0)return;
        if(items.MajorManaPotionCharges>0 && items.MajorManaPotionCharges!=legacyCharges)
            throw new ArgumentException("Conflicting Major Mana Potion inventories.");
        if(items.MajorManaPotionCharges==0)
        {
            items.MajorManaPotionCharges=legacyCharges;
            items.MajorManaPotionBelowManaPercent=legacyThreshold;
        }
    }

    public static IEnumerable<string> BaselineModelingNotes(
        ForeverLevel60BaselineObservation? baseline,string classKey)
    {
        if(baseline is null)return [];
        return [
            $"Prepared level-60 baseline: {baseline.VerificationStatus}; {baseline.SourceKind}; build {baseline.ClientBuild}; source {baseline.SourceReference}. Metadata does not independently certify an observation.",
            baseline.ObservedUnbuffedMaximumMana is not null
                ? "Maximum Mana uses the supplied naked observation plus post-baseline Intellect and flat Mana contributions."
                : $"Base Mana {ForeverClientBaseManaCatalog.GetBaseMana(classKey,60)} is directly sourced from PlayerExpectedStat ContentSetID=0, build {ForeverClientBaseManaCatalog.SourceBuild}, SHA-256 {ForeverClientBaseManaCatalog.SourceSha256}; it is not maximum Mana.",
            baseline.ObservedUnbuffedMaximumHealth is not null
                ? "Maximum Health uses the supplied naked observation plus post-baseline Stamina and flat Health contributions."
                : "No naked maximum Health observation: MaximumHealth is a manual scenario cap, not observed or source-verified Health; post-baseline Stamina and flat Health contributions are added.",
            "The existing 15 Mana per post-baseline Intellect and 10 Health per post-baseline Stamina conversions remain provisional. Historical-provisional primary attributes are not Forever-verified. Racial passives are not added by this preparation step. Catalog equipment requires separately pinned class-restriction evidence, explicit class eligibility and static-only support; its actual source build and confidence are retained.",
            "Prepared combat items use one shared inventory and scheduler; conflicting positive legacy/shared potion quantities are rejected. A selected shared inventory keeps its threshold; legacy-only inventory copies the legacy threshold."
        ];
    }

    public static void ValidatePercent(decimal value,string name)
    {
        if(value is < 0m or > 100m)throw new ArgumentException($"{name} must be between 0 and 100.");
    }

    public sealed class SpellRolls(decimal hit,decimal crit,decimal criticalMultiplier) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect)=>
            Resolve(context,src,dst,ability,effect,CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if(effect.CanMiss && (decimal)context.Random.NextDouble()*100m>=
                Math.Min(100m,hit+src.Stats.Get("spell-hit-percent")))return CombatRollResult.Miss();
            if(effect.CanCrit && (decimal)context.Random.NextDouble()*100m<
                Math.Min(100m,crit+effect.BonusCriticalChancePercent))
                return CombatRollResult.Critical(criticalMultiplier);
            return CombatRollResult.Hit();
        }
    }
    public sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect,
            decimal raw)=>DamageMitigationResult.Unmitigated(raw);
    }
}
