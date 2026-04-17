using System.Linq.Expressions;
using System.Reflection;

namespace SebastianGuzmanMorla.Validator.Extensions;

public static class ExpressionExtensions
{
    public static PropertyInfo GetPropertyInfo<TSource, TProperty>(this Expression<Func<TSource, TProperty>> expression)
    {
        if (expression.Body is not MemberExpression member)
        {
            throw new ArgumentException($"Expression '{expression}' refers to a method, not a property.");
        }

        return member.Member as PropertyInfo ??
               throw new ArgumentException($"Expression '{expression}' refers to a field, not a property.");
    }
}