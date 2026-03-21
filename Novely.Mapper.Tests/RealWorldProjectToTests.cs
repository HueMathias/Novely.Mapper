using Novely.Mapper;
using NUnit.Framework;

namespace Novely.Mapper.Tests;

/// <summary>
/// Reproduit le scénario réel : entité EF Orders avec navigation properties
/// circulaires (Orders → Customers → ICollection&lt;Orders&gt;) projetée vers
/// un DTO Order (héritage OrderSimple → Order) via ProjectTo.
/// </summary>
[TestFixture]
public class RealWorldProjectToTests
{
    #region Entités EF (simulent les tables DB)

    public class Customers
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public virtual ICollection<Orders> Orders { get; set; } = [];       // ← cycle : Customers → Orders
        public virtual ICollection<Contacts> Contacts { get; set; } = [];
    }

    public class Contacts
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int CustomerId { get; set; }
        public virtual Customers? Customer { get; set; }                    // ← cycle : Contacts → Customers
    }

    public class Suppliers
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public virtual ICollection<Orders> Orders { get; set; } = [];       // ← cycle : Suppliers → Orders
    }

    public class OrderStatus
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
    }

    public class Orders
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string? FolderNumber { get; set; }
        public int? BillNumber { get; set; }
        public bool IsBilled { get; set; }
        public DateTime? BillingDate { get; set; }
        public decimal? PercentCommission { get; set; }
        public decimal? Commission { get; set; }
        public DateTime? SettlementDate { get; set; }
        public DateTime Inserted { get; set; } = DateTime.Now;
        public DateTime? LastUpdated { get; set; }
        public int StatusId { get; set; }
        public int SupplierId { get; set; }
        public int CustomerId { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? ResponsibleId { get; set; }
        public string? Comment { get; set; }

        // Navigation properties (style EF Core)
        public virtual OrderStatus Status { get; set; } = null!;
        public virtual Suppliers Supplier { get; set; } = null!;
        public virtual Customers Customer { get; set; } = null!;            // ← Customers a ICollection<Orders> = cycle
    }

    #endregion

    #region DTOs (ce que l'application consomme)

    public class CustomerSimple
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Email { get; set; }
    }

    public class SupplierDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
    }

    public class OrderSimple
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string? FolderNumber { get; set; }
        public int? BillNumber { get; set; }
        public bool IsBilled { get; set; }
        public DateTime? BillingDate { get; set; }
        public decimal? PercentCommission { get; set; }
        public decimal? Commission { get; set; }
        public DateTime? SettlementDate { get; set; }
        public DateTime Inserted { get; set; }
        public DateTime? LastUpdated { get; set; }
        public int StatusId { get; set; }
        public int SupplierId { get; set; }
        public int CustomerId { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? ResponsibleId { get; set; }
        public string? Comment { get; set; }
    }

    public class Order : OrderSimple
    {
        public CustomerSimple? Customer { get; set; }
        public SupplierDto? Supplier { get; set; }
    }

    #endregion

    private NovelyMapper _mapper = null!;

    [SetUp]
    public void SetUp()
    {
        _mapper = new NovelyMapper();
        _mapper.CreateMap<Customers, CustomerSimple>();
        _mapper.CreateMap<Suppliers, SupplierDto>();
        _mapper.CreateMap<Orders, Order>().ReverseMap();
    }

    [Test]
    public void ProjectTo_OrdersToOrder_ShouldNotStackOverflow()
    {
        // Simule db.Set<Orders>().ProjectTo<Orders, Order>(mapper)
        var orders = new List<Orders>
        {
            new()
            {
                Id = 1,
                Total = 1500.50m,
                FolderNumber = "F-001",
                Inserted = new DateTime(2025, 1, 15),
                StatusId = 1,
                SupplierId = 10,
                CustomerId = 100,
                PercentCommission = 5.5m,
                Commission = 82.53m,
                Customer = new Customers
                {
                    Id = 100,
                    Name = "Acme Corp",
                    Email = "contact@acme.com"
                },
                Supplier = new Suppliers { Id = 10, CompanyName = "FourniTech" },
                Status = new OrderStatus { Id = 1, Label = "Hot Quote" }
            },
            new()
            {
                Id = 2,
                Total = 750m,
                Inserted = new DateTime(2025, 2, 20),
                StatusId = 1,
                SupplierId = 20,
                CustomerId = 200,
                BillNumber = 42,
                IsBilled = true,
                BillingDate = new DateTime(2025, 3, 1),
                Customer = new Customers { Id = 200, Name = "Beta Inc" },
                Supplier = new Suppliers { Id = 20, CompanyName = "SupplyMax" },
                Status = new OrderStatus { Id = 1, Label = "Hot Quote" }
            }
        };

        // ProjectTo (simule IQueryable EF)
        var result = orders.AsQueryable()
            .ProjectTo<Orders, Order>(_mapper)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));

            // Première commande
            Assert.That(result[0].Id, Is.EqualTo(1));
            Assert.That(result[0].Total, Is.EqualTo(1500.50m));
            Assert.That(result[0].FolderNumber, Is.EqualTo("F-001"));
            Assert.That(result[0].PercentCommission, Is.EqualTo(5.5m));
            Assert.That(result[0].Commission, Is.EqualTo(82.53m));
            Assert.That(result[0].Customer, Is.Not.Null);
            Assert.That(result[0].Customer!.Name, Is.EqualTo("Acme Corp"));
            Assert.That(result[0].Customer.Email, Is.EqualTo("contact@acme.com"));
            Assert.That(result[0].Supplier, Is.Not.Null);
            Assert.That(result[0].Supplier!.CompanyName, Is.EqualTo("FourniTech"));

            // Deuxième commande
            Assert.That(result[1].Id, Is.EqualTo(2));
            Assert.That(result[1].Total, Is.EqualTo(750m));
            Assert.That(result[1].BillNumber, Is.EqualTo(42));
            Assert.That(result[1].IsBilled, Is.True);
            Assert.That(result[1].BillingDate, Is.EqualTo(new DateTime(2025, 3, 1)));
            Assert.That(result[1].Customer!.Name, Is.EqualTo("Beta Inc"));
        });
    }

    [Test]
    public void ProjectTo_OrdersToOrder_WithNullNavigations_ShouldHandleGracefully()
    {
        var orders = new List<Orders>
        {
            new()
            {
                Id = 3,
                Total = 100m,
                Inserted = DateTime.Now,
                StatusId = 2,
                SupplierId = 0,
                CustomerId = 0,
                Customer = null!,
                Supplier = null!,
                Status = null!
            }
        };

        var result = orders.AsQueryable()
            .ProjectTo<Orders, Order>(_mapper)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(3));
            Assert.That(result[0].Customer, Is.Null);
            Assert.That(result[0].Supplier, Is.Null);
        });
    }

    [Test]
    public void Map_OrdersToOrder_SingleObject_ShouldWork()
    {
        var entity = new Orders
        {
            Id = 5,
            Total = 999.99m,
            FolderNumber = "F-005",
            PercentCommission = 10m,
            Inserted = new DateTime(2025, 6, 1),
            StatusId = 3,
            SupplierId = 30,
            CustomerId = 300,
            Comment = "Urgent",
            Customer = new Customers { Id = 300, Name = "Gamma SA", Email = "info@gamma.com" },
            Supplier = new Suppliers { Id = 30, CompanyName = "MegaSupply" },
            Status = new OrderStatus { Id = 3, Label = "Confirmed" }
        };

        var result = _mapper.Map<Orders, Order>(entity);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(5));
            Assert.That(result.Total, Is.EqualTo(999.99m));
            Assert.That(result.PercentCommission, Is.EqualTo(10m));
            Assert.That(result.Comment, Is.EqualTo("Urgent"));
            Assert.That(result.Customer, Is.Not.Null);
            Assert.That(result.Customer!.Name, Is.EqualTo("Gamma SA"));
            Assert.That(result.Supplier, Is.Not.Null);
            Assert.That(result.Supplier!.CompanyName, Is.EqualTo("MegaSupply"));
        });
    }

    [Test]
    public void Map_OrdersToOrder_ViaMapTTarget_Collection_ShouldWork()
    {
        // Simule : mapper.Map<IEnumerable<Order>>(db.Orders.ToList())
        var entities = new List<Orders>
        {
            new()
            {
                Id = 10, Total = 500m, Inserted = DateTime.Now,
                StatusId = 1, SupplierId = 1, CustomerId = 1,
                Customer = new Customers { Id = 1, Name = "Client A" },
                Supplier = new Suppliers { Id = 1, CompanyName = "Fournisseur A" },
                Status = new OrderStatus { Id = 1, Label = "New" }
            }
        };

        var result = _mapper.Map<IEnumerable<Order>>(entities);
        var list = result.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Id, Is.EqualTo(10));
            Assert.That(list[0].Customer!.Name, Is.EqualTo("Client A"));
        });
    }

    [Test]
    public void ProjectTo_WithCircularCustomerOrders_ShouldNotTimeout()
    {
        // Le scénario du bug : Customers.Orders (ICollection<Orders>) → Orders.Customer (Customers) = cycle
        // Quand Customers a des Orders qui référencent le même Customer, ProjectTo ne doit pas boucler
        var customer = new Customers { Id = 1, Name = "CycleTest" };
        var order = new Orders
        {
            Id = 1, Total = 100m, Inserted = DateTime.Now,
            StatusId = 1, SupplierId = 1, CustomerId = 1,
            Customer = customer,
            Supplier = new Suppliers { Id = 1, CompanyName = "Sup" },
            Status = new OrderStatus { Id = 1, Label = "X" }
        };
        customer.Orders.Add(order);

        var expr = _mapper.GetProjectionExpression<Orders, Order>();
        var func = expr.Compile();
        var result = func(order);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Customer, Is.Not.Null);
            Assert.That(result.Customer!.Name, Is.EqualTo("CycleTest"));
        });
    }
}
