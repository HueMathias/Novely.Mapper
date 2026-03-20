namespace Novely.Mapper.Tests;

[TestFixture]
public class ConditionTests
{
    private class Source
    {
        public int Id { get; set; }
        public string Status { get; set; } = null!;
        public bool IsEnabled { get; set; }
    }

    private class Target
    {
        public int Id { get; set; }
        public string Status { get; set; } = null!;
    }

    [Test]
    public void MapWhen_ConditionTrue_ShouldMapProperty()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Status, opt => opt
                .MapFrom(s => s.Status)
                .MapWhen(s => s.IsEnabled));

        var source = new Source { Id = 1, Status = "Active", IsEnabled = true };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Status, Is.EqualTo("Active"));
        });
    }

    [Test]
    public void MapWhen_ConditionFalse_ShouldUseDefault()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Status, opt => opt
                .MapFrom(s => s.Status)
                .MapWhen(s => s.IsEnabled));

        var source = new Source { Id = 1, Status = "Active", IsEnabled = false };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Status, Is.Null); // default(string)
        });
    }

    [Test]
    public void MapWhen_OnMapToExisting_ConditionFalse_ShouldPreserveExistingValue()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Status, opt => opt
                .MapFrom(s => s.Status)
                .MapWhen(s => s.IsEnabled));

        var source = new Source { Id = 1, Status = "New", IsEnabled = false };
        var existing = new Target { Id = 0, Status = "OldStatus" };

        mapper.Map(source, existing);

        Assert.Multiple(() =>
        {
            Assert.That(existing.Id, Is.EqualTo(1));
            Assert.That(existing.Status, Is.EqualTo("OldStatus")); // préservé
        });
    }
}
