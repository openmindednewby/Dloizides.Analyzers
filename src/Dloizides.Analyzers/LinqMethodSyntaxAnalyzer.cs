using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinqMethodSyntaxAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.LinqMethodSyntax);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.QueryExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var query = (QueryExpressionSyntax)context.Node;
        if (GeneratedPath.IsMatch(query.SyntaxTree.FilePath) || query.Ancestors().OfType<QueryExpressionSyntax>().Any())
            return;
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.LinqMethodSyntax, query.FromClause.GetLocation()));
    }
}
