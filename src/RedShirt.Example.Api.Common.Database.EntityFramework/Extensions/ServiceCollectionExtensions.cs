using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.EntityFramework.Factories;
using RedShirt.Example.Api.Common.Database.Extensions;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add Entity Framework Core support for MariaDB / MySQL, including the shared connection-string source
    ///     and an async <see cref="IExampleApiDbContextFactory" />.
    /// </summary>
    public static IServiceCollection AddEntityFramework(this IServiceCollection services)
    {
        return services
            .AddCommonDatabase()
            .AddSingleton<IExampleApiDbContextFactory, ExampleApiDbContextFactory>();
    }
}
