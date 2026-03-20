namespace Novely.Mapper.Tests;

[TestFixture]
public class ReverseMapTests
{
    private class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class Target
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;
    }

    [Test]
    public void ReverseMap_ShouldInvertSimpleMappings()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Nom, opt => opt.MapFrom(s => s.Name))
            .ReverseMap();

        // Forward
        var source = new Source { Id = 1, Name = "Alice" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Nom, Is.EqualTo("Alice"));
        });

        // Reverse
        var reversed = mapper.Map<Target, Source>(target);

        Assert.Multiple(() =>
        {
            Assert.That(reversed.Id, Is.EqualTo(1));
            Assert.That(reversed.Name, Is.EqualTo("Alice"));
        });
    }

    [Test]
    public void ReverseMap_ConventionProperties_ShouldBeAutomaticallyReversible()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Source>().ReverseMap();

        var source = new Source { Id = 42, Name = "Bob" };
        var result = mapper.Map<Source, Source>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(42));
            Assert.That(result.Name, Is.EqualTo("Bob"));
        });
    }

    [Test]
    public void ReverseMap_ShouldAllowFurtherConfiguration()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Nom, opt => opt.MapFrom(s => s.Name))
            .ReverseMap()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Nom));

        var target = new Target { Id = 1, Nom = "Charlie" };
        var reversed = mapper.Map<Target, Source>(target);

        Assert.Multiple(() =>
        {
            Assert.That(reversed.Id, Is.EqualTo(1));
            Assert.That(reversed.Name, Is.EqualTo("Charlie"));
        });
    }
}
