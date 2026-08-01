using CarAutoParts.Application.Config;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CarAutoParts.Api.Filters;

/// <summary>Returns 404 when the configured module feature is disabled.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    public string ModuleKey { get; }

    public RequireFeatureAttribute(string moduleKey) => ModuleKey = moduleKey;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var gate = context.HttpContext.RequestServices.GetService(typeof(IFeatureGate)) as IFeatureGate;
        if (gate is null)
        {
            await next();
            return;
        }

        if (!await gate.ModuleEnabledAsync(ModuleKey, context.HttpContext.RequestAborted))
        {
            context.Result = new NotFoundObjectResult(new { error = $"Module '{ModuleKey}' is disabled." });
            return;
        }

        await next();
    }
}
