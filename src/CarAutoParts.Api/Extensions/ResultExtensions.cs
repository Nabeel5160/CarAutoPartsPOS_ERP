using CarAutoParts.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Extensions;

public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.Succeeded)
            return new OkResult();

        return new BadRequestObjectResult(new { error = result.Error ?? "Request failed." });
    }

    public static ActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.Succeeded)
            return new OkObjectResult(result.Data);

        return new BadRequestObjectResult(new { error = result.Error ?? "Request failed." });
    }
}
