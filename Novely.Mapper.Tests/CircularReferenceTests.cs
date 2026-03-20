using Novely.Mapper;
using NUnit.Framework;

namespace Novely.Mapper.Tests;

[TestFixture]
public class CircularReferenceTests
{
    #region Models — Référence circulaire directe (A ↔ B)

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Supplier? Supplier { get; set; }
    }

    public class Supplier
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public Customer? Customer { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public SupplierDto? Supplier { get; set; }
    }

    public class SupplierDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public CustomerDto? Customer { get; set; }
    }

    #endregion

    #region Models — Auto-référence (arbre)

    public class TreeNode
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public TreeNode? Parent { get; set; }
        public List<TreeNode> Children { get; set; } = new();
    }

    public class TreeNodeDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public TreeNodeDto? Parent { get; set; }
        public List<TreeNodeDto> Children { get; set; } = new();
    }

    #endregion

    #region Models — Cycle via chaîne (A → B → C → A)

    public class Prospect
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ProspectSupplier> ProspectSuppliers { get; set; } = new();
    }

    public class ProspectSupplier
    {
        public int SupplierId { get; set; }
        public Prospect? Prospect { get; set; }
    }

    public class ProspectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ProspectSupplierDto> ProspectSuppliers { get; set; } = new();
    }

    public class ProspectSupplierDto
    {
        public int SupplierId { get; set; }
        public ProspectDto? Prospect { get; set; }
    }

    #endregion

    [Test]
    public void CircularReference_Direct_ShouldNotStackOverflow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        var customer = new Customer
        {
            Id = 1,
            Name = "Alice",
            Supplier = new Supplier
            {
                Id = 10,
                CompanyName = "Acme",
                Customer = null // pas de boucle runtime dans les données
            }
        };

        var result = mapper.Map<Customer, CustomerDto>(customer);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Name, Is.EqualTo("Alice"));
            Assert.That(result.Supplier, Is.Not.Null);
            Assert.That(result.Supplier!.Id, Is.EqualTo(10));
            Assert.That(result.Supplier.CompanyName, Is.EqualTo("Acme"));
            Assert.That(result.Supplier.Customer, Is.Null);
        });
    }

    [Test]
    public void CircularReference_Direct_WithActualCycle_ShouldBreakCycleWithNull()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        var customer = new Customer { Id = 1, Name = "Alice" };
        var supplier = new Supplier { Id = 10, CompanyName = "Acme", Customer = customer };
        customer.Supplier = supplier;

        // Ne doit pas StackOverflow — le cycle est cassé : la back-reference retourne null
        var result = mapper.Map<Customer, CustomerDto>(customer);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Supplier, Is.Not.Null);
            Assert.That(result.Supplier!.CompanyName, Is.EqualTo("Acme"));
            // La back-reference vers le customer déjà en cours de mapping retourne null
            Assert.That(result.Supplier.Customer, Is.Null);
        });
    }

    [Test]
    public void CircularReference_NullNavigation_ShouldReturnNull()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        var customer = new Customer { Id = 1, Name = "Bob", Supplier = null };
        var result = mapper.Map<Customer, CustomerDto>(customer);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Supplier, Is.Null);
        });
    }

    [Test]
    public void CircularReference_SelfReferencing_Tree_ShouldNotStackOverflow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<TreeNode, TreeNodeDto>();

        var root = new TreeNode
        {
            Id = 1,
            Label = "Root",
            Children = new List<TreeNode>
            {
                new() { Id = 2, Label = "Child1", Children = new() },
                new()
                {
                    Id = 3,
                    Label = "Child2",
                    Children = new List<TreeNode>
                    {
                        new() { Id = 4, Label = "GrandChild", Children = new() }
                    }
                }
            }
        };

        var result = mapper.Map<TreeNode, TreeNodeDto>(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Label, Is.EqualTo("Root"));
            Assert.That(result.Children, Has.Count.EqualTo(2));
            Assert.That(result.Children[0].Label, Is.EqualTo("Child1"));
            Assert.That(result.Children[1].Children, Has.Count.EqualTo(1));
            Assert.That(result.Children[1].Children[0].Label, Is.EqualTo("GrandChild"));
        });
    }

    [Test]
    public void CircularReference_ViaChain_ProspectSupplier_ShouldNotStackOverflow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Prospect, ProspectDto>();
        mapper.CreateMap<ProspectSupplier, ProspectSupplierDto>();

        var prospect = new Prospect { Id = 1, Name = "Lead" };
        var ps = new ProspectSupplier { SupplierId = 42, Prospect = prospect };
        prospect.ProspectSuppliers.Add(ps);

        var result = mapper.Map<Prospect, ProspectDto>(prospect);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Name, Is.EqualTo("Lead"));
            Assert.That(result.ProspectSuppliers, Has.Count.EqualTo(1));
            Assert.That(result.ProspectSuppliers[0].SupplierId, Is.EqualTo(42));
            // La back-reference vers le prospect déjà en cours de mapping retourne null
            Assert.That(result.ProspectSuppliers[0].Prospect, Is.Null);
        });
    }

    [Test]
    public void CircularReference_MapToExisting_ShouldNotStackOverflow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        var customer = new Customer { Id = 1, Name = "Alice" };
        var supplier = new Supplier { Id = 10, CompanyName = "Acme", Customer = customer };
        customer.Supplier = supplier;

        var target = new CustomerDto();
        mapper.Map(customer, target);

        Assert.Multiple(() =>
        {
            Assert.That(target.Id, Is.EqualTo(1));
            Assert.That(target.Supplier, Is.Not.Null);
            Assert.That(target.Supplier!.CompanyName, Is.EqualTo("Acme"));
            // Back-reference cyclique retourne null
            Assert.That(target.Supplier.Customer, Is.Null);
        });
    }

    [Test]
    public void CircularReference_Collection_ShouldNotStackOverflow()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        var customer = new Customer { Id = 1, Name = "Alice" };
        var supplier = new Supplier { Id = 10, CompanyName = "Acme", Customer = customer };
        customer.Supplier = supplier;

        var customers = new List<Customer> { customer };
        var results = mapper.Map<Customer, CustomerDto>(customers).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Supplier, Is.Not.Null);
            // Back-reference cyclique retourne null
            Assert.That(results[0].Supplier!.Customer, Is.Null);
        });
    }

    [Test]
    public void CircularReference_SameTypeMultipleInstances_ShouldMapEachIndependently()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<Customer, CustomerDto>();
        mapper.CreateMap<Supplier, SupplierDto>();

        // Deux customers distincts (pas de cycle), chacun avec un supplier
        var c1 = new Customer { Id = 1, Name = "Alice", Supplier = new Supplier { Id = 10, CompanyName = "A" } };
        var c2 = new Customer { Id = 2, Name = "Bob", Supplier = new Supplier { Id = 20, CompanyName = "B" } };

        var r1 = mapper.Map<Customer, CustomerDto>(c1);
        var r2 = mapper.Map<Customer, CustomerDto>(c2);

        Assert.Multiple(() =>
        {
            Assert.That(r1.Id, Is.EqualTo(1));
            Assert.That(r1.Supplier!.CompanyName, Is.EqualTo("A"));
            Assert.That(r2.Id, Is.EqualTo(2));
            Assert.That(r2.Supplier!.CompanyName, Is.EqualTo("B"));
        });
    }
}
