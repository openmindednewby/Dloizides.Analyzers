namespace Dloizides.Analyzers.Tests;

public class Dlz0036NullForgivingTests
{
    private const string Dlz0036 = "DLZ0036";
    private const string Dlz0019 = "DLZ0019";
    private const string Forgiven = "#nullable enable\nnamespace Sample { public sealed class Job { public int Length(string? name) => name!.Length; } }";
    private const string Checked = "#nullable enable\nnamespace Sample { public sealed class Job { public int Length(string? name) => name?.Length ?? 0; public bool Not(bool flag) => !flag; } }";

    [Fact]
    public async Task Analyze_WithNullForgivingOperator_ReportsDlz0036AtTheOperator()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new NullForgivingAnalyzer(), Forgiven, HouseRuleRunner.SamplePath);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(Dlz0036, diagnostic.Id);
        Assert.Equal("!", Forgiven.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public async Task Analyze_WithNullCheckAndLogicalNot_ReportsNothing()
    {
        var ids = await HouseRuleRunner.IdsAsync(new NullForgivingAnalyzer(), Checked);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithNullForgivingInMigrations_ReportsNothing()
    {
        var ids = await HouseRuleRunner.IdsAsync(new NullForgivingAnalyzer(), Forgiven, "/repo/src/Migrations/20261125_Init.cs");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/null-forgiving-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new NullForgivingAnalyzer(), Forgiven, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/null-forgiving-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new NullForgivingAnalyzer(), Forgiven, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
