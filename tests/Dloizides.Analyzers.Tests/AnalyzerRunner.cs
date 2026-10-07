using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

internal static class AnalyzerRunner
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    public static async Task<IReadOnlyList<string>> IdsAsync(
        string source,
        string path = "Sample.cs",
        DocumentationMode mode = DocumentationMode.Parse,
        string assemblyName = "Sample",
        ReportDiagnostic dlz0005 = ReportDiagnostic.Default)
    {
        var diagnostics = await RunAsync(source, path, mode, assemblyName, dlz0005);
        return diagnostics.Select(d => d.Id).ToList();
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        string path,
        DocumentationMode mode,
        string assemblyName = "Sample",
        ReportDiagnostic dlz0005 = ReportDiagnostic.Default)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, mode);
        var tree = CSharpSyntaxTree.ParseText(source, options, path);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { ["DLZ0005"] = dlz0005 }));
        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(compileErrors);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NoCommentsAnalyzer(), new DocCommentAnalyzer(), new TestNameAnalyzer(), new FeatureFolderAnalyzer());
        var all = await compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync();
        return all.Where(d => d.Id.StartsWith("DLZ", StringComparison.Ordinal) || d.Id.StartsWith("AD", StringComparison.Ordinal))
            .ToImmutableArray();
    }
}
