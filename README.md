# NovelyMapper

NovelyMapper est un mapper d'objets **leger et fluent** pour .NET 8.0, concu pour faciliter la transformation d'objets entre differents types.
Il s'inspire d'AutoMapper mais reste **100% gratuit et open-source**.

- Compilation d'Expression Trees pour des performances optimales
- Cache thread-safe via `ConcurrentDictionary`
- API fluent avec profils, `ForMember`, `Ignore`, `ReverseMap`, `ConvertUsing`, etc.
- Support des `record` types (constructeurs parametres)
- Mapping d'objets imbriques et de collections
- Projection `IQueryable` (`ProjectTo`) pour Entity Framework
- Injection de dependances avec profils multiples

---

## Sommaire

- [Installation](#installation)
- [Utilisation de base](#utilisation-de-base)
  - [Mapping simple](#mapping-simple)
  - [Mapping de collections](#mapping-de-collections)
  - [ForMember avec MemberOptions](#formember-avec-memberoptions)
- [Profils](#profils)
  - [Creation d'un profil](#creation-dun-profil)
  - [Profils multiples](#profils-multiples)
  - [Scan d'assemblies](#scan-dassemblies)
- [Injection de dependances](#injection-de-dependances)
- [Fonctionnalites avancees](#fonctionnalites-avancees)
  - [Ignore](#ignore)
  - [MapFrom](#mapfrom)
  - [Condition (MapWhen)](#condition-mapwhen)
  - [NullSubstitute](#nullsubstitute)
  - [ConvertUsing (mapping complet)](#convertusing-mapping-complet)
  - [ConvertUsing (par membre)](#convertusing-par-membre)
  - [ReverseMap](#reversemap)
  - [BeforeMap / AfterMap](#beforemap--aftermap)
  - [Map vers un objet existant](#map-vers-un-objet-existant)
  - [Mapping de records](#mapping-de-records)
  - [Mapping d'objets imbriques](#mapping-dobjets-imbriques)
  - [Mapping de collections imbriquees](#mapping-de-collections-imbriquees)
  - [ProjectTo (IQueryable)](#projectto-iqueryable)
  - [Validation de la configuration](#validation-de-la-configuration)
  - [Options globales](#options-globales)
- [API Reference](#api-reference)

---

## Installation

Via la CLI .NET :

```bash
dotnet add package NovelyMapper
```

Via le Package Manager Console (Visual Studio) :

```powershell
Install-Package NovelyMapper
```

**Prerequis** : .NET 8.0 ou superieur.

---

## Utilisation de base

### Mapping simple

```csharp
using Novely.Mapper;

// Creer un mapper et enregistrer un mapping
var mapper = new NovelyMapper();
mapper.CreateMap<Source, Target>();

// Mapper un objet
var source = new Source { Id = 1, Name = "Alice" };
var target = mapper.Map<Source, Target>(source);
// target.Id == 1, target.Name == "Alice"
```

Les proprietes sont matchees **automatiquement par nom**. Si les noms correspondent, aucune configuration supplementaire n'est necessaire.

### Mapping de collections

```csharp
var sources = new List<Source>
{
    new() { Id = 1, Name = "Alice" },
    new() { Id = 2, Name = "Bob" }
};

IEnumerable<Target> targets = mapper.Map<Source, Target>(sources);
```

Le mapping de collections est **lazy** (`IEnumerable<T>`) : les objets sont mappes un par un lors de l'iteration.

### ForMember avec MemberOptions

Lorsque les noms de proprietes different, utilisez `ForMember` pour configurer le mapping :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(dest => dest.Nom, opt => opt.MapFrom(src => src.Name));
```

L'API `MemberOptions` offre un ensemble complet d'options chainables :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(dest => dest.X, opt => opt.Ignore())
    .ForMember(dest => dest.Y, opt => opt.MapFrom(src => src.Z))
    .ForMember(dest => dest.Status, opt => opt
        .MapFrom(src => src.IsActive ? "Actif" : "Inactif")
        .MapWhen(src => src.IsEnabled))
    .ForMember(dest => dest.Label, opt => opt
        .MapFrom(src => src.Label)
        .NullSubstitute("N/A"));
```

---

## Profils

### Creation d'un profil

Un profil regroupe les configurations de mapping. Chaque profil recoit l'instance du mapper via son constructeur :

```csharp
public class AppMapperProfile : NovelyMapperProfile
{
    public AppMapperProfile(NovelyMapper mapper) : base(mapper)
    {
        CreateMap<User, UserDto>();

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.ClientName, opt => opt.MapFrom(s => s.Client.Name));

        CreateMap<Product, ProductDto>()
            .ForMember(d => d.InternalCode, opt => opt.Ignore());
    }
}
```

### Profils multiples

Vous pouvez separer les mappings en plusieurs profils pour une meilleure organisation :

```csharp
public class UserProfile : NovelyMapperProfile
{
    public UserProfile(NovelyMapper mapper) : base(mapper)
    {
        CreateMap<User, UserDto>();
        CreateMap<User, UserSummaryDto>();
    }
}

public class OrderProfile : NovelyMapperProfile
{
    public OrderProfile(NovelyMapper mapper) : base(mapper)
    {
        CreateMap<Order, OrderDto>();
    }
}
```

Enregistrement avec plusieurs profils :

```csharp
// Par types explicites
builder.Services.UseNovelyMapper(typeof(UserProfile), typeof(OrderProfile));
```

### Scan d'assemblies

Detecte automatiquement tous les profils d'un ou plusieurs assemblies :

```csharp
builder.Services.UseNovelyMapper(typeof(Program).Assembly);
```

Tous les types non-abstraits heritant de `NovelyMapperProfile` sont decouverts et instancies automatiquement.

---

## Injection de dependances

Enregistrez le mapper en singleton dans votre conteneur DI :

```csharp
// Avec un profil unique
builder.Services.UseNovelyMapper<AppMapperProfile>();

// Sans profil (mapper vide, pour ajouter des mappings manuellement)
builder.Services.UseNovelyMapper();

// Avec plusieurs profils
builder.Services.UseNovelyMapper(typeof(UserProfile), typeof(OrderProfile));

// Avec scan d'assemblies
builder.Services.UseNovelyMapper(typeof(Program).Assembly);
```

Utilisez ensuite `IMapper` via injection :

```csharp
public class UserService
{
    private readonly IMapper _mapper;

    public UserService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public UserDto GetUser(int id)
    {
        var user = _repository.GetById(id);
        return _mapper.Map<User, UserDto>(user);
    }
}
```

> **Note** : `INovelyMapper` est toujours disponible pour retrocompatibilite (il herite de `IMapper`). Les deux interfaces sont enregistrees dans le conteneur DI.

> **Isolation** : chaque appel a `UseNovelyMapper` cree une instance independante du mapper. Deux `ServiceProvider` avec des profils differents ne s'interferent pas.

---

## Fonctionnalites avancees

### Ignore

Exclut une propriete du mapping. La propriete conserve sa valeur par defaut (ou sa valeur existante lors d'un `Map` vers objet existant).

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Secret, opt => opt.Ignore())
    .ForMember(d => d.InternalId, opt => opt.Ignore());
```

### MapFrom

Specifie une expression source personnalisee pour une propriete cible :

```csharp
mapper.CreateMap<User, UserDto>()
    .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.FirstName} {s.LastName}"))
    .ForMember(d => d.Age, opt => opt.MapFrom(s => DateTime.Now.Year - s.BirthYear));
```

### Condition (MapWhen)

Applique le mapping uniquement si une condition est remplie. Sinon, la propriete conserve sa valeur par defaut (`default(T)`) ou sa valeur existante (lors d'un `Map` vers objet existant).

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Status, opt => opt
        .MapFrom(s => s.Status)
        .MapWhen(s => s.IsEnabled));
```

Comportement :
- **Map vers nouvel objet** : si la condition est fausse, la propriete vaut `default(T)`
- **Map vers objet existant** : si la condition est fausse, la propriete conserve sa valeur d'origine

### NullSubstitute

Fournit une valeur de remplacement lorsque la valeur source est `null` :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Label, opt => opt.NullSubstitute("N/A"))
    .ForMember(d => d.Description, opt => opt
        .MapFrom(s => s.Description)
        .NullSubstitute("Aucune description"));
```

### ConvertUsing (mapping complet)

Remplace entierement la logique de mapping par un convertisseur personnalise. Aucun Expression Tree n'est genere :

```csharp
mapper.CreateMap<Source, Target>()
    .ConvertUsing(src => new Target
    {
        Id = src.Id,
        FullName = $"{src.FirstName} {src.LastName}",
        Score = CalculateScore(src)
    });
```

### ConvertUsing (par membre)

Convertisseur personnalise pour une propriete specifique :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Length, opt => opt.ConvertUsing(s => s.Value?.Length ?? 0));
```

### ReverseMap

Cree automatiquement le mapping inverse. Les `MapFrom` simples (expressions `MemberExpression`) sont inverses. Les mappings par convention sont naturellement bidirectionnels.

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Nom, opt => opt.MapFrom(s => s.Name))
    .ReverseMap();

// Forward : Source → Target
var target = mapper.Map<Source, Target>(source);

// Reverse : Target → Source (automatiquement configure)
var source = mapper.Map<Target, Source>(target);
```

`ReverseMap()` retourne le config du mapping inverse, permettant de le configurer davantage :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Nom, opt => opt.MapFrom(s => s.Name))
    .ReverseMap()
    .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Nom)); // config supplementaire du reverse
```

### BeforeMap / AfterMap

Executez des actions avant ou apres le mapping :

```csharp
mapper.CreateMap<Source, Target>()
    .BeforeMap((src, dest) =>
    {
        // dest est vide a ce stade (constructeur par defaut)
        Log.Info($"Mapping de {src.Id}");
    })
    .AfterMap((src, dest) =>
    {
        // dest est rempli avec les valeurs mappees
        dest.FullLabel = $"{dest.Id}-{dest.Name}";
    });
```

> **Note** : `BeforeMap` necessite un type cible avec constructeur sans parametre (les `record` ne sont pas supportes avec `BeforeMap`).

### Map vers un objet existant

Met a jour les proprietes d'un objet existant au lieu d'en creer un nouveau :

```csharp
mapper.CreateMap<Source, Target>();

var existing = new Target { Id = 0, Name = "ancien", Extra = "a conserver" };
mapper.Map(source, existing);
// existing.Id et existing.Name sont mis a jour
// existing.Extra est conserve (pas de propriete correspondante dans Source)
```

Les proprietes ignorees (`Ignore()`) conservent leur valeur d'origine. Les conditions (`MapWhen`) preservent la valeur existante si la condition est fausse.

### Mapping de records

NovelyMapper detecte automatiquement les types sans constructeur sans parametre et resout le meilleur constructeur :

```csharp
public record PersonDto(int Id, string Name);

mapper.CreateMap<Person, PersonDto>();

var dto = mapper.Map<Person, PersonDto>(person);
// Le constructeur PersonDto(int, string) est utilise automatiquement
```

Les parametres du constructeur sont resolus par **correspondance de nom** (case-insensitive) avec les proprietes source. Les parametres avec valeur par defaut ou ignores utilisent `default(T)`.

```csharp
public record PersonWithEmail(int Id, string Name, string? Email = null);

mapper.CreateMap<Person, PersonWithEmail>()
    .ForMember(d => d.Email, opt => opt.Ignore());
// Email recoit null (parametre ignore dans le constructeur)
```

### Mapping d'objets imbriques

Si un mapping est enregistre pour les types imbriques, NovelyMapper les mappe automatiquement par convention de nom :

```csharp
// Modeles
public class Parent { public Child Child { get; set; } }
public class ParentDto { public ChildDto Child { get; set; } }

// Enregistrer les deux mappings
mapper.CreateMap<Child, ChildDto>();
mapper.CreateMap<Parent, ParentDto>();

var dto = mapper.Map<Parent, ParentDto>(parent);
// dto.Child est automatiquement mappe via le mapping Child → ChildDto
```

Les objets imbriques `null` restent `null` dans le resultat.

### Mapping de collections imbriquees

Les collections de types complexes sont automatiquement mappees si le mapping element est enregistre :

```csharp
public class Parent { public List<Child> Children { get; set; } }
public class ParentDto { public List<ChildDto> Children { get; set; } }

mapper.CreateMap<Child, ChildDto>();
mapper.CreateMap<Parent, ParentDto>();

// parent.Children (List<Child>) → dto.Children (List<ChildDto>)
```

Types de collections supportes : `List<T>`, `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `T[]`.

### ProjectTo (IQueryable)

Projette un `IQueryable` vers un type cible en generant une `Expression<Func<S,T>>` traduisible en SQL par Entity Framework :

```csharp
using Novely.Mapper;

// Avec IQueryable<TSource> (generique)
IQueryable<UserDto> dtos = dbContext.Users
    .Where(u => u.IsActive)
    .ProjectTo<User, UserDto>(mapper);

// Avec IQueryable (non-generique)
IQueryable<UserDto> dtos = ((IQueryable)dbContext.Users)
    .ProjectTo<UserDto>(mapper);
```

Vous pouvez aussi recuperer l'expression brute :

```csharp
Expression<Func<User, UserDto>> expr = mapper.GetProjectionExpression<User, UserDto>();
```

> **Limitations ProjectTo** : seules les expressions pures sont supportees. `BeforeMap`/`AfterMap`, `ConvertUsing` (delegate), et `MapWhen` (delegate) ne sont **pas** traduits en SQL. Utilisez `MapFrom` avec des expressions pour les mappings personnalises dans `ProjectTo`.

### Validation de la configuration

Verifiez que toutes les proprietes cibles ont une source :

```csharp
mapper.CreateMap<Source, Target>()
    .ForMember(d => d.Unmapped, opt => opt.Ignore());

// Leve NovelyMapperValidationException si des proprietes ne sont pas mappees
mapper.AssertConfigurationIsValid();
```

Une propriete est consideree comme valide si elle est :
- Matchee par convention (meme nom dans le type source)
- Configuree via `MapFrom` ou `ConvertUsing`
- Ignoree via `Ignore()`
- Couverte par un parametre du constructeur (records)
- Couverte par un mapping imbrique enregistre

```csharp
try
{
    mapper.AssertConfigurationIsValid();
}
catch (NovelyMapperValidationException ex)
{
    foreach (var error in ex.Errors)
        Console.WriteLine(error);
}
```

### Options globales

Configurez le comportement global du mapper :

```csharp
builder.Services.UseNovelyMapper<AppProfile>(options =>
{
    options.MissingPropertyBehavior = MissingPropertyBehavior.Throw;
});
```

| Option | Valeurs | Description |
|--------|---------|-------------|
| `MissingPropertyBehavior` | `Silent` (defaut), `Throw` | Comportement quand une propriete cible n'a pas de source |

---

## API Reference

### IMapper

Interface principale pour l'injection de dependances.

| Methode | Description |
|---------|-------------|
| `CreateMap<TSource, TTarget>()` | Enregistre un mapping et retourne la config |
| `Map<TSource, TTarget>(source)` | Mappe un objet vers un nouveau TTarget |
| `Map<TSource, TTarget>(source, target)` | Met a jour un TTarget existant |
| `Map<TSource, TTarget>(IEnumerable)` | Mappe une collection (lazy) |
| `GetProjectionExpression<S,T>()` | Retourne l'expression pour ProjectTo |
| `AssertConfigurationIsValid()` | Valide tous les mappings enregistres |

> `INovelyMapper` herite de `IMapper` et reste disponible pour retrocompatibilite.

### INovelyMapperConfig&lt;TSource, TTarget&gt;

| Methode | Description |
|---------|-------------|
| `ForMember(selector, Action<MemberOptions>)` | Configure une propriete |
| `ReverseMap()` | Cree le mapping inverse |
| `BeforeMap(Action<S,T>)` | Action avant le mapping |
| `AfterMap(Action<S,T>)` | Action apres le mapping |
| `ConvertUsing(Func<S,T>)` | Convertisseur complet |

### MemberOptions&lt;TSource&gt;

| Methode | Description |
|---------|-------------|
| `MapFrom(Expression<Func<S, object>>)` | Expression source |
| `Ignore()` | Ignore cette propriete |
| `MapWhen(Func<S, bool>)` | Condition d'application |
| `NullSubstitute(object)` | Valeur si source null |
| `ConvertUsing(Func<S, object>)` | Convertisseur par membre |

### Extensions

| Methode | Description |
|---------|-------------|
| `services.UseNovelyMapper<TProfile>()` | Enregistre avec un profil |
| `services.UseNovelyMapper(params Type[])` | Enregistre avec plusieurs profils |
| `services.UseNovelyMapper(params Assembly[])` | Scan d'assemblies |
| `query.ProjectTo<S,T>(mapper)` | Projection IQueryable generique |
| `query.ProjectTo<T>(mapper)` | Projection IQueryable non-generique |

---

## Licence

Ce projet est distribue sous licence open-source. Voir le fichier [LICENSE](LICENSE) pour plus de details.
