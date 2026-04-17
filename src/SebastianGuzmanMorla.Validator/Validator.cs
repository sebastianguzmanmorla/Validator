using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using SebastianGuzmanMorla.Validator.Extensions;
using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public abstract class Validator<TEntity> : IValidator<TEntity> where TEntity : notnull
{
    private readonly Dictionary<string, IValidationPropertyRules<TEntity>> _propertiesRules = [];

    protected virtual ImmutableArray<Func<IServiceProvider, TEntity, CancellationToken, Task<ValidationResult>>>
        InterfaceValidations { get; } = [];

    public async Task<ValidationResult> Validate(TEntity value, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validatorResult = new();

        if (!await InterfaceValidation(serviceProvider, value, validatorResult, cancellationToken))
        {
            return validatorResult;
        }

        foreach ((string propertyPath, IValidationPropertyRules<TEntity> propertyRules) in _propertiesRules)
        {
            if (propertyRules.WhenRule is not null &&
                !await propertyRules.WhenRule.Invoke(serviceProvider, value, cancellationToken))
            {
                continue;
            }

            if
            (
                cancellationToken.IsCancellationRequested ||
                (propertyRules.EntityType is not null &&
                 propertyRules.GetValue(value) is { } subValue &&
                 !await EntityValidation(serviceProvider, propertyRules.EntityType, subValue, validatorResult,
                     propertyPath, cancellationToken)) ||
                !await RuleValidation(serviceProvider, value, propertyRules.Rules, validatorResult, propertyPath,
                    cancellationToken)
            )
            {
                return validatorResult;
            }
        }

        return validatorResult;
    }

    public Task<ValidationResult> Validate(object value, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        if (value is TEntity entityValue)
        {
            return Validate(entityValue, serviceProvider, cancellationToken);
        }

        throw new ArgumentException($"{nameof(value)} is not of type {typeof(TEntity).Name}");
    }

    private async Task<bool> InterfaceValidation
    (
        IServiceProvider serviceProvider,
        TEntity value,
        ValidationResult validatorResult,
        CancellationToken cancellationToken = default
    )
    {
        foreach (Func<IServiceProvider, TEntity, CancellationToken, Task<ValidationResult>> validator in
                 InterfaceValidations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            ValidationResult result = await validator.Invoke(serviceProvider, value, cancellationToken);

            if (result.Errors is not null)
            {
                validatorResult.Errors ??= [];

                foreach ((string propertyPath, List<string> errors) in result.Errors)
                {
                    validatorResult.Errors.TryAdd(propertyPath, []);
                    validatorResult.Errors[propertyPath].AddRange(errors);
                }
            }

            if (result.ErrorHandle != ValidationErrorHandle.StopAll)
            {
                continue;
            }

            validatorResult.ErrorHandle = ValidationErrorHandle.StopAll;

            return false;
        }

        return true;
    }

    private static async Task<bool> EntityValidation
    (
        IServiceProvider serviceProvider,
        Type entityType,
        object value,
        ValidationResult validatorResult,
        string propertyPath,
        CancellationToken cancellationToken = default
    )
    {
        Type validatorType = typeof(IValidator<>).MakeGenericType(entityType);

        IValidator validator = (IValidator)serviceProvider.GetRequiredService(validatorType);

        if (value is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i] ?? throw new InvalidOperationException();

                ValidationResult result = await validator.Validate(item, serviceProvider, cancellationToken);

                if (result.Errors is null)
                {
                    continue;
                }

                validatorResult.Errors ??= [];

                foreach ((string subPropertyPath, List<string> errors) in result.Errors)
                {
                    string path = propertyPath + $"[{i}]" + subPropertyPath.Replace("$", string.Empty);

                    validatorResult.Errors.TryAdd(path, []);
                    validatorResult.Errors[path].AddRange(errors);
                }
            }
        }
        else
        {
            ValidationResult result = await validator.Validate(value, serviceProvider, cancellationToken);

            if (result.Errors is not null)
            {
                validatorResult.Errors ??= [];

                foreach ((string subPropertyPath, List<string> errors) in result.Errors)
                {
                    string path = propertyPath + subPropertyPath.Replace("$", string.Empty);

                    validatorResult.Errors.TryAdd(path, []);
                    validatorResult.Errors[path].AddRange(errors);
                }
            }

            if (result.ErrorHandle != ValidationErrorHandle.StopAll)
            {
                return true;
            }
        }

        validatorResult.ErrorHandle = ValidationErrorHandle.StopAll;

        return false;
    }

    private static async Task<bool> RuleValidation
    (
        IServiceProvider serviceProvider,
        TEntity value,
        List<IValidationRule<TEntity>> rules,
        ValidationResult validatorResult,
        string propertyPath,
        CancellationToken cancellationToken = default
    )
    {
        foreach (IValidationRule<TEntity> rule in rules)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            bool valid = await rule.Validate(serviceProvider, value, cancellationToken);

            if (!valid)
            {
                validatorResult.Errors ??= [];
                validatorResult.Errors.TryAdd(propertyPath, []);
                validatorResult.Errors[propertyPath].Add(rule.ValidationMessage.Invoke(serviceProvider, value));
            }

            if (!valid && rule.ErrorHandle == ValidationErrorHandle.StopAll)
            {
                validatorResult.ErrorHandle = ValidationErrorHandle.StopAll;

                return false;
            }

            if (!valid && rule.ErrorHandle == ValidationErrorHandle.StopProperty)
            {
                break;
            }
        }

        return true;
    }

    protected ValidationPropertyRules<TEntity, TProperty> RuleFor<TProperty>
    (
        Expression<Func<TEntity, TProperty>> expression
    )
    {
        string[] path = expression.ToString().Split(".");

        path[0] = "$";

        string finalPath = string.Join(".", path);

        if (_propertiesRules.TryGetValue(finalPath, out IValidationPropertyRules<TEntity>? value) &&
            value is ValidationPropertyRules<TEntity, TProperty> propertyRules)
        {
            return propertyRules;
        }

        propertyRules = new ValidationPropertyRules<TEntity, TProperty>
        {
            PropertyInfo = expression.GetPropertyInfo(),
            PropertyDelegate = expression.Compile()
        };

        _propertiesRules.Add(finalPath, propertyRules);

        return propertyRules;
    }

    protected ValidationPropertyRules<TEntity, TProperty> RuleForWhen<TProperty>
    (
        Expression<Func<TEntity, TProperty>> expression,
        Func<IServiceProvider, TEntity, CancellationToken, Task<bool>> when
    )
    {
        string[] path = expression.ToString().Split(".");

        path[0] = "$";

        string finalPath = string.Join(".", path);

        if (_propertiesRules.TryGetValue(finalPath, out IValidationPropertyRules<TEntity>? value) &&
            value is ValidationPropertyRules<TEntity, TProperty> propertyRules)
        {
            return propertyRules;
        }

        propertyRules = new ValidationPropertyRules<TEntity, TProperty>
        {
            PropertyInfo = expression.GetPropertyInfo(),
            PropertyDelegate = expression.Compile(),
            WhenRule = when
        };

        _propertiesRules.Add(finalPath, propertyRules);

        return propertyRules;
    }

    protected void ValidateEntity<TProperty>
    (
        Expression<Func<TEntity, TProperty>> expression
    )
        where TProperty : class
    {
        string[] path = expression.ToString().Split(".");

        path[0] = "$";

        string finalPath = string.Join(".", path);

        Type propertyType = typeof(TProperty);

        Type entityType = typeof(IList).IsAssignableFrom(propertyType) && propertyType.IsGenericType
            ? propertyType.GetGenericArguments()[0]
            : propertyType;

        ValidationPropertyRules<TEntity, TProperty> propertyRules = new()
        {
            PropertyInfo = expression.GetPropertyInfo(),
            PropertyDelegate = expression.Compile(),
            EntityType = entityType
        };

        _propertiesRules.Add(finalPath, propertyRules);
    }
}