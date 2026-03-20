namespace Novely.Mapper;

/// <summary>
/// Exception levée lorsque la validation de la configuration du mapper échoue.
/// Contient la liste détaillée de toutes les propriétés non mappées avec des suggestions de correction.
/// </summary>
public class NovelyMapperValidationException : NovelyMapperException
{
    /// <summary>
    /// Liste des erreurs de validation détectées.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public NovelyMapperValidationException(IEnumerable<string> errors)
        : base(FormatValidationMessage(errors))
    {
        Errors = errors.ToList().AsReadOnly();
    }

    private static string FormatValidationMessage(IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        return $"Validation de la configuration échouée ({errorList.Count} erreur(s)) :\n\n"
               + string.Join("\n\n", errorList.Select((e, i) => $"  [{i + 1}] {e}"));
    }
}
