using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

public class ReadOnlyParameterPackTests
{
    private const string PrivateForeach = "using System.Collections.Generic; namespace Sample { public sealed class Job { public int Run() => Sum(new List<int>()); private int Sum(List<int> rows) { var total = 0; foreach (var row in rows) total += row; return total; } } }";
    private const string PrivateCount = "using System.Collections.Generic; namespace Sample { public sealed class Job { public int Run() => First(new List<int>()); private int First(List<int> rows) => rows.Count + rows[0]; } }";

    [Fact]
    public void ShippedSeverities_WithReadOnlyParamRule_IsWarning()
    {
        var severities = PackRuleFiles.ShippedSeverities();

        var severity = severities["S3242"];

        Assert.Equal(ReportDiagnostic.Warn, severity);
    }

    [Fact]
    public async Task Analyze_WithPrivateListParameterOnlyIterated_SonarS3242ReportsNothing()
    {
        var ids = await SonarIdsAsync(PrivateForeach);

        Assert.DoesNotContain("S3242", ids);
    }

    [Fact]
    public async Task Analyze_WithPrivateListParameterCountedAndIndexed_SonarS3242Reports()
    {
        var ids = await SonarIdsAsync(PrivateCount);

        Assert.Contains("S3242", ids);
    }

    private static async Task<IReadOnlyList<string>> SonarIdsAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), "/repo/src/Sample.cs");
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithSpecificDiagnosticOptions(PackRuleFiles.ShippedSeverities());
        var compilation = CSharpCompilation.Create("Shop.Api", new[] { tree }, PatternRunner.References, options);
        var diagnostics = await compilation
            .WithAnalyzers(PackRuleFiles.Analyzers("SonarPath"), new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(diagnostic => diagnostic.Id).ToList();
    }
}
