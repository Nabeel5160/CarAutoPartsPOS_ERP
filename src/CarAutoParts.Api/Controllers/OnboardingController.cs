using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/onboarding")]
[Route("api/v1/onboarding")]
public class OnboardingController : ApiControllerBase
{
    private readonly IOnboardingService _onboarding;

    public OnboardingController(IOnboardingService onboarding) => _onboarding = onboarding;

    [HttpGet("status")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await _onboarding.GetStatusAsync(ct));

    [HttpPost("complete")]
    [Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Complete([FromBody] CompleteOnboardingDto dto, CancellationToken ct)
        => FromResult(await _onboarding.CompleteAsync(dto, ct));
}
