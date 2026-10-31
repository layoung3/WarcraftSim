using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Api.Controllers;

/// <summary>Read-only versioned offline items and shared loadout resolution.
/// No runtime network dependency. Imported metadata is never user-supplied
/// combat stats, and unsupported item effects fail closed.</summary>
[ApiController]
[Route("api/forever/equipment")]
public sealed class ForeverEquipmentController : ControllerBase
{
    [HttpGet("items")]
    public IActionResult Items([FromQuery] string? search=null,
        [FromQuery] string? slot=null,[FromQuery] int page=1,[FromQuery] int pageSize=25,
        [FromQuery] bool usableOnly=false,[FromQuery] string? classKey=null,
        [FromQuery] string? race=null,[FromQuery] int level=60)
    {
        if(page<1 || pageSize is <1 or >200)
            return BadRequest(new { Error="Page must be positive and pageSize 1–200." });
        var catalog=ForeverEquipmentCatalog.GetCatalog();
        IEnumerable<ForeverEquipmentItem> source=catalog.Items;
        if(!string.IsNullOrWhiteSpace(search))
            source=source.Where(x=>x.Name.Contains(search,StringComparison.OrdinalIgnoreCase) ||
                x.Id.ToString().Contains(search,StringComparison.Ordinal));
        if(!string.IsNullOrWhiteSpace(slot))
            source=source.Where(x=>x.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase));
        if(usableOnly)
        {
            if(string.IsNullOrWhiteSpace(classKey)||string.IsNullOrWhiteSpace(race))
                return BadRequest(new{Error="Class and race required for compatible item filtering."});
            source=source.Where(item=>item.AllowedSlots.Any(equipSlot=>{
                try{
                    _=ForeverSharedEquipmentResolver.Resolve(new CharacterEquipmentLoadout {
                        Items=[new CharacterEquipmentItem {SlotKey=equipSlot,ItemId=item.Id}]
                    },classKey,level,race);
                    return true;
                }catch(ArgumentException){return false;}
            }));
        }
        var sorted=source.OrderBy(x=>x.Id).ToArray();
        return Ok(new {
            catalogVersion=catalog.CatalogVersion,
            total=sorted.Length,page,pageSize,
            items=sorted.Skip((page-1)*pageSize).Take(pageSize)
        });
    }

    [HttpGet("enchants")]
    public IActionResult Enchants([FromQuery] string? search=null,
        [FromQuery] string? slot=null,[FromQuery] bool? modeled=null,
        [FromQuery] bool? temporary=null,[FromQuery] int page=1,[FromQuery] int pageSize=25)
    {
        if(page<1 || pageSize is <1 or >200)
            return BadRequest(new {Error="Page must be positive and pageSize 1–200."});
        IEnumerable<ForeverEquipmentEnchant> query=ForeverEquipmentCatalog.GetCatalog().Enchants;
        if(!string.IsNullOrWhiteSpace(search))
            query=query.Where(x=>x.Name.Contains(search,StringComparison.OrdinalIgnoreCase)
                || x.SpellId.ToString().Contains(search,StringComparison.Ordinal));
        if(!string.IsNullOrWhiteSpace(slot))
            query=query.Where(x=>x.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase));
        if(modeled.HasValue)query=query.Where(x=>x.Modeled==modeled.Value);
        if(temporary.HasValue)query=query.Where(x=>x.IsTemporary==temporary.Value);
        var all=query.OrderBy(x=>x.SpellId).ToArray();
        return Ok(new {catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
            total=all.Length,page,pageSize,enchants=all.Skip((page-1)*pageSize).Take(pageSize)});
    }

    // Slot + actual item target filtering, not just displayed enchant slot.
    // Off-hand nonweapons must never be offered weapon-only applications.
    [HttpGet("items/{itemId:int}/compatible-enchants")]
    public IActionResult CompatibleEnchants(int itemId,[FromQuery] string slot,
        [FromQuery] bool? temporary=null)
    {
        ForeverEquipmentItem item;
        try{item=ForeverEquipmentCatalog.FindItem(itemId);}
        catch(ArgumentException e){return NotFound(new {Error=e.Message});}
        if(string.IsNullOrWhiteSpace(slot) ||
           !item.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase))
            return BadRequest(new {Error="Requested slot is not compatible with this item."});
        var result=ForeverEquipmentCatalog.GetCatalog().Enchants.Where(enchant=>
            enchant.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase) &&
            (!temporary.HasValue || enchant.IsTemporary==temporary.Value) &&
            (!enchant.RequiresWeapon || item.Weapon is not null) &&
            (!enchant.RequiresTwoHandedWeapon || item.Weapon?.Hand=="two-hand"))
            .OrderBy(x=>x.SpellId).ToArray();
        return Ok(new {catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
            itemId,slot,total=result.Length,
            enchants=result});
    }

    [HttpGet("enchants/{spellId:int}")]
    public IActionResult GetEnchant(int spellId)
    {
        try{return Ok(new {catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
            enchant=ForeverEquipmentCatalog.FindEnchant(spellId)});}
        catch(ArgumentException e){return NotFound(new {Error=e.Message});}
    }

    [HttpGet("effects")]
    public IActionResult Effects([FromQuery] string? search=null,[FromQuery] bool? modeled=null)
    {
        IEnumerable<ForeverExecutableEquipmentEffect> query=ForeverEquipmentEffectsCatalog.GetCatalog().Effects;
        if(!string.IsNullOrWhiteSpace(search))
            query=query.Where(x=>x.Key.Contains(search,StringComparison.OrdinalIgnoreCase));
        if(modeled.HasValue)query=query.Where(x=>x.Modeled==modeled.Value);
        return Ok(new {catalogVersion=ForeverEquipmentEffectsCatalog.GetCatalog().CatalogVersion,
            effects=query.OrderBy(x=>x.Key).ToArray()});
    }

    [HttpGet("effects/{key}")]
    public IActionResult GetEffect(string key)
    {
        var effect=ForeverEquipmentEffectsCatalog.GetCatalog().Effects
            .FirstOrDefault(x=>x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
        return effect is null ? NotFound(new {Error=$"Unknown equipment effect '{key}'."}) :
            Ok(new {catalogVersion=ForeverEquipmentEffectsCatalog.GetCatalog().CatalogVersion,effect});
    }

    [HttpGet("items/{id:int}")]
    public IActionResult Get(int id)
    {
        try
        {
            return Ok(new {
                catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
                item=ForeverEquipmentCatalog.FindItem(id)
            });
        }
        catch(ArgumentException e){return NotFound(new {Error=e.Message});}
    }

    [HttpPost("resolve")]
    public IActionResult Resolve([FromBody] ForeverEquipmentResolveRequest? request)
    {
        if(request is null)return BadRequest(new {Error="Equipment selection required."});
        try
        {
            var resolved=ForeverSharedEquipmentResolver.Resolve(
                request.Equipment,request.ClassKey,request.Level,request.Race);
            return Ok(new {
                catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
                classKey=request.ClassKey,level=request.Level,
                stats=resolved.Stats,rawStats=resolved.RawStats,
                weapons=resolved.Weapons,sourceAttribution=resolved.Sources,
                normalizedLoadout=resolved.Loadout,
                equippedEnchants=resolved.Loadout.Items
                    .SelectMany(x=>new[]{x.EnchantSpellId,x.TemporaryEnchantSpellId}
                        .Where(id=>id.HasValue).Select(id=>new {
                            x.SlotKey,Enchant=ForeverEquipmentCatalog.FindEnchant(id!.Value)
                        })).ToArray(),
                equippedEffects=ForeverEquipmentEffectsCatalog.Select(resolved.Loadout),
                unmodeledEquippedEffects=ForeverEquipmentEffectsCatalog
                    .Select(resolved.Loadout,includeUnmodeled:true)
                    .Where(x=>!x.Modeled).ToArray()
            });
        }
        catch(ArgumentException e){return BadRequest(new {Error=e.Message});}
    }
}
public sealed class ForeverEquipmentResolveRequest
{
    public string ClassKey { get; set; }="";
    public int Level { get; set; }=60;
    public string? Race { get; set; }
    public CharacterEquipmentLoadout Equipment { get; set; }=new();
}
