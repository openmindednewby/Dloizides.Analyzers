using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivingAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "null-forgiving-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.NullForgiving, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.NullForgiving.Id, start.CancellationToken);
            start.RegisterSyntaxTreeAction(tree => Analyze(tree, baseline));
        });
    }

    private static void Analyze(SyntaxTreeAnalysisContext context, ViolationBaseline baseline)
    {
        var path = context.Tree.FilePath;
        if (GeneratedPath.IsMatch(path) || CommentTrivia.IsExcludedPath(path))
            return;
        var violations = context.Tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            .Select(node => Diagnostic.Create(Descriptors.NullForgiving, node.OperatorToken.GetLocation()))
            .ToList();
        baseline.Report(context, violations);
    }
}
