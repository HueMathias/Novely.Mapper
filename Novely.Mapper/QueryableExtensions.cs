namespace Novely.Mapper;

/// <summary>
/// Extensions pour projeter des IQueryable vers des types cibles (traduction SQL via EF).
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Projette un IQueryable&lt;TSource&gt; vers un IQueryable&lt;TTarget&gt; en utilisant l'expression de mapping.
    /// </summary>
    public static IQueryable<TTarget> ProjectTo<TSource, TTarget>(this IQueryable<TSource> source, INovelyMapper mapper)
    {
        var projection = mapper.GetProjectionExpression<TSource, TTarget>();
        return source.Select(projection);
    }

    /// <summary>
    /// Projette un IQueryable non-générique vers un IQueryable&lt;TTarget&gt; en utilisant l'expression de mapping.
    /// </summary>
    public static IQueryable<TTarget> ProjectTo<TTarget>(this IQueryable source, INovelyMapper mapper)
    {
        var sourceType = source.ElementType;
        var method = typeof(INovelyMapper).GetMethod(nameof(INovelyMapper.GetProjectionExpression))!
            .MakeGenericMethod(sourceType, typeof(TTarget));
        var projection = (System.Linq.Expressions.LambdaExpression)method.Invoke(mapper, null)!;

        var selectMethod = typeof(Queryable).GetMethods()
            .Where(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .First(m =>
            {
                var paramType = m.GetParameters()[1].ParameterType;
                return paramType.IsGenericType
                    && paramType.GetGenericTypeDefinition() == typeof(System.Linq.Expressions.Expression<>);
            })
            .MakeGenericMethod(sourceType, typeof(TTarget));

        return (IQueryable<TTarget>)selectMethod.Invoke(null, [source, projection])!;
    }
}
