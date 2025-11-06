using System.Linq.Expressions;

namespace Novely.Mapper;

/// <summary>
/// Interface permettant de configurer un mapping entre un type source <typeparamref name="TSource"/>
/// et un type cible <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">Le type source des objets à mapper.</typeparam>
/// <typeparam name="TTarget">Le type cible vers lequel mapper les objets. Doit posséder un constructeur public sans paramètre.</typeparam>
/// <remarks>
/// Cette interface fournit des méthodes pour personnaliser le mapping, comme :
/// <list type="bullet">
/// <item><description><c>ForMember</c> : mapper une propriété source vers une propriété cible spécifique.</description></item>
/// <item><description><c>ReverseMap</c> : créer automatiquement le mapping inverse.</description></item>
/// </list>
/// Les instances de cette interface sont obtenues via <see cref="INovelyMapper.CreateMap{TSource, TTarget}"/>.
/// </remarks>
public interface INovelyMapperConfig<TSource, TTarget> where TTarget : new()
{
    /// <summary>
    /// Configure le mapping d'une propriété spécifique du type cible vers une expression provenant du type source.
    /// </summary>
    /// <typeparam name="TMember">Le type de la propriété cible à mapper.</typeparam>
    /// <param name="targetSelector">
    /// Expression lambda sélectionnant la propriété du type cible (<typeparamref name="TTarget"/>) à configurer.
    /// Exemple : <c>dest => dest.PropertyName</c>.
    /// </param>
    /// <param name="sourceSelector">
    /// Expression lambda sélectionnant la valeur source (<typeparamref name="TSource"/>) à mapper vers la propriété cible.
    /// Exemple : <c>src => src.OtherProperty</c>.
    /// </param>
    /// <returns>
    /// L'instance courante de <see cref="INovelyMapperConfig{TSource, TTarget}"/> pour permettre un chaînage fluide des appels.
    /// </returns>
    /// <remarks>
    /// Cette méthode permet de surcharger automatiquement le mapping par convention pour une ou plusieurs propriétés spécifiques.
    /// Elle peut être appelée plusieurs fois pour configurer différentes propriétés.
    /// </remarks>
    INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(Expression<Func<TTarget, TMember>> targetSelector, Expression<Func<TSource, object>> sourceSelector);
}

/// <summary>
/// Implémentation concrète de <see cref="INovelyMapperConfig{TSource, TTarget}"/>.
/// Permet de définir les règles de mapping entre un type source <typeparamref name="TSource"/>
/// et un type cible <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">Le type source des objets à mapper.</typeparam>
/// <typeparam name="TTarget">Le type cible vers lequel mapper les objets. Doit posséder un constructeur public sans paramètre.</typeparam>
/// <remarks>
/// Cette classe est utilisée en interne par <see cref="NovelyMapper"/> et exposée via l'interface <see cref="INovelyMapperConfig{TSource, TTarget}"/>.
/// Elle fournit les méthodes <c>ForMember</c>, <c>ReverseMap</c> et autres pour configurer le mapping.
/// </remarks>
public class NovelyMapperConfig<TSource, TTarget> : INovelyMapperConfig<TSource, TTarget> where TTarget : new()
{
    /// <summary>
    /// Dictionnaire stockant les mappings personnalisés pour les propriétés spécifiques du type cible.
    /// </summary>
    /// <remarks>
    /// La clé est le nom de la propriété du type cible (<typeparamref name="TTarget"/>),
    /// et la valeur est une fonction prenant un objet source (<typeparamref name="TSource"/>) et retournant la valeur à affecter.
    /// 
    /// Ces mappings sont ajoutés via la méthode <see cref="INovelyMapperConfig{TSource, TTarget}.ForMember{TMember}"/>.
    /// Elles permettent de surcharger le mapping automatique pour certaines propriétés.
    /// </remarks>
    internal readonly Dictionary<string, Func<TSource, object>> CustomMappings = [];

    public INovelyMapperConfig<TSource, TTarget> ForMember<TMember>(Expression<Func<TTarget, TMember>> targetSelector, Expression<Func<TSource, object>> sourceSelector)
    {
        ArgumentNullException.ThrowIfNull(targetSelector);

        ArgumentNullException.ThrowIfNull(sourceSelector);

        var targetName = targetSelector.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression u when u.Operand is MemberExpression m => m.Member.Name,
            _ => throw new ArgumentException("L’expression cible doit être une propriété.")
        };

        CustomMappings[targetName] = sourceSelector.Compile();
        return this;
    }
}