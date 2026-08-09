using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RedShirt.Example.Api.Core.Cqrs;

internal static class CqrsServiceCollectionExtensions
{
    private static bool IsSpecificCqrsHandlerInterface(Type type)
    {
        if (!type.IsInterface || type.IsGenericTypeDefinition)
        {
            return false;
        }

        // ReSharper disable once InvertIf
        if (type.IsGenericType)
        {
            /*
             * In practice, I say today that I doubt I'll be using any generic type implementations of an ICqrsHandler interface.
             * Using a direct type kind of defeats the purpose of the flexibility offered by using an intermediate type.
             * Intended path: FooCommandHandler -> IFooCommandHandler -> ICqrsHandler<IFooInput, IFooOutput>
             *
             * As I look over Cursor's take on this challenge, I'm REALLY tempted to just throw an InvalidOperation to say "no, you're doing it wrong".
             * But maybe a user of this template will have a use case in the future that does involve a generic type.
             * And in that case, I don't want them to have to dive down into here just because I was opinionated about how I wanted to do things.
             * This is supposed to be a flexible template.
             */

            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ICqrsHandler<>) || definition == typeof(ICqrsHandler<,>))
            {
                return false;
            }
        }

        return type.GetInterfaces().Any(inherited =>
            inherited.IsGenericType
            && (inherited.GetGenericTypeDefinition() == typeof(ICqrsHandler<>)
                || inherited.GetGenericTypeDefinition() == typeof(ICqrsHandler<,>)));
    }

    /// <summary>
    ///     Wrapper function to automatically add CQRS-oriented handlers/validators.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        // For each class in the assembly
        foreach (var implementationType in assembly.DefinedTypes
                     .Where(type => type is
                     {
                         IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false
                     }))
        {
            foreach (var serviceType in implementationType.ImplementedInterfaces
                         .Where(IsSpecificCqrsHandlerInterface))
            {
                services.AddTransient(serviceType, implementationType);
            }
        }

        return services
            .AddValidatorsFromAssembly(
                typeof(CqrsServiceCollectionExtensions).Assembly,
                ServiceLifetime.Transient);
    }
}