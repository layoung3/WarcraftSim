using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// One class-wide learned spellbook. The rank map, equipment and explicit tuning
/// are the only inputs; an originating preset or specialization is not consulted.
/// Numeric spell ranks remain labeled provisional where current endgame values
/// are unavailable. Dated official hotfixes override older client tooltips.
/// </summary>
public static class ForeverShamanClassSpellbook
{
    public static ForeverElementalShamanTuning ReviewedElementalTuning() => new()
    {
        // Current 70888 client mirror, consistent with official November2:
        // https://theforeverera.com/en/spells/shaman/ (reviewed2026-11-12).
        LightningBoltDamage = 353m, //334–372
        ChainLightningDamage = 510m, //482–538
        ChainLightningMana = 495m,
        EarthShockDamage = 531m, //517–545
        FlameShockDirectDamage = 292m,
        FlameShockTickDamage = 80m,
        FlameShockMana = 410m,
        LightningOverloadChancePercentRank1 = 10m,
        LightningOverloadChancePercentRank2 = 20m,
        LightningOverloadChancePercentRank3 = 30m
    };

    public static ForeverRestorationShamanTuning ReviewedRestorationTuning() => new()
    {
        HealingPower=0m,RiptideMana=230m,RiptidePeriodicTick=161m
    };

    public static ForeverRestorationShamanTalents RestorationTalents(
        ForeverEnhancementShamanTalents talents) => new()
    {
        RestorationPointsSpent = talents.RestorationPointsSpent,
        OtherTreePointsSpent = talents.ElementalPointsSpent + talents.EnhancementPointsSpent,
        Ranks = talents.Ranks.Where(x => ForeverShamanClassTalents.Find(x.Key)?.Tree == "restoration")
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
    };

    public static IReadOnlyList<AbilityDefinition> Create(ForeverEnhancementShamanTalents talents,
        ForeverEnhancementShamanTuning enhancement, ForeverElementalShamanTuning elemental,
        ForeverRestorationShamanTuning restoration, decimal weaponSpeed,
        decimal weaponMinimum, decimal weaponMaximum, bool dualWield,decimal baseMana=1376m)
    {
        ForeverShamanClassTalents.Validate(talents.Ranks, talents.ElementalPointsSpent,
            talents.EnhancementPointsSpent, talents.RestorationPointsSpent);
        int rank(string key) => talents.Rank(key);
        var elementRanks = new Dictionary<string,int>(talents.Ranks, StringComparer.OrdinalIgnoreCase)
        {
            // Each totem is discounted once after the shared book is assembled.
            ["totemic-focus"] = 0
        };
        var elements = new ForeverElementalShamanTalents { Ranks = elementRanks };
        var book = ForeverElementalShamanSpellbook.Create(elements, elemental, 0m)
            .Where(x => x.Key is not ("lava-burst" or "elemental-mastery" or "lightning-bolt-rank7"))
            .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach(var ability in ForeverEnhancementShamanSpellbook.Create(enhancement, talents, 0m,
            weaponSpeed, weaponMinimum, weaponMaximum, dualWield))
            if(!book.ContainsKey(ability.Key) && ability.Key != "maelstrom-lightning-bolt")
                book.Add(ability.Key, ability);
        var resto = RestorationTalents(talents);
        foreach(var ability in ForeverRestorationShamanSpellbook.Create(resto, restoration, classWide:true))
            book.Add(ability.Key, ability);
        foreach(var ability in ForeverShamanClassUtilityAbilities.Create(talents,baseMana))book.Add(ability.Key,ability);

        var nature = 1m + .01m * rank("concussion");
        var fire = 1m + .05m * rank("call-of-flame");
        var purification = 1m + .02m * rank("purification");
        var healingWay = rank("healing-way") switch {1=>1.08m,2=>1.17m,3=>1.25m,_=>1m};
        var chain = book["chain-lightning"];
        chain.CastTimeSeconds = 2.5m - (rank("elemental-alacrity") switch {1=>.17m,2=>.33m,3=>.5m,_=>0m});
        // Current70888 rank4:492–520,430Mana. The .386 coefficient is
        // source-backed historical provisional, independent of current base.
        book.Add("frost-shock",new AbilityDefinition
        {
            Key="frost-shock",Name="Frost Shock Rank 4",ClassKey="shaman",GlobalCooldownSeconds=1.5m,
            CooldownSeconds=6m-.2m*rank("reverberation"),SharedCooldownGroupKey="shaman-shocks",
            SharedCooldownSeconds=6m-.2m*rank("reverberation"),
            ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=430m*(1m-.02m*rank("convection"))*(rank("shamanistic-focus")>0?.55m:1m)}],
            Effects=[new AbilityEffectDefinition
            {
                Key="frost-shock-impact",EffectType=AbilityEffectTypes.DirectDamage,TargetType=AbilityTargetTypes.Enemy,
                SchoolKey="frost",ResolutionType=CombatResolutionTypes.Spell,CanMiss=true,CanCrit=true,
                MinimumValue=506m,MaximumValue=506m,ScalingStatKey="spell-power",ScalingCoefficient=.386m
            }]
        });
        // Current70888 rank5 confirms226–252. This takes precedence over
        // historical upstream232–258, retained in provenance notes.
        var nova = book["fire-nova"].Effects.Single();
        nova.MinimumValue = nova.MaximumValue = 239m * fire * (1m + .1m * rank("trial-by-fire"));
        var magma = new AbilityDefinition
        {
            Key="magma-totem", Name="Magma Totem Rank 4", ClassKey="shaman",
            GlobalCooldownSeconds=1m,
            ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=650m}],
            Effects=[new AbilityEffectDefinition
            {
                Key="magma-totem-tick", EffectType=AbilityEffectTypes.PeriodicDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="fire",
                ResolutionType=CombatResolutionTypes.Spell,CanMiss=false,CanCrit=true,
                AuraKey="magma-totem",DurationSeconds=20m+10m*rank("trial-by-fire"),
                TickIntervalSeconds=2m,IncludeExpirationBoundaryTick=true,MaxTargets=9,
                MinimumValue=73m*fire,MaximumValue=73m*fire,
                ScalingStatKey="spell-power",ScalingCoefficient=.033m*fire
            }]
        };
        book.Add(magma.Key,magma);
        if(rank("lightning-mastery")>0)
            book.Add("lightning-mastery",new AbilityDefinition
            {
                Key="lightning-mastery",Name="Lightning Mastery",ClassKey="shaman",
                IsOffGlobalCooldown=true,CooldownSeconds=180m,
                Effects=[new AbilityEffectDefinition
                {
                    Key="lightning-mastery-buff",EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self,AuraKey="lightning-mastery",
                    DurationSeconds=15m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                    CanMiss=false,CanCrit=false
                }]
            });
        if(rank("maelstrom-weapon")>0)
        {
            var baseBolt=book["lightning-bolt"];
            book.Add("maelstrom-lightning-bolt",new AbilityDefinition
            {
                Key="maelstrom-lightning-bolt",Name="Maelstrom Lightning Bolt",ClassKey="shaman",
                GlobalCooldownSeconds=1.5m,RequiredSourceAuraKeys=["maelstrom-ready"],
                Effects=[new AbilityEffectDefinition
                {
                    Key="maelstrom-bolt-hit",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="nature",
                    ResolutionType=CombatResolutionTypes.Spell,CanMiss=true,CanCrit=true,
                    MinimumValue=baseBolt.Effects[0].MinimumValue,MaximumValue=baseBolt.Effects[0].MaximumValue,
                    ScalingStatKey="spell-power",ScalingCoefficient=.714m*nature
                }]
            });
        }
        foreach(var ability in book.Values)
        {
            ability.SpecializationKey="";
            if(!ability.IsPassive && ability.Key is not ("ghost-wolf" or "cancel-ghost-wolf"))
                ability.ForbiddenSourceAuraKeys.Add("shaman-ghost-wolf");
            foreach(var effect in ability.Effects)
            {
                var coefficient=effect.Key switch
                {
                    "lightning-bolt-impact"=>.714m*nature,
                    "chain-lightning-impact"=>.571m*nature,
                    "earth-shock-impact"=>.386m*nature,
                    "flame-shock-impact"=>.214m*fire,
                    "flame-shock-tick"=>.10m*fire,
                    "searing-totem-tick"=>.017m*fire,
                    "fire-nova-impact"=>.143m*fire*(1m+.1m*rank("trial-by-fire")),
                    "overload-bolt-impact"=>.714m*nature*.5m,
                    "overload-chain-impact"=>.571m*nature*.5m,
                    "healing-wave-heal"=>.857m*purification*healingWay,
                    "lesser-healing-wave-heal"=>.429m*purification,
                    "chain-heal-primary"=>.714m*purification,
                    "riptide-tick"=>.10m*purification,
                    _=>0m
                };
                if(coefficient>0m)
                {
                    effect.ScalingStatKey=effect.EffectType is AbilityEffectTypes.DirectHealing or AbilityEffectTypes.PeriodicHealing
                        ? "healing-power" : "spell-power";
                    effect.ScalingCoefficient=coefficient;
                }
            }
            if(ForeverShamanTotemCalls.Slots.ContainsKey(ability.Key))
                foreach(var cost in ability.ResourceCosts.Where(x=>x.ResourceKey=="mana"))
                    cost.Amount*=1m-.05m*rank("totemic-focus");
        }
        var spring=book["mana-spring-totem"].Effects.Single();
        spring.MinimumValue*=1m+.05m*rank("restorative-totems");
        spring.MaximumValue=spring.MinimumValue;
        // Overload follows the real Chain Lightning target cap and coefficient.
        book["lightning-overload-chain"].Effects.Single().MaxTargets=3;
        foreach(var key in new[]{"lightning-overload-bolt","lightning-overload-chain"})
            book[key].IsPassive=true;
        return book.Values.ToArray();
    }
}
