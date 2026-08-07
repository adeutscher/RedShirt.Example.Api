using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Common.Services.Utility;

namespace RedShirt.Example.Api.Common.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCommonServices_RegistersExpectedSingletons()
    {
        var services = new ServiceCollection()
            .AddCommonServices();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ISleepService>());
        Assert.Same(
            provider.GetRequiredService<ISleepService>(),
            provider.GetRequiredService<ISleepService>());
    }
}
