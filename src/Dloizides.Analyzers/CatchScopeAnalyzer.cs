using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatchScopeAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "catch-scope-baseline.txt";

    private const string RepositorySuffix = "Repository";
    private const string SaveChangesPrefix = "SaveChanges";
    private const string DbContextName = "DbContext";
    private const string EfCoreNamespace = "Microsoft.EntityFrameworkCore";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.CatchScope, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.CatchScope.Id, start.CancellationToken);
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
            .OfType<TryStatementSyntax>()
            .Where(IsInLoop)
            .SelectMany(statement => ToDiagnostics(context.SemanticModel, statement, context.CancellationToken))
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static ImmutableArray<Diagnostic> ToDiagnostics(SemanticModel model, TryStatementSyntax statement, CancellationToken cancellationToken)
    {
        var shared = OwnNodes(statement.Block)
            .OfType<AwaitExpressionSyntax>()
            .Select(awaited => SharedTarget(model, awaited.Expression, cancellationToken))
            .FirstOrDefault(target => target is not null);
        if (shared is null)
            return ImmutableArray<Diagnostic>.Empty;
        return statement.Catches
            .Where(clause => !Rethrows(clause))
            .Select(clause => Diagnostic.Create(Descriptors.CatchScope, clause.CatchKeyword.GetLocation(), shared))
            .ToImmutableArray();
    }

    private static bool IsInLoop(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case CommonForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
                    return true;
                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax:
                    return false;
            }
        }
        return false;
    }

    private static bool Rethrows(CatchClauseSyntax clause) =>
        OwnNodes(clause.Block).Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax);

    private static IEnumerable<SyntaxNode> OwnNodes(SyntaxNode node) =>
        node.DescendantNodes(child => child is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax);

    private static string? SharedTarget(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case InvocationExpressionSyntax invocation:
                    if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: var name }
                        && name.StartsWith(SaveChangesPrefix, StringComparison.Ordinal))
                        return name;
                    current = invocation.Expression;
                    break;
                case MemberAccessExpressionSyntax access:
                    if (model.GetTypeInfo(access.Expression, cancellationToken).Type is { } receiver && IsShared(receiver))
                        return receiver.Name;
                    current = access.Expression;
                    break;
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    break;
                default:
                    return null;
            }
        }
    }

    private static bool IsShared(ITypeSymbol type)
    {
        if (type.Name.EndsWith(RepositorySuffix, StringComparison.Ordinal))
            return true;
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == DbContextName && current.ContainingNamespace?.ToDisplayString() == EfCoreNamespace)
                return true;
        }
        return false;
    }
}
