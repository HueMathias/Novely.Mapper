using Novely.Mapper;
using NUnit.Framework;

namespace Novely.Mapper.Tests;

/// <summary>
/// Tests pour Map&lt;TTarget&gt;(object source) avec des collections.
/// Vérifie que mapper.Map&lt;IEnumerable&lt;TargetDto&gt;&gt;(listOfSource) fonctionne
/// en inférant le mapping élémentaire à partir du mapping enregistré.
/// </summary>
[TestFixture]
public class MapCollectionInferenceTests
{
    #region Models

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CustomerExcel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    private NovelyMapper _mapper = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new NovelyMapper();
        _mapper.CreateMap<Customer, CustomerExcel>().ReverseMap();
    }

    [Test]
    public void Map_ListToIEnumerable_ShouldInferElementMapping()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        var result = _mapper.Map<IEnumerable<CustomerExcel>>(customers);

        var list = result.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(list, Has.Count.EqualTo(2));
            Assert.That(list[0].Id, Is.EqualTo(1));
            Assert.That(list[0].Name, Is.EqualTo("Alice"));
            Assert.That(list[1].Id, Is.EqualTo(2));
            Assert.That(list[1].Name, Is.EqualTo("Bob"));
        });
    }

    [Test]
    public void Map_ListToListTarget_ShouldInferElementMapping()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Alice" }
        };

        var result = _mapper.Map<List<CustomerExcel>>(customers);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Alice"));
        });
    }

    [Test]
    public void Map_ListToArray_ShouldInferElementMapping()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        var result = _mapper.Map<CustomerExcel[]>(customers);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(2));
            Assert.That(result[0].Name, Is.EqualTo("Alice"));
        });
    }

    [Test]
    public void Map_ReverseMap_ListToIEnumerable_ShouldWork()
    {
        // Utilise le ReverseMap : CustomerExcel → Customer
        var excels = new List<CustomerExcel>
        {
            new() { Id = 1, Name = "FromExcel" }
        };

        var result = _mapper.Map<IEnumerable<Customer>>(excels);

        var list = result.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Name, Is.EqualTo("FromExcel"));
        });
    }

    [Test]
    public void Map_ArrayToIEnumerable_ShouldInferElementMapping()
    {
        var customers = new Customer[]
        {
            new() { Id = 1, Name = "Alice" }
        };

        var result = _mapper.Map<IEnumerable<CustomerExcel>>(customers);

        Assert.That(result.First().Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void Map_EmptyList_ShouldReturnEmptyCollection()
    {
        var customers = new List<Customer>();

        var result = _mapper.Map<IEnumerable<CustomerExcel>>(customers);

        Assert.That(result.Count(), Is.EqualTo(0));
    }

    [Test]
    public void Map_ListToICollectionTarget_ShouldInferElementMapping()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Alice" }
        };

        var result = _mapper.Map<ICollection<CustomerExcel>>(customers);

        Assert.That(result, Has.Count.EqualTo(1));
    }
}
