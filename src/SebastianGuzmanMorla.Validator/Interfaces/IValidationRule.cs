namespace SebastianGuzmanMorla.Validator.Interfaces;

public interface IValidationRule<in TEntity>
{
    public Func<IServiceProvider, TEntity, string> ValidationMessage { get; }
    public ValidationErrorHandle ErrorHandle { get; }

    public Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the rule and returns its error message with the result for this invocation.
    /// </summary>
    public async Task<(bool IsValid, string ErrorMessage)> ValidateWithMessage(
        IServiceProvider serviceProvider, TEntity item, CancellationToken cancellationToken = default)
    {
        bool isValid = await Validate(serviceProvider, item, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return (isValid, isValid ? string.Empty : ValidationMessage.Invoke(serviceProvider, item));
    }
}
