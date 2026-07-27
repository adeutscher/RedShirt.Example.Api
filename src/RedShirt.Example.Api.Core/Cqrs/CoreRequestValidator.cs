using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Exceptions.Responses;

namespace RedShirt.Example.Api.Core.Cqrs;

public interface ICoreRequestValidator
{
    Task ValidateAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);
}

internal sealed class CoreRequestValidator(IServiceProvider serviceProvider) : ICoreRequestValidator
{
    public async Task ValidateAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        // Find all relevant validators
        var validators = serviceProvider.GetServices<IValidator<TRequest>>().ToArray();
        if (validators.Length == 0)
        {
            return;
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        foreach (var validationResult in validationResults)
        {
            if (validationResult.Errors.FirstOrDefault(e => e is not null) is { } error)
            {
                throw new BadRequestException(error.ErrorMessage);
            }
        }
    }
}