using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

public class PackDefaultsTests
{
    private const string AwaitInMisnamedFile = "using System.Threading.Tasks;\npublic sealed class Worker\n{\n    public async Task RunAsync() => await Task.Delay(1);\n}\n";
    private const string OpenPublicClass = "namespace Sample { public interface IClock { int Now(); } public class Clock : IClock { public int Now() => 0; } }";
    private const string UnforwardedToken ="using System.Threading;\nusing System.Threading.Tasks;\npublic sealed class Worker\n{\n    public Task RunAsync(CancellationToken cancellationToken) => Task.Delay(1);\n}\n";

    [Theory]
    [InlineData("MA0004")]
    [InlineData("MA0048")]
    public async Task Analyze_WithoutShippedConfig_ReportsPlantedRule(string ruleId)
    {
        var severities = ImmutableDictionary<string, ReportDiagnostic>.Empty;

        var ids = await MeziantouIdsAsync(severities);

        Assert.Contains(ruleId, ids);
    }

    [Theory]
    [InlineData("MA0004")]
    [InlineData("MA0048")]
    public async Task Analyze_WithShippedConfig_DoesNotReportRule(string ruleId)
    {
        var severities = PackRuleFiles.ShippedSeverities();

        var ids = await MeziantouIdsAsync(severities);

        Assert.DoesNotContain(ruleId, ids);
    }

    [Theory]
    [InlineData("MA0040")]
    [InlineData("CA2016")]
    public void ShippedSeverities_WithCancelForwardRule_IsError(string ruleId)
    {
        var severities = PackRuleFiles.ShippedSeverities();

        var severity = severities[ruleId];

        Assert.Equal(ReportDiagnostic.Error, severity);
    }

    [Fact]
    public async Task Analyze_WithShippedConfigAndUnforwardedToken_ReportsMa0040AsError()
    {
        var severities = PackRuleFiles.ShippedSeverities();

        var diagnostics = await MeziantouDiagnosticsAsync(UnforwardedToken, severities);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MA0040" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyze_WithShippedConfigAndOpenPublicClass_ReportsMa0053()
    {
        var options = PackRuleFiles.ShippedOptions();

        var diagnostics = await MeziantouDiagnosticsAsync(OpenPublicClass, PackRuleFiles.ShippedSeverities(), options);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MA0053");
    }

    [Fact]
    public async Task Analyze_WithoutPublicClassOption_DoesNotReportMa0053()
    {
        var options = ImmutableDictionary<string, string>.Empty;

        var diagnostics = await MeziantouDiagnosticsAsync(OpenPublicClass, PackRuleFiles.ShippedSeverities(), options);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MA0053");
    }

    private static async Task<IReadOnlyList<string>> MeziantouIdsAsync(ImmutableDictionary<string, ReportDiagnostic> severities)
    {
        var diagnostics = await MeziantouDiagnosticsAsync(AwaitInMisnamedFile, severities);
        return diagnostics.Select(diagnostic => diagnostic.Id).ToList();
    }

    private static Task<ImmutableArray<Diagnostic>> MeziantouDiagnosticsAsync(
        string source,
        ImmutableDictionary<string, ReportDiagnostic> severities,
        ImmutableDictionary<string, string>? analyzerOptions = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), "Sample.cs");
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithSpecificDiagnosticOptions(severities);
        var compilation = CSharpCompilation.Create("Shop.Api", new[] { tree }, PatternRunner.References, options);
        var provider = new GlobalOptionsProvider(analyzerOptions ?? ImmutableDictionary<string, string>.Empty);
        var analyzerConfig = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, provider);
        return compilation.WithAnalyzers(PackRuleFiles.Analyzers("MeziantouPath"), analyzerConfig).GetAnalyzerDiagnosticsAsync();
    }
}
