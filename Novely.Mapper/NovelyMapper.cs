using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Novely.Mapper;

/// <summary>
/// Interface standard pour l'injection de dépendances du mapper.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Crée une configuration de mapping entre TSource et TTarget.
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>();

    /// <summary>
    /// Mappe un objet source vers un nouvel objet cible. Le type source est inféré au runtime.
    /// </summary>
    TTarget Map<TTarget>(object source);

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
/// Interface définissant les fonctionnalités principales d'un mapper NovelyMapper.
/// Hérite de <see cref="IMapper"/> pour rétrocompatibilité.
/// </summary>
public interface INovelyMapper : IMapper
{
}

/// <summary>
/// Implémentation principale du mapper NovelyMapper.
/// </summary>
public class NovelyMapper : INovelyMapper
{
    private readonly ConcurrentDictionary<(Type, Type), Delegate> compiledMappings = new();
    private readonly ConcurrentDictionary<(Type, Type), Delegate> compiledUpdateMappings = new();
    internal readonly ConcurrentDictionary<(Type, Type), object> pendingConfigs = new();

    /// <summary>
    /// Pile de compilation thread-local pour détecter les références circulaires
    /// lors de la construction des Expression Trees. Évite les StackOverflowException.
    /// </summary>
    [ThreadStatic]
    private static HashSet<(Type, Type)>? _compilationStack;

    /// <summary>
    /// Ensemble thread-local des objets source en cours de mapping.
    /// Détecte les cycles dans les données runtime (ex: customer.Supplier.Customer == customer)
    /// et retourne default au lieu de recurser infiniment.
    /// </summary>
    [ThreadStatic]
    private static HashSet<object>? _runtimeVisited;

    internal NovelyMapperOptions Options { get; set; } = new();

    public INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>()
    {
        var config = new NovelyMapperConfig<TSource, TTarget>(this);
        pendingConfigs[(typeof(TSource), typeof(TTarget))] = config;
        return config;
    }

    public TTarget Map<TTarget>(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sourceType = source.GetType();
        var targetType = typeof(TTarget);

        // Détection de collection : si TTarget est IEnumerable<TElem> (ou List<>, ICollection<>, etc.)
        // et que source implémente IEnumerable<TSrcElem>, router vers Map<TSrcElem, TElem>(IEnumerable<TSrcElem>)
        if (TryGetCollectionElementType(targetType, out var targetElemType)
            && TryGetCollectionElementType(sourceType, out var sourceElemType)
            && pendingConfigs.ContainsKey((sourceElemType, targetElemType)))
        {
            // Appeler Map<TSrcElem, TElem>(IEnumerable<TSrcElem>) via réflexion
            var collectionMapMethod = typeof(NovelyMapper)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(Map)
                            && m.GetGenericArguments().Length == 2
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType.IsGenericType
                            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .MakeGenericMethod(sourceElemType, targetElemType);

            var enumerable = collectionMapMethod.Invoke(this, [source])!;

            // Matérialiser si le type cible est concret (List<T>, T[])
            if (targetType.IsArray)
            {
                var toArrayMethod = typeof(Enumerable).GetMethod("ToArray")!
                    .MakeGenericMethod(targetElemType);
                return (TTarget)toArrayMethod.Invoke(null, [enumerable])!;
            }

            if (targetType.IsAssignableFrom(enumerable.GetType()))
                return (TTarget)enumerable;

            var toListMethod = typeof(Enumerable).GetMethod("ToList")!
                .MakeGenericMethod(targetElemType);
            return (TTarget)toListMethod.Invoke(null, [enumerable])!;
        }

        // Appeler Map<TSource, TTarget> via réflexion avec le vrai type runtime
        var method = typeof(NovelyMapper)
            .GetMethods()
            .First(m => m.Name == nameof(Map)
                        && m.GetGenericArguments().Length == 2
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == m.GetGenericArguments()[0])
            .MakeGenericMethod(sourceType, targetType);

        return (TTarget)method.Invoke(this, [source])!;
    }

    public TTarget Map<TSource, TTarget>(TSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Détection de cycle runtime pour les types référence :
        // si cet objet source est déjà en cours de mapping dans la pile d'appels,
        // retourner default (null) pour casser la boucle infinie.
        bool isTopLevel = _runtimeVisited == null;
        var visited = _runtimeVisited ??= new(ReferenceEqualityComparer.Instance);

        if (!typeof(TSource).IsValueType && !visited.Add(source))
            return default!;

        try
        {
        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw NovelyMapperException.MissingMapping(typeof(TSource), typeof(TTarget));

        var config = (NovelyMapperConfig<TSource, TTarget>)configObj;

        // ConvertUsing
        if (config.CustomConverter != null)
        {
            try
            {
                return config.CustomConverter(source);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.RuntimeMappingFailed(
                    typeof(TSource), typeof(TTarget), "ConvertUsing", ex);
            }
        }

        // BeforeMap → update mapping
        if (config.BeforeMapAction != null)
        {
            var target = CreateInstance<TTarget>();

            try
            {
                config.BeforeMapAction(source, target);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.RuntimeMappingFailed(
                    typeof(TSource), typeof(TTarget), "BeforeMap", ex);
            }

            var updateFunc = GetOrCompileUpdateMapping<TSource, TTarget>();

            try
            {
                updateFunc(source, target);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw DiagnoseRuntimeError(source, target, ex);
            }

            InvokeAfterMap(config, source, target);
            return target;
        }

        // Standard path
        var func = GetOrCompileMapping<TSource, TTarget>();

        TTarget result;
        try
        {
            result = func(source);
        }
        catch (Exception ex) when (ex is not NovelyMapperException)
        {
            // Re-exécuter propriété par propriété pour identifier la fautive
            throw DiagnoseRuntimeError<TSource, TTarget>(source, ex);
        }

        InvokeAfterMap(config, source, result);
        return result;

        } // try
        finally
        {
            if (!typeof(TSource).IsValueType)
                visited.Remove(source);
            if (isTopLevel)
                _runtimeVisited = null;
        }
    }

    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        bool isTopLevel = _runtimeVisited == null;
        var visited = _runtimeVisited ??= new(ReferenceEqualityComparer.Instance);

        if (!typeof(TSource).IsValueType)
            visited.Add(source);

        try
        {

        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw NovelyMapperException.MissingMapping(typeof(TSource), typeof(TTarget));

        var config = (NovelyMapperConfig<TSource, TTarget>)configObj;

        if (config.BeforeMapAction != null)
        {
            try
            {
                config.BeforeMapAction(source, target);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.RuntimeMappingFailed(
                    typeof(TSource), typeof(TTarget), "BeforeMap", ex);
            }
        }

        var updateFunc = GetOrCompileUpdateMapping<TSource, TTarget>();

        try
        {
            updateFunc(source, target);
        }
        catch (Exception ex) when (ex is not NovelyMapperException)
        {
            throw DiagnoseRuntimeError(source, target, ex);
        }

        InvokeAfterMap(config, source, target);
        return target;

        } // try
        finally
        {
            if (!typeof(TSource).IsValueType)
                visited.Remove(source);
            if (isTopLevel)
                _runtimeVisited = null;
        }
    }

    public IEnumerable<TTarget> Map<TSource, TTarget>(IEnumerable<TSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.ContainsKey(key))
            throw NovelyMapperException.MissingMapping(typeof(TSource), typeof(TTarget));

        return MapIterator<TSource, TTarget>(sources);
    }

    private IEnumerable<TTarget> MapIterator<TSource, TTarget>(IEnumerable<TSource> sources)
    {
        int index = 0;
        foreach (var item in sources)
        {
            TTarget result;
            try
            {
                result = Map<TSource, TTarget>(item);
            }
            catch (NovelyMapperException ex)
            {
                throw NovelyMapperException.CollectionItemMappingFailed(
                    typeof(TSource), typeof(TTarget), index, ex);
            }
            catch (Exception ex)
            {
                throw NovelyMapperException.CollectionItemMappingFailed(
                    typeof(TSource), typeof(TTarget), index, ex);
            }

            yield return result;
            index++;
        }
    }

    public Expression<Func<TSource, TTarget>> GetProjectionExpression<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            throw NovelyMapperException.MissingMapping(typeof(TSource), typeof(TTarget));

        try
        {
            var param = Expression.Parameter(typeof(TSource), "src");
            var body = BuildMappingExpression(typeof(TSource), typeof(TTarget), param, configObj);
            return Expression.Lambda<Func<TSource, TTarget>>(body, param);
        }
        catch (Exception ex) when (ex is not NovelyMapperException)
        {
            throw NovelyMapperException.MappingCompilationFailed(
                typeof(TSource), typeof(TTarget), null, ex);
        }
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

    #region Runtime Error Diagnosis

    /// <summary>
    /// Lorsqu'un delegate compilé échoue au runtime, re-exécute le mapping propriété
    /// par propriété pour identifier exactement laquelle a causé l'erreur.
    /// </summary>
    private NovelyMapperException DiagnoseRuntimeError<TSource, TTarget>(
        TSource source, Exception originalException)
    {
        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            return NovelyMapperException.RuntimeMappingFailed(
                typeof(TSource), typeof(TTarget), "mapping des propriétés", originalException);

        var config = (IMapperConfig)configObj;
        var memberConfigs = config.GetMemberConfigs();
        var customMappings = config.GetCustomMappings();

        foreach (var prop in typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite))
        {
            try
            {
                // Tenter de compiler et exécuter le binding pour cette propriété seule
                var param = Expression.Parameter(typeof(TSource), "src");
                var binding = BuildMemberBinding(
                    typeof(TSource), typeof(TTarget), prop, param,
                    memberConfigs, customMappings);

                if (binding == null) continue;

                // Compiler un mini-lambda qui évalue juste cette propriété
                var memberInit = Expression.MemberInit(
                    BuildConstructorExpression(
                        typeof(TSource), typeof(TTarget), param,
                        memberConfigs, customMappings).Item1,
                    binding);
                var lambda = Expression.Lambda(memberInit, param);
                var compiled = lambda.Compile();
                compiled.DynamicInvoke(source);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return NovelyMapperException.PropertyMappingFailed(
                    typeof(TSource), typeof(TTarget), prop.Name, ex.InnerException);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                return NovelyMapperException.PropertyMappingFailed(
                    typeof(TSource), typeof(TTarget), prop.Name, ex);
            }
        }

        // Si on n'a pas trouvé la propriété fautive, retourner l'erreur originale avec contexte
        return NovelyMapperException.RuntimeMappingFailed(
            typeof(TSource), typeof(TTarget), "mapping des propriétés", originalException);
    }

    /// <summary>
    /// Variante pour le mapping vers un objet existant (update mapping).
    /// Re-exécute propriété par propriété pour identifier l'erreur.
    /// </summary>
    private NovelyMapperException DiagnoseRuntimeError<TSource, TTarget>(
        TSource source, TTarget target, Exception originalException)
    {
        var key = (typeof(TSource), typeof(TTarget));
        if (!pendingConfigs.TryGetValue(key, out var configObj))
            return NovelyMapperException.RuntimeMappingFailed(
                typeof(TSource), typeof(TTarget), "mapping des propriétés", originalException);

        var config = (IMapperConfig)configObj;
        var memberConfigs = config.GetMemberConfigs();
        var customMappings = config.GetCustomMappings();

        foreach (var prop in typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite))
        {
            try
            {
                var sourceParam = Expression.Parameter(typeof(TSource), "src");
                var targetParam = Expression.Parameter(typeof(TTarget), "dest");

                var assignment = BuildPropertyAssignment(
                    typeof(TSource), typeof(TTarget), prop,
                    sourceParam, targetParam,
                    memberConfigs, customMappings);

                if (assignment == null) continue;

                var block = Expression.Block(assignment);
                var lambda = Expression.Lambda(block, sourceParam, targetParam);
                var compiled = lambda.Compile();
                compiled.DynamicInvoke(source, target);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return NovelyMapperException.PropertyMappingFailed(
                    typeof(TSource), typeof(TTarget), prop.Name, ex.InnerException);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                return NovelyMapperException.PropertyMappingFailed(
                    typeof(TSource), typeof(TTarget), prop.Name, ex);
            }
        }

        return NovelyMapperException.RuntimeMappingFailed(
            typeof(TSource), typeof(TTarget), "mapping des propriétés", originalException);
    }

    #endregion

    #region Private helpers

    private static void InvokeAfterMap<TSource, TTarget>(
        NovelyMapperConfig<TSource, TTarget> config, TSource source, TTarget target)
    {
        if (config.AfterMapAction == null) return;
        try
        {
            config.AfterMapAction(source, target);
        }
        catch (Exception ex) when (ex is not NovelyMapperException)
        {
            throw NovelyMapperException.RuntimeMappingFailed(
                typeof(TSource), typeof(TTarget), "AfterMap", ex);
        }
    }

    #endregion

    #region Compilation

    private Func<TSource, TTarget> GetOrCompileMapping<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!compiledMappings.TryGetValue(key, out var del))
        {
            if (!pendingConfigs.TryGetValue(key, out var pending))
                throw NovelyMapperException.MissingMapping(typeof(TSource), typeof(TTarget));

            try
            {
                var param = Expression.Parameter(typeof(TSource), "src");
                var body = BuildMappingExpression(typeof(TSource), typeof(TTarget), param, pending);
                var lambda = Expression.Lambda<Func<TSource, TTarget>>(body, param);
                del = lambda.Compile();
                compiledMappings[key] = del;
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.MappingCompilationFailed(
                    typeof(TSource), typeof(TTarget), null, ex);
            }
        }

        return (Func<TSource, TTarget>)del;
    }

    private Action<TSource, TTarget> GetOrCompileUpdateMapping<TSource, TTarget>()
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!compiledUpdateMappings.TryGetValue(key, out var del))
        {
            pendingConfigs.TryGetValue(key, out var configObj);

            try
            {
                del = CompileUpdateMapping<TSource, TTarget>(configObj);
                compiledUpdateMappings[key] = del;
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.MappingCompilationFailed(
                    typeof(TSource), typeof(TTarget), null, ex);
            }
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

        // Ajouter la paire courante à la pile de compilation pour détecter les cycles
        // (CompileUpdateMapping ne passe pas par BuildMappingExpression au top-level)
        var stack = _compilationStack ??= new();
        var key = (typeof(TSource), typeof(TTarget));
        stack.Add(key);

        try
        {

        var assignments = new List<Expression>();

        foreach (var prop in typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite))
        {
            try
            {
                var assignment = BuildPropertyAssignment(
                    typeof(TSource), typeof(TTarget), prop,
                    sourceParam, targetParam,
                    memberConfigs, customMappings);
                if (assignment != null)
                    assignments.Add(assignment);
            }
            catch (Exception ex) when (ex is not NovelyMapperException)
            {
                throw NovelyMapperException.MappingCompilationFailed(
                    typeof(TSource), typeof(TTarget), prop.Name, ex);
            }
        }

        if (assignments.Count == 0)
            assignments.Add(Expression.Empty());

        var block = Expression.Block(assignments);
        var lambda = Expression.Lambda<Action<TSource, TTarget>>(block, sourceParam, targetParam);
        return lambda.Compile();

        } // try
        finally
        {
            stack.Remove(key);
        }
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
        // Détection de référence circulaire : si cette paire est déjà en cours
        // de compilation dans la pile d'appels, émettre un appel runtime Map()
        // pour casser le cycle au lieu de recurser infiniment (StackOverflow).
        var stack = _compilationStack ??= new();
        var key = (sourceType, targetType);
        if (!stack.Add(key))
        {
            return BuildRuntimeMapCall(sourceType, targetType, sourceExpr);
        }

        try
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

                try
                {
                    var binding = BuildMemberBinding(
                        sourceType, targetType, prop, sourceExpr,
                        memberConfigs, customMappings);
                    if (binding != null)
                        bindings.Add(binding);
                }
                catch (Exception ex) when (ex is not NovelyMapperException)
                {
                    throw NovelyMapperException.MappingCompilationFailed(
                        sourceType, targetType, prop.Name, ex);
                }
            }

            return Expression.MemberInit(newExpr, bindings);
        }
        finally
        {
            stack.Remove(key);
        }
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
                valueExpr = ResolveValueExpression(valueExpr, targetProp.PropertyType);
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
            valueExpr = BuildConventionBasedExpression(sourceType, targetType, targetProp, sourceExpr);
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
                valueExpr = ResolveValueExpression(valueExpr, targetProp.PropertyType);
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
            valueExpr = BuildConventionBasedExpression(sourceType, targetType, targetProp, sourceExpr);
        }

        if (valueExpr == null) return null;

        return Expression.Assign(Expression.Property(targetExpr, targetProp), valueExpr);
    }

    private Expression? BuildConventionBasedExpression(
        Type sourceType, Type targetType, PropertyInfo targetProp, Expression sourceExpr)
    {
        var sourceProp = sourceType.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);
        if (sourceProp == null) return null;

        // Types identiques
        if (sourceProp.PropertyType == targetProp.PropertyType)
            return Expression.Property(sourceExpr, sourceProp);

        // Type assignable
        if (targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            return Expression.Convert(Expression.Property(sourceExpr, sourceProp), targetProp.PropertyType);

        // Nullable<T> → T : extraire .GetValueOrDefault()
        var sourceUnderlying = Nullable.GetUnderlyingType(sourceProp.PropertyType);
        if (sourceUnderlying != null && sourceUnderlying == targetProp.PropertyType)
        {
            var sourceAccess = Expression.Property(sourceExpr, sourceProp);
            return Expression.Call(sourceAccess,
                sourceProp.PropertyType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!);
        }

        // T → Nullable<T> : conversion implicite
        var targetUnderlying = Nullable.GetUnderlyingType(targetProp.PropertyType);
        if (targetUnderlying != null && targetUnderlying == sourceProp.PropertyType)
        {
            return Expression.Convert(
                Expression.Property(sourceExpr, sourceProp), targetProp.PropertyType);
        }

        // Nullable<T> → Nullable<U> ou T → U avec conversion implicite entre les types sous-jacents
        if (sourceUnderlying != null && targetUnderlying != null
            && targetUnderlying.IsAssignableFrom(sourceUnderlying))
        {
            var sourceAccess = Expression.Property(sourceExpr, sourceProp);
            return Expression.Convert(sourceAccess, targetProp.PropertyType);
        }

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

        // Types incompatibles avec même nom : signaler via MissingPropertyBehavior
        if (Options.MissingPropertyBehavior == MissingPropertyBehavior.Throw)
        {
            throw NovelyMapperException.TypeMismatch(
                sourceType, targetType, targetProp.Name,
                sourceProp.PropertyType, targetProp.PropertyType);
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

        if (ctors.Length == 0)
        {
            throw new NovelyMapperException(
                $"Le type '{NovelyMapperException.FormatTypeName(targetType)}' n'a aucun constructeur public.",
                sourceType, targetType, null,
                "Ajoutez un constructeur public au type cible, ou utilisez ConvertUsing pour un mapping entièrement personnalisé.");
        }

        // Collecter les paramètres non résolus de chaque constructeur pour un meilleur message d'erreur
        var bestUnmatchedParams = new List<string>();

        foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = ctor.GetParameters();
            var args = new Expression[parameters.Length];
            var matched = true;
            var matchedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatchedForThisCtor = new List<string>();

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
                        argExpr = ResolveValueExpression(argExpr, parameters[i].ParameterType);
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

                    if (sourceProp != null)
                    {
                        if (parameters[i].ParameterType.IsAssignableFrom(sourceProp.PropertyType))
                        {
                            argExpr = Expression.Property(sourceExpr, sourceProp);
                        }
                        else
                        {
                            // Nullable<T> → T ou T → Nullable<T>
                            var srcUnder = Nullable.GetUnderlyingType(sourceProp.PropertyType);
                            var paramUnder = Nullable.GetUnderlyingType(parameters[i].ParameterType);
                            if (srcUnder != null && srcUnder == parameters[i].ParameterType)
                            {
                                argExpr = Expression.Call(
                                    Expression.Property(sourceExpr, sourceProp),
                                    sourceProp.PropertyType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!);
                            }
                            else if (paramUnder != null && paramUnder == sourceProp.PropertyType)
                            {
                                argExpr = Expression.Convert(
                                    Expression.Property(sourceExpr, sourceProp),
                                    parameters[i].ParameterType);
                            }
                        }
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
                    unmatchedForThisCtor.Add(
                        $"'{paramName}' ({NovelyMapperException.FormatTypeName(parameters[i].ParameterType)})");
                }
                else
                {
                    args[i] = argExpr;
                    if (targetProp != null) matchedProps.Add(targetProp.Name);
                }
            }

            if (matched)
                return (Expression.New(ctor, args), matchedProps);

            // Garder les paramètres non résolus du constructeur avec le moins de manques
            if (bestUnmatchedParams.Count == 0 || unmatchedForThisCtor.Count < bestUnmatchedParams.Count)
                bestUnmatchedParams = unmatchedForThisCtor;
        }

        throw NovelyMapperException.ConstructorResolutionFailed(
            sourceType, targetType, bestUnmatchedParams);
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

        if (toListCall.Type == targetCollectionType)
            return toListCall;

        if (targetCollectionType.IsAssignableFrom(toListCall.Type))
            return Expression.Convert(toListCall, targetCollectionType);

        return Expression.Convert(toListCall, targetCollectionType);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Émet un appel runtime à Map&lt;S,T&gt;() pour casser une référence circulaire
    /// dans l'arbre d'expressions. Le mapping sera résolu au runtime via le cache.
    /// </summary>
    private Expression BuildRuntimeMapCall(Type sourceType, Type targetType, Expression sourceExpr)
    {
        var mapMethod = typeof(NovelyMapper)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(Map)
                        && m.GetGenericArguments().Length == 2
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == m.GetGenericArguments()[0])
            .MakeGenericMethod(sourceType, targetType);

        return Expression.Call(Expression.Constant(this), mapMethod, sourceExpr);
    }

    private static T CreateInstance<T>()
    {
        var defaultCtor = typeof(T).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (defaultCtor != null)
            return (T)defaultCtor.Invoke(null);

        throw NovelyMapperException.BeforeMapRequiresParameterlessCtor(typeof(T));
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

    /// <summary>
    /// Résout une expression de valeur vers le type cible :
    /// - Si les types sont identiques, retourne tel quel
    /// - Si un mapping imbriqué est enregistré (types complexes différents), l'applique avec null-check
    /// - Si une collection avec mapping élémentaire est détectée, applique le mapping de collection
    /// - Sinon, fait un Expression.Convert classique
    /// </summary>
    private Expression ResolveValueExpression(Expression valueExpr, Type targetType)
    {
        if (valueExpr.Type == targetType)
            return valueExpr;

        var sourceType = valueExpr.Type;

        // Mapping imbriqué pour types complexes
        if (IsComplexType(sourceType) && IsComplexType(targetType)
            && pendingConfigs.ContainsKey((sourceType, targetType)))
        {
            pendingConfigs.TryGetValue((sourceType, targetType), out var nestedConfig);
            var nestedMapping = BuildMappingExpression(sourceType, targetType, valueExpr, nestedConfig);

            // Null-check pour les types référence
            if (!sourceType.IsValueType)
            {
                return Expression.Condition(
                    Expression.Equal(valueExpr, Expression.Constant(null, sourceType)),
                    Expression.Default(targetType),
                    nestedMapping);
            }

            return nestedMapping;
        }

        // Collection de types complexes
        if (TryGetCollectionElementType(sourceType, out var sourceElem)
            && TryGetCollectionElementType(targetType, out var targetElem)
            && sourceElem != targetElem
            && pendingConfigs.ContainsKey((sourceElem, targetElem)))
        {
            var collectionMapping = BuildCollectionMappingExpression(
                sourceType, sourceElem, targetElem, targetType, valueExpr);

            if (!sourceType.IsValueType)
            {
                return Expression.Condition(
                    Expression.Equal(valueExpr, Expression.Constant(null, sourceType)),
                    Expression.Default(targetType),
                    collectionMapping);
            }

            return collectionMapping;
        }

        // Nullable<T> → T
        var srcUnderlying = Nullable.GetUnderlyingType(sourceType);
        if (srcUnderlying != null && srcUnderlying == targetType)
        {
            return Expression.Call(valueExpr,
                sourceType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!);
        }

        // T → Nullable<T>
        var tgtUnderlying = Nullable.GetUnderlyingType(targetType);
        if (tgtUnderlying != null && tgtUnderlying == sourceType)
        {
            return Expression.Convert(valueExpr, targetType);
        }

        return Expression.Convert(valueExpr, targetType);
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
