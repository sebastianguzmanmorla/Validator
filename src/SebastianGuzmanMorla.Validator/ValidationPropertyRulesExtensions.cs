using System.Linq.Expressions;
using System.Net.Mail;

namespace SebastianGuzmanMorla.Validator;

public static class ValidationPropertyRulesExtensions
{
    public static ValidationPropertyRules<TEntity, string?> NotEmpty<TEntity>
    (
        this ValidationPropertyRules<TEntity, string?> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(!string.IsNullOrEmpty(propertyRules.PropertyDelegate.Invoke(x))),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, string[]> NotEmpty<TEntity>
    (
        this ValidationPropertyRules<TEntity, string[]> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) =>
            {
                string[] value = propertyRules.PropertyDelegate.Invoke(x);
                return Task.FromResult(value.Length > 0);
            },
            validationMessage,
            errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, Guid?> NotEmpty<TEntity>
    (
        this ValidationPropertyRules<TEntity, Guid?> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(propertyRules.PropertyDelegate.Invoke(x).GetValueOrDefault() != Guid.Empty),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, Guid> NotEmpty<TEntity>
    (
        this ValidationPropertyRules<TEntity, Guid> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(propertyRules.PropertyDelegate.Invoke(x) != Guid.Empty), validationMessage,
            errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, string?> MinimumLength<TEntity>
    (
        this ValidationPropertyRules<TEntity, string?> propertyRules,
        int min,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            string? value = propertyRules.PropertyDelegate.Invoke(x);
            return Task.FromResult(value is null || value.Length >= min);
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, string?> MaximumLength<TEntity>
    (
        this ValidationPropertyRules<TEntity, string?> propertyRules,
        int max,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            string? value = propertyRules.PropertyDelegate.Invoke(x);
            return Task.FromResult(value is null || value.Length <= max);
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, string?> EmailAddress<TEntity>
    (
        this ValidationPropertyRules<TEntity, string?> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            string? value = propertyRules.PropertyDelegate.Invoke(x);

            if (string.IsNullOrEmpty(value))
            {
                return Task.FromResult(false);
            }

            try
            {
                MailAddress addr = new($"{value}");

                return Task.FromResult(addr.Address == $"{value}");
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> NotNull<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(propertyRules.PropertyDelegate.Invoke(x) is not null), validationMessage,
            errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Equal<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        TProperty value,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(propertyRules.PropertyDelegate.Invoke(x)?.Equals(value) ?? false),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Equal<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        Expression<Func<TEntity, TProperty>> expression,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        Func<TEntity, TProperty> property = expression.Compile();

        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(propertyRules.PropertyDelegate.Invoke(x)?.Equals(property.Invoke(x)) ?? false),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> NotEqual<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        TProperty value,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(!propertyRules.PropertyDelegate.Invoke(x)?.Equals(value) ?? true),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> NotEqual<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        Expression<Func<TEntity, TProperty>> expression,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        Func<TEntity, TProperty> property = expression.Compile();

        propertyRules.Rules.Add(new ValidationRule<TEntity>(
            (_, x, _) => Task.FromResult(!propertyRules.PropertyDelegate.Invoke(x)?.Equals(property.Invoke(x)) ?? true),
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Minimum<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        TProperty min,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            TProperty value = propertyRules.PropertyDelegate.Invoke(x);

            if (value is null)
            {
                return Task.FromResult(false);
            }

            Comparer<TProperty> comparer = Comparer<TProperty>.Default;
            return Task.FromResult(comparer.Compare(value, min) >= 0);
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Maximum<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        TProperty max,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            TProperty value = propertyRules.PropertyDelegate.Invoke(x);

            if (value is null)
            {
                return Task.FromResult(false);
            }

            Comparer<TProperty> comparer = Comparer<TProperty>.Default;
            return Task.FromResult(comparer.Compare(value, max) <= 0);
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Between<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        TProperty min,
        TProperty max,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>((_, x, _) =>
        {
            TProperty value = propertyRules.PropertyDelegate(x);

            if (value is null)
            {
                return Task.FromResult(false);
            }

            Comparer<TProperty> comparer = Comparer<TProperty>.Default;

            return Task.FromResult(
                comparer.Compare(value, min) >= 0 &&
                comparer.Compare(value, max) <= 0
            );
        }, validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Matches<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        string regex,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRuleMatches<TEntity, TProperty>(propertyRules.PropertyDelegate, regex,
            validationMessage, errorHandle));

        return propertyRules;
    }

    public static ValidationPropertyRules<TEntity, TProperty> Must<TEntity, TProperty>
    (
        this ValidationPropertyRules<TEntity, TProperty> propertyRules,
        Func<IServiceProvider, TEntity, CancellationToken, Task<bool>> expression,
        Func<IServiceProvider, TEntity, string> validationMessage,
        ValidationErrorHandle errorHandle = ValidationErrorHandle.Continue
    )
    {
        propertyRules.Rules.Add(new ValidationRule<TEntity>(expression, validationMessage, errorHandle));

        return propertyRules;
    }
}