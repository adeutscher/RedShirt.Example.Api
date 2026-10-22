using Microsoft.AspNetCore.Mvc;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Reflection;

namespace RedShirt.Example.Api.OpenApi;

internal sealed class DecimalFormatOperationProcessor : IOperationProcessor
{
    private static bool IsComplexQueryParameter(ParameterInfo parameter)
    {
        return parameter.ParameterType.IsClass
               && parameter.ParameterType != typeof(string)
               && parameter.GetCustomAttribute<FromRouteAttribute>() is null
               && parameter.GetCustomAttribute<FromBodyAttribute>() is null
               && parameter.GetCustomAttribute<FromHeaderAttribute>() is null
               && parameter.GetCustomAttribute<FromServicesAttribute>() is null;
    }

    public bool Process(OperationProcessorContext context)
    {
        var fromQueryTypes = context.MethodInfo.GetParameters()
            .Where(parameter => parameter.GetCustomAttribute<FromQueryAttribute>() is not null
                                || IsComplexQueryParameter(parameter))
            .Select(parameter => parameter.ParameterType)
            .Where(type => type.IsClass && type != typeof(string))
            .Distinct()
            .ToList();

        if (fromQueryTypes.Count == 0)
        {
            return true;
        }

        foreach (var operationParameter in context.OperationDescription.Operation.Parameters)
        {
            foreach (var queryType in fromQueryTypes)
            {
                var property = queryType.GetProperty(
                    operationParameter.Name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property is null || !DecimalOpenApiSchema.IsDecimalType(property.PropertyType))
                {
                    continue;
                }

                DecimalOpenApiSchema.Apply(operationParameter.ActualSchema);
                break;
            }
        }

        return true;
    }
}