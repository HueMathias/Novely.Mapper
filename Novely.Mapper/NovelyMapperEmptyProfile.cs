namespace Novely.Mapper;

/// <summary>
/// Profil NovelyMapper vide utilisé comme profil par défaut.
/// </summary>
/// <remarks>
/// Cette classe hérite de <see cref="NovelyMapperProfile"/> et ne contient aucun mapping.
/// Elle peut être utilisée lorsque l'on souhaite initialiser le <see cref="NovelyMapper"/> sans définir de mappings spécifiques.
/// Utile notamment pour l'extension DI <c>UseNovelyMapper()</c> sans fournir de profil concret.
/// </remarks>
public class NovelyMapperEmptyProfile : NovelyMapperProfile
{
    public NovelyMapperEmptyProfile() { }
}
