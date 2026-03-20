namespace Novely.Mapper.Tests;

[TestFixture]
public class ProjectToTests
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
    }

    private class TargetWithCustom
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;
    }

    [Test]
    public void ProjectTo_Generic_ShouldProject()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        var list = new List<Source>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        var result = list.AsQueryable().ProjectTo<Source, Target>(mapper).ToList();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Id, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Alice"));
            Assert.That(result[1].Id, Is.EqualTo(2));
            Assert.That(result[1].Name, Is.EqualTo("Bob"));
        });
    }

    [Test]
    public void ProjectTo_NonGeneric_ShouldProject()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        var list = new List<Source>
        {
            new() { Id = 1, Name = "Alice" }
        };

        IQueryable queryable = list.AsQueryable();
        var result = queryable.ProjectTo<Target>(mapper).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void ProjectTo_WithMapFrom_ShouldInlineExpression()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithCustom>()
            .ForMember(d => d.Nom, opt => opt.MapFrom(s => s.Name));

        var list = new List<Source>
        {
            new() { Id = 1, Name = "Charlie" }
        };

        var result = list.AsQueryable().ProjectTo<Source, TargetWithCustom>(mapper).ToList();

        Assert.That(result[0].Nom, Is.EqualTo("Charlie"));
    }

    [Test]
    public void GetProjectionExpression_ShouldReturnValidExpression()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>();

        var expr = mapper.GetProjectionExpression<Source, Target>();

        Assert.That(expr, Is.Not.Null);

        // Compiler et exécuter
        var func = expr.Compile();
        var result = func(new Source { Id = 42, Name = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(42));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void ProjectTo_WithoutMapping_ShouldThrow()
    {
        var mapper = new NovelyMapper();

        var list = new List<Source> { new() { Id = 1, Name = "x" } };

        Assert.Throws<InvalidOperationException>(
            () => list.AsQueryable().ProjectTo<Source, Target>(mapper).ToList());
    }
}
