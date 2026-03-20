using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Novely.Mapper;

/// <summary>
/// Interface définissant les fonctionnalités principales d'un mapper NovelyMapper.
/// </summary>
public interface INovelyMapper
{
    /// <summary>
    /// Crée une configuration de mapping entre TSource et TTarget.
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>();

    /// <summary>
    /// Mappe un objet source vers un nouvel objet cible.
    /// </summary>
    TTarget Map<TSource, TTarget>(TSource source);

    /// <summary>
    /// Mappe un objet source vers un objet cible existant.
    /// </summary>
    TTarget Map<TSource, TTarget>(TSource source, TTarget target);

    /// <summary>
    /// Mappe une collection d'objets source vers des objets cibles.
    /// </summary>
    IEnumerable<TTarget> Map<TSource, TTarget>(IEnumerable<TSource> sources);

    /// <summary>
    /// Retourne l'expression de projection pour utilisation avec IQueryable (ProjectTo).
    /// </summary>
    Expression<Func<TSource, TTarget>> GetProjectionExpression<TSource, TTarget>();

    /// <summary>
    /// Valide que toutes les propriétés cibles ont une source configurée.
    /// </summary>
    void AssertConfigurationIsValid();
}

/// <summary>
/// Implémentation principale du mapper NovelyMapper.
/// </summary>
public class NovelyMapper : INovelyMapper
{
    private readonly ConcurrentDictionary<(Type, Type), Delegate> compiledMappings = new();
    private readonly ConcurrentDictionary<(Type, Type), Delegate> compiledUpdateMappings = new();
    internal readonly ConcurrentDictionary<(Type, Type), object> pendingConfigs = new();

    internal NovelyMapperOptions Options { get; set; } = new();

    public INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>()
    {
        var config = new NovelyMapperConfig<TSource, TTarget>(this);
        pendingConfigs[(typeof(TSource), typeof(TTarget))] = config;
        return config;
    }

    public TTarget Map<TSource, TTarget>(TSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw new InvalidOperationException(
                $"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

        var config = (NovelyMapperConfig<TSource, TTarget>)configObj;

        if (config.CustomConverter != null)
            return config.CustomConverter(source);

        if (config.BeforeMapAction != null)
        {
            var target = CreateInstance<TTarget>();
            config.BeforeMapAction(source, target);
            var updateFunc = GetOrCompileUpdateMapping<TSource, TTarget>();
            updateFunc(source, target);
            config.AfterMapAction?.Invoke(source, target);
            return target;
        }

        var func = GetOrCompileMapping<TSource, TTarget>();
        var result = func(source);
        config.AfterMapAction?.Invoke(source, result);
        return result;
    }

    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw new InvalidOperationException(
                $"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

        var config = (NovelyMapperConfig<TSource, TTarget>)configObj;

        config.BeforeMapAction?.Invoke(source, target);
        var updateFunc = GetOrCompileUpdateMapping<TSource, TTarget>();
        updateFunc(source, target);
        config.AfterMapAction?.Invoke(source, target);
        return target;
    }

    public IEnumerable<TTarget> Map<TSource, TTarget>(IEnumerable<TSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.ContainsKey(key))
            throw new InvalidOperationException(
                $"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

        return sources.Select(item => Map<TSource, TTarget>(item));
    }

    public Expression<Func<TSource, TTarget>> GetProjectionExpression<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw new InvalidOperationException(
                $"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

        var param = Expression.Parameter(typeof(TSource), "src");
        var body = BuildMappingExpression(typeof(TSource), typeof(TTarget), param, configObj);
        return Expression.Lambda<Func<TSource, TTarget>>(body, param);
    }

    public void AssertConfigurationIsValid()
    {
        var errors = new List<string>();

        foreach (var kvp in pendingConfigs)
        {
            var config = (IMapperConfig)kvp.Value;
            var configErrors = config.Validate((s, t) => pendingConfigs.ContainsKey((s, t)));
            errors.AddRange(configErrors);
        }

        if (errors.Count > 0)
            throw new NovelyMapperValidationException(errors);
    }

    #region Compilation

    private Func<TSource, TTarget> GetOrCompileMapping<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!compiledMappings.TryGetValue(key, out var del))
        {
            if (!pendingConfigs.TryGetValue(key, out var pending))
                throw new InvalidOperationException(
                    $"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

            var param = Expression.Parameter(typeof(TSource), "src");
            var body = BuildMappingExpression(typeof(TSource), typeof(TTarget), param, pending);
            var lambda = Expression.Lambda<Func<TSource, TTarget>>(body, param);
            del = lambda.Compile();
            compiledMappings[key] = del;
        }

        return (Func<TSource, TTarget>)del;
    }

    private Action<TSource, TTarget> GetOrCompileUpdateMapping<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!compiledUpdateMappings.TryGetValue(key, out var del))
        {
            pendingConfigs.TryGetValue(key, out var configObj);
            del = CompileUpdateMapping<TSource, TTarget>(configObj);
            compiledUpdateMappings[key] = del;
        }

        return (Action<TSource, TTarget>)del;
    }

    private Action<TSource, TTarget> CompileUpdateMapping<TSource, TTarget>(object? configObj)
    {
        var sourceParam = Expression.Parameter(typeof(TSource), "src");
        var targetParam = Expression.Parameter(typeof(TTarget), "dest");
        var config = configObj as IMapperConfig;
        var memberConfigs = config?.GetMemberConfigs() ?? new Dictionary<string, IMemberOptions>();
        var customMappings = config?.GetCustomMappings() ?? new Dictionary<string, Delegate>();

        var assignments = new List<Expression>();

        foreach (var prop in typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite))
        {
            var assignment = BuildPropertyAssignment(
                typeof(TSource), typeof(TTarget), prop,
                sourceParam, targetParam,
                memberConfigs, customMappings);
            if (assignment != null)
                assignments.Add(assignment);
        }

        if (assignments.Count == 0)
            assignments.Add(Expression.Empty());

        var block = Expression.Block(assignments);
        var lambda = Expression.Lambda<Action<TSource, TTarget>>(block, sourceParam, targetParam);
        return lambda.Compile();
    }

    #endregion

    #region Expression Building

    /// <summary>
    /// Construit l'expression MemberInit pour créer un nouvel objet cible.
    /// Utilisé pour Map et ProjectTo, et récursivement pour les objets imbriqués.
    /// </summary>
    internal Expression BuildMappingExpression(
        Type sourceType, Type targetType, Expression sourceExpr, object? configObj)
    {
        var config = configObj as IMapperConfig;
        var memberConfigs = config?.GetMemberConfigs() ?? new Dictionary<string, IMemberOptions>();
        var customMappings = config?.GetCustomMappings() ?? new Dictionary<string, Delegate>();

        // Construire l'appel au constructeur
        var (newExpr, ctorMatchedProps) = BuildConstructorExpression(
            sourceType, targetType, sourceExpr, memberConfigs, customMappings);

        // Construire les bindings pour les propriétés non couvertes par le constructeur
        var bindings = new List<MemberBinding>();

        foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite))
        {
            if (ctorMatchedProps.Contains(prop.Name)) continue;

            var binding = BuildMemberBinding(
                sourceType, targetType, prop, sourceExpr,
                memberConfigs, customMappings);
            if (binding != null)
                bindings.Add(binding);
        }

        return Expression.MemberInit(newExpr, bindings);
    }

    private MemberBinding? BuildMemberBinding(
        Type sourceType, Type targetType, PropertyInfo targetProp, Expression sourceExpr,
        IReadOnlyDictionary<string, IMemberOptions> memberConfigs,
        IReadOnlyDictionary<string, Delegate> customMappings)
    {
        Expression? valueExpr = null;

        if (memberConfigs.TryGetValue(targetProp.Name, out var opts))
        {
            if (opts.IsIgnored) return null;

            if (opts.MemberConverter != null)
            {
                valueExpr = Expression.Convert(
                    Expression.Invoke(Expression.Constant(opts.MemberConverter), sourceExpr),
                    targetProp.PropertyType);
            }
            else if (opts.MapFromExpression != null)
            {
                valueExpr = InlineLambda(opts.MapFromExpression, sourceExpr);
                valueExpr = UnwrapObjectConvert(valueExpr);
                if (valueExpr.Type != targetProp.PropertyType)
                    valueExpr = Expression.Convert(valueExpr, targetProp.PropertyType);
            }
            else
            {
                var sp = sourceType.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (sp == null) return null;
                valueExpr = Expression.Property(sourceExpr, sp);
            }

            // NullSubstitute
            if (opts.HasNullSubstitute && !targetProp.PropertyType.IsValueType)
            {
                valueExpr = Expression.Coalesce(
                    valueExpr,
                    Expression.Constant(opts.NullSubstituteValue, targetProp.PropertyType));
            }

            // Condition (ternaire pour MemberInit)
            if (opts.Condition != null)
            {
                var conditionResult = Expression.Invoke(
                    Expression.Constant(opts.Condition), sourceExpr);
                valueExpr = Expression.Condition(
                    conditionResult, valueExpr, Expression.Default(targetProp.PropertyType));
            }
        }
        else if (customMappings.TryGetValue(targetProp.Name, out var customGetter))
        {
            var invoke = Expression.Invoke(Expression.Constant(customGetter), sourceExpr);
            valueExpr = Expression.Convert(invoke, targetProp.PropertyType);
        }
        else
        {
            valueExpr = BuildConventionBasedExpression(sourceType, targetProp, sourceExpr);
        }

        if (valueExpr == null) return null;

        return Expression.Bind(targetProp, valueExpr);
    }

    private Expression? BuildPropertyAssignment(
        Type sourceType, Type targetType, PropertyInfo targetProp,
        Expression sourceExpr, Expression targetExpr,
        IReadOnlyDictionary<string, IMemberOptions> memberConfigs,
        IReadOnlyDictionary<string, Delegate> customMappings)
    {
        Expression? valueExpr = null;

        if (memberConfigs.TryGetValue(targetProp.Name, out var opts))
        {
            if (opts.IsIgnored) return null;

            if (opts.MemberConverter != null)
            {
                valueExpr = Expression.Convert(
                    Expression.Invoke(Expression.Constant(opts.MemberConverter), sourceExpr),
                    targetProp.PropertyType);
            }
            else if (opts.MapFromExpression != null)
            {
                valueExpr = InlineLambda(opts.MapFromExpression, sourceExpr);
                valueExpr = UnwrapObjectConvert(valueExpr);
                if (valueExpr.Type != targetProp.PropertyType)
                    valueExpr = Expression.Convert(valueExpr, targetProp.PropertyType);
            }
            else
            {
                var sp = sourceType.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (sp == null) return null;
                valueExpr = Expression.Property(sourceExpr, sp);
            }

            // NullSubstitute
            if (opts.HasNullSubstitute && !targetProp.PropertyType.IsValueType)
            {
                valueExpr = Expression.Coalesce(
                    valueExpr,
                    Expression.Constant(opts.NullSubstituteValue, targetProp.PropertyType));
            }

            // Condition (pour update : IfThen préserve la valeur existante)
            if (opts.Condition != null)
            {
                var conditionResult = Expression.Invoke(
                    Expression.Constant(opts.Condition), sourceExpr);
                return Expression.IfThen(
                    conditionResult,
                    Expression.Assign(Expression.Property(targetExpr, targetProp), valueExpr));
            }
        }
        else if (customMappings.TryGetValue(targetProp.Name, out var customGetter))
        {
            var invoke = Expression.Invoke(Expression.Constant(customGetter), sourceExpr);
            valueExpr = Expression.Convert(invoke, targetProp.PropertyType);
        }
        else
        {
            valueExpr = BuildConventionBasedExpression(sourceType, targetProp, sourceExpr);
        }

        if (valueExpr == null) return null;

        return Expression.Assign(Expression.Property(targetExpr, targetProp), valueExpr);
    }

    private Expression? BuildConventionBasedExpression(
        Type sourceType, PropertyInfo targetProp, Expression sourceExpr)
    {
        var sourceProp = sourceType.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);
        if (sourceProp == null) return null;

        // Types identiques
        if (sourceProp.PropertyType == targetProp.PropertyType)
            return Expression.Property(sourceExpr, sourceProp);

        // Type assignable
        if (targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            return Expression.Convert(Expression.Property(sourceExpr, sourceProp), targetProp.PropertyType);

        // Objet imbriqué (types complexes différents avec mapping enregistré)
        if (IsComplexType(sourceProp.PropertyType) && IsComplexType(targetProp.PropertyType)
            && pendingConfigs.ContainsKey((sourceProp.PropertyType, targetProp.PropertyType)))
        {
            var nestedSource = Expression.Property(sourceExpr, sourceProp);
            pendingConfigs.TryGetValue((sourceProp.PropertyType, targetProp.PropertyType), out var nestedConfig);
            var nestedMapping = BuildMappingExpression(
                sourceProp.PropertyType, targetProp.PropertyType, nestedSource, nestedConfig);

            // Null check pour les types référence
            if (!sourceProp.PropertyType.IsValueType)
            {
                return Expression.Condition(
                    Expression.Equal(nestedSource, Expression.Constant(null, sourceProp.PropertyType)),
                    Expression.Default(targetProp.PropertyType),
                    nestedMapping);
            }

            return nestedMapping;
        }

        // Collection de types complexes
        if (TryGetCollectionElementType(sourceProp.PropertyType, out var sourceElem)
            && TryGetCollectionElementType(targetProp.PropertyType, out var targetElem)
            && sourceElem != targetElem
            && pendingConfigs.ContainsKey((sourceElem, targetElem)))
        {
            var sourceCollection = Expression.Property(sourceExpr, sourceProp);
            var collectionMapping = BuildCollectionMappingExpression(
                sourceProp.PropertyType, sourceElem, targetElem, targetProp.PropertyType,
                sourceCollection);

            // Null check pour les collections
            if (!sourceProp.PropertyType.IsValueType)
            {
                return Expression.Condition(
                    Expression.Equal(sourceCollection, Expression.Constant(null, sourceProp.PropertyType)),
                    Expression.Default(targetProp.PropertyType),
                    collectionMapping);
            }

            return collectionMapping;
        }

        return null;
    }

    #endregion

    #region Constructor Resolution

    private (NewExpression, HashSet<string>) BuildConstructorExpression(
        Type sourceType, Type targetType, Expression sourceExpr,
        IReadOnlyDictionary<string, IMemberOptions> memberConfigs,
        IReadOnlyDictionary<string, Delegate> customMappings)
    {
        // Essayer le constructeur sans paramètre en premier
        var defaultCtor = targetType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (defaultCtor != null)
            return (Expression.New(defaultCtor), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        // Chercher le meilleur constructeur paramétré
        var ctors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = ctor.GetParameters();
            var args = new Expression[parameters.Length];
            var matched = true;
            var matchedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = parameters[i].Name!;
                Expression? argExpr = null;

                // Trouver la propriété cible correspondant au paramètre constructeur
                var targetProp = targetType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p =>
                        string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

                // Vérifier les MemberOptions
                if (targetProp != null && memberConfigs.TryGetValue(targetProp.Name, out var opts))
                {
                    if (opts.MemberConverter != null)
                    {
                        argExpr = Expression.Convert(
                            Expression.Invoke(Expression.Constant(opts.MemberConverter), sourceExpr),
                            parameters[i].ParameterType);
                    }
                    else if (opts.MapFromExpression != null)
                    {
                        argExpr = InlineLambda(opts.MapFromExpression, sourceExpr);
                        argExpr = UnwrapObjectConvert(argExpr);
                        if (argExpr.Type != parameters[i].ParameterType)
                            argExpr = Expression.Convert(argExpr, parameters[i].ParameterType);
                    }
                }

                // Vérifier les legacy CustomMappings
                if (argExpr == null && targetProp != null
                    && customMappings.TryGetValue(targetProp.Name, out var customGetter))
                {
                    var invoke = Expression.Invoke(Expression.Constant(customGetter), sourceExpr);
                    argExpr = Expression.Convert(invoke, parameters[i].ParameterType);
                }

                // Convention : matcher par nom (case-insensitive)
                if (argExpr == null)
                {
                    var sourceProp = sourceType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p =>
                            string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

                    if (sourceProp != null
                        && parameters[i].ParameterType.IsAssignableFrom(sourceProp.PropertyType))
                    {
                        argExpr = Expression.Property(sourceExpr, sourceProp);
                    }
                }

                // Paramètre ignoré → utiliser default
                if (argExpr == null && targetProp != null
                    && memberConfigs.TryGetValue(targetProp.Name, out var ignoredOpts)
                    && ignoredOpts.IsIgnored)
                {
                    argExpr = Expression.Default(parameters[i].ParameterType);
                }

                // Paramètre optionnel avec valeur par défaut
                if (argExpr == null && parameters[i].HasDefaultValue)
                {
                    argExpr = Expression.Constant(
                        parameters[i].DefaultValue, parameters[i].ParameterType);
                }

                if (argExpr == null)
                {
                    matched = false;
                    break;
                }

                args[i] = argExpr;
                if (targetProp != null) matchedProps.Add(targetProp.Name);
            }

            if (matched)
                return (Expression.New(ctor, args), matchedProps);
        }

        throw new InvalidOperationException(
            $"Aucun constructeur approprié trouvé pour {targetType.Name}. " +
            $"Le type doit avoir soit un constructeur sans paramètre, " +
            $"soit un constructeur dont les paramètres correspondent aux propriétés source.");
    }

    #endregion

    #region Collection Mapping

    private Expression BuildCollectionMappingExpression(
        Type sourceCollectionType, Type sourceElemType, Type targetElemType, Type targetCollectionType,
        Expression sourceCollectionExpr)
    {
        // Construire : source.Select(x => new TargetElem { ... }).ToList() ou .ToArray()
        var elemParam = Expression.Parameter(sourceElemType, "x");
        pendingConfigs.TryGetValue((sourceElemType, targetElemType), out var elemConfig);
        var elemMapping = BuildMappingExpression(sourceElemType, targetElemType, elemParam, elemConfig);
        var elemLambda = Expression.Lambda(elemMapping, elemParam);

        var selectMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(sourceElemType, targetElemType);
        var selectCall = Expression.Call(selectMethod, sourceCollectionExpr, elemLambda);

        if (targetCollectionType.IsArray)
        {
            var toArrayMethod = typeof(Enumerable).GetMethod("ToArray")!
                .MakeGenericMethod(targetElemType);
            return Expression.Call(toArrayMethod, selectCall);
        }

        var toListMethod = typeof(Enumerable).GetMethod("ToList")!
            .MakeGenericMethod(targetElemType);
        var toListCall = Expression.Call(toListMethod, selectCall);

        if (targetCollectionType.IsAssignableFrom(toListCall.Type))
            return toListCall;

        return Expression.Convert(toListCall, targetCollectionType);
    }

    #endregion

    #region Helpers

    private static T CreateInstance<T>()
    {
        var defaultCtor = typeof(T).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (defaultCtor != null)
            return (T)defaultCtor.Invoke(null);

        throw new InvalidOperationException(
            $"Impossible de créer une instance de {typeof(T).Name}. " +
            $"BeforeMap et Map vers existant nécessitent un constructeur sans paramètre.");
    }

    private static bool IsComplexType(Type type)
    {
        return !type.IsPrimitive
               && type != typeof(string)
               && type != typeof(decimal)
               && type != typeof(DateTime)
               && type != typeof(DateTimeOffset)
               && type != typeof(Guid)
               && !type.IsEnum
               && !(type.IsValueType && Nullable.GetUnderlyingType(type) != null);
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return IsComplexType(elementType);
        }

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable != null)
        {
            elementType = enumerable.GetGenericArguments()[0];
            return IsComplexType(elementType);
        }

        return false;
    }

    internal static Expression InlineLambda(LambdaExpression lambda, Expression argument)
    {
        return new ParameterReplacer(lambda.Parameters[0], argument).Visit(lambda.Body);
    }

    internal static Expression UnwrapObjectConvert(Expression expr)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Convert } unary
            && unary.Type == typeof(object))
            return unary.Operand;
        return expr;
    }

    private class ParameterReplacer(ParameterExpression oldParam, Expression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
    }

    #endregion
}
