using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/suppliers")]
public class SuppliersController : ApiControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    [Authorize(Policy = Permissions.SuppliersView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _suppliers.GetSuppliersAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.SuppliersView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => NotFoundOrOk(await _suppliers.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.SuppliersManage)]
    public async Task<IActionResult> Create([FromBody] SupplierDto dto, CancellationToken ct)
        => FromResult(await _suppliers.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.SuppliersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] SupplierDto dto, CancellationToken ct)
        => FromResult(await _suppliers.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.SuppliersManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _suppliers.DeleteAsync(id, ct));

    [HttpGet("{id:int}/ledger")]
    [Authorize(Policy = Permissions.SuppliersView)]
    public async Task<IActionResult> GetLedger(int id, CancellationToken ct)
        => Ok(await _suppliers.GetLedgerAsync(id, ct));
}
