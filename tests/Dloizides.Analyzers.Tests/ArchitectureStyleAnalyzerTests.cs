using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

public class ArchitectureStyleAnalyzerTests
{
    private const string Dlz0011 = "DLZ0011";

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    [Theory]
    [InlineData("Clean")]
    [InlineData("CleanVerticalSlice")]
    [InlineData(" Clean ")]
    public async Task Analyze_WithApprovedStyle_ReportsNothing(string style)
    {
        var diagnostics = await RunAsync(style, "public class A { }");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WithoutStyleProperty_ReportsDlz0011NamingItUnset()
    {
        var diagnostics = await RunAsync(null, "public class A { }");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(Dlz0011, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "HouseArchitectureStyle is '(not set)': set it to Clean or CleanVerticalSlice in Directory.Build.props (PAT-STYLE-1)",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WithEmptyStyle_ReportsDlz0011()
    {
        var diagnostics = await RunAsync("", "public class A { }");

        Assert.Equal(new[] { Dlz0011 }, diagnostics.Select(d => d.Id));
    }

    [Theory]
    [InlineData("Onion")]
    [InlineData("cleanverticalslice")]
    [InlineData("VerticalSlice")]
    public async Task Analyze_WithUnknownStyle_ReportsDlz0011NamingTheValue(string style)
    {
        var diagnostics = await RunAsync(style, "public class A { }");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(Dlz0011, diagnostic.Id);
        Assert.Contains($"HouseArchitectureStyle is '{style}':", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WithoutStyleAcrossThreeFiles_ReportsOncePerCompilation()
    {
        var diagnostics = await RunAsync(null, "public class A { }", "public class B { }", "public class C { }");

        Assert.Equal(new[] { Dlz0011 }, diagnostics.Select(d => d.Id));
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string? style, params string[] sources)
    {
        var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"Sample{index}.cs"));
        var compilation = CSharpCompilation.Create(
            "Shop.Api", trees, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ArchitectureStyleAnalyzer());
        return await compilation.WithAnalyzers(analyzers, BuildPropertyOptions.WithArchitectureStyle(style)).GetAnalyzerDiagnosticsAsync();
    }
}
