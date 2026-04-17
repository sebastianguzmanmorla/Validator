namespace SebastianGuzmanMorla.Validator.Interfaces;

public interface IValidator<in TEntity> : IValidator where TEntity : notnull
{
    public Task<ValidationResult> Validate(TEntity value, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}

public interface IValidator
{
    public Task<ValidationResult> Validate(object value, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}