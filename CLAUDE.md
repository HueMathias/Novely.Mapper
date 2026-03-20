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

## Architecture

La solution contient deux projets :

- **Novely.Mapper/** — Bibliothèque principale (package NuGet `NovelyMapper` v8.0.0)
- **Novely.Mapper.Tests/** — Tests unitaires NUnit

### Classes principales

| Fichier | Rôle |
|---|---|
| `NovelyMapper.cs` | Interface `INovelyMapper` et implémentation. Compilation lazy des mappings via Expression Trees, cache dans `ConcurrentDictionary`. 3 méthodes : `Map<S,T>`, `MapList<S,T>`, `CreateMap<S,T>` |
| `NovelyMapperConfig.cs` | Interface `INovelyMapperConfig<S,T>` et implémentation. Stocke les mappings custom par propriété. API fluent via `ForMember` |
| `NovelyMapperProfile.cs` | Classe abstraite de base pour définir des profils de mapping. Propriété statique `Mapper` partagée entre profils |
| `NovelyMapperExtensions.cs` | Extensions DI : `UseNovelyMapper<TProfile>()` enregistre le mapper en singleton dans `IServiceCollection` |

### Flux de mapping

1. Un profil hérite de `NovelyMapperProfile` et appelle `CreateMap<TSource, TTarget>()` dans son constructeur
2. `ForMember(dest => dest.Prop, src => src.OtherProp)` permet le mapping custom de propriétés
3. Au premier appel `Map<S,T>()`, le mapper compile un delegate via Expression Trees et le met en cache
4. Les propriétés sont matchées automatiquement par nom ; les mappings custom surchargent ce comportement

### Injection de dépendances

`UseNovelyMapper<TProfile>()` enregistre `NovelyMapper` et `INovelyMapper` en singleton, instancie le profil, et appelle `Initialize()` pour lier le mapper au profil.

## CI/CD

GitHub Actions (`.github/workflows/dotnet.yml`) : build et test sur ubuntu-latest avec .NET 8.0.x, déclenché sur push/PR vers master.
