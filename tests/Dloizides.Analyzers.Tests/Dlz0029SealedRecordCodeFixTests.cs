using System.Collections.Immutable;
using Dloizides.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Dloizides.Analyzers.Tests;

public class Dlz0029SealedRecordCodeFixTests
{
    [Theory]
    [InlineData("public record Price(decimal Amount);", "public sealed record Price(decimal Amount);")]
    [InlineData("record Price(decimal Amount);", "sealed record Price(decimal Amount);")]
    [InlineData("    internal record class Price(decimal Amount);", "    internal sealed record class Price(decimal Amount);")]
    [InlineData("public partial record Price(decimal Amount);", "public sealed partial record Price(decimal Amount);")]
    [InlineData("    partial record Price(decimal Amount);", "    sealed partial record Price(decimal Amount);")]
    [InlineData("[System.Serializable]\n    public record Price(decimal Amount);", "[System.Serializable]\n    public sealed record Price(decimal Amount);")]
    public async Task RegisterCodeFixes_WithUnsealedRecord_AddsSealed(string declaration, string expected)
    {
        var source = "namespace Sample\n{\n" + declaration + "\n}\n";

        var fixedSource = await FixAsync(source);

        Assert.Equal("namespace Sample\n{\n" + expected + "\n}\n", fixedSource);
    }

    private static async Task<string> FixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.CurrentSolution
            .AddProject("Sample", "Sample", LanguageNames.CSharp)
            .WithCompilationOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(PatternRunner.References)
            .AddDocument("Sample.cs", SourceText.From(source), filePath: HouseRuleRunner.SamplePath);
        var compilation = await document.Project.GetCompilationAsync(CancellationToken.None);
        var diagnostic = Assert.Single(await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SealedRecordAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None));
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new SealedRecordCodeFixProvider().RegisterCodeFixesAsync(context);
        var operation = Assert.Single(await Assert.Single(actions).GetOperationsAsync(CancellationToken.None));
        var changed = ((ApplyChangesOperation)operation).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync(CancellationToken.None)).ToString();
    }
}
