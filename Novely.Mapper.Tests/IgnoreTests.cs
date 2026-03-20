namespace Novely.Mapper.Tests;

[TestFixture]
public class IgnoreTests
{
    private class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Secret { get; set; } = null!;
    }

    private class Target
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Secret { get; set; } = null!;
    }

    [Test]
    public void ForMember_Ignore_ShouldLeavePropertyAtDefault()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Secret, opt => opt.Ignore());

        var source = new Source { Id = 1, Name = "Test", Secret = "hidden" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Name, Is.EqualTo("Test"));
            Assert.That(target.Secret, Is.Null);
        });
    }

    [Test]
    public void ForMember_Ignore_MultipleProperties()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Name, opt => opt.Ignore())
            .ForMember(d => d.Secret, opt => opt.Ignore());

        var source = new Source { Id = 42, Name = "Alice", Secret = "pwd" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(42));
            Assert.That(target.Name, Is.Null);
            Assert.That(target.Secret, Is.Null);
        });
    }

    [Test]
    public void ForMember_Ignore_ShouldPreserveValueOnMapToExisting()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Secret, opt => opt.Ignore());

        var source = new Source { Id = 1, Name = "Test", Secret = "hidden" };
        var existing = new Target { Id = 0, Name = "old", Secret = "keep-me" };
        mapper.Map(source, existing);

        Assert.Multiple(() =>
        {
            Assert.That(existing.Id, Is.EqualTo(1));
            Assert.That(existing.Name, Is.EqualTo("Test"));
            Assert.That(existing.Secret, Is.EqualTo("keep-me"));
        });
    }
}
