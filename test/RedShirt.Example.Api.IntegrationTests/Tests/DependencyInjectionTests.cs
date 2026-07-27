using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Extensions;
using System.Reflection;
using CoreServiceCollectionExtensions = RedShirt.Example.Api.Core.Extensions.ServiceCollectionExtensions;

namespace RedShirt.Example.Api.IntegrationTests.Tests;

public class DependencyInjectionTests
{
    /// <summary>
    ///     Sanity-check dependency injection completeness for services associated with API controllers or their endpoints.
    /// </summary>
    [Fact]
    public void Controller_DependencyInjection_Test()
    {
        /*
         * Note: Referencing ProducesJsonAttribute because it is a decently static
         *      class that we're about to reference a method from.
         *
         * Run cold, the assembly we're after wouldn't show up in `AppDomain.CurrentDomain.GetAssemblies()`.
         */
        var controllerClasses = Assembly.GetAssembly(typeof(ProducesJsonAttribute))
            !.DefinedTypes
            .Where(t =>
                t != typeof(Controller)
                && t != typeof(ControllerBase)
                && t.IsAssignableTo(typeof(ControllerBase)))
            .ToList();

        // Sanity-check our test's seeking
        Assert.NotEmpty(controllerClasses);

        var configuration = new ConfigurationBuilder().Build();

        var serviceCollection = new ServiceCollection();

        var environment = new Dictionary<string, string>
        {
            ["AWS_SERVICE_URL"] = "http://localhost:8000",
            ["AWS_ACCESS_KEY_ID"] = "foo",
            ["AWS_SECRET_ACCESS_KEY"] = "bar",
            ["AWS_SESSION_TOKEN"] = "foobar"
        };

        TestUtilities.WrapEnvironment(environment, () =>
        {
            serviceCollection.ConfigureApiServices(configuration);

            foreach (var controllerType in controllerClasses)
            {
                // Unclear on why, but need to declare our type as an implementation of itself
                serviceCollection.AddSingleton(controllerType, controllerType);
            }

            var provider = serviceCollection.BuildServiceProvider();

            foreach (var controllerType in controllerClasses)
            {
                // Confirm that we can build each controller
                var service = provider.GetService(controllerType);
                Assert.NotNull(service);

                // Look for uses of [FromServices] attribute on methods
                foreach (var method in controllerType.GetMethods())
                {
                    foreach (var parameter in method.GetParameters()
                                 .Where(parameter => parameter.GetCustomAttributes<FromServicesAttribute>().Any()))
                    {
                        provider.GetRequiredService(parameter.ParameterType);
                    }
                }
            }
        });
    }

    [Fact]
    public void CqrsValidator_DependencyInjection_Test()
    {
        /*
         * Note: Referencing the Core project's ServiceCollectionExtensions because it is
         *      a decently static class in the assembly that registers FluentValidation validators.
         *
         * Run cold, the assembly we're after wouldn't show up in `AppDomain.CurrentDomain.GetAssemblies()`.
         */
        var validatorInterfaces = Assembly.GetAssembly(typeof(CoreServiceCollectionExtensions))
            !.DefinedTypes
            .Where(t => t is {IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false})
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .Distinct()
            .ToList();

        // Sanity-check our test's seeking
        Assert.NotEmpty(validatorInterfaces);

        var configuration = new ConfigurationBuilder().Build();

        var serviceCollection = new ServiceCollection();

        var environment = new Dictionary<string, string>
        {
            ["AWS_SERVICE_URL"] = "http://localhost:8000",
            ["AWS_ACCESS_KEY_ID"] = "foo",
            ["AWS_SECRET_ACCESS_KEY"] = "bar",
            ["AWS_SESSION_TOKEN"] = "foobar"
        };

        TestUtilities.WrapEnvironment(environment, () =>
        {
            serviceCollection.ConfigureApiServices(configuration);

            var provider = serviceCollection.BuildServiceProvider();

            foreach (var validatorInterface in validatorInterfaces)
            {
                // AddValidatorsFromAssembly registers IValidator<T>, not the concrete validator type.
                var service = provider.GetService(validatorInterface);
                Assert.NotNull(service);
            }
        });
    }
}
