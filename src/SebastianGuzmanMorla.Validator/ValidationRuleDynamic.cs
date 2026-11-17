using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationRuleDynamic<TEntity>(
    Func<IServiceProvider, TEntity, CancellationToken, Task<(bool IsValid, string? ErrorMessage)>> validation,
    ValidationErrorHandle errorHandle
) : IValidationRule<TEntity>
{
    /// <summary>
    /// For a dynamic message, use <see cref="ValidateWithMessage"/>. The legacy accessor returns an empty string.
    /// </summary>
    public Func<IServiceProvider, TEntity, string> ValidationMessage => static (_, _) => string.Empty;

    public ValidationErrorHandle ErrorHandle => errorHandle;

    public async Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default)
    {
        (bool isValid, _) = await validation.Invoke(serviceProvider, item, cancellationToken);
        return isValid;
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateWithMessage(
        IServiceProvider serviceProvider, TEntity item, CancellationToken cancellationToken = default)
    {
        (bool isValid, string? errorMessage) = await validation.Invoke(serviceProvider, item, cancellationToken);
        return (isValid, errorMessage ?? string.Empty);
    }
}
