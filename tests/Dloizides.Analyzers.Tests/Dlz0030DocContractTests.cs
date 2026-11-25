namespace Dloizides.Analyzers.Tests;

public class Dlz0030DocContractTests
{
    private const string Dlz0030 = "DLZ0030";
    private const string Dlz0019 = "DLZ0019";
    private const string DocumentedContract = "namespace Sample { public interface IClock { /// <summary>Now in UTC.</summary>\n int Now(); } ";
    private const string UndocumentedContract = "namespace Sample { public interface IClock { int Now(); } }";

    [Theory]
    [InlineData(UndocumentedContract)]
    [InlineData("namespace Sample { public interface IClock { int Now { get; } } }")]
    [InlineData("namespace Sample { public interface IClock { event System.EventHandler Ticked; } }")]
    [InlineData("namespace Sample { public sealed class Outer { public interface IClock { int Now(); } } }")]
    [InlineData(DocumentedContract + "public sealed class Clock : IClock { /// <summary>Now in UTC.</summary>\n public int Now() => 0; } }")]
    [InlineData(DocumentedContract + "public sealed class Clock : IClock { /// <summary>Now in UTC.</summary>\n int IClock.Now() => 0; } }")]
    public async Task Analyze_WithMissingContractOrRepeatedSummary_ReportsDlz0030(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new DocContractAnalyzer(), source);

        Assert.Equal(new[] { Dlz0030 }, ids);
    }

    [Theory]
    [InlineData(DocumentedContract + "}")]
    [InlineData(DocumentedContract + "public sealed class Clock : IClock { public int Now() => 0; } }")]
    [InlineData(DocumentedContract + "public sealed class Clock : IClock { /// <inheritdoc/>\n public int Now() => 0; } }")]
    [InlineData("namespace Sample { internal interface IClock { int Now(); } }")]
    [InlineData("namespace Sample { internal sealed class Outer { public interface IClock { int Now(); } } }")]
    [InlineData("namespace Sample { public sealed class Clock { /// <summary>Now in UTC.</summary>\n public int Now() => 0; } }")]
    public async Task Analyze_WithContractOnInterfaceOnly_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new DocContractAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithSummaryOnlyOnImplementation_ReportsTheInterfaceMember()
    {
        var source = "namespace Sample { public interface IClock { int Now(); } public sealed class Clock : IClock { /// <summary>Now in UTC.</summary>\n public int Now() => 0; } }";

        var diagnostics = await HouseRuleRunner.RunAsync(new DocContractAnalyzer(), source, HouseRuleRunner.SamplePath);

        Assert.Equal(new[] { "Public interface member 'Now' has no <summary>: the interface member carries the contract (DOC-CONTRACT-1)" }, diagnostics.Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/doc-contract-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new DocContractAnalyzer(), UndocumentedContract, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/doc-contract-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new DocContractAnalyzer(), UndocumentedContract, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
