namespace Dloizides.Analyzers.Tests;

public class SuppressionJustificationAnalyzerTests
{
    private const string Dlz0010 = "DLZ0010";

    private const string Aliases = """
        using Exempt = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
        using UnconditionalExempt = System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute;

        """;

    private const string Pinger = Aliases + """
        using System.Net.Http;
        public class Pinger
        {
            {0}
            public HttpClient Create() => new HttpClient();
        }
        """;

    [Theory]
    [InlineData("[Exempt(\"Dloizides.Patterns\", \"DLZ0006\")]")]
    [InlineData("[Exempt(\"Dloizides.Patterns\", \"DLZ0006:HttpClientFactory\", Justification = \"\")]")]
    [InlineData("[Exempt(\"Dloizides.Patterns\", \"DLZ0006\", Justification = \"   \")]")]
    [InlineData("[UnconditionalExempt(\"Dloizides.Patterns\", \"DLZ0006\")]")]
    public async Task Analyze_WithPatternSuppressionWithoutJustification_ReportsDlz0010(string attribute)
    {
        var ids = await PatternRunner.IdsAsync(Pinger.Replace("{0}", attribute));

        Assert.Equal(new[] { Dlz0010 }, ids);
    }

    [Fact]
    public async Task Analyze_WithAssemblySuppressionWithoutJustification_ReportsDlz0010()
    {
        var source = Aliases + "[assembly: Exempt(\"Dloizides.Patterns\", \"DLZ0009\", Scope = \"type\", Target = \"~T:Memo\")]\npublic class Memo { }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0010 }, ids);
    }

    [Fact]
    public async Task Analyze_WithJustifiedPatternSuppression_ReportsNothing()
    {
        var attribute = "[Exempt(\"Dloizides.Patterns\", \"DLZ0006\", Justification = \"Kubernetes client needs its own handler\")]";

        var ids = await PatternRunner.IdsAsync(Pinger.Replace("{0}", attribute));

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithOtherRuleSuppressionWithoutJustification_ReportsNothing()
    {
        var source = Aliases + "public class Memo { [Exempt(\"Design\", \"CA1024\")] public int Get() => 1; }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }
}
