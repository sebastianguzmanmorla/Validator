namespace SebastianGuzmanMorla.Validator.Interfaces;

public interface IValidationPropertyRules<TEntity>
{
    public Func<IServiceProvider, TEntity, CancellationToken, Task<bool>>? WhenRule { get; }
    public List<IValidationRule<TEntity>> Rules { get; }
    public Type? EntityType { get; }
    public object? GetValue(TEntity entity);
}