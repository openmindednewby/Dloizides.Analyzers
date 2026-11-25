using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.Rules;

internal static class ExpressionFlow
{
    public static IReadOnlyList<ExpressionSyntax> Reads(SemanticModel model, IEnumerable<IdentifierNameSyntax> names, ISymbol holder, CancellationToken cancellationToken) =>
        names
            .Where(name => name.Identifier.ValueText == holder.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, holder))
            .Select(Outer)
            .Where(node => !IsWrite(node))
            .ToList();

    public static ISymbol? AssignedTarget(SemanticModel model, ExpressionSyntax node, CancellationToken cancellationToken)
    {
        var target = node.Parent switch
        {
            AssignmentExpressionSyntax assignment when assignment.Right == node && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) =>
                model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => model.GetDeclaredSymbol(declarator, cancellationToken),
            _ => null,
        };
        return target is IFieldSymbol or ILocalSymbol ? target : null;
    }

    private static ExpressionSyntax Outer(IdentifierNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access && access.Name == name && access.Expression is ThisExpressionSyntax
            ? access
            : name;

    private static bool IsWrite(ExpressionSyntax node) =>
        node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node;
}
