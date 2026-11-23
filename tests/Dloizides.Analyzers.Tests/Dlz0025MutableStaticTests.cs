using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

public class Dlz0025MutableStaticTests
{
    private const string Dlz0025 = "DLZ0025";

    [Theory]
    [InlineData("private static bool _enableFose;")]
    [InlineData("public static int Counter = 0;")]
    [InlineData("[System.ThreadStatic] private static int _depth;")]
    [InlineData("public static bool EnableFose { get; set; }")]
    [InlineData("public static string Name { get; private set; } = \"a\";")]
    [InlineData("private static int _x; public static int X { get => _x; set => _x = value; }")]
    public async Task Analyze_WithWritableStatic_ReportsDlz0025(string member)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MutableStaticAnalyzer(), $"public sealed class MigrationManager {{ {member} }}");

        Assert.Contains(Dlz0025, ids);
        Assert.All(ids, id => Assert.Equal(Dlz0025, id));
    }

    [Theory]
    [InlineData("private const int Max = 3;")]
    [InlineData("private static readonly string[] Names = { \"a\" };")]
    [InlineData("public static int Max { get; } = 3;")]
    [InlineData("public static int Twice => 2;")]
    [InlineData("private bool _enableFose; public bool EnableFose { get; set; }")]
    public async Task Analyze_WithConstReadonlyOrInstanceState_ReportsNothing(string member)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MutableStaticAnalyzer(), $"public sealed class Settings {{ {member} }}");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithWritableStaticInTestAssembly_ReportsNothing()
    {
        var tree = CSharpSyntaxTree.ParseText("public sealed class Fixture { private static int _runs; }");
        var compilation = CSharpCompilation.Create(
            "Shop.Api.Tests",
            new[] { tree },
            PatternRunner.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MutableStaticAnalyzer());

        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();

        Assert.Empty(diagnostics);
    }
}
