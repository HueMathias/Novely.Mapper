namespace Novely.Mapper;

/// <summary>
/// Classe de base abstraite pour définir des profils de mappings NovelyMapper.
/// </summary>
/// <remarks>
/// Les classes héritant de <see cref="NovelyMapperProfile"/> permettent de déclarer tous les mappings
/// de l'application via la méthode <see cref="INovelyMapperConfig{TSource, TTarget}"/> et <c>CreateMap&lt;TSource, TTarget&gt;</c>.
/// 
/// Exemple d'utilisation :
/// <code>
/// public class MyMapperProfile : NovelyMapperProfile
/// {
///     public MyMapperProfile()
///     {
///         CreateMap&lt;EntityA, EntityB&gt;()
///             .ForMember(dest =&gt; dest.Nom, src =&gt; src.Name);
///     }
/// }
/// </code>
/// 
/// Cette classe utilise le <see cref="NovelyMapper"/> global pour enregistrer et centraliser tous les mappings.
/// Il est recommandé de l'utiliser avec l'extension DI <c>UseNovelyMapper&lt;TProfile&gt;()</c>.
/// </remarks>
public abstract class NovelyMapperProfile
{
    /// <summary>
    /// Accès au mapper global utilisé par tous les profils.
    /// </summary>
    protected static NovelyMapper Mapper { get; private set; } = new NovelyMapper();

    /// <summary>
    /// Définit le mapper global utilisé par tous les profils.
    /// </summary>
    /// <param name="mapper">Instance de <see cref="NovelyMapper"/> à utiliser comme mapper global.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="mapper"/> est <c>null</c>.</exception>
    internal static void Initialize(NovelyMapper mapper)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Crée un mapping source → target dans le mapper global.
    /// </summary>
    /// <typeparam name="TSource">Le type source du mapping.</typeparam>
    /// <typeparam name="TTarget">Le type cible du mapping. Doit posséder un constructeur public sans paramètre.</typeparam>
    /// <returns>Une instance de <see cref="INovelyMapperConfig{TSource, TTarget}"/> pour configurer le mapping.</returns>
    protected static INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>() where TTarget : new()
    {
        return Mapper.CreateMap<TSource, TTarget>();
    }
}