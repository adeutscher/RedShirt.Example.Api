using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Extensions;
using RedShirt.Example.Api.DataStores.Common.Extensions;
using RedShirt.Example.Api.Upload.Core.Configuration;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Factories;
using RedShirt.Example.Api.Upload.Implementation.Repositories;
using RedShirt.Example.Api.Upload.Implementation.Services;

namespace RedShirt.Example.Api.Upload.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUploads(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddCommonDatabase()
            .AddS3FileStorage()
            .Configure<UploadOptions>(configuration.GetSection(UploadOptions.ConfigurationSectionName))
            .AddSingleton<IUploadDbContextFactory, UploadDbContextFactory>()
            .AddSingleton<IUploadRepository, UploadRepository>()
            .AddSingleton<IUploadService, UploadService>()
            .AddSingleton<IUploadEventBroadcaster, StubUploadEventBroadcaster>();
    }
}