namespace Novely.Mapper.Tests;

[TestFixture]
public class ValidationTests
{
    private class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class TargetWithExtra
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Unmapped { get; set; } = null!;
    }

    private class TargetFullyMapped
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    [Test]
    public void AssertConfigurationIsValid_AllMapped_ShouldNotThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetFullyMapped>();

        Assert.DoesNotThrow(() => mapper.AssertConfigurationIsValid());
    }

    [Test]
    public void AssertConfigurationIsValid_UnmappedProperty_ShouldThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithExtra>();

        var ex = Assert.Throws<NovelyMapperValidationException>(
            () => mapper.AssertConfigurationIsValid());

        Assert.That(ex!.Errors, Has.Count.EqualTo(1));
        Assert.That(ex.Errors[0], Does.Contain("Unmapped"));
    }

    [Test]
    public void AssertConfigurationIsValid_IgnoredProperty_ShouldNotThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithExtra>()
            .ForMember(d => d.Unmapped, opt => opt.Ignore());

        Assert.DoesNotThrow(() => mapper.AssertConfigurationIsValid());
    }

    [Test]
    public void AssertConfigurationIsValid_CustomMappedProperty_ShouldNotThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithExtra>()
            .ForMember(d => d.Unmapped, opt => opt.MapFrom(s => s.Name));

        Assert.DoesNotThrow(() => mapper.AssertConfigurationIsValid());
    }

    [Test]
    public void AssertConfigurationIsValid_RecordWithConstructor_ShouldNotThrow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, RecordMappingTests.PersonDto>();

        Assert.DoesNotThrow(() => mapper.AssertConfigurationIsValid());
    }

    [Test]
    public void ValidationException_ShouldContainAllErrors()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Source, TargetWithExtra>();

        var ex = Assert.Throws<NovelyMapperValidationException>(
            () => mapper.AssertConfigurationIsValid());

        Assert.That(ex!.Message, Does.Contain("Validation"));
        Assert.That(ex.Errors, Is.Not.Empty);
    }
}
