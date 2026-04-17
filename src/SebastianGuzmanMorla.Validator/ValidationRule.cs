using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationRule<TEntity>(
    Func<IServiceProvider, TEntity, CancellationToken, Task<bool>> validation,
    Func<IServiceProvider, TEntity, string> validationMessage,
    ValidationErrorHandle errorHandle
) : IValidationRule<TEntity>
{
    public Func<IServiceProvider, TEntity, string> ValidationMessage => validationMessage;
    public ValidationErrorHandle ErrorHandle => errorHandle;

    public Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default)
    {
        return validation.Invoke(serviceProvider, item, cancellationToken);
    }
}