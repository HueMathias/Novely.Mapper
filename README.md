# NovelyMapper

NovelyMapper est un mapper léger et fluent pour .NET, conçu pour faciliter la transformation d’objets entre différents types.  
Il s’inspire d’AutoMapper mais reste **100% gratuit et open-source**. 

---

## Sommaire

- [Installation](#installation)  
- [Utilisation](#utilisation)  
  - [Création de profils](#création-de-profils)  
  - [ForMember](#formember)  
- [Injection de dépendances](#injection-de-dépendances)  

---

## Installation

Installe le package via NuGet :

```bash
dotnet add package NovelyMapper
```

Ou via le Package Manager Console :

```bash
Install-Package NovelyMapper
```

## Utilisation

### Création de profils

Créer un profil permet  de définir les mappings entre les types.

```csharp
public class MyMapperProfile : NovelyMapperProfile
{
    public MyMapperProfile()
    {
        
    }
}
```

### ForMember

La méthode `ForMember` permet de configurer le mapping pour une propriété spécifique.

```csharp
CreateMap<EntityA, EntityB>().ForMember(dest => dest.Nom, src => src.Name);
```

## Injection de dépendances

Pour utiliser NovelyMapper avec l’injection de dépendances, il faut l'ajouter dans le conteneur de services.

```csharp
builder.Services.UseNovelyMapper<MyMapperProfile>();
```

Ou sans profil (profil vide par défaut) :

```csharp
builder.Services.UseNovelyMapper();
```