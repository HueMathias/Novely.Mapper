using Microsoft.Extensions.DependencyInjection;

namespace Novely.Mapper.Tests;

[TestFixture]
public class ProfileIsolationTests
{
    private class SourceA
    {
        public string Value { get; set; } = null!;
    }

    private class TargetA
    {
        public string Value { get; set; } = null!;
    }

    private class SourceB
    {
        public int Number { get; set; }
    }

    private class TargetB
    {
        public int Number { get; set; }
    }

    public class ProfileA : NovelyMapperProfile
    {
        public ProfileA(NovelyMapper mapper) : base(mapper)
        {
            CreateMap<SourceA, TargetA>();
        }
    }

    public class ProfileB : NovelyMapperProfile
    {
        public ProfileB(NovelyMapper mapper) : base(mapper)
        {
            CreateMap<SourceB, TargetB>();
        }
    }

    [Test]
    public void TwoProviders_WithDifferentProfiles_ShouldNotInterfere()
    {
        var services1 = new ServiceCollection();
        services1.UseNovelyMapper<ProfileA>();
        var provider1 = services1.BuildServiceProvider();
        var mapper1 = provider1.GetRequiredService<INovelyMapper>();

        var services2 = new ServiceCollection();
        services2.UseNovelyMapper<ProfileB>();
        var provider2 = services2.BuildServiceProvider();
        var mapper2 = provider2.GetRequiredService<INovelyMapper>();

        // mapper1 gère SourceA → TargetA
        var a = mapper1.Map<SourceA, TargetA>(new SourceA { Value = "test" });
        Assert.That(a.Value, Is.EqualTo("test"));

        // mapper2 gère SourceB → TargetB
        var b = mapper2.Map<SourceB, TargetB>(new SourceB { Number = 42 });
        Assert.That(b.Number, Is.EqualTo(42));

        // mapper1 NE gère PAS SourceB → TargetB
        Assert.Throws<InvalidOperationException>(
            () => mapper1.Map<SourceB, TargetB>(new SourceB { Number = 1 }));

        provider1.Dispose();
        provider2.Dispose();
    }

    [Test]
    public void InstanceBasedProfile_ShouldNotUseStaticState()
    {
        // Créer deux mappers indépendants
        var mapper1 = new NovelyMapper();
        var mapper2 = new NovelyMapper();

        // Profil sur mapper1
        _ = new ProfileA(mapper1);

        // Profil sur mapper2
        _ = new ProfileB(mapper2);

        // mapper1 gère SourceA → TargetA mais PAS SourceB → TargetB
        Assert.DoesNotThrow(() => mapper1.Map<SourceA, TargetA>(new SourceA { Value = "ok" }));
        Assert.Throws<InvalidOperationException>(
            () => mapper1.Map<SourceB, TargetB>(new SourceB { Number = 1 }));

        // mapper2 gère SourceB → TargetB mais PAS SourceA → TargetA
        Assert.DoesNotThrow(() => mapper2.Map<SourceB, TargetB>(new SourceB { Number = 42 }));
        Assert.Throws<InvalidOperationException>(
            () => mapper2.Map<SourceA, TargetA>(new SourceA { Value = "fail" }));
    }
}
