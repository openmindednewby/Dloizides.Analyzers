using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0001 for every line or block comment that is not a tool directive.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoCommentsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.NoComments);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        if (CommentTrivia.IsExcludedPath(context.Tree.FilePath))
            return;
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia())
            if (CommentTrivia.IsPlainComment(trivia))
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.NoComments, trivia.GetLocation()));
    }
}
