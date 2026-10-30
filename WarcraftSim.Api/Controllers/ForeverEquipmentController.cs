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
        [FromQuery] string? slot=null,[FromQuery] int page=1,[FromQuery] int pageSize=25)
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
        var sorted=source.OrderBy(x=>x.Id).ToArray();
        return Ok(new {
            catalogVersion=catalog.CatalogVersion,
            total=sorted.Length,page,pageSize,
            items=sorted.Skip((page-1)*pageSize).Take(pageSize)
        });
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
                request.Equipment,request.ClassKey,request.Level);
            return Ok(new {
                catalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
                classKey=request.ClassKey,level=request.Level,
                stats=resolved.Stats,rawStats=resolved.RawStats,
                weapons=resolved.Weapons,sourceAttribution=resolved.Sources,
                normalizedLoadout=resolved.Loadout
            });
        }
        catch(ArgumentException e){return BadRequest(new {Error=e.Message});}
    }
}
public sealed class ForeverEquipmentResolveRequest
{
    public string ClassKey { get; set; }="";
    public int Level { get; set; }=60;
    public CharacterEquipmentLoadout Equipment { get; set; }=new();
}
