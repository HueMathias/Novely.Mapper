using Novely.Mapper;
using NUnit.Framework;

namespace Novely.Mapper.Tests;

[TestFixture]
public class NullableConventionTests
{
    #region Models

    public class SourceWithNullableInt
    {
        public int? Value { get; set; }
        public string Name { get; set; } = "";
    }

    public class TargetWithInt
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
    }

    public class SourceWithInt
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
    }

    public class TargetWithNullableInt
    {
        public int? Value { get; set; }
        public string Name { get; set; } = "";
    }

    public class SourceMultiNullable
    {
        public int? Age { get; set; }
        public DateTime? BirthDate { get; set; }
        public decimal? Amount { get; set; }
    }

    public class TargetMultiNonNullable
    {
        public int Age { get; set; }
        public DateTime BirthDate { get; set; }
        public decimal Amount { get; set; }
    }

    public record RecordWithNullable(int? Value, string Name);
    public record RecordWithNonNullable(int Value, string Name);

    #endregion

    [Test]
    public void Convention_NullableToNonNullable_WithValue_ShouldMap()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullableInt, TargetWithInt>();

        var source = new SourceWithNullableInt { Value = 42, Name = "Test" };
        var result = mapper.Map<SourceWithNullableInt, TargetWithInt>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(42));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void Convention_NullableToNonNullable_WithNull_ShouldUseDefault()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullableInt, TargetWithInt>();

        var source = new SourceWithNullableInt { Value = null, Name = "Test" };
        var result = mapper.Map<SourceWithNullableInt, TargetWithInt>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(0));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void Convention_NonNullableToNullable_ShouldMap()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithInt, TargetWithNullableInt>();

        var source = new SourceWithInt { Value = 42, Name = "Test" };
        var result = mapper.Map<SourceWithInt, TargetWithNullableInt>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(42));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void Convention_MultipleNullableToNonNullable_ShouldMapAll()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceMultiNullable, TargetMultiNonNullable>();

        var source = new SourceMultiNullable
        {
            Age = 30,
            BirthDate = new DateTime(1995, 1, 1),
            Amount = 99.99m
        };
        var result = mapper.Map<SourceMultiNullable, TargetMultiNonNullable>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Age, Is.EqualTo(30));
            Assert.That(result.BirthDate, Is.EqualTo(new DateTime(1995, 1, 1)));
            Assert.That(result.Amount, Is.EqualTo(99.99m));
        });
    }

    [Test]
    public void Convention_NullableToNonNullable_OnMapToExisting_ShouldMap()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<SourceWithNullableInt, TargetWithInt>();

        var source = new SourceWithNullableInt { Value = 42, Name = "Test" };
        var target = new TargetWithInt { Value = 0, Name = "" };
        mapper.Map(source, target);

        Assert.Multiple(() =>
        {
            Assert.That(target.Value, Is.EqualTo(42));
            Assert.That(target.Name, Is.EqualTo("Test"));
        });
    }

    [Test]
    public void Convention_NullableToNonNullable_Record_ShouldMapViaConstructor()
    {
        var mapper = new NovelyMapper();
        mapper.CreateMap<RecordWithNullable, RecordWithNonNullable>();

        var source = new RecordWithNullable(42, "Test");
        var result = mapper.Map<RecordWithNullable, RecordWithNonNullable>(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(42));
            Assert.That(result.Name, Is.EqualTo("Test"));
        });
    }
}
