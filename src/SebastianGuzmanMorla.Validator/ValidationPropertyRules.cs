using System.Reflection;
using SebastianGuzmanMorla.Validator.Interfaces;

namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationPropertyRules<TEntity, TProperty> : IValidationPropertyRules<TEntity>
{
    public required PropertyInfo PropertyInfo { get; init; }
    public required Func<TEntity, TProperty> PropertyDelegate { get; init; }
    public Func<IServiceProvider, TEntity, CancellationToken, Task<bool>>? WhenRule { get; init; }
    public List<IValidationRule<TEntity>> Rules { get; } = [];
    public Type? EntityType { get; init; }

    public object? GetValue(TEntity entity)
    {
        return PropertyDelegate.Invoke(entity);
    }
}