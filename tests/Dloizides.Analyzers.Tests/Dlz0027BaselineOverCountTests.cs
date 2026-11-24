using System.Globalization;

namespace Dloizides.Analyzers.Tests;

public class Dlz0027BaselineOverCountTests
{
    private const string Dlz0016 = "DLZ0016";
    private const string Dlz0017 = "DLZ0017";
    private const string Dlz0018 = "DLZ0018";
    private const string Dlz0027 = "DLZ0027";
    private const string SourcePath = "/repo/src/Sample.cs";
    private const string LinqBaselinePath = "/repo/linq-syntax-baseline.txt";
    private const string MemberOrderBaselinePath = "/repo/member-order-baseline.txt";
    private const string FileSizeBaselinePath = "/repo/file-size-baseline.txt";
    private const string TwoQueries =
        "using System.Linq;\npublic sealed class Sample\n{\n    public object First(int[] n) => from x in n select x;\n    public object Second(int[] n) => from x in n select x;\n}\n";
    private const string TwoMisorders =
        "public class Sample { private int a; public int b; private void C() { } public void D() { } }";

    [Fact]
    public async Task Analyze_WithMoreQueriesThanListedLinqCount_ReportsEveryQueryAndOneDlz0027()
    {
        var baseline = new InMemoryAdditionalText(LinqBaselinePath, "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0016, Dlz0016, Dlz0027 }, ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Analyze_WithMoreMisordersThanListedMemberOrderCount_ReportsEveryMisorderAndOneDlz0027()
    {
        var baseline = new InMemoryAdditionalText(MemberOrderBaselinePath, "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), TwoMisorders, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0017, Dlz0017, Dlz0027 }, ids.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("src/Other.cs 5")]
    public async Task Analyze_WithUnlistedFileOverZero_ReportsNoDlz0027(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, new InMemoryAdditionalText(LinqBaselinePath, baseline));

        Assert.Equal(new[] { Dlz0016, Dlz0016 }, ids);
    }

    [Theory]
    [InlineData("src/Sample.cs 2")]
    [InlineData("src/Sample.cs 0")]
    public async Task Analyze_WithMisordersAtOrUnlistedCount_ReportsNoDlz0027(string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), TwoMisorders, SourcePath, new InMemoryAdditionalText(MemberOrderBaselinePath, baseline));

        Assert.DoesNotContain(Dlz0027, ids);
    }

    [Fact]
    public async Task Analyze_WithBaselinedFileOverItsLines_ReportsOnlyDlz0018()
    {
        var source = "public sealed class Sample\n{\n" + new string('\n', 318) + "}\n";

        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), source, SourcePath, new InMemoryAdditionalText(FileSizeBaselinePath, "src/Sample.cs 320"));

        Assert.Equal(new[] { Dlz0018 }, ids);
    }

    [Fact]
    public async Task Analyze_WithOneQueryOverListedCount_NamesHowManyToFix()
    {
        var baseline = new InMemoryAdditionalText(LinqBaselinePath, "src/Sample.cs 1");

        var diagnostics = await HouseRuleRunner.RunAsync(new LinqMethodSyntaxAnalyzer(), TwoQueries, SourcePath, baseline);

        Assert.Equal(
            "'Sample.cs' has 2 DLZ0016 violations but linq-syntax-baseline.txt allows 1: fix 1 of those reported, most likely the ones in your change",
            Assert.Single(diagnostics, d => d.Id == Dlz0027).GetMessage(CultureInfo.InvariantCulture));
    }
}
