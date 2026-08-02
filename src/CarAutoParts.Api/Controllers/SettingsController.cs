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
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/webp"
    };

    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private readonly ISettingsService _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settings,
        IWebHostEnvironment env,
        ILogger<SettingsController> logger)
    {
        _settings = settings;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _settings.GetSettingsAsync(ct));

    [HttpPut]
    [Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Update([FromBody] CompanySettingsDto dto, CancellationToken ct)
        => FromResult(await _settings.UpdateSettingsAsync(dto, ct));

    /// <summary>
    /// Upload shop logo (png/jpg/jpeg/webp, max 2MB). Stored under wwwroot/uploads/company/logo.{ext}.
    /// LogoUrl is API-relative: <c>/uploads/company/logo.png</c> — resolve against API base (e.g. http://host:5280).
    /// </summary>
    [HttpPost("logo")]
    [Authorize(Policy = Permissions.SettingsManage)]
    [RequestSizeLimit(MaxLogoBytes + 64_000)]
    public async Task<IActionResult> UploadLogo(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Image file is required." });
        if (file.Length > MaxLogoBytes)
            return BadRequest(new { error = "Logo must be 2 MB or smaller." });

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return BadRequest(new { error = "Allowed types: png, jpg, jpeg, webp." });
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Invalid image content type." });

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var companyDir = Path.Combine(webRoot, "uploads", "company");
        Directory.CreateDirectory(companyDir);

        // Remove previous logo.* files so extension swaps don't leave orphans.
        foreach (var old in Directory.EnumerateFiles(companyDir, "logo.*"))
        {
            try { System.IO.File.Delete(old); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old logo {Path}", old); }
        }

        var fileName = "logo" + ext.ToLowerInvariant();
        var physicalPath = Path.Combine(companyDir, fileName);
        await using (var stream = System.IO.File.Create(physicalPath))
            await file.CopyToAsync(stream, ct);

        // API-relative URL; Blazor resolves against ApiBaseUrl (http://host:5280/uploads/...).
        var relativeUrl = "/uploads/company/" + fileName;
        var result = await _settings.SetLogoAsync(relativeUrl, relativeUrl, ct);
        if (!result.Succeeded)
            return FromResult(result);

        return Ok(new { logoUrl = result.Data, logoPath = relativeUrl });
    }

    /// <summary>Clear LogoUrl/LogoPath and delete the stored file under uploads/company when present.</summary>
    [HttpDelete("logo")]
    [Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct)
    {
        var result = await _settings.ClearLogoAsync(ct);
        if (!result.Succeeded)
            return FromResult(result);

        TryDeleteStoredLogo(result.Data);
        return Ok(new { cleared = true });
    }

    private void TryDeleteStoredLogo(string? previous)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var companyDir = Path.Combine(webRoot, "uploads", "company");
        if (Directory.Exists(companyDir))
        {
            foreach (var old in Directory.EnumerateFiles(companyDir, "logo.*"))
            {
                try { System.IO.File.Delete(old); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete logo file {Path}", old); }
            }
        }

        if (string.IsNullOrWhiteSpace(previous)) return;
        // Only delete if previous pointed under /uploads/company/
        var marker = "/uploads/company/";
        var idx = previous.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return;
        var relative = previous[idx..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.Combine(webRoot, relative);
        if (System.IO.File.Exists(candidate))
        {
            try { System.IO.File.Delete(candidate); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete logo {Path}", candidate); }
        }
    }
}
