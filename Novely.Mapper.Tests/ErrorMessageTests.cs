using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper.Tests;

[TestFixture]
public class ErrorMessageTests
{
    #region Test types

    private class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class Target
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class TargetWithUnmapped
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Extra { get; set; } = null!;
    }

    public record NoMatchRecord(int X, int Y, int Z);

    private class SourceWithWrongType
    {
        public int Id { get; set; }
        public string Value { get; set; } = null!;
    }

    private class TargetWithWrongType
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private class BadProfile : NovelyMapperProfile
    {
        public BadProfile(NovelyMapper mapper) : base(mapper)
        {
            throw new InvalidOperationException("Erreur dans le profil !");
        }
    }

    #endregion

    [Test]
    public void MissingMapping_ShouldShowBothTypesAndSuggestion()
    {
        var mapper = new NovelyMapper();

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(new Source { Id = 1 }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.SourceType, Is.EqualTo(typeof(Source)));
            Assert.That(ex.TargetType, Is.EqualTo(typeof(Target)));
            Assert.That(ex.Message, Does.Contain("Source"));
            Assert.That(ex.Message, Does.Contain("Target"));
            Assert.That(ex.Message, Does.Contain("CreateMap"));
            Assert.That(ex.Suggestion, Is.Not.Null);
        });
    }

    [Test]
    public void MissingMapping_MapToExisting_ShouldShowSuggestion()
    {
        var mapper = new NovelyMapper();

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map(new Source { Id = 1 }, new Target()));

        Assert.That(ex!.Message, Does.Contain("CreateMap"));
    }

    [Test]
    public void MissingMapping_Collection_ShouldShowSuggestion()
    {
        var mapper = new NovelyMapper();
        var list = new List<Source> { new() { Id = 1 } };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(list).ToList());

        Assert.That(ex!.Message, Does.Contain("CreateMap"));
    }

    [Test]
    public void MissingMapping_ProjectTo_ShouldShowSuggestion()
    {
        var mapper = new NovelyMapper();

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.GetProjectionExpression<Source, Target>());

        Assert.That(ex!.Message, Does.Contain("CreateMap"));
    }

    [Test]
    public void ConstructorResolution_ShouldListUnmatchedParams()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, NoMatchRecord>();

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, NoMatchRecord>(new Source { Id = 1, Name = "test" }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("NoMatchRecord"));
            Assert.That(ex.Message, Does.Contain("non résolus"));
            // Au moins Y et Z ne matchent pas
            Assert.That(ex.Message, Does.Contain("'y'").Or.Contain("'Y'"));
            Assert.That(ex.Suggestion, Does.Contain("ForMember"));
            Assert.That(ex.Suggestion, Does.Contain("Ignore"));
        });
    }

    [Test]
    public void ConvertUsing_RuntimeError_ShouldWrapWithContext()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ConvertUsing(s => throw new InvalidOperationException("boom"));

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(new Source { Id = 1 }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("ConvertUsing"));
            Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(ex.InnerException!.Message, Is.EqualTo("boom"));
        });
    }

    [Test]
    public void AfterMap_RuntimeError_ShouldWrapWithContext()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .AfterMap((s, t) => throw new InvalidOperationException("aftermap error"));

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(new Source { Id = 1 }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("AfterMap"));
            Assert.That(ex.InnerException!.Message, Is.EqualTo("aftermap error"));
        });
    }

    [Test]
    public void BeforeMap_RuntimeError_ShouldWrapWithContext()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .BeforeMap((s, t) => throw new InvalidOperationException("beforemap error"));

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(new Source { Id = 1 }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("BeforeMap"));
            Assert.That(ex.InnerException!.Message, Is.EqualTo("beforemap error"));
        });
    }

    [Test]
    public void BeforeMap_OnRecordWithoutParameterlessCtor_ShouldShowClearError()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, RecordMappingTests.PersonDto>()
            .BeforeMap((s, t) => { });

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, RecordMappingTests.PersonDto>(new Source { Id = 1, Name = "test" }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PersonDto"));
            Assert.That(ex.Message, Does.Contain("BeforeMap"));
        });
    }

    [Test]
    public void NullSubstitute_WrongType_ShouldThrowAtConfigTime()
    {
        var mapper = new NovelyMapper();

        var ex = Assert.Throws<NovelyMapperException>(() =>
            mapper.CreateMap<Source, Target>()
                .ForMember(d => d.Id, opt => opt.NullSubstitute("not an int")));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("NullSubstitute"));
            Assert.That(ex.Message, Does.Contain("Id"));
            Assert.That(ex.Message, Does.Contain("Int32"));
            Assert.That(ex.Message, Does.Contain("String"));
        });
    }

    [Test]
    public void Validation_UnmappedProperty_ShouldShowForMemberSuggestion()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithUnmapped>();

        var ex = Assert.Throws<NovelyMapperValidationException>(
            () => mapper.AssertConfigurationIsValid());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Errors, Has.Count.EqualTo(1));
            Assert.That(ex.Errors[0], Does.Contain("Extra"));
            Assert.That(ex.Errors[0], Does.Contain("ForMember"));
            Assert.That(ex.Errors[0], Does.Contain("Ignore"));
        });
    }

    [Test]
    public void Validation_TypeMismatch_ShouldShowBothTypesAndSuggestion()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithWrongType, TargetWithWrongType>();

        var ex = Assert.Throws<NovelyMapperValidationException>(
            () => mapper.AssertConfigurationIsValid());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Errors, Has.Count.EqualTo(1));
            Assert.That(ex.Errors[0], Does.Contain("Value"));
            Assert.That(ex.Errors[0], Does.Contain("String"));
            Assert.That(ex.Errors[0], Does.Contain("Int32"));
            Assert.That(ex.Errors[0], Does.Contain("ForMember").Or.Contain("CreateMap"));
        });
    }

    [Test]
    public void ProfileWithBadConstructor_ShouldShowClearError()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<NovelyMapperException>(
            () => services.UseNovelyMapper(typeof(BadProfile)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("BadProfile"));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException!.Message, Does.Contain("Erreur dans le profil"));
        });
    }

    [Test]
    public void ProfileAbstract_ShouldShowClearError()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<NovelyMapperException>(
            () => services.UseNovelyMapper(typeof(NovelyMapperProfile)));

        Assert.That(ex!.Message, Does.Contain("abstrait"));
    }


    #region Collection error tests

    private class SourceWithNullable
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class TargetWithComputed
    {
        public int Id { get; set; }
        public string UpperName { get; set; } = null!;
    }

    [Test]
    public void MapCollection_ErrorAtIndex_ShouldShowIndex()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullable, TargetWithComputed>()
            .ForMember(d => d.UpperName, opt => opt.ConvertUsing(s => s.Name.ToUpper()));

        var sources = new List<SourceWithNullable>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = null! },  // va planter ici
            new() { Id = 3, Name = "Charlie" }
        };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<SourceWithNullable, TargetWithComputed>(sources).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.CollectionIndex, Is.EqualTo(1));
            Assert.That(ex.Message, Does.Contain("index 1"));
            Assert.That(ex.SourceType, Is.EqualTo(typeof(SourceWithNullable)));
            Assert.That(ex.TargetType, Is.EqualTo(typeof(TargetWithComputed)));
        });
    }

    [Test]
    public void MapCollection_ErrorAtFirstElement_ShouldShowIndex0()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullable, TargetWithComputed>()
            .ForMember(d => d.UpperName, opt => opt.ConvertUsing(s => s.Name.ToUpper()));

        var sources = new List<SourceWithNullable>
        {
            new() { Id = 1, Name = null! }
        };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<SourceWithNullable, TargetWithComputed>(sources).ToList());

        Assert.That(ex!.CollectionIndex, Is.EqualTo(0));
    }

    #endregion

    #region Property identification tests

    [Test]
    public void RuntimeError_ShouldIdentifyFaultyProperty()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullable, TargetWithComputed>()
            .ForMember(d => d.UpperName, opt => opt.ConvertUsing(s => s.Name.ToUpper()));

        var source = new SourceWithNullable { Id = 1, Name = null! };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<SourceWithNullable, TargetWithComputed>(source));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.PropertyName, Is.EqualTo("UpperName"));
            Assert.That(ex.Message, Does.Contain("UpperName"));
            Assert.That(ex.InnerException, Is.TypeOf<NullReferenceException>());
        });
    }

    [Test]
    public void RuntimeError_MapToExisting_ShouldIdentifyFaultyProperty()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullable, TargetWithComputed>()
            .ForMember(d => d.UpperName, opt => opt.ConvertUsing(s => s.Name.ToUpper()));

        var source = new SourceWithNullable { Id = 1, Name = null! };
        var existing = new TargetWithComputed { Id = 0, UpperName = "old" };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map(source, existing));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.PropertyName, Is.EqualTo("UpperName"));
            Assert.That(ex.Message, Does.Contain("UpperName"));
        });
    }

    [Test]
    public void CollectionError_ShouldShowBothIndexAndProperty()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullable, TargetWithComputed>()
            .ForMember(d => d.UpperName, opt => opt.ConvertUsing(s => s.Name.ToUpper()));

        var sources = new List<SourceWithNullable>
        {
            new() { Id = 1, Name = "OK" },
            new() { Id = 2, Name = "Fine" },
            new() { Id = 3, Name = null! } // élément 2 va planter
        };

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<SourceWithNullable, TargetWithComputed>(sources).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.CollectionIndex, Is.EqualTo(2));
            Assert.That(ex.Message, Does.Contain("index 2"));
            // La propriété fautive est propagée depuis l'inner exception
            Assert.That(ex.PropertyName, Is.EqualTo("UpperName"));
        });
    }

    #endregion

    [Test]
    public void NovelyMapperException_ShouldExposeStructuredProperties()
    {
        var mapper = new NovelyMapper();

        var ex = Assert.Throws<NovelyMapperException>(
            () => mapper.Map<Source, Target>(new Source()));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.SourceType, Is.EqualTo(typeof(Source)));
            Assert.That(ex.TargetType, Is.EqualTo(typeof(Target)));
            Assert.That(ex.Suggestion, Is.Not.Null.And.Not.Empty);
        });
    }
}
