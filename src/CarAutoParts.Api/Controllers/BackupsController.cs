using CarAutoParts.Api.Contracts;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/backups")]
public class BackupsController : ApiControllerBase
{
    private readonly IBackupService _backups;

    public BackupsController(IBackupService backups) => _backups = backups;

    [HttpGet]
    [Authorize(Policy = Permissions.BackupView)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
        => Ok(await _backups.GetHistoryAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.BackupManage)]
    public async Task<IActionResult> Create([FromBody] CreateBackupRequest? request, CancellationToken ct)
        => FromResult(await _backups.CreateBackupAsync(request?.IsAutomatic ?? false, ct));

    [HttpPost("restore")]
    [Authorize(Policy = Permissions.BackupManage)]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Restore(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Backup file is required." });

        var tempPath = Path.Combine(Path.GetTempPath(), $"cap-restore-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
                await file.CopyToAsync(stream, ct);

            return FromResult(await _backups.RestoreBackupAsync(tempPath, ct));
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}
