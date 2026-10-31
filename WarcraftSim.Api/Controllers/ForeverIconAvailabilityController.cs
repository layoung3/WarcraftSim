using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// Lists only locally cached, already present icon filenames. The browser
/// never probes nonexistent CDN or local icon assets. No runtime network calls,
/// icon guessing or automatic game-data promotion.
/// </summary>
[ApiController]
[Route("api/forever/icons")]
public sealed class ForeverIconAvailabilityController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("available")]
    public IActionResult Available()
    {
        var root=environment.WebRootPath;
        var directory=string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,"forever-icons");
        if(directory is null || !Directory.Exists(directory))
            return Ok(new{cached=0,files=Array.Empty<string>()});
        var names=Directory.EnumerateFiles(directory,"*.jpg",SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x=>x is not null && Regex.IsMatch(x,@"^(?:[a-z0-9_]+|filedata-[1-9][0-9]*)$",
                RegexOptions.CultureInvariant))
            .Select(x=>x!)
            .OrderBy(x=>x,StringComparer.Ordinal)
            .ToArray();
        return Ok(new{cached=names.Length,files=names});
    }
}
