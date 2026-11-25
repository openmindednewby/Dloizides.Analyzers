using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionFlowAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "exception-flow-baseline.txt";

    private static readonly ImmutableHashSet<string> ParseExceptions = ImmutableHashSet.Create(
        System.StringComparer.Ordinal,
        "System.FormatException",
        "System.InvalidCastException",
        "System.OverflowException");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.ExceptionFlow, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.ExceptionFlow.Id, start.CancellationToken);
            start.RegisterSemanticModelAction(model => Analyze(model, baseline));
        });
    }

    private static void Analyze(SemanticModelAnalysisContext context, ViolationBaseline baseline)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (GeneratedPath.IsMatch(tree.FilePath))
            return;
        var violations = tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .Select(clause => ToDiagnostic(context.SemanticModel, clause, context.CancellationToken))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static Diagnostic? ToDiagnostic(SemanticModel model, CatchClauseSyntax clause, CancellationToken cancellationToken)
    {
        var caught = clause.Declaration is null ? null : model.GetTypeInfo(clause.Declaration.Type, cancellationToken).Type;
        return caught is not null
            && ParseExceptions.Contains(caught.ToDisplayString())
            && clause.Block.Statements.All(statement => IsFallback(model, statement, cancellationToken))
            ? Diagnostic.Create(Descriptors.ExceptionFlow, clause.CatchKeyword.GetLocation(), caught.Name)
            : null;
    }

    private static bool IsFallback(SemanticModel model, StatementSyntax statement, CancellationToken cancellationToken) =>
        statement switch
        {
            ReturnStatementSyntax ret => ret.Expression is null || IsConstant(model, ret.Expression, cancellationToken),
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && IsConstant(model, assignment.Right, cancellationToken),
            ContinueStatementSyntax or BreakStatementSyntax => true,
            _ => false,
        };

    private static bool IsConstant(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken) =>
        expression is DefaultExpressionSyntax
        || expression.IsKind(SyntaxKind.DefaultLiteralExpression)
        || model.GetConstantValue(expression, cancellationToken).HasValue;
}
