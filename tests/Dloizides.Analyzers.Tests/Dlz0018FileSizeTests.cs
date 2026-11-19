using System.Globalization;

namespace Dloizides.Analyzers.Tests;

public class Dlz0018FileSizeTests
{
    private const string Dlz0018 = "DLZ0018";
    private const string SourcePath = "/repo/src/Big.cs";
    private const string BaselinePath = "/repo/file-size-baseline.txt";

    [Theory]
    [InlineData(301, "")]
    [InlineData(321, "src/Big.cs 320")]
    [InlineData(301, "src/Other.cs 400")]
    [InlineData(301, "src/Big.cs 250")]
    [InlineData(400, "src/Big.cs not-a-number")]
    public async Task Analyze_WithFileOverItsAllowedLength_ReportsDlz0018(int lines, string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), ClassOf(lines), SourcePath, new InMemoryAdditionalText(BaselinePath, baseline));

        Assert.Equal(new[] { Dlz0018 }, ids);
    }

    [Theory]
    [InlineData(300, "")]
    [InlineData(301, "src/Big.cs 301")]
    [InlineData(330, "src\\Big.cs 330")]
    [InlineData(330, "./src/Big.cs 330")]
    [InlineData(330, "src/Other.cs 400\nsrc/Big.cs\t330")]
    [InlineData(330, "SRC/big.cs 330")]
    public async Task Analyze_WithFileWithinItsAllowedLength_ReportsNothing(int lines, string baseline)
    {
        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), ClassOf(lines), SourcePath, new InMemoryAdditionalText(BaselinePath, baseline));

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithBaselineUnderAnotherFileName_ReportsDlz0018()
    {
        var sizes = new InMemoryAdditionalText("/repo/sizes.txt", "src/Big.cs 330");

        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), ClassOf(330), SourcePath, sizes);

        Assert.Equal(new[] { Dlz0018 }, ids);
    }

    [Theory]
    [InlineData("/repo/src/obj/Debug/Big.cs")]
    [InlineData("/repo/src/Migrations/20260101_Init.cs")]
    [InlineData("/repo/src/Big.g.cs")]
    [InlineData("/repo/src/Big.Designer.cs")]
    public async Task Analyze_WithLongFileInGeneratedPath_ReportsNothing(string path)
    {
        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), ClassOf(400), path);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData(300, 0)]
    [InlineData(301, 1)]
    public async Task Analyze_WithoutFinalNewline_CountsTheLastLine(int lines, int expected)
    {
        var source = ClassOf(lines).TrimEnd('\n');

        var ids = await HouseRuleRunner.IdsAsync(new FileSizeAnalyzer(), source, SourcePath);

        Assert.Equal(expected, ids.Count);
    }

    [Fact]
    public async Task Analyze_WithBaselinedFileThatGrew_NamesTheBaselineLength()
    {
        var baseline = new InMemoryAdditionalText(BaselinePath, "src/Big.cs 320");

        var diagnostics = await HouseRuleRunner.RunAsync(new FileSizeAnalyzer(), ClassOf(321), SourcePath, baseline);

        Assert.Equal(
            "'Big.cs' has 321 lines; keep it at or under 320 (FILE-SIZE-1: a file listed in file-size-baseline.txt may only shrink)",
            Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture));
    }

    private static string ClassOf(int lines) => "public sealed class Big\n{\n" + new string('\n', lines - 3) + "}\n";
}
