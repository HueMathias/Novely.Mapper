namespace Novely.Mapper;

/// <summary>
/// Exception levée lorsque la validation de la configuration du mapper échoue.
/// </summary>
public class NovelyMapperValidationException : Exception
{
    /// <summary>
    /// Liste des erreurs de validation détectées.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public NovelyMapperValidationException(IEnumerable<string> errors)
        : base($"Validation de la configuration échouée :\n{string.Join("\n", errors)}")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
