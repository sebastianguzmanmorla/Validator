namespace SebastianGuzmanMorla.Validator.Interfaces;

public interface IValidationRule<in TEntity>
{
    public Func<IServiceProvider, TEntity, string> ValidationMessage { get; }
    public ValidationErrorHandle ErrorHandle { get; }

    public Task<bool> Validate(IServiceProvider serviceProvider, TEntity item,
        CancellationToken cancellationToken = default);
}