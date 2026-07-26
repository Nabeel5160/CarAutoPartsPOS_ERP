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
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _users.GetUsersAsync(ct));

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
