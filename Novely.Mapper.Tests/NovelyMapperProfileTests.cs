#pragma warning disable CS0618 // ForMember obsolète

using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper.Tests;

public class NovelyMapperProfileTests
{
    #region Data
    public class TestProfile : NovelyMapperProfile
    {
        public TestProfile(NovelyMapper mapper) : base(mapper)
        {
            CreateMap<EntityA, EntityB>()
                .ForMember(dest => dest.Nom, src => src.Name);
        }
    }

    private class EntityA
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class EntityB
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;
    }
    #endregion

    private ServiceProvider provider = null!;
    private INovelyMapper mapper = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();

        // Enregistrement du mapper avec le profil de test
        services.UseNovelyMapper<TestProfile>();

        // Build du provider
        provider = services.BuildServiceProvider();

        // Récupération du mapper depuis DI
        mapper = provider.GetRequiredService<INovelyMapper>();
    }

    [TearDown]
    public void TearDown()
    {
        // Dispose du ServiceProvider pour libérer les ressources
        provider.Dispose();
    }

    [Test]
    public void Mapper_ShouldBeRegisteredInDI()
    {
        Assert.That(mapper, Is.Not.Null);
    }

    [Test]
    public void Mapper_ShouldMapSingleObject_Correctly()
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
    public void Mapper_ShouldMapListOfObjects_Correctly()
    {
        var listA = new[]
        {
                new EntityA { Id = 1, Name = "Mathias" },
                new EntityA { Id = 2, Name = "Léa" }
            };

        var listB = mapper.Map<EntityA, EntityB>(listA).ToList();

        Assert.That(listB, Has.Count.EqualTo(listA.Length));
        for (int i = 0; i < listA.Length; i++)
        {
            Assert.Multiple(() =>
            {
                Assert.That(listB[i].Id, Is.EqualTo(listA[i].Id));
                Assert.That(listB[i].Nom, Is.EqualTo(listA[i].Name));
            });
        }
    }
}
