using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.ListRecords;

namespace RedShirt.Example.Api.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApiCore(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddSingleton<ICacheBasedIdempotencyService, CacheBasedIdempotencyService>()
            .AddSingleton<ICacheBasedIdempotencyWrapperService, CacheBasedIdempotencyWrapperService>()
            .Configure<CacheBasedIdempotencyService.ConfigurationModel>(configuration.GetSection("Core:Idempotency"))
            .AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .AddSingleton<ICoreRequestValidator, CoreRequestValidator>()
            .AddTransient<ICreateExampleItemCommandHandler, CreateExampleItemCommandHandler>()
            .AddTransient<IDeleteExampleItemCommandHandler, DeleteExampleItemCommandHandler>()
            .AddTransient<IGetExampleItemRecordQueryHandler, GetExampleItemRecordQueryHandler>()
            .AddTransient<IListExampleItemRecordsQueryHandler, ListExampleItemRecordsQueryHandler>();
    }
}