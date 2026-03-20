namespace Novely.Mapper.Tests;

[TestFixture]
public class NestedMappingTests
{
    private class Child
    {
        public int Id { get; set; }
        public string Value { get; set; } = null!;
    }

    private class ChildDto
    {
        public int Id { get; set; }
        public string Value { get; set; } = null!;
    }

    private class Parent
    {
        public int Id { get; set; }
        public Child Child { get; set; } = null!;
        public List<Child> Children { get; set; } = null!;
    }

    private class ParentDto
    {
        public int Id { get; set; }
        public ChildDto Child { get; set; } = null!;
        public List<ChildDto> Children { get; set; } = null!;
    }

    private class ParentWithArray
    {
        public ChildDto[] Children { get; set; } = null!;
    }

    [Test]
    public void Map_WithNestedObject_ShouldMapNested()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Child, ChildDto>();
        mapper.CreateMap<Parent, ParentDto>();

        var parent = new Parent
        {
            Id = 1,
            Child = new Child { Id = 10, Value = "test" },
            Children = new List<Child>
            {
                new() { Id = 20, Value = "a" },
                new() { Id = 30, Value = "b" }
            }
        };

        var dto = mapper.Map<Parent, ParentDto>(parent);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(1));
            Assert.That(dto.Child.Id, Is.EqualTo(10));
            Assert.That(dto.Child.Value, Is.EqualTo("test"));
            Assert.That(dto.Children, Has.Count.EqualTo(2));
            Assert.That(dto.Children[0].Value, Is.EqualTo("a"));
            Assert.That(dto.Children[1].Value, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Map_WithNullNestedObject_ShouldReturnNull()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Child, ChildDto>();
        mapper.CreateMap<Parent, ParentDto>();

        var parent = new Parent { Id = 1, Child = null!, Children = null! };
        var dto = mapper.Map<Parent, ParentDto>(parent);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(1));
            Assert.That(dto.Child, Is.Null);
            Assert.That(dto.Children, Is.Null);
        });
    }

    [Test]
    public void Map_WithNestedCollection_ToArray()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Child, ChildDto>();
        mapper.CreateMap<Parent, ParentWithArray>();

        var parent = new Parent
        {
            Id = 1,
            Child = null!,
            Children = new List<Child>
            {
                new() { Id = 1, Value = "x" }
            }
        };

        var dto = mapper.Map<Parent, ParentWithArray>(parent);

        Assert.That(dto.Children, Has.Length.EqualTo(1));
        Assert.That(dto.Children[0].Value, Is.EqualTo("x"));
    }
}
