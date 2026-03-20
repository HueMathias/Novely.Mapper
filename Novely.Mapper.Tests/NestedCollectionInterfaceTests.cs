namespace Novely.Mapper.Tests;

/// <summary>
/// Tests pour le mapping automatique des propriétés de navigation imbriquées
/// (scénario EF Core : ICollection → IEnumerable, objets imbriqués de types différents, etc.)
/// </summary>
[TestFixture]
public class NestedCollectionInterfaceTests
{
    #region Models

    private class ContactEntity
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
    }

    private class ContactDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
    }

    private class ResponsibleEntity
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
    }

    private class ResponsibleDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
    }

    private class CommentEntity
    {
        public int Id { get; set; }
        public string Text { get; set; } = null!;
    }

    private class CommentDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = null!;
    }

    // EF-style entity with ICollection navigation properties
    private class CustomerEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public ResponsibleEntity Responsible { get; set; } = null!;
        public ICollection<ContactEntity> Contacts { get; set; } = null!;
        public ICollection<CommentEntity> Comments { get; set; } = null!;
    }

    // DTO with IEnumerable / different collection types
    private class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public ResponsibleDto Responsible { get; set; } = null!;
        public IEnumerable<ContactDto> Contacts { get; set; } = null!;
        public List<CommentDto> Comments { get; set; } = null!;
    }

    // ICollection → ICollection
    private class CustomerDtoWithICollection
    {
        public int Id { get; set; }
        public ICollection<ContactDto> Contacts { get; set; } = null!;
    }

    // ICollection → Array
    private class CustomerDtoWithArray
    {
        public int Id { get; set; }
        public ContactDto[] Contacts { get; set; } = null!;
    }

    // Deep nesting: Order → Customer → Contacts
    private class OrderEntity
    {
        public int Id { get; set; }
        public CustomerEntity Customer { get; set; } = null!;
    }

    private class OrderDto
    {
        public int Id { get; set; }
        public CustomerDto Customer { get; set; } = null!;
    }

    #endregion

    [Test]
    public void Map_ICollection_To_IEnumerable_WithRegisteredElementMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<ResponsibleEntity, ResponsibleDto>();
        mapper.CreateMap<CommentEntity, CommentDto>();
        mapper.CreateMap<CustomerEntity, CustomerDto>();

        var source = new CustomerEntity
        {
            Id = 1,
            Name = "Acme",
            Responsible = new ResponsibleEntity { Id = 5, FullName = "John Doe" },
            Contacts = new List<ContactEntity>
            {
                new() { Id = 10, Email = "a@test.com" },
                new() { Id = 20, Email = "b@test.com" }
            },
            Comments = new List<CommentEntity>
            {
                new() { Id = 100, Text = "Premier commentaire" }
            }
        };

        var result = mapper.Map<CustomerEntity, CustomerDto>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Name, Is.EqualTo("Acme"));

            // Nested object mapping
            Assert.That(result.Responsible, Is.Not.Null);
            Assert.That(result.Responsible.FullName, Is.EqualTo("John Doe"));

            // ICollection → IEnumerable
            Assert.That(result.Contacts, Is.Not.Null);
            Assert.That(result.Contacts.Count(), Is.EqualTo(2));
            Assert.That(result.Contacts.First().Email, Is.EqualTo("a@test.com"));

            // ICollection → List
            Assert.That(result.Comments, Is.Not.Null);
            Assert.That(result.Comments, Has.Count.EqualTo(1));
            Assert.That(result.Comments[0].Text, Is.EqualTo("Premier commentaire"));
        });
    }

    [Test]
    public void Map_ICollection_To_ICollection_WithRegisteredElementMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<CustomerEntity, CustomerDtoWithICollection>();

        var source = new CustomerEntity
        {
            Id = 1,
            Name = "Test",
            Responsible = null!,
            Contacts = new List<ContactEntity>
            {
                new() { Id = 10, Email = "x@test.com" }
            },
            Comments = null!
        };

        var result = mapper.Map<CustomerEntity, CustomerDtoWithICollection>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Contacts, Is.Not.Null);
            Assert.That(result.Contacts.Count, Is.EqualTo(1));
            Assert.That(result.Contacts.First().Email, Is.EqualTo("x@test.com"));
        });
    }

    [Test]
    public void Map_ICollection_To_Array_WithRegisteredElementMapping()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<CustomerEntity, CustomerDtoWithArray>();

        var source = new CustomerEntity
        {
            Id = 1,
            Name = "Test",
            Responsible = null!,
            Contacts = new List<ContactEntity>
            {
                new() { Id = 10, Email = "y@test.com" },
                new() { Id = 20, Email = "z@test.com" }
            },
            Comments = null!
        };

        var result = mapper.Map<CustomerEntity, CustomerDtoWithArray>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Contacts, Is.Not.Null);
            Assert.That(result.Contacts, Has.Length.EqualTo(2));
            Assert.That(result.Contacts[1].Email, Is.EqualTo("z@test.com"));
        });
    }

    [Test]
    public void Map_DeepNestedNavigation_ShouldMapRecursively()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<ResponsibleEntity, ResponsibleDto>();
        mapper.CreateMap<CommentEntity, CommentDto>();
        mapper.CreateMap<CustomerEntity, CustomerDto>();
        mapper.CreateMap<OrderEntity, OrderDto>();

        var source = new OrderEntity
        {
            Id = 1,
            Customer = new CustomerEntity
            {
                Id = 2,
                Name = "Deep Corp",
                Responsible = new ResponsibleEntity { Id = 3, FullName = "Jane Smith" },
                Contacts = new List<ContactEntity>
                {
                    new() { Id = 10, Email = "deep@test.com" }
                },
                Comments = new List<CommentEntity>
                {
                    new() { Id = 100, Text = "Deep comment" }
                }
            }
        };

        var result = mapper.Map<OrderEntity, OrderDto>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Customer, Is.Not.Null);
            Assert.That(result.Customer.Name, Is.EqualTo("Deep Corp"));
            Assert.That(result.Customer.Responsible.FullName, Is.EqualTo("Jane Smith"));
            Assert.That(result.Customer.Contacts.Count(), Is.EqualTo(1));
            Assert.That(result.Customer.Contacts.First().Email, Is.EqualTo("deep@test.com"));
            Assert.That(result.Customer.Comments[0].Text, Is.EqualTo("Deep comment"));
        });
    }

    [Test]
    public void Map_NullNavigationProperties_ShouldReturnNull()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<ResponsibleEntity, ResponsibleDto>();
        mapper.CreateMap<CommentEntity, CommentDto>();
        mapper.CreateMap<CustomerEntity, CustomerDto>();

        var source = new CustomerEntity
        {
            Id = 1,
            Name = "Null Corp",
            Responsible = null!,
            Contacts = null!,
            Comments = null!
        };

        var result = mapper.Map<CustomerEntity, CustomerDto>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.Responsible, Is.Null);
            Assert.That(result.Contacts, Is.Null);
            Assert.That(result.Comments, Is.Null);
        });
    }

    [Test]
    public void Map_EmptyCollection_ShouldReturnEmptyCollection()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<ContactEntity, ContactDto>();
        mapper.CreateMap<ResponsibleEntity, ResponsibleDto>();
        mapper.CreateMap<CommentEntity, CommentDto>();
        mapper.CreateMap<CustomerEntity, CustomerDto>();

        var source = new CustomerEntity
        {
            Id = 1,
            Name = "Empty Corp",
            Responsible = new ResponsibleEntity { Id = 1, FullName = "Test" },
            Contacts = new List<ContactEntity>(),
            Comments = new List<CommentEntity>()
        };

        var result = mapper.Map<CustomerEntity, CustomerDto>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Contacts.Count(), Is.EqualTo(0));
            Assert.That(result.Comments, Has.Count.EqualTo(0));
        });
    }
}
