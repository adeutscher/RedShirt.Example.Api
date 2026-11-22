using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.TestControllers;

// Controllers for ControllerActionEndpointConventionBuilderExtensionsTests. MVC discovers only top-level public
// controllers, so these cannot be nested in the test class.

[ApiController]
[Route("convention-test/no-policy")]
public class NoPolicyConventionTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}

[ApiController]
[Route("convention-test/controller-policy")]
[EnableRateLimiting(ConventionTestPolicies.Controller)]
public class ControllerPolicyConventionTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }

    [HttpGet("action-policy")]
    [EnableRateLimiting(ConventionTestPolicies.Action)]
    public IActionResult GetWithActionPolicy()
    {
        return Ok();
    }

    [HttpGet("disabled")]
    [DisableRateLimiting]
    public IActionResult GetDisabled()
    {
        return Ok();
    }
}

[ApiController]
[Route("convention-test/disabled")]
[DisableRateLimiting]
public class DisabledConventionTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}

public static class ConventionTestPolicies
{
    public const string Default = "default";
    public const string Controller = "controller-policy";
    public const string Action = "action-policy";
}
