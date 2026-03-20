namespace Novely.Mapper;

/// <summary>
/// Classe de base abstraite pour définir des profils de mappings NovelyMapper.
/// Chaque profil reçoit une instance de mapper via son constructeur (injection par instance, pas statique).
/// </summary>
public abstract class NovelyMapperProfile
{
    /// <summary>
    /// Accès au mapper associé à ce profil.
    /// </summary>
    protected NovelyMapper Mapper { get; }

    /// <summary>
    /// Constructeur de base pour les profils de mapping.
    /// </summary>
    /// <param name="mapper">Instance de <see cref="NovelyMapper"/> à utiliser pour enregistrer les mappings.</param>
    protected NovelyMapperProfile(NovelyMapper mapper)
    {
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Crée un mapping source → target dans le mapper associé.
    /// </summary>
    protected INovelyMapperConfig<TSource, TTarget> CreateMap<TSource, TTarget>()
    {
        return Mapper.CreateMap<TSource, TTarget>();
    }
}
