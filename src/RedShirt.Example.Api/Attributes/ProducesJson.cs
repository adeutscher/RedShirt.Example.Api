using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RedShirt.Example.Api.Attributes;

/// <summary>
///     Declares application/json for controller responses. ProblemDetails results keep their RFC 9457
///     media type (application/problem+json) instead of being forced to application/json.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ProducesJsonAttribute() : ProducesAttribute("application/json")
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails })
        {
            return;
        }

        base.OnResultExecuting(context);
    }
}