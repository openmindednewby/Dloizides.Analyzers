namespace Dloizides.Analyzers.Tests;

public class Dlz0029SealedRecordTests
{
    private const string Dlz0029 = "DLZ0029";
    private const string Dlz0019 = "DLZ0019";
    private const string Dlz0027 = "DLZ0027";
    private const string OpenRecord = "namespace Sample { public record Price(decimal Amount); }";

    [Theory]
    [InlineData(OpenRecord)]
    [InlineData("namespace Sample { public record class Price(decimal Amount); }")]
    [InlineData("namespace Sample { internal record Price { public decimal Amount { get; init; } } }")]
    [InlineData("namespace Sample { public partial record Price(decimal Amount); public partial record Price { public decimal Twice => Amount * 2; } }")]
    [InlineData("namespace Sample { public sealed class Outer { public record Inner(int Value); } }")]
    [InlineData("namespace Sample { public record Box<T>(T Value); }")]
    public async Task Analyze_WithRecordNothingDerivesFrom_ReportsDlz0029(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), source);

        Assert.Equal(new[] { Dlz0029 }, ids);
    }

    [Theory]
    [InlineData("namespace Sample { public sealed record Price(decimal Amount); }")]
    [InlineData("namespace Sample { public abstract record Shape; public sealed record Circle(double Radius) : Shape; }")]
    [InlineData("namespace Sample { public record Shape(string Name); public sealed record Circle(double Radius) : Shape(\"circle\"); }")]
    [InlineData("namespace Sample { public record Box<T>(T Value); public sealed record IntBox(int Value) : Box<int>(Value); }")]
    [InlineData("namespace Sample { public record struct Point(int X, int Y); }")]
    [InlineData("namespace Sample { public readonly record struct Point(int X, int Y); }")]
    [InlineData("namespace Sample { public class Plain { } }")]
    public async Task Analyze_WithSealedAbstractDerivedOrStructRecord_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTwoLevelHierarchy_ReportsOnlyTheLeaf()
    {
        var source = "namespace Sample { public abstract record Event; public record Created(int Id) : Event; public record Renamed(int Id, string Name) : Created(Id); public record Leaf(int Id) : Event; }";

        var diagnostics = await HouseRuleRunner.RunAsync(new SealedRecordAnalyzer(), source, HouseRuleRunner.SamplePath);

        Assert.Equal(new[] { "Renamed", "Leaf" }, diagnostics.Select(d => source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Analyze_WithGeneratedPath_ReportsNothing()
    {
        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), OpenRecord, "/repo/src/obj/Debug/Sample.cs");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/sealed-record-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), OpenRecord, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/sealed-record-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), OpenRecord, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAboveBaselineCount_ReportsDlz0029AndDlz0027()
    {
        var baseline = new InMemoryAdditionalText("/repo/sealed-record-baseline.txt", "src/Sample.cs 1");
        var source = "namespace Sample { public record Price(decimal Amount); public record Tax(decimal Rate); }";

        var ids = await HouseRuleRunner.IdsAsync(new SealedRecordAnalyzer(), source, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0027, Dlz0029, Dlz0029 }, ids.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
