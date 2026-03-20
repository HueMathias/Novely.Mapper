using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper.Tests;

[TestFixture]
public class MultipleProfileTests
{
    private class UserEntity
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
    }

    private class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
    }

    private class OrderEntity
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
    }

    private class OrderDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
    }

    public class UserProfile : NovelyMapperProfile
    {
        public UserProfile(NovelyMapper mapper) : base(mapper)
        {
            CreateMap<UserEntity, UserDto>();
        }
    }

    public class OrderProfile : NovelyMapperProfile
    {
        public OrderProfile(NovelyMapper mapper) : base(mapper)
        {
            CreateMap<OrderEntity, OrderDto>();
        }
    }

    [Test]
    public void UseNovelyMapper_WithMultipleTypes_ShouldRegisterAllMappings()
    {
        var services = new ServiceCollection();
        services.UseNovelyMapper(typeof(UserProfile), typeof(OrderProfile));
        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<INovelyMapper>();

        var user = mapper.Map<UserEntity, UserDto>(new UserEntity { Id = 1, Username = "alice" });
        Assert.That(user.Username, Is.EqualTo("alice"));

        var order = mapper.Map<OrderEntity, OrderDto>(new OrderEntity { OrderId = 100, Amount = 42.5m });
        Assert.That(order.Amount, Is.EqualTo(42.5m));

        provider.Dispose();
    }

    [Test]
    public void UseNovelyMapper_WithAssemblyScan_ShouldFindProfiles()
    {
        var services = new ServiceCollection();
        services.UseNovelyMapper(typeof(MultipleProfileTests).Assembly);
        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<INovelyMapper>();

        // Les profils UserProfile et OrderProfile devraient être détectés
        var user = mapper.Map<UserEntity, UserDto>(new UserEntity { Id = 1, Username = "bob" });
        Assert.That(user.Username, Is.EqualTo("bob"));

        provider.Dispose();
    }

    [Test]
    public void UseNovelyMapper_WithInvalidType_ShouldThrow()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(
            () => services.UseNovelyMapper(typeof(string)));
    }
}
