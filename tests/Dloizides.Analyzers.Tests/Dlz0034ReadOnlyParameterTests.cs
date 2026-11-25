namespace Dloizides.Analyzers.Tests;

public class Dlz0034ReadOnlyParameterTests
{
    private const string Dlz0034 = "DLZ0034";
    private const string Dlz0019 = "DLZ0019";
    private const string ForeachOnly = "using System.Collections.Generic; namespace Sample { public sealed class Job { public int Run() => Sum(new List<int>()); private int Sum(List<int> rows) { var total = 0; foreach (var row in rows) total += row; return total; } } }";

    [Theory]
    [InlineData(ForeachOnly)]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public int First(List<int> rows) => rows.Count > 0 ? rows[0] : 0; } }")]
    [InlineData("using System.Collections.Generic; using System.Linq; namespace Sample { public sealed class Job { public int Positive(List<int> rows) => rows.Where(row => row > 0).Count(); } }")]
    public async Task Analyze_WithListParameterOnlyRead_ReportsDlz0034(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ReadOnlyParameterAnalyzer(), source);

        Assert.Equal(new[] { Dlz0034 }, ids);
    }

    [Theory]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public void Fill(List<int> rows) => rows.Add(1); } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public void Reset(List<int> rows) { rows[0] = 0; } } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public int Pass(List<int> rows) => Keep(rows); private static int Keep(List<int> rows) { rows.Clear(); return 0; } } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public interface ISum { int Sum(List<int> rows); } public sealed class Job : ISum { public int Sum(List<int> rows) => rows.Count; } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public int Sum(IReadOnlyList<int> rows) => rows.Count; } }")]
    public async Task Analyze_WithListMutatedPassedOrContract_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ReadOnlyParameterAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithForeachOnlyParameter_ReportsAtTheParameter()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new ReadOnlyParameterAnalyzer(), ForeachOnly, HouseRuleRunner.SamplePath);

        var span = Assert.Single(diagnostics).Location.SourceSpan;
        Assert.Equal("List<int> rows", ForeachOnly.Substring(span.Start, span.Length));
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/readonly-param-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new ReadOnlyParameterAnalyzer(), ForeachOnly, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
