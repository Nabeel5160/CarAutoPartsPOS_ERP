using CarAutoParts.Api.Extensions;
using CarAutoParts.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult FromResult(Result result) => result.ToActionResult();

    protected ActionResult FromResult<T>(Result<T> result) => result.ToActionResult();

    protected ActionResult NotFoundOrOk<T>(T? value) =>
        value is null ? NotFound() : Ok(value);
}
