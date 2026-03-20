namespace Novely.Mapper.Tests;

[TestFixture]
public class MapToExistingTests
{
    private class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class Target
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Extra { get; set; } = null!;
    }

    [Test]
    public void MapToExisting_ShouldUpdateMappedProperties()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        var source = new Source { Id = 1, Name = "Alice" };
        var existing = new Target { Id = 0, Name = "old", Extra = "preserved" };

        mapper.Map(source, existing);

        Assert.Multiple(() =>
        {
            Assert.That(existing.Id, Is.EqualTo(1));
            Assert.That(existing.Name, Is.EqualTo("Alice"));
            Assert.That(existing.Extra, Is.EqualTo("preserved"));
        });
    }

    [Test]
    public void MapToExisting_ShouldReturnSameInstance()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        var source = new Source { Id = 1, Name = "Bob" };
        var existing = new Target();

        var result = mapper.Map(source, existing);

        Assert.That(result, Is.SameAs(existing));
    }

    [Test]
    public void MapToExisting_WithCustomMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Extra, opt => opt.MapFrom(s => s.Name + "_extra"));

        var source = new Source { Id = 1, Name = "Charlie" };
        var existing = new Target { Id = 99, Name = "old", Extra = "old" };

        mapper.Map(source, existing);

        Assert.Multiple(() =>
        {
            Assert.That(existing.Id, Is.EqualTo(1));
            Assert.That(existing.Name, Is.EqualTo("Charlie"));
            Assert.That(existing.Extra, Is.EqualTo("Charlie_extra"));
        });
    }

    [Test]
    public void MapToExisting_NullSource_ShouldThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        Assert.Throws<ArgumentNullException>(() => mapper.Map<Source, Target>(null!, new Target()));
    }

    [Test]
    public void MapToExisting_NullTarget_ShouldThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        Assert.Throws<ArgumentNullException>(() => mapper.Map(new Source(), (Target)null!));
    }
}
