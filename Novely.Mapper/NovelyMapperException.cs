using System.Linq.Expressions;

namespace Novely.Mapper;

/// <summary>
/// Exception levée lors d'une erreur de configuration ou d'exécution du mapper.
/// Fournit un contexte détaillé pour faciliter le débogage.
/// </summary>
public class NovelyMapperException : Exception
{
    /// <summary>Type source du mapping concerné (si applicable).</summary>
    public Type? SourceType { get; }

    /// <summary>Type cible du mapping concerné (si applicable).</summary>
    public Type? TargetType { get; }

    /// <summary>Nom de la propriété concernée (si applicable).</summary>
    public string? PropertyName { get; }

    /// <summary>Index de l'élément dans la collection (si applicable, -1 sinon).</summary>
    public int CollectionIndex { get; } = -1;

    /// <summary>Suggestion pour corriger l'erreur.</summary>
    public string? Suggestion { get; }

    public NovelyMapperException(string message)
        : base(message) { }

    public NovelyMapperException(string message, Exception innerException)
        : base(message, innerException) { }

    internal NovelyMapperException(
        string message,
        Type? sourceType,
        Type? targetType,
        string? propertyName,
        string? suggestion,
        Exception? innerException = null,
        int collectionIndex = -1)
        : base(FormatMessage(message, sourceType, targetType, propertyName, suggestion, collectionIndex), innerException)
    {
        SourceType = sourceType;
        TargetType = targetType;
        PropertyName = propertyName;
        Suggestion = suggestion;
        CollectionIndex = collectionIndex;
    }

    private static string FormatMessage(
        string message, Type? sourceType, Type? targetType,
        string? propertyName, string? suggestion,
        int collectionIndex = -1)
    {
        var parts = new List<string> { message };

        if (sourceType != null && targetType != null)
            parts.Add($"  Mapping : {FormatTypeName(sourceType)} → {FormatTypeName(targetType)}");

        if (collectionIndex >= 0)
            parts.Add($"  Élément : index {collectionIndex} dans la collection");

        if (propertyName != null)
            parts.Add($"  Propriété : {propertyName}");

        if (suggestion != null)
            parts.Add($"  → Suggestion : {suggestion}");

        return string.Join("\n", parts);
    }

    internal static string FormatTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
        return $"{name}<{args}>";
    }

    // --- Factory methods ---

    internal static NovelyMapperException MissingMapping(Type sourceType, Type targetType)
        => new(
            "Aucune configuration de mapping trouvée.",
            sourceType, targetType, null,
            $"Appelez mapper.CreateMap<{FormatTypeName(sourceType)}, {FormatTypeName(targetType)}>() " +
            $"dans votre profil ou avant l'appel à Map.");

    internal static NovelyMapperException MappingCompilationFailed(
        Type sourceType, Type targetType, string? propertyName, Exception inner)
        => new(
            "Erreur lors de la compilation du mapping.",
            sourceType, targetType, propertyName,
            propertyName != null
                ? $"Vérifiez la configuration ForMember pour '{propertyName}'. " +
                  $"Le type de l'expression MapFrom doit être compatible avec le type de la propriété cible."
                : "Vérifiez que les types source et cible sont compatibles.",
            inner);

    internal static NovelyMapperException ConstructorResolutionFailed(
        Type sourceType, Type targetType, IReadOnlyList<string> unmatchedParams)
        => new(
            $"Aucun constructeur approprié trouvé pour '{FormatTypeName(targetType)}'.\n" +
            $"  Paramètres non résolus : {string.Join(", ", unmatchedParams)}",
            sourceType, targetType, null,
            "Options :\n" +
            "    1. Ajoutez un constructeur sans paramètre au type cible.\n" +
            "    2. Ajoutez les propriétés manquantes au type source (même nom, case-insensitive).\n" +
            "    3. Configurez les paramètres avec ForMember(d => d.Param, opt => opt.MapFrom(...)).\n" +
            "    4. Ignorez les paramètres avec ForMember(d => d.Param, opt => opt.Ignore()).");

    internal static NovelyMapperException BeforeMapRequiresParameterlessCtor(Type targetType)
        => new(
            $"Impossible de créer une instance de '{FormatTypeName(targetType)}' pour BeforeMap.",
            null, targetType, null,
            $"BeforeMap nécessite un constructeur sans paramètre sur le type cible. " +
            $"Retirez BeforeMap ou ajoutez un constructeur sans paramètre à '{FormatTypeName(targetType)}'.");

    internal static NovelyMapperException RuntimeMappingFailed(
        Type sourceType, Type targetType, string context, Exception inner)
        => new(
            $"Erreur lors de l'exécution du mapping ({context}).",
            sourceType, targetType, null,
            $"Vérifiez que votre delegate {context} ne lève pas d'exception " +
            $"et que les types sont compatibles.",
            inner);

    internal static NovelyMapperException CollectionItemMappingFailed(
        Type sourceType, Type targetType, int index, Exception inner)
    {
        var propertyName = (inner as NovelyMapperException)?.PropertyName;
        var innerSuggestion = (inner as NovelyMapperException)?.Suggestion;

        return new(
            $"Erreur lors du mapping de l'élément à l'index {index} dans la collection.",
            sourceType, targetType, propertyName,
            innerSuggestion ?? "Vérifiez les données de l'élément source à cet index.",
            inner,
            collectionIndex: index);
    }

    internal static NovelyMapperException PropertyMappingFailed(
        Type sourceType, Type targetType, string propertyName, Exception inner)
        => new(
            "Erreur lors du mapping d'une propriété.",
            sourceType, targetType, propertyName,
            $"Vérifiez la configuration de la propriété '{propertyName}'. " +
            $"L'expression MapFrom, ConvertUsing, ou MapWhen a levé une exception.",
            inner);

    internal static NovelyMapperException InvalidTargetSelector(Type targetType, Expression? expression)
        => new(
            $"L'expression de sélection de propriété est invalide.",
            null, targetType, null,
            $"ForMember attend une expression de type 'dest => dest.PropertyName'. " +
            $"Expression reçue : {expression?.GetType().Name ?? "null"}.");

    internal static NovelyMapperException NullSubstituteTypeMismatch(
        Type targetType, string propertyName, Type propertyType, Type? valueType)
        => new(
            $"Le type de la valeur NullSubstitute ne correspond pas au type de la propriété.",
            null, targetType, propertyName,
            $"La propriété '{propertyName}' est de type '{FormatTypeName(propertyType)}' " +
            $"mais la valeur NullSubstitute est de type '{(valueType != null ? FormatTypeName(valueType) : "null")}'. " +
            $"Fournissez une valeur du bon type.");

    internal static NovelyMapperException ProfileInstantiationFailed(Type profileType, Exception inner)
        => new(
            $"Impossible d'instancier le profil '{FormatTypeName(profileType)}'.",
            null, null, null,
            $"Le profil doit avoir un constructeur public avec un paramètre NovelyMapper :\n" +
            $"    public {profileType.Name}(NovelyMapper mapper) : base(mapper) {{ }}",
            inner);

    internal static NovelyMapperException TypeMismatch(
        Type sourceType, Type targetType, string propertyName,
        Type sourcePropType, Type targetPropType)
        => new(
            $"Types incompatibles pour la propriété '{propertyName}'.",
            sourceType, targetType, propertyName,
            $"Source '{propertyName}' est de type '{FormatTypeName(sourcePropType)}', " +
            $"cible '{propertyName}' est de type '{FormatTypeName(targetPropType)}'. " +
            $"Configurez un ForMember avec MapFrom ou ConvertUsing, " +
            $"ou enregistrez un mapping CreateMap<{FormatTypeName(sourcePropType)}, {FormatTypeName(targetPropType)}>().");
}
