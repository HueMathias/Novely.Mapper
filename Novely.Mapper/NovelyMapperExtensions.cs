using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre NovelyMapper avec un profil unique.
    /// </summary>
    public static IServiceCollection UseNovelyMapper<TProfile>(this IServiceCollection services)
        where TProfile : NovelyMapperProfile
    {
        var mapper = new NovelyMapper();
        services.AddSingleton<INovelyMapper>(mapper);
        services.AddSingleton(mapper);

        InstantiateProfile(typeof(TProfile), mapper);

        return services;
    }

    /// <summary>
    /// Enregistre NovelyMapper avec un profil unique et des options personnalisées.
    /// </summary>
    public static IServiceCollection UseNovelyMapper<TProfile>(
        this IServiceCollection services,
        Action<NovelyMapperOptions> configureOptions)
        where TProfile : NovelyMapperProfile
    {
        var mapper = new NovelyMapper();
        configureOptions(mapper.Options);

        services.AddSingleton<INovelyMapper>(mapper);
        services.AddSingleton(mapper);

        InstantiateProfile(typeof(TProfile), mapper);

        return services;
    }

    /// <summary>
    /// Enregistre NovelyMapper avec un profil vide par défaut.
    /// </summary>
    public static IServiceCollection UseNovelyMapper(this IServiceCollection services)
    {
        return services.UseNovelyMapper<NovelyMapperEmptyProfile>();
    }

    /// <summary>
    /// Enregistre NovelyMapper avec plusieurs types de profils.
    /// </summary>
    public static IServiceCollection UseNovelyMapper(this IServiceCollection services, params Type[] profileTypes)
    {
        var mapper = new NovelyMapper();
        services.AddSingleton<INovelyMapper>(mapper);
        services.AddSingleton(mapper);

        foreach (var profileType in profileTypes)
        {
            if (!typeof(NovelyMapperProfile).IsAssignableFrom(profileType))
                throw new NovelyMapperException(
                    $"Le type '{profileType.Name}' ne dérive pas de NovelyMapperProfile.",
                    null, null, null,
                    $"Le type passé à UseNovelyMapper doit hériter de NovelyMapperProfile. " +
                    $"Vérifiez que '{profileType.Name}' hérite bien de NovelyMapperProfile.");

            if (profileType.IsAbstract)
                throw new NovelyMapperException(
                    $"Le type '{profileType.Name}' est abstrait et ne peut pas être instancié.",
                    null, null, null,
                    $"Passez un type concret (non-abstrait) à UseNovelyMapper.");

            InstantiateProfile(profileType, mapper);
        }

        return services;
    }

    /// <summary>
    /// Enregistre NovelyMapper en scannant les assemblies pour trouver tous les profils.
    /// </summary>
    public static IServiceCollection UseNovelyMapper(this IServiceCollection services, params Assembly[] assemblies)
    {
        var mapper = new NovelyMapper();
        services.AddSingleton<INovelyMapper>(mapper);
        services.AddSingleton(mapper);

        var profileTypes = assemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => typeof(NovelyMapperProfile).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t != typeof(NovelyMapperEmptyProfile));

        foreach (var profileType in profileTypes)
        {
            InstantiateProfile(profileType, mapper);
        }

        return services;
    }

    private static void InstantiateProfile(Type profileType, NovelyMapper mapper)
    {
        try
        {
            Activator.CreateInstance(profileType, mapper);
        }
        catch (MissingMethodException ex)
        {
            throw NovelyMapperException.ProfileInstantiationFailed(profileType, ex);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Le constructeur du profil a levé une exception → propager avec contexte
            throw new NovelyMapperException(
                $"Le constructeur du profil '{profileType.Name}' a levé une exception.",
                null, null, null,
                $"Vérifiez le code dans le constructeur de '{profileType.Name}'. " +
                $"L'erreur d'origine est : {ex.InnerException.Message}",
                ex.InnerException);
        }
    }
}
