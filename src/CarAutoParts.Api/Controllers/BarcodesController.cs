using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/barcodes")]
public class BarcodesController : ApiControllerBase
{
    private readonly IBarcodeService _barcodes;

    public BarcodesController(IBarcodeService barcodes) => _barcodes = barcodes;

    [HttpGet("{code}")]
    public IActionResult Generate(string code, [FromQuery] int width = 300, [FromQuery] int height = 100)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "Code is required." });

        var png = _barcodes.GenerateBarcodeImage(code, width, height);
        return File(png, "image/png");
    }
}
