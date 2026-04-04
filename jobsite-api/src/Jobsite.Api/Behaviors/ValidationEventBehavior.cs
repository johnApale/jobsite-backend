using FluentValidation;
using FluentValidation.Results;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;

namespace Jobsite.Api.Behaviors;

/// <summary>
/// Domain event pipeline behavior that runs all FluentValidation validators
/// registered for the event type before the handler executes.
/// </summary>
public sealed class ValidationEventBehavior : IDomainEventPipelineBehavior
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationEventBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(IDomainEvent domainEvent, Func<Task> next, CancellationToken ct)
    {
        Type validatorType = typeof(IValidator<>).MakeGenericType(domainEvent.GetType());
        IEnumerable<object?> validators = _serviceProvider.GetServices(validatorType);

        List<object> resolvedValidators = validators.Where(v => v is not null).Select(v => v!).ToList();

        if (resolvedValidators.Count == 0)
        {
            await next();
            return;
        }

        IValidationContext validationContext = (IValidationContext)Activator.CreateInstance(
            typeof(ValidationContext<>).MakeGenericType(domainEvent.GetType()),
            domainEvent)!;

        List<Task<ValidationResult>> validationTasks = [];
        foreach (object validator in resolvedValidators)
        {
            System.Reflection.MethodInfo? validateMethod = validator.GetType()
                .GetMethod("ValidateAsync", [validationContext.GetType(), typeof(CancellationToken)]);

            if (validateMethod is not null)
            {
                validationTasks.Add((Task<ValidationResult>)validateMethod.Invoke(
                    validator, [validationContext, ct])!);
            }
        }

        ValidationResult[] results = await Task.WhenAll(validationTasks);

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

        await next();
    }
}
