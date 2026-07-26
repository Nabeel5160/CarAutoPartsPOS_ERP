using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/settings")]
public class SettingsController : ApiControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _settings.GetSettingsAsync(ct));

    [HttpPut]
    [Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Update([FromBody] CompanySettingsDto dto, CancellationToken ct)
        => FromResult(await _settings.UpdateSettingsAsync(dto, ct));
}
