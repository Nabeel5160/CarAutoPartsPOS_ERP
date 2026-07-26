using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/products")]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    [Authorize(Policy = Permissions.ProductsView)]
    public async Task<IActionResult> GetAll([FromQuery] ProductQueryDto query, CancellationToken ct)
        => Ok(await _products.GetProductsAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.ProductsView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => NotFoundOrOk(await _products.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.ProductsCreate)]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto, CancellationToken ct)
        => FromResult(await _products.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ProductsUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] ProductCreateDto dto, CancellationToken ct)
        => FromResult(await _products.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ProductsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _products.DeleteAsync(id, ct));

    [HttpPost("import")]
    [Authorize(Policy = Permissions.ProductsImport)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Excel file is required." });

        await using var stream = file.OpenReadStream();
        return FromResult(await _products.ImportFromExcelAsync(stream, ct));
    }

    [HttpGet("export")]
    [Authorize(Policy = Permissions.ProductsExport)]
    public async Task<IActionResult> Export([FromQuery] ProductQueryDto query, CancellationToken ct)
    {
        var bytes = await _products.ExportToExcelAsync(query, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products.xlsx");
    }
}
