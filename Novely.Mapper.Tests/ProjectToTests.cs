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

        Assert.Throws<NovelyMapperException>(
            () => list.AsQueryable().ProjectTo<Source, Target>(mapper).ToList());
    }

    #region Circular reference models

    private class CustomerEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public SupplierEntity? Supplier { get; set; }
    }

    private class SupplierEntity
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public CustomerEntity? Customer { get; set; }
    }

    private class CustomerProjection
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public SupplierProjection? Supplier { get; set; }
    }

    private class SupplierProjection
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public CustomerProjection? Customer { get; set; }
    }

    #endregion

    [Test]
    public void ProjectTo_WithCircularReference_ShouldNotContainRuntimeCall()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<CustomerEntity, CustomerProjection>();
        mapper.CreateMap<SupplierEntity, SupplierProjection>();

        // Doit compiler sans StackOverflow
        var expr = mapper.GetProjectionExpression<CustomerEntity, CustomerProjection>();
        Assert.That(expr, Is.Not.Null);

        // L'expression ne doit pas contenir de MethodCallExpression vers Map
        // (non traduisible en SQL) — vérifier via compilation et exécution
        var func = expr.Compile();
        var customer = new CustomerEntity
        {
            Id = 1,
            Name = "Alice",
            Supplier = new SupplierEntity
            {
                Id = 10,
                CompanyName = "Acme",
                Customer = null
            }
        };

        var result = func(customer);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Supplier, Is.Not.Null);
            Assert.That(result.Supplier!.CompanyName, Is.EqualTo("Acme"));
            // Back-reference circulaire = null en projection
            Assert.That(result.Supplier.Customer, Is.Null);
        });
    }

    [Test]
    public void ProjectTo_WithCircularReference_ShouldProjectCollection()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<CustomerEntity, CustomerProjection>();
        mapper.CreateMap<SupplierEntity, SupplierProjection>();

        var customers = new List<CustomerEntity>
        {
            new() { Id = 1, Name = "Alice", Supplier = new SupplierEntity { Id = 10, CompanyName = "Acme" } },
            new() { Id = 2, Name = "Bob", Supplier = null }
        };

        var result = customers.AsQueryable()
            .ProjectTo<CustomerEntity, CustomerProjection>(mapper)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Supplier, Is.Not.Null);
            Assert.That(result[0].Supplier!.Customer, Is.Null);
            Assert.That(result[1].Supplier, Is.Null);
        });
    }
}
