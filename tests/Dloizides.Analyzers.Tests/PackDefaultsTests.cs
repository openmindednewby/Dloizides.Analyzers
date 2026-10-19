using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

public class PackDefaultsTests
{
    private const string AwaitInMisnamedFile = "using System.Threading.Tasks;\npublic sealed class Worker\n{\n    public async Task RunAsync() => await Task.Delay(1);\n}\n";

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

    private static async Task<IReadOnlyList<string>> MeziantouIdsAsync(ImmutableDictionary<string, ReportDiagnostic> severities)
    {
        var tree = CSharpSyntaxTree.ParseText(AwaitInMisnamedFile, new CSharpParseOptions(LanguageVersion.Latest), "Sample.cs");
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithSpecificDiagnosticOptions(severities);
        var compilation = CSharpCompilation.Create("Shop.Api", new[] { tree }, PatternRunner.References, options);
        var diagnostics = await compilation.WithAnalyzers(PackRuleFiles.Analyzers("MeziantouPath")).GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(diagnostic => diagnostic.Id).ToList();
    }
}
