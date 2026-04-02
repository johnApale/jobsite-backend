using FluentValidation;
using FluentValidation.Results;
using Jobsite.SharedKernel.Errors;
using MediatR;

namespace Jobsite.Api.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs all FluentValidation validators
/// registered for the request type before the handler executes.
/// </summary>
public sealed class ValidationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        ValidationContext<TRequest> validationContext = new(request);

        ValidationResult[] results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(validationContext, cancellationToken)));

        Dictionary<string, string> failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.First().ErrorMessage);

        if (failures.Count > 0)
        {
            throw AppErrors.Validation.WithDetails(failures);
        }

        return await next(cancellationToken);
    }
}
