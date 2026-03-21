# NovelyMapper

Un mapper d'objets **leger et fluent** pour .NET 8.0, alternative open-source a AutoMapper.

- Compilation d'Expression Trees pour des performances optimales
- API fluent : `ForMember`, `Ignore`, `MapFrom`, `MapWhen`, `NullSubstitute`, `ConvertUsing`, `ReverseMap`
- Support des `record` types (constructeurs parametres)
- Mapping d'objets imbriques et de collections
- Conversion automatique `T?` ↔ `T` et resolution des mappings imbriques dans `MapFrom`
- Gestion des references circulaires (pas de `StackOverflowException`)
- Inference de collection dans `Map<TTarget>(object)` (ex: `Map<IEnumerable<Dto>>(list)`)
- Projection `IQueryable` (`ProjectTo`) pour Entity Framework
- `BeforeMap` / `AfterMap`, `Map` vers objet existant
- Validation de configuration (`AssertConfigurationIsValid`)
- Injection de dependances avec profils multiples et scan d'assemblies

## Quick Start

```csharp
// 1. Creer un profil
public class AppProfile : NovelyMapperProfile
{
    public AppProfile(NovelyMapper mapper) : base(mapper)
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
            .ForMember(d => d.InternalId, opt => opt.Ignore());
    }
}

// 2. Enregistrer dans le DI
builder.Services.UseNovelyMapper<AppProfile>();

// 3. Utiliser
var dto = mapper.Map<User, UserDto>(user);
```

Pour la documentation complete : [GitHub](https://github.com/HueMathias/Novely.Mapper)
