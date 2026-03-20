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

        Activator.CreateInstance(typeof(TProfile), mapper);

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

        Activator.CreateInstance(typeof(TProfile), mapper);

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
                throw new ArgumentException($"{profileType.Name} ne dérive pas de NovelyMapperProfile.");

            if (profileType.IsAbstract)
                throw new ArgumentException($"{profileType.Name} est abstrait et ne peut pas être instancié.");

            Activator.CreateInstance(profileType, mapper);
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
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(NovelyMapperProfile).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t != typeof(NovelyMapperEmptyProfile));

        foreach (var profileType in profileTypes)
        {
            Activator.CreateInstance(profileType, mapper);
        }

        return services;
    }
}
