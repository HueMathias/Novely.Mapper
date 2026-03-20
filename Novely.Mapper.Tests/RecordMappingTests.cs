namespace Novely.Mapper.Tests;

[TestFixture]
public class RecordMappingTests
{
    public record PersonDto(int Id, string Name);

    public record PersonWithExtraDto(int Id, string Name, string? Email);

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    [Test]
    public void Map_ToRecord_ShouldMapViaConstructor()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Person, PersonDto>();

        var person = new Person { Id = 1, Name = "Alice" };
        var dto = mapper.Map<Person, PersonDto>(person);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(1));
            Assert.That(dto.Name, Is.EqualTo("Alice"));
        });
    }

    [Test]
    public void Map_FromRecord_ShouldMapFromProperties()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<PersonDto, Person>();

        var dto = new PersonDto(1, "Bob");
        var person = mapper.Map<PersonDto, Person>(dto);

        Assert.Multiple(() =>
        {
            Assert.That(person.Id, Is.EqualTo(1));
            Assert.That(person.Name, Is.EqualTo("Bob"));
        });
    }

    [Test]
    public void Map_ToRecord_WithCustomMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<EntityA, PersonDto>()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name));

        var a = new EntityA { Id = 5, Name = "Charlie" };
        var dto = mapper.Map<EntityA, PersonDto>(a);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(5));
            Assert.That(dto.Name, Is.EqualTo("Charlie"));
        });
    }

    [Test]
    public void Map_RecordToRecord()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<PersonDto, PersonWithExtraDto>()
            .ForMember(d => d.Email, opt => opt.Ignore());

        var source = new PersonDto(1, "Alice");
        var target = mapper.Map<PersonDto, PersonWithExtraDto>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Name, Is.EqualTo("Alice"));
            Assert.That(target.Email, Is.Null);
        });
    }
}
