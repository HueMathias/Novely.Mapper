using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Novely.Mapper;

/// <summary>
/// Interface définissant les fonctionnalités principales d'un mapper NovelyMapper.
/// Fournit des méthodes pour créer des mappings entre types, mapper des objets uniques ou des collections,
/// et accéder aux configurations de mapping.
/// </summary>
public interface INovelyMapper
{
    /// <summary>
    /// Crée une configuration de mapping entre le type source <typeparamref name="TSource"/> et le type cible <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">Le type source à mapper.</typeparam>
    /// <typeparam name="TTarget">Le type cible vers lequel mapper. Doit avoir un constructeur public sans paramètre.</typeparam>
    /// <returns>
    /// Une instance d'<see cref="INovelyMapperConfig{TSource, TTarget}"/> permettant de configurer les règles de mapping,
    /// comme <c>ForMember</c> ou <c>ReverseMap</c>.
    /// </returns>
    /// <remarks>
    /// Cette méthode enregistre le mapping dans le <see cref="NovelyMapper"/> global utilisé par <see cref="NovelyMapperProfile"/>.
    /// Elle doit être utilisée dans un profil hérité de <see cref="NovelyMapperProfile"/> ou directement sur le mapper global.
    /// </remarks>
    INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>() where TTarget : new();

    /// <summary>
    /// Mappe un objet du type <typeparamref name="TSource"/> vers un objet du type <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">Le type source de l'objet à mapper.</typeparam>
    /// <typeparam name="TTarget">Le type cible vers lequel mapper l'objet. Doit avoir un constructeur public sans paramètre.</typeparam>
    /// <param name="source">L'objet source à mapper.</param>
    /// <returns>Une nouvelle instance de <typeparamref name="TTarget"/> contenant les valeurs mappées depuis <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="source"/> est <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Si aucune configuration de mapping n'existe pour le couple <typeparamref name="TSource"/> → <typeparamref name="TTarget"/>.
    /// </exception>
    /// <remarks>
    /// Cette méthode utilise les mappings préalablement définis via <see cref="INovelyMapperConfig{TSource, TTarget}"/> ou les profils hérités de <see cref="NovelyMapperProfile"/>.
    /// </remarks>
    TTarget Map<TSource, TTarget>(TSource source) where TTarget : new();

    /// <summary>
    /// Mappe une collection d'objets du type <typeparamref name="TSource"/> vers des objets du type <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">Le type source des objets à mapper.</typeparam>
    /// <typeparam name="TTarget">Le type cible vers lequel mapper les objets. Doit avoir un constructeur public sans paramètre.</typeparam>
    /// <param name="sources">La collection d'objets source à mapper.</param>
    /// <returns>
    /// Une séquence d'objets de type <typeparamref name="TTarget"/> contenant les valeurs mappées depuis <paramref name="sources"/>.
    /// La séquence est générée paresseusement avec <c>yield return</c>, ce qui évite la création d'une liste complète inutilement.
    /// </returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="sources"/> est <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Si aucune configuration de mapping n'existe pour le couple <typeparamref name="TSource"/> → <typeparamref name="TTarget"/>.
    /// </exception>
    /// <remarks>
    /// Cette méthode utilise les mappings préalablement définis via <see cref="INovelyMapperConfig{TSource, TTarget}"/> ou les profils hérités de <see cref="NovelyMapperProfile"/>.
    /// </remarks>
    IEnumerable<TTarget> Map<TSource, TTarget>(IEnumerable<TSource> sources) where TTarget : new();
}

/// <summary>
/// Implémentation principale du mapper NovelyMapper.
/// Fournit des méthodes pour créer des mappings entre types, mapper des objets ou des collections,
/// et gérer les configurations de mapping globales.
/// </summary>
/// <remarks>
/// Cette classe implémente l'interface <see cref="INovelyMapper"/> et sert de point central pour tous les profils
/// héritant de <see cref="NovelyMapperProfile"/>.
/// Les mappings doivent être créés via <see cref="INovelyMapper.CreateMap{TSource, TTarget}"/> avant d'être utilisés.
/// </remarks>
public class NovelyMapper : INovelyMapper
{
    /// <summary>
    /// Dictionnaire thread-safe contenant les mappings compilés.
    /// La clé est un tuple (<see cref="TSource"/>, <see cref="TTarget"/>), et la valeur est un <see cref="Delegate"/>
    /// représentant la fonction de mapping compilée.
    /// </summary>
    private readonly ConcurrentDictionary<(Type, Type), Delegate> compiledMappings = new();

    /// <summary>
    /// Dictionnaire thread-safe contenant les configurations de mapping en attente de compilation.
    /// La clé est un tuple (<see cref="TSource"/>, <see cref="TTarget"/>), et la valeur est un objet de type <see cref="NovelyMapperConfig{TSource, TTarget}"/>.
    /// </summary>
    private readonly ConcurrentDictionary<(Type, Type), object> pendingConfigs = new();

    public INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>() where TTarget : new()
    {
        var config = new NovelyMapperConfig<TSource, TTarget>();
        pendingConfigs[(typeof(TSource), typeof(TTarget))] = config;
        return config;
    }

    public TTarget Map<TSource, TTarget>(TSource source) where TTarget : new()
    {
        ArgumentNullException.ThrowIfNull(source);

        var func = GetOrCompileMapping<TSource, TTarget>();
        return func(source);
    }

    public IEnumerable<TTarget> Map<TSource, TTarget>(IEnumerable<TSource> sources) where TTarget : new()
    {
        ArgumentNullException.ThrowIfNull(sources);

        var func = GetOrCompileMapping<TSource, TTarget>();

        foreach (var item in sources)
            yield return func(item);
    }

    /// <summary>
    /// Récupère le mapping compilé entre TSource et TTarget, ou le compile si nécessaire.
    /// </summary>
    private Func<TSource, TTarget> GetOrCompileMapping<TSource, TTarget>() where TTarget : new()
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!compiledMappings.TryGetValue(key, out var del))
        {
            if (!pendingConfigs.TryGetValue(key, out var pending))
                throw new InvalidOperationException($"Aucune configuration trouvée pour {typeof(TSource).Name} → {typeof(TTarget).Name}");

            CompileMapping((NovelyMapperConfig<TSource, TTarget>)pending);
            del = compiledMappings[key];
        }

        return (Func<TSource, TTarget>)del;
    }


    private void CompileMapping<TSource, TTarget>(NovelyMapperConfig<TSource, TTarget> config)
            where TTarget : new()
    {
        var key = (typeof(TSource), typeof(TTarget));

        var param = Expression.Parameter(typeof(TSource), "src");

        var bindings = typeof(TTarget)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p =>
            {
                if (config.CustomMappings.TryGetValue(p.Name, out var customGetter))
                {
                    var invoke = Expression.Invoke(Expression.Constant(customGetter), param);
                    var converted = Expression.Convert(invoke, p.PropertyType);
                    return Expression.Bind(p, converted);
                }

                var sourceProp = typeof(TSource).GetProperty(p.Name);
                if (sourceProp == null) return null;

                return Expression.Bind(p, Expression.Property(param, sourceProp));
            })
            .Where(b => b != null)
            .ToArray();

        var body = Expression.MemberInit(Expression.New(typeof(TTarget)), bindings);
        var lambda = Expression.Lambda<Func<TSource, TTarget>>(body, param);

        compiledMappings[key] = lambda.Compile();
    }
}