using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

internal static class PatternRunner
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    public static async Task<IReadOnlyList<string>> IdsAsync(string source, string assemblyName = "Shop.Api", string path = "Sample.cs")
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(compileErrors);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new HttpClientFactoryAnalyzer(),
            new TypedOptionsAnalyzer(),
            new DeclaredAuthorizationAnalyzer(),
            new StaticCacheAnalyzer(),
            new SuppressionJustificationAnalyzer(),
            new RecordContractAnalyzer(),
            new CompositionRootAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(d => d.Id).ToList();
    }
}
