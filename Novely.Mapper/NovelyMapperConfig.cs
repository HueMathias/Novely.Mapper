using System.Linq.Expressions;
using System.Reflection;

namespace Novely.Mapper;

/// <summary>
/// Interface permettant de configurer un mapping entre un type source et un type cible.
/// </summary>
public interface INovelyMapperConfig<TSource, TTarget>
{
    /// <summary>
    /// Configure le mapping d'une propriété via une expression source.
    /// </summary>
    [Obsolete("Utilisez ForMember avec Action<MemberOptions<TSource>> à la place.")]
    INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(
        Expression<Func<TTarget, TMember>> targetSelector,
        Expression<Func<TSource, object>> sourceSelector);

    /// <summary>
    /// Configure le mapping d'une propriété via les options de membre.
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(
        Expression<Func<TTarget, TMember>> targetSelector,
        Action<MemberOptions<TSource>> memberOptions);

    /// <summary>
    /// Crée automatiquement le mapping inverse (TTarget → TSource).
    /// Les expressions MapFrom simples sont inversées ; les expressions complexes sont ignorées.
    /// </summary>
    INovelyMapperConfig<TTarget, TSource> ReverseMap();

    /// <summary>
    /// Exécute une action avant le mapping.
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> BeforeMap(Action<TSource, TTarget> action);

    /// <summary>
    /// Exécute une action après le mapping.
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> AfterMap(Action<TSource, TTarget> action);

    /// <summary>
    /// Utilise un convertisseur personnalisé pour le mapping complet (remplace la compilation d'expression tree).
    /// </summary>
    INovelyMapperConfig<TSource, TTarget> ConvertUsing(Func<TSource, TTarget> converter);
}

internal interface IMapperConfig
{
    Type SourceType { get; }
    Type TargetType { get; }
    IReadOnlyDictionary<string, IMemberOptions> GetMemberConfigs();
    IReadOnlyDictionary<string, Delegate> GetCustomMappings();
    Delegate? GetCustomConverter();
    Delegate? GetBeforeMapAction();
    Delegate? GetAfterMapAction();
    List<string> Validate(Func<Type, Type, bool> hasMappingFor);
}

/// <summary>
/// Implémentation de la configuration de mapping entre TSource et TTarget.
/// </summary>
public class NovelyMapperConfig<TSource, TTarget> : INovelyMapperConfig<TSource, TTarget>, IMapperConfig
{
    private readonly NovelyMapper _mapper;

    internal readonly Dictionary<string, Func<TSource, object>> CustomMappings = [];
    internal readonly Dictionary<string, MemberOptions<TSource>> MemberConfigs = [];
    internal Func<TSource, TTarget>? CustomConverter;
    internal Action<TSource, TTarget>? BeforeMapAction;
    internal Action<TSource, TTarget>? AfterMapAction;

    internal NovelyMapperConfig(NovelyMapper mapper)
    {
        _mapper = mapper;
    }

    #region IMapperConfig

    Type IMapperConfig.SourceType => typeof(TSource);
    Type IMapperConfig.TargetType => typeof(TTarget);

    IReadOnlyDictionary<string, IMemberOptions> IMapperConfig.GetMemberConfigs()
        => MemberConfigs.ToDictionary(kv => kv.Key, kv => (IMemberOptions)kv.Value);

    IReadOnlyDictionary<string, Delegate> IMapperConfig.GetCustomMappings()
        => CustomMappings.ToDictionary(kv => kv.Key, kv => (Delegate)kv.Value);

    Delegate? IMapperConfig.GetCustomConverter() => CustomConverter;
    Delegate? IMapperConfig.GetBeforeMapAction() => BeforeMapAction;
    Delegate? IMapperConfig.GetAfterMapAction() => AfterMapAction;

    List<string> IMapperConfig.Validate(Func<Type, Type, bool> hasMappingFor)
    {
        var errors = new List<string>();
        var targetType = typeof(TTarget);
        var sourceType = typeof(TSource);

        // Déterminer les propriétés couvertes par le constructeur
        var ctorHandled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultCtor = targetType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (defaultCtor == null)
        {
            var ctors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
            {
                var parameters = ctor.GetParameters();
                var allMatch = parameters.All(p =>
                {
                    var prop = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(pr => string.Equals(pr.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (prop == null) return false;
                    if (MemberConfigs.ContainsKey(prop.Name)) return true;
                    if (CustomMappings.ContainsKey(prop.Name)) return true;
                    var sp = sourceType.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance);
                    return sp != null;
                });

                if (allMatch)
                {
                    foreach (var p in parameters)
                    {
                        var prop = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(pr => string.Equals(pr.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                        if (prop != null) ctorHandled.Add(prop.Name);
                    }
                    break;
                }
            }
        }

        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var prop in targetProps)
        {
            if (ctorHandled.Contains(prop.Name)) continue;
            if (MemberConfigs.TryGetValue(prop.Name, out var opts) && opts._isIgnored) continue;
            if (MemberConfigs.ContainsKey(prop.Name)) continue;
            if (CustomMappings.ContainsKey(prop.Name)) continue;

            var sourceProp = sourceType.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
            if (sourceProp != null)
            {
                // Types identiques ou assignables → OK
                if (prop.PropertyType.IsAssignableFrom(sourceProp.PropertyType)) continue;
                // Mapping imbriqué enregistré → OK
                if (hasMappingFor(sourceProp.PropertyType, prop.PropertyType)) continue;
            }

            if (sourceProp == null)
            {
                errors.Add(
                    $"{sourceType.Name} → {targetType.Name} : la propriété '{prop.Name}' " +
                    $"de {targetType.Name} n'a pas de propriété source correspondante.");
            }
        }

        return errors;
    }

    #endregion

    #region Public API

    [Obsolete("Utilisez ForMember avec Action<MemberOptions<TSource>> à la place.")]
    public INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(
        Expression<Func<TTarget, TMember>> targetSelector,
        Expression<Func<TSource, object>> sourceSelector)
    {
        ArgumentNullException.ThrowIfNull(targetSelector);
        ArgumentNullException.ThrowIfNull(sourceSelector);

        var targetName = ExtractMemberName(targetSelector);
        CustomMappings[targetName] = sourceSelector.Compile();
        return this;
    }

    public INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(
        Expression<Func<TTarget, TMember>> targetSelector,
        Action<MemberOptions<TSource>> memberOptions)
    {
        ArgumentNullException.ThrowIfNull(targetSelector);
        ArgumentNullException.ThrowIfNull(memberOptions);

        var targetName = ExtractMemberName(targetSelector);
        var opts = new MemberOptions<TSource>();
        memberOptions(opts);
        MemberConfigs[targetName] = opts;
        return this;
    }

    public INovelyMapperConfig<TTarget, TSource> ReverseMap()
    {
        var reverseConfig = _mapper.CreateMap<TTarget, TSource>();

        // Inverser les MemberOptions avec MapFrom simple (MemberExpression)
        foreach (var (targetPropName, opts) in MemberConfigs)
        {
            if (opts._isIgnored || opts._mapFromExpression == null) continue;

            var sourcePropName = ExtractPropertyNameFromExpression(opts._mapFromExpression.Body);
            if (sourcePropName == null) continue;

            // Forward : TTarget.targetPropName ← TSource.sourcePropName
            // Reverse : TSource.sourcePropName ← TTarget.targetPropName
            var targetPropOnReverse = typeof(TSource).GetProperty(sourcePropName,
                BindingFlags.Public | BindingFlags.Instance);
            var sourcePropOnReverse = typeof(TTarget).GetProperty(targetPropName,
                BindingFlags.Public | BindingFlags.Instance);
            if (targetPropOnReverse == null || sourcePropOnReverse == null) continue;

            // MapFrom pour le reverse : src (TTarget) => src.targetPropName
            var srcParam = Expression.Parameter(typeof(TTarget), "src");
            var srcPropAccess = Expression.Property(srcParam, sourcePropOnReverse);
            var srcConverted = Expression.Convert(srcPropAccess, typeof(object));
            var mapFromLambda = Expression.Lambda<Func<TTarget, object>>(srcConverted, srcParam);

            // Sélecteur cible pour le reverse : dest (TSource) => dest.sourcePropName
            var destParam = Expression.Parameter(typeof(TSource), "dest");
            var destPropAccess = Expression.Property(destParam, targetPropOnReverse);
            var destConverted = Expression.Convert(destPropAccess, typeof(object));
            var targetSelectorExpr = Expression.Lambda<Func<TSource, object>>(destConverted, destParam);

            reverseConfig.ForMember(targetSelectorExpr, opt => opt.MapFrom(mapFromLambda));
        }

        // Inverser les legacy CustomMappings via l'ancien ForMember
        // Les delegates compilés ne sont pas inversibles → on les ignore
        // Les mappings par convention sont automatiquement réversibles

        return reverseConfig;
    }

    public INovelyMapperConfig<TSource, TTarget> BeforeMap(Action<TSource, TTarget> action)
    {
        BeforeMapAction = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    public INovelyMapperConfig<TSource, TTarget> AfterMap(Action<TSource, TTarget> action)
    {
        AfterMapAction = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    public INovelyMapperConfig<TSource, TTarget> ConvertUsing(Func<TSource, TTarget> converter)
    {
        CustomConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }

    #endregion

    #region Helpers

    internal static string ExtractMemberName<TMember>(Expression<Func<TTarget, TMember>> selector)
    {
        return selector.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression u when u.Operand is MemberExpression m => m.Member.Name,
            _ => throw new ArgumentException("L'expression cible doit être une propriété.")
        };
    }

    private static string? ExtractPropertyNameFromExpression(Expression expr)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            expr = unary.Operand;

        if (expr is MemberExpression memberExpr)
            return memberExpr.Member.Name;

        return null;
    }

    #endregion
}
