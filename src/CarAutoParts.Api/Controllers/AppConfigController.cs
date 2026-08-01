using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Route("api/app-config")]
public class AppConfigController : ApiControllerBase
{
    private readonly IAppConfigService _config;

    public AppConfigController(IAppConfigService config) => _config = config;

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(CancellationToken ct) =>
        Ok(await _config.GetPublicAsync(ct));

    [HttpGet]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _config.GetAsync(ct));

    [HttpPut]
    [Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Update([FromBody] AppConfigUpdateRequest request, CancellationToken ct) =>
        FromResult(await _config.UpdateAsync(request, ct));
}
