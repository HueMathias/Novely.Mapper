namespace Novely.Mapper.Tests;

// Classes de test
public class EntityA
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class EntityB
{
    public int Id { get; set; }
    public string Nom { get; set; } = null!;
}

[TestFixture]
public class NovelyMapperTests
{
    private INovelyMapper mapper;

    [SetUp]
    public void Setup()
    {
        mapper = new NovelyMapper();
        mapper.CreateMap<EntityA, EntityB>().ForMember(dest => dest.Nom, src => src.Name);
    }

    [Test]
    public void Map_SingleObject_ShouldMapPropertiesCorrectly()
    {
        var a = new EntityA { Id = 1, Name = "Mathias" };
        var b = mapper.Map<EntityA, EntityB>(a);

        Assert.Multiple(() =>
        {
            Assert.That(b.Id, Is.EqualTo(a.Id));
            Assert.That(b.Nom, Is.EqualTo(a.Name));
        });
    }

    [Test]
    public void MapAll_Collection_ShouldMapAllObjects()
    {
        var listA = new List<EntityA>
            {
                new() { Id = 1, Name = "Alice" },
                new() { Id = 2, Name = "Bob" }
            };

        var listB = mapper.Map<EntityA, EntityB>(listA);

        CollectionAssert.AreEqual(
            new[] { "Alice", "Bob" },
            new List<string>(Enumerable.Select(listB, x => x.Nom))
        );
    }

    [Test]
    public void Map_NullSource_ShouldThrowArgumentNullException()
    {
        EntityA a = null;
        Assert.Throws<ArgumentNullException>(() => mapper.Map<EntityA, EntityB>(a));
    }

    [Test]
    public void MapAll_NullCollection_ShouldThrowArgumentNullException()
    {
        List<EntityA> listA = null;
        Assert.Throws<ArgumentNullException>(() => mapper.Map<EntityA, EntityB>(listA).ToList());
    }

    [Test]
    public void Map_WithoutCreateMap_ShouldThrowInvalidOperationException()
    {
        var mapper = new NovelyMapper();
        var a = new EntityA { Id = 1, Name = "Test" };
        Assert.Throws<InvalidOperationException>(() => mapper.Map<EntityA, EntityB>(a));
    }
}