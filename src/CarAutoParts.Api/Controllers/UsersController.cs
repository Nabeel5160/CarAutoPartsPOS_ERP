using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = Permissions.UsersView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        if (page is null && pageSize is null && string.IsNullOrWhiteSpace(search))
            return Ok(await _users.GetUsersAsync(ct));

        return Ok(await _users.GetUsersPagedAsync(new QuerySpec
        {
            Page = page ?? 1,
            PageSize = pageSize ?? QueryLimits.DefaultPageSize,
            Search = search
        }, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto, CancellationToken ct)
        => FromResult(await _users.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UserCreateDto dto, CancellationToken ct)
        => FromResult(await _users.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _users.DeleteAsync(id, ct));
}

[Authorize]
[Route("api/roles")]
public class RolesController : ApiControllerBase
{
    private readonly IUserService _users;

    public RolesController(IUserService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = Permissions.UsersView)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _users.GetRolesAsync(ct));
}
