using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/brands")]
public class BrandsController : ApiControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands) => _brands = brands;

    /// <summary>Full list (dropdowns) when no page; paged grid when page/pageSize present.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.BrandsView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        if (page is null && pageSize is null && string.IsNullOrWhiteSpace(search))
            return Ok(await _brands.GetAllAsync(ct));

        return Ok(await _brands.GetPagedAsync(new QuerySpec
        {
            Page = page ?? 1,
            PageSize = pageSize ?? QueryLimits.DefaultPageSize,
            Search = search
        }, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.BrandsManage)]
    public async Task<IActionResult> Create([FromBody] BrandDto dto, CancellationToken ct)
        => FromResult(await _brands.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.BrandsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] BrandDto dto, CancellationToken ct)
        => FromResult(await _brands.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.BrandsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _brands.DeleteAsync(id, ct));
}
