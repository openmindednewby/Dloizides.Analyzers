using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinqMethodSyntaxAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "linq-syntax-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.LinqMethodSyntax, Descriptors.StaleBaseline);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.LinqMethodSyntax.Id, start.CancellationToken);
            start.RegisterSyntaxTreeAction(tree => Analyze(tree, baseline));
        });
    }

    private static void Analyze(SyntaxTreeAnalysisContext context, ViolationBaseline baseline)
    {
        if (GeneratedPath.IsMatch(context.Tree.FilePath))
            return;
        var violations = context.Tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<QueryExpressionSyntax>()
            .Where(query => !query.Ancestors().OfType<QueryExpressionSyntax>().Any())
            .Select(query => Diagnostic.Create(Descriptors.LinqMethodSyntax, query.FromClause.GetLocation()))
            .ToList();
        baseline.Report(context, violations);
    }
}
