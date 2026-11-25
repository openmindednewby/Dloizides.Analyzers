namespace Dloizides.Analyzers.Tests;

public class Dlz0032PairByKeyTests
{
    private const string Dlz0032 = "DLZ0032";
    private const string Dlz0019 = "DLZ0019";
    private const string ZipWithSelector = "using System.Linq; namespace Sample { public static class Pairs { public static int[] Sum(int[] a, int[] b) => a.Zip(b, (x, y) => x + y).ToArray(); } }";

    [Theory]
    [InlineData(ZipWithSelector)]
    [InlineData("using System.Linq; namespace Sample { public static class Pairs { public static int Count(int[] a, string[] b) => a.Zip(b).Count(); } }")]
    [InlineData("using System.Linq; namespace Sample { public static class Pairs { public static int Count(int[] a, string[] b) => Enumerable.Zip(a, b).Count(); } }")]
    public async Task Analyze_WithEnumerableZip_ReportsDlz0032(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new PairByKeyAnalyzer(), source);

        Assert.Equal(new[] { Dlz0032 }, ids);
    }

    [Theory]
    [InlineData("namespace Sample { public sealed class Archive { public int Zip(int level) => level; public int Pack() => Zip(3); } }")]
    [InlineData("using System.Linq; namespace Sample { public static class Pairs { public static int[] Keyed(int[] a) => a.ToDictionary(x => x).Values.ToArray(); } }")]
    public async Task Analyze_WithOtherZipOrKeyedPairing_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new PairByKeyAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithZip_ReportsAtTheZipName()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new PairByKeyAnalyzer(), ZipWithSelector, HouseRuleRunner.SamplePath);

        var span = Assert.Single(diagnostics).Location.SourceSpan;
        Assert.Equal("Zip", ZipWithSelector.Substring(span.Start, span.Length));
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/pair-by-key-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new PairByKeyAnalyzer(), ZipWithSelector, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/pair-by-key-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new PairByKeyAnalyzer(), ZipWithSelector, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
