using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Front.Web.Controllers;

[Route("api/location")]
public sealed partial class LocationController(LocationApiClient locationApi) : Controller
{
    [HttpGet("states")]
    public async Task<IActionResult> States(CancellationToken cancellationToken)
    {
        var states = await locationApi.GetStatesAsync(cancellationToken);
        return Json(states);
    }

    [HttpGet("municipalities/{stateId}")]
    public async Task<IActionResult> Municipalities(string stateId, CancellationToken cancellationToken)
    {
        if (!LocationIdPattern().IsMatch(stateId)) return BadRequest();
        var municipalities = await locationApi.GetMunicipalitiesAsync(stateId, cancellationToken);
        return Json(municipalities);
    }

    [HttpGet("localities/{municipalityId}")]
    public async Task<IActionResult> Localities(string municipalityId, CancellationToken cancellationToken)
    {
        if (!LocationIdPattern().IsMatch(municipalityId)) return BadRequest();
        var localities = await locationApi.GetLocalitiesAsync(municipalityId, cancellationToken);
        return Json(localities);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex LocationIdPattern();
}
