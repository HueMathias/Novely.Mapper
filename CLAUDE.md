# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projet

Novely.Mapper est un mapper d'objets léger et fluent pour .NET 8.0, alternative open-source à AutoMapper. Il utilise la compilation d'Expression Trees pour générer des fonctions de mapping à la volée, avec mise en cache thread-safe via ConcurrentDictionary.

## Commandes

```bash
dotnet restore                          # Restaurer les dépendances
dotnet build --no-restore               # Build la solution
dotnet test --no-build --verbosity normal  # Lancer tous les tests (NUnit)
dotnet test --no-build --filter "FullyQualifiedName~NomDuTest"  # Lancer un test spécifique
```

**Note** : `dotnet build -q` peut échouer de façon cryptique sur Windows après modification de fichiers — préférer `dotnet build` sans `-q`.

## Architecture

La solution contient deux projets :

- **Novely.Mapper/** — Bibliothèque principale (package NuGet `NovelyMapper` v8.0.0)
- **Novely.Mapper.Tests/** — Tests unitaires NUnit

### Classes principales

| Fichier | Rôle |
|---|---|
| `NovelyMapper.cs` | Interface `IMapper` (interface DI principale), `INovelyMapper` (hérite de `IMapper`, rétrocompatibilité) et implémentation `NovelyMapper`. Méthodes : `CreateMap`, `Map` (single/existing/collection), `GetProjectionExpression`, `AssertConfigurationIsValid`. Compilation lazy via Expression Trees, diagnostic runtime par propriété en cas d'erreur |
| `NovelyMapperConfig.cs` | Interface `INovelyMapperConfig<S,T>` et implémentation. API fluent : `ForMember` (MemberOptions), `ReverseMap`, `BeforeMap`, `AfterMap`, `ConvertUsing`. Implémente `IMapperConfig` (interface interne non-générique pour la validation) |
| `MemberOptions.cs` | Options par membre : `MapFrom`, `Ignore`, `MapWhen`, `NullSubstitute`, `ConvertUsing`. Implémente `IMemberOptions` (interface interne) |
| `NovelyMapperProfile.cs` | Classe abstraite instance-based. Le profil reçoit `NovelyMapper` via constructeur (pas de static) |
| `NovelyMapperEmptyProfile.cs` | Profil vide par défaut pour `UseNovelyMapper()` sans générique |
| `NovelyMapperExtensions.cs` | Extensions DI : `UseNovelyMapper<T>()`, multi-profils par types ou scan d'assembly (`GetExportedTypes` pour exclure les non-publics) |
| `NovelyMapperException.cs` | Exception dédiée avec propriétés structurées (`SourceType`, `TargetType`, `PropertyName`, `CollectionIndex`, `Suggestion`). Factory methods pour chaque scénario d'erreur |
| `NovelyMapperValidationException.cs` | Hérite de `NovelyMapperException`. Levée par `AssertConfigurationIsValid()` avec liste d'erreurs formatées |
| `QueryableExtensions.cs` | `ProjectTo<T>()` pour IQueryable (projection EF via expression). Accepte `IMapper` en paramètre |
| `NovelyMapperOptions.cs` | Options globales : `MissingPropertyBehavior` (Silent/Throw) |

### Flux de mapping

1. Un profil hérite de `NovelyMapperProfile(NovelyMapper mapper)` et appelle `CreateMap<S,T>()` dans son constructeur
2. `ForMember(d => d.Prop, opt => opt.MapFrom(s => s.X))` configure les mappings via `MemberOptions`
3. Au premier appel `Map<S,T>()`, le mapper compile un delegate via Expression Trees et le met en cache
4. Propriétés matchées par nom ; mappings custom, objets imbriqués et collections de types complexes sont résolus récursivement
5. En cas d'erreur runtime, le mapper re-exécute propriété par propriété pour identifier la fautive

### Mapping automatique des propriétés de navigation (style EF Core)

Le mapper résout automatiquement les propriétés de navigation imbriquées sans configuration explicite, à condition que les mappings des types éléments soient enregistrés :

- **Objet imbriqué** : si `Source.Child` (type `A`) et `Target.Child` (type `B`) ont des types différents et que `CreateMap<A, B>()` est enregistré, le mapping est appliqué automatiquement (avec null-check pour les types référence)
- **Collection imbriquée** : si `Source.Items` est `ICollection<A>` et `Target.Items` est `IEnumerable<B>`, `List<B>`, `ICollection<B>` ou `B[]`, et que `CreateMap<A, B>()` est enregistré, le mapper génère un `.Select(x => map(x)).ToList()` ou `.ToArray()` automatiquement
- **Imbrication profonde** : ces résolutions sont récursives (ex: `Order.Customer.Contacts` fonctionne si chaque niveau a son mapping enregistré)
- **Null-safety** : les propriétés de navigation null retournent `null` (pas d'exception)

### Injection de dépendances

`UseNovelyMapper<TProfile>()` crée un `NovelyMapper`, l'enregistre en singleton sous `IMapper`, `INovelyMapper` et `NovelyMapper`, puis instancie le profil via `Activator.CreateInstance(typeof(TProfile), mapper)`. L'interface `IMapper` est l'interface principale pour l'injection. `INovelyMapper` hérite de `IMapper` et reste disponible pour rétrocompatibilité. Multi-profils : `UseNovelyMapper(params Type[])` ou `UseNovelyMapper(params Assembly[])`.

## Gestion d'erreurs

- Toutes les erreurs du mapper sont des `NovelyMapperException` (ou sous-classes)
- Les exceptions custom doivent hériter de `NovelyMapperException` pour le filtre `when (ex is not NovelyMapperException)`
- Les erreurs de collection incluent l'index (`CollectionIndex`) et propagent la propriété fautive
- Le diagnostic runtime "retry per-property" identifie la propriété en erreur sans impacter les performances nominales
- `Expression.MemberInit` ne supporte pas `TryCatch` dans les bindings — c'est pourquoi le diagnostic utilise une re-exécution

## Conventions

- Les deux projets ont `<Nullable>enable</Nullable>` — utiliser les annotations nullable (`?`, `!`) correctement pour éviter les warnings
- Tests NUnit : `[TestFixture]`, `[Test]`, `[SetUp]`, `[TearDown]`. Utiliser `Assert.Multiple` pour les assertions groupées
- L'ancien `ForMember(dest, src)` à deux expressions est marqué `[Obsolete]` — les tests existants utilisent `#pragma warning disable CS0618`
- 1 fichier de test par fonctionnalité (ex: `IgnoreTests.cs`, `ReverseMapTests.cs`, `ErrorMessageTests.cs`)

## CI/CD

GitHub Actions (`.github/workflows/dotnet.yml`) : build et test sur ubuntu-latest avec .NET 8.0.x, déclenché sur push/PR vers master.
