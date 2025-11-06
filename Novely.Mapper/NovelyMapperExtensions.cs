using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre <see cref="NovelyMapper"/> dans le conteneur de services et initialise un profil de mappings.
    /// </summary>
    /// <typeparam name="TProfile">
    /// Type du profil héritant de <see cref="NovelyMapperProfile"/> à instancier automatiquement.
    /// Ce profil sera utilisé pour enregistrer tous les mappings au démarrage de l'application.
    /// </typeparam>
    /// <param name="services">Instance de <see cref="IServiceCollection"/> dans laquelle le mapper sera enregistré.</param>
    /// <returns>La même instance de <see cref="IServiceCollection"/> pour permettre un chaînage fluide.</returns>
    /// <remarks>
    /// Cette méthode effectue les opérations suivantes :
    /// <list type="bullet">
    /// <item><description>Crée un singleton <see cref="NovelyMapper"/> et l'enregistre dans le DI.</description></item>
    /// <item><description>Initialise le mapper global via <see cref="NovelyMapperProfile.Initialize(NovelyMapper)"/>.</description></item>
    /// <item><description>Instancie le profil <typeparamref name="TProfile"/> pour enregistrer automatiquement les mappings.</description></item>
    /// </list>
    /// Cette extension permet de configurer le mapper dans <c>Program.cs</c> facilement et d'utiliser le mapping via DI dans l'application.
    /// </remarks>
    public static IServiceCollection UseNovelyMapper<TProfile>(this IServiceCollection services) where TProfile : NovelyMapperProfile, new()
    {
        var mapper = new NovelyMapper();

        // Initialisation du mapper global pour tous les profils
        NovelyMapperProfile.Initialize(mapper);

        // Enregistrement du mapper dans le DI
        services.AddSingleton<INovelyMapper>(mapper);
        services.AddSingleton(mapper);

        // Instanciation du profil pour enregistrer les mappings
        if (typeof(TProfile) != typeof(NovelyMapperProfile))
        {
            // Appel du constructeur du profil
            _ = new TProfile();
        }

        return services;
    }

    /// <summary>
    /// Enregistre <see cref="NovelyMapper"/> dans le conteneur de services avec un profil vide par défaut.
    /// </summary>
    /// <param name="services">Instance de <see cref="IServiceCollection"/> dans laquelle le mapper sera enregistré.</param>
    /// <returns>La même instance de <see cref="IServiceCollection"/> pour permettre un chaînage fluide.</returns>
    /// <remarks>
    /// Cette méthode utilise <see cref="NovelyMapperEmptyProfile"/> comme profil par défaut.
    /// Elle est équivalente à <c>services.UseNovelyMapper&lt;NovelyMapperEmptyProfile&gt;()</c>.
    /// Utile lorsque l'on souhaite simplement enregistrer le mapper global sans définir de mappings spécifiques.
    /// </remarks>
    public static IServiceCollection UseNovelyMapper(this IServiceCollection services)
    {
        return services.UseNovelyMapper<NovelyMapperEmptyProfile>();
    }
}
