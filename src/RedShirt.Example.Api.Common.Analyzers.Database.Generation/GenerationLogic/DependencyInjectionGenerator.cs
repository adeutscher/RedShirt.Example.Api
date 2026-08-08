using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Extensions;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Models;
using System.Text;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.GenerationLogic;

public static class DependencyInjectionGenerator
{
    public static StringBuilder GetDependencyInjectionStatement(this StringBuilder sb,
        ClassSummaryModel classSummaryModel)
    {
        sb
            .AppendLine()
            .AppendLine("internal static partial class DependencyInjectionExtensions")
            .OpenBracket(0)
            .AppendLineWithIndent("internal static Microsoft.Extensions.DependencyInjection.IServiceCollection "
                                  + $"AddGenerated{classSummaryModel.BaseName}(this Microsoft.Extensions.DependencyInjection.IServiceCollection serviceCollection)")
            .OpenBracket();

        if (!classSummaryModel.DoNotGenerateService)
        {
            // Service
            sb
                .AppendLineWithIndent(2,
                    $"serviceCollection = Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{classSummaryModel.ServiceInterfaceName}, {classSummaryModel.ServiceClassName}>(serviceCollection);");
        }

        // Repository
        sb
            .AppendLineWithIndent(2,
                $"serviceCollection = Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{classSummaryModel.RepositoryInterfaceName}, {classSummaryModel.RepositoryName}>(serviceCollection);")
            // Generic DTO Storage for DTO
            .AppendLineWithIndent(2,
                $"serviceCollection = {classSummaryModel.BaseNamespace}.Common.Database.Extensions.ServiceCollectionExtensions.AddGenericDtoHandler<{classSummaryModel.FullDtoName}, {classSummaryModel.Key.Type}>(serviceCollection);")
            .AppendLineWithIndent(2, "return serviceCollection;")
            .CloseBracket()
            .CloseBracket(0);

        return sb;
    }
}