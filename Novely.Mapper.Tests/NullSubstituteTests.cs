namespace Novely.Mapper.Tests;

[TestFixture]
public class NullSubstituteTests
{
    private class Source
    {
        public string Label { get; set; } = null!;
        public string Description { get; set; } = null!;
    }

    private class Target
    {
        public string Label { get; set; } = null!;
        public string Description { get; set; } = null!;
    }

    [Test]
    public void NullSubstitute_WhenSourceIsNull_ShouldUseSubstitute()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Label, opt => opt.NullSubstitute("N/A"));

        var source = new Source { Label = null!, Description = "test" };
        var target = mapper.Map<Source, Target>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Label, Is.EqualTo("N/A"));
            Assert.That(target.Description, Is.EqualTo("test"));
        });
    }

    [Test]
    public void NullSubstitute_WhenSourceIsNotNull_ShouldUseSourceValue()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Label, opt => opt.NullSubstitute("N/A"));

        var source = new Source { Label = "Real Value", Description = "test" };
        var target = mapper.Map<Source, Target>(source);

        Assert.That(target.Label, Is.EqualTo("Real Value"));
    }

    [Test]
    public void NullSubstitute_WithMapFrom_ShouldApplyToMappedValue()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, Target>()
            .ForMember(d => d.Label, opt => opt
                .MapFrom(s => s.Description)
                .NullSubstitute("fallback"));

        var source = new Source { Label = "x", Description = null! };
        var target = mapper.Map<Source, Target>(source);

        Assert.That(target.Label, Is.EqualTo("fallback"));
    }
}
