using System.Text.RegularExpressions;
using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationRuleMatches<TEntity, TProperty>(
    Func<TEntity, TProperty> propertyDelegate,
    string regex,
    Func<IServiceProvider, TEntity, string> validationMessage,
    ValidationErrorHandle errorHandle
) : IValidationRule<TEntity>
{
    private readonly Regex _regex = new(regex, RegexOptions.NonBacktracking | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public Func<IServiceProvider, TEntity, string> ValidationMessage => validationMessage;
    public ValidationErrorHandle ErrorHandle => errorHandle;

    public Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default)
    {
        TProperty value = propertyDelegate.Invoke(item);

        if (value is string stringValue)
        {
            return string.IsNullOrEmpty(stringValue)
                ? Task.FromResult(false)
                : Task.FromResult(_regex.IsMatch(stringValue));
        }

        return Task.FromResult(false);
    }
}