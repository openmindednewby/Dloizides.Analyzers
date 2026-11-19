using System.Globalization;

namespace Dloizides.Analyzers.Tests;

public class ViolationBaselineTests
{
    private const string Dlz0016 = "DLZ0016";
    private const string Dlz0017 = "DLZ0017";
    private const string Dlz0019 = "DLZ0019";
    private const string SourcePath = "/repo/src/Sample.cs";
    private const string LinqBaselinePath = "/repo/linq-syntax-baseline.txt";
    private const string MemberOrderBaselinePath = "/repo/member-order-baseline.txt";
    private const string TwoQueries =
        "using System.Linq;\npublic sealed class Sample\n{\n    public object First(int[] n) => from x in n select x;\n    public object Second(int[] n) => from x in n select x;\n}\n";
    private const string TwoMisorders =
        "public class Sample { private int a; public int b; private void C() { } public void D() { } }";

    [Theory]
    [InlineData("")]
    [InlineData("src/Sample.cs 1")]
    [InlineData("src/Other.cs 5")]
    [InlineData("src/Sample.cs many")]
    public async Task Analyze_WithMoreQueriesThanLinqBaseline_ReportsEveryQuery(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, new InMemoryAdditionalText(LinqBaselinePath, baseline));

        Assert.Equal(new[] { Dlz0016, Dlz0016 }, ids);
    }

    [Theory]
    [InlineData("src/Sample.cs 2")]
    [InlineData(@"SRC\sample.cs 2")]
    [InlineData("./src/Sample.cs\t2")]
    [InlineData("src/Other.cs 4\nsrc/Sample.cs 2")]
    public async Task Analyze_WithQueriesAtLinqBaseline_ReportsNothing(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, new InMemoryAdditionalText(LinqBaselinePath, baseline));

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithQueriesListedInAnotherRulesBaseline_ReportsEveryQuery()
    {
        var memberOrderBaseline = new InMemoryAdditionalText(MemberOrderBaselinePath, "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, memberOrderBaseline);

        Assert.Equal(new[] { Dlz0016, Dlz0016 }, ids);
    }

    [Theory]
    [InlineData("")]
    [InlineData("src/Sample.cs 1")]
    [InlineData("src/Other.cs 5")]
    public async Task Analyze_WithMoreMisordersThanMemberOrderBaseline_ReportsEveryMisorder(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), TwoMisorders, SourcePath, new InMemoryAdditionalText(MemberOrderBaselinePath, baseline));

        Assert.Equal(new[] { Dlz0017, Dlz0017 }, ids);
    }

    [Theory]
    [InlineData("src/Sample.cs 2")]
    [InlineData(@"SRC\sample.cs 2")]
    public async Task Analyze_WithMisordersAtMemberOrderBaseline_ReportsNothing(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), TwoMisorders, SourcePath, new InMemoryAdditionalText(MemberOrderBaselinePath, baseline));

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithLinqBaselineAboveActualCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText(LinqBaselinePath, "src/Sample.cs 3");

        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }

    [Fact]
    public async Task Analyze_WithMemberOrderBaselineAboveActualCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText(MemberOrderBaselinePath, "src/Sample.cs 5");

        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), TwoMisorders, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }

    [Theory]
    [InlineData(310, "src/Big.cs 320")]
    [InlineData(280, "src/Big.cs 320")]
    public async Task Analyze_WithFileSizeBaselineAboveActualLines_ReportsDlz0019(int lines, string baseline)
    {
        var source = "public sealed class Big\n{\n" + new string('\n', lines - 3) + "}\n";

        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), source, "/repo/src/Big.cs", new InMemoryAdditionalText("/repo/file-size-baseline.txt", baseline));

        Assert.Equal(new[] { Dlz0019 }, ids);
    }

    [Fact]
    public async Task Analyze_WithStaleLinqEntry_NamesTheActualCount()
    {
        var baseline = new InMemoryAdditionalText(LinqBaselinePath, "src/Sample.cs 3");

        var diagnostics = await HouseRuleRunner.RunAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, baseline);

        Assert.Equal(
            "'Sample.cs' has 2 DLZ0016 violations but linq-syntax-baseline.txt allows 3: lower the entry to 2, or remove it at 0 (shrink-only baseline)",
            Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture));
    }
}
