using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationRuleDynamic<TEntity>(
    Func<IServiceProvider, TEntity, CancellationToken, Task<(bool IsValid, string? ErrorMessage)>> validation,
    ValidationErrorHandle errorHandle
) : IValidationRule<TEntity>
{
    private string _errorMessage = string.Empty;

    public Func<IServiceProvider, TEntity, string> ValidationMessage => (_, _) => _errorMessage;
    public ValidationErrorHandle ErrorHandle => errorHandle;

    public async Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default)
    {
        (bool isValid, string? errorMessage) = await validation.Invoke(serviceProvider, item, cancellationToken);
        _errorMessage = errorMessage ?? string.Empty;
        return isValid;
    }
}