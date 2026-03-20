using Novely.Mapper;
using NUnit.Framework;

namespace Novely.Mapper.Tests;

[TestFixture]
public class MapFromNestedResolutionTests
{
    #region Models

    public class Inner
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
    }

    public class InnerDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
    }

    public class Source
    {
        public Inner Data { get; set; } = null!;
        public string Name { get; set; } = "";
    }

    public class Target
    {
        public InnerDto Info { get; set; } = null!;
        public string Name { get; set; } = "";
    }

    public class SourceWithCollection
    {
        public List<Inner> Items { get; set; } = new();
    }

    public class TargetWithCollection
    {
        public List<InnerDto> MappedItems { get; set; } = new();
    }

    public class SourceWithNullableInner
    {
        public Inner? Data { get; set; }
    }

    public class TargetWithNullableInnerDto
    {
        public InnerDto? Info { get; set; }
    }

    #endregion

    [Test]
    public void MapFrom_WithRegisteredNestedMapping_ShouldResolveAutomatically()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Inner, InnerDto>();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Info, opt => opt.MapFrom(s => s.Data));

        var source = new Source
        {
            Data = new Inner { Id = 1, Label = "Hello" },
            Name = "Test"
        };

        var result = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Info, Is.Not.Null);
            Assert.That(result.Info.Id, Is.EqualTo(1));
            Assert.That(result.Info.Label, Is.EqualTo("Hello"));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void MapFrom_WithNullNestedObject_ShouldReturnNull()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Inner, InnerDto>();
        mapper.CreateMap<SourceWithNullableInner, TargetWithNullableInnerDto>()
            .ForMember(d => d.Info, opt => opt.MapFrom(s => s.Data));

        var source = new SourceWithNullableInner { Data = null };

        var result = mapper.Map<SourceWithNullableInner, TargetWithNullableInnerDto>(source);

        Assert.That(result.Info, Is.Null);
    }

    [Test]
    public void MapFrom_WithRegisteredCollectionMapping_ShouldResolveAutomatically()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Inner, InnerDto>();
        mapper.CreateMap<SourceWithCollection, TargetWithCollection>()
            .ForMember(d => d.MappedItems, opt => opt.MapFrom(s => s.Items));

        var source = new SourceWithCollection
        {
            Items = new List<Inner>
            {
                new() { Id = 1, Label = "A" },
                new() { Id = 2, Label = "B" }
            }
        };

        var result = mapper.Map<SourceWithCollection, TargetWithCollection>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.MappedItems, Has.Count.EqualTo(2));
            Assert.That(result.MappedItems[0].Id, Is.EqualTo(1));
            Assert.That(result.MappedItems[1].Label, Is.EqualTo("B"));
        });
    }

    [Test]
    public void MapFrom_WithNestedMapping_OnMapToExisting_ShouldResolve()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Inner, InnerDto>();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Info, opt => opt.MapFrom(s => s.Data));

        var source = new Source
        {
            Data = new Inner { Id = 5, Label = "Updated" },
            Name = "Test"
        };
        var target = new Target { Info = new InnerDto(), Name = "" };

        mapper.Map(source, target);

        Assert.Multiple(() =>
        {
            Assert.That(target.Info.Id, Is.EqualTo(5));
            Assert.That(target.Info.Label, Is.EqualTo("Updated"));
        });
    }

    [Test]
    public void MapFrom_NullableToNonNullable_ShouldResolve()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullableInt, TargetWithInt>()
            .ForMember(d => d.Value, opt => opt.MapFrom(s => s.NullableValue));

        var source = new SourceWithNullableInt { NullableValue = 42 };
        var result = mapper.Map<SourceWithNullableInt, TargetWithInt>(source);

        Assert.That(result.Value, Is.EqualTo(42));
    }

    #region Nullable MapFrom Models

    public class SourceWithNullableInt
    {
        public int? NullableValue { get; set; }
    }

    public class TargetWithInt
    {
        public int Value { get; set; }
    }

    #endregion
}
