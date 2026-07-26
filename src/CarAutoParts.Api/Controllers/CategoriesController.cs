using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    [HttpGet]
    [Authorize(Policy = Permissions.CategoriesView)]
    public async Task<IActionResult> GetTree(CancellationToken ct)
        => Ok(await _categories.GetTreeAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.CategoriesManage)]
    public async Task<IActionResult> Create([FromBody] CategoryDto dto, CancellationToken ct)
        => FromResult(await _categories.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CategoriesManage)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryDto dto, CancellationToken ct)
        => FromResult(await _categories.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CategoriesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _categories.DeleteAsync(id, ct));
}
