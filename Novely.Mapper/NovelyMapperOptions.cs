namespace Novely.Mapper;

/// <summary>
/// Comportement lorsqu'une propriété cible n'a pas de correspondance source.
/// </summary>
public enum MissingPropertyBehavior
{
    /// <summary>La propriété est ignorée silencieusement (valeur par défaut).</summary>
    Silent,

    /// <summary>Une exception est levée lors de la compilation du mapping.</summary>
    Throw
}

/// <summary>
/// Options globales de configuration du mapper.
/// </summary>
public class NovelyMapperOptions
{
    /// <summary>
    /// Comportement lors de propriétés cibles non mappées. Par défaut : <see cref="MissingPropertyBehavior.Silent"/>.
    /// </summary>
    public MissingPropertyBehavior MissingPropertyBehavior { get; set; } = MissingPropertyBehavior.Silent;
}
