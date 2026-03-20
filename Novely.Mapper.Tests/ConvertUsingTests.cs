namespace Novely.Mapper.Tests;

[TestFixture]
public class ConvertUsingTests
{
    private class Source
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }

    private class Target
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
    }

    private class SimpleSource
    {
        public string Value { get; set; } = null!;
    }

    private class SimpleTarget
    {
        public string Value { get; set; } = null!;
        public int Length { get; set; }
    }

    [Test]
    public void ConvertUsing_ShouldUseCustomConverter()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ConvertUsing(s => new Target
            {
                Id = s.Id,
                FullName = $"{s.FirstName} {s.LastName}"
            });

        var source = new Source { Id = 1, FirstName = "John", LastName = "Doe" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.FullName, Is.EqualTo("John Doe"));
        });
    }

    [Test]
    public void ConvertUsing_ShouldOverrideExpressionTree()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ConvertUsing(s => new Target { Id = s.Id * 10, FullName = "custom" });

        var source = new Source { Id = 5, FirstName = "A", LastName = "B" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(50));
            Assert.That(target.FullName, Is.EqualTo("custom"));
        });
    }

    [Test]
    public void MemberConvertUsing_ShouldUseCustomMemberConverter()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SimpleSource, SimpleTarget>()
            .ForMember(d => d.Length, opt => opt.ConvertUsing(s => s.Value?.Length ?? 0));

        var source = new SimpleSource { Value = "hello" };
        var target = mapper.Map<SimpleSource, SimpleTarget>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Value, Is.EqualTo("hello"));
            Assert.That(target.Length, Is.EqualTo(5));
        });
    }

    [Test]
    public void ConvertUsing_WithCollection()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ConvertUsing(s => new Target { Id = s.Id, FullName = s.FirstName });

        var sources = new List<Source>
        {
            new() { Id = 1, FirstName = "A", LastName = "B" },
            new() { Id = 2, FirstName = "C", LastName = "D" }
        };

        var targets = mapper.Map<Source, Target>(sources).ToList();

        Assert.That(targets, Has.Count.EqualTo(2));
        Assert.That(targets[0].FullName, Is.EqualTo("A"));
        Assert.That(targets[1].FullName, Is.EqualTo("C"));
    }
}
