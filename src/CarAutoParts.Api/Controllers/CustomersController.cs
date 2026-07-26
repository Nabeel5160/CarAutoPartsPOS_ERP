using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/customers")]
public class CustomersController : ApiControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _customers.GetCustomersAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => NotFoundOrOk(await _customers.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto, CancellationToken ct)
        => FromResult(await _customers.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto, CancellationToken ct)
        => FromResult(await _customers.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _customers.DeleteAsync(id, ct));

    [HttpGet("{id:int}/ledger")]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<IActionResult> GetLedger(int id, CancellationToken ct)
        => Ok(await _customers.GetLedgerAsync(id, ct));
}
