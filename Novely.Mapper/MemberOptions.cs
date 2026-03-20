using System.Linq.Expressions;

namespace Novely.Mapper;

internal interface IMemberOptions
{
    LambdaExpression? MapFromExpression { get; }
    bool IsIgnored { get; }
    Delegate? Condition { get; }
    object? NullSubstituteValue { get; }
    bool HasNullSubstitute { get; }
    Delegate? MemberConverter { get; }
}

/// <summary>
/// Options de configuration pour le mapping d'une propriété spécifique.
/// </summary>
/// <typeparam name="TSource">Le type source du mapping.</typeparam>
public class MemberOptions<TSource> : IMemberOptions
{
    internal Expression<Func<TSource, object>>? _mapFromExpression;
    internal bool _isIgnored;
    internal Func<TSource, bool>? _condition;
    internal object? _nullSubstituteValue;
    internal bool _hasNullSubstitute;
    internal Func<TSource, object>? _memberConverter;

    LambdaExpression? IMemberOptions.MapFromExpression => _mapFromExpression;
    bool IMemberOptions.IsIgnored => _isIgnored;
    Delegate? IMemberOptions.Condition => _condition;
    object? IMemberOptions.NullSubstituteValue => _nullSubstituteValue;
    bool IMemberOptions.HasNullSubstitute => _hasNullSubstitute;
    Delegate? IMemberOptions.MemberConverter => _memberConverter;

    /// <summary>
    /// Spécifie l'expression source à utiliser pour mapper cette propriété.
    /// </summary>
    public MemberOptions<TSource> MapFrom(Expression<Func<TSource, object>> source)
    {
        _mapFromExpression = source ?? throw new ArgumentNullException(nameof(source));
        return this;
    }

    /// <summary>
    /// Ignore cette propriété lors du mapping (conserve la valeur par défaut).
    /// </summary>
    public MemberOptions<TSource> Ignore()
    {
        _isIgnored = true;
        return this;
    }

    /// <summary>
    /// Applique le mapping uniquement si la condition est vraie.
    /// </summary>
    public MemberOptions<TSource> MapWhen(Func<TSource, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    /// <summary>
    /// Fournit une valeur de substitution si la valeur source est null.
    /// </summary>
    public MemberOptions<TSource> NullSubstitute(object value)
    {
        _nullSubstituteValue = value;
        _hasNullSubstitute = true;
        return this;
    }

    /// <summary>
    /// Utilise un convertisseur personnalisé pour cette propriété.
    /// </summary>
    public MemberOptions<TSource> ConvertUsing(Func<TSource, object> converter)
    {
        _memberConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        return this;
    }
}
