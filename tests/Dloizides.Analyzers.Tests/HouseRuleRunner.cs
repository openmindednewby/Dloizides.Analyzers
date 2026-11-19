using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Dloizides.Analyzers.Tests;

internal static class HouseRuleRunner
{
    public const string SamplePath = "/repo/src/Sample.cs";

    public static async Task<IReadOnlyList<string>> IdsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string path = SamplePath,
        params AdditionalText[] additionalFiles)
    {
        var diagnostics = await RunAsync(analyzer, source, path, additionalFiles);
        return diagnostics.Select(diagnostic => diagnostic.Id).ToList();
    }

    public static Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string path,
        params AdditionalText[] additionalFiles)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path);
        var compilation = CSharpCompilation.Create(
            "Shop.Api",
            new[] { tree },
            PatternRunner.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var options = new AnalyzerOptions(ImmutableArray.Create(additionalFiles));
        return compilation.WithAnalyzers(ImmutableArray.Create(analyzer), options).GetAnalyzerDiagnosticsAsync();
    }
}

internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText text;

    public InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        text = SourceText.From(content);
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default) => text;
}
