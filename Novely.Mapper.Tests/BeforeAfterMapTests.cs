namespace Novely.Mapper.Tests;

[TestFixture]
public class BeforeAfterMapTests
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
        public string FullLabel { get; set; } = null!;
    }

    [Test]
    public void AfterMap_ShouldExecuteAfterMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .AfterMap((src, dest) => dest.FullLabel = $"{dest.Id}-{dest.Name}");

        var source = new Source { Id = 1, Name = "Alice" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Name, Is.EqualTo("Alice"));
            Assert.That(target.FullLabel, Is.EqualTo("1-Alice"));
        });
    }

    [Test]
    public void BeforeMap_ShouldExecuteBeforePropertyMapping()
    {
        var executed = false;

        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .BeforeMap((src, dest) =>
            {
                executed = true;
                // dest est vide à ce stade (constructeur par défaut)
                Assert.That(dest.Id, Is.EqualTo(0));
            });

        var source = new Source { Id = 5, Name = "Bob" };
        mapper.Map<Source, Target>(source);

        Assert.That(executed, Is.True);
    }

    [Test]
    public void BeforeMap_AndAfterMap_ShouldBothExecute()
    {
        var log = new List<string>();

        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .BeforeMap((src, dest) => log.Add("before"))
            .AfterMap((src, dest) => log.Add("after"));

        mapper.Map<Source, Target>(new Source { Id = 1, Name = "test" });

        Assert.That(log, Is.EqualTo(new[] { "before", "after" }));
    }

    [Test]
    public void AfterMap_OnMapToExisting_ShouldExecute()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .AfterMap((src, dest) => dest.FullLabel = $"mapped-{dest.Name}");

        var source = new Source { Id = 1, Name = "Charlie" };
        var existing = new Target { Id = 0, Name = "old", FullLabel = "old" };

        mapper.Map(source, existing);

        Assert.That(existing.FullLabel, Is.EqualTo("mapped-Charlie"));
    }
}
