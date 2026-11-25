using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.Rules;

internal sealed class SettingsParameterUse
{
    private const string OptionsNamespace = "Microsoft.Extensions.Options";
    private const string ChainSeparator = ".";
    private const int MinimumProperties = 2;
    private static readonly string[] SettingsSuffixes = { "Settings", "Options" };

    private readonly SemanticModel model;
    private readonly IReadOnlyList<IdentifierNameSyntax> names;
    private readonly CancellationToken cancellationToken;
    private readonly List<IReadOnlyList<IPropertySymbol>> chains = new();
    private readonly HashSet<ISymbol> visited = new(SymbolEqualityComparer.Default);
    private bool escaped;

    private SettingsParameterUse(SemanticModel model, IReadOnlyList<IdentifierNameSyntax> names, CancellationToken cancellationToken)
    {
        this.model = model;
        this.names = names;
        this.cancellationToken = cancellationToken;
    }

    public static Diagnostic? ToDiagnostic(SemanticModel model, TypeDeclarationSyntax type, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol symbol
            || symbol.RefKind != RefKind.None
            || parameter.Modifiers.Any(SyntaxKind.ThisKeyword)
            || !IsSettings(symbol.Type))
            return null;
        var use = new SettingsParameterUse(model, type.DescendantNodes().OfType<IdentifierNameSyntax>().ToList(), cancellationToken);
        use.Trace(symbol);
        var chain = use.escaped ? new List<IPropertySymbol>() : use.CommonChain();
        return chain.Count == 0
            ? null
            : Diagnostic.Create(Descriptors.NarrowSettingsParameter, parameter.GetLocation(), symbol.Name, string.Join(ChainSeparator, chain.Select(property => property.Name)), symbol.Type.Name);
    }

    private static bool IsSettings(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false, SpecialType: SpecialType.None } named
        && SettingsSuffixes.Any(suffix => named.Name.EndsWith(suffix, StringComparison.Ordinal))
        && named.ContainingNamespace.ToDisplayString() != OptionsNamespace
        && named.GetMembers().OfType<IPropertySymbol>().Count(property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public) >= MinimumProperties;

    private static ExpressionSyntax Unwrap(ExpressionSyntax node)
    {
        while (node.Parent is PostfixUnaryExpressionSyntax suppress && suppress.IsKind(SyntaxKind.SuppressNullableWarningExpression)
            || node.Parent is ParenthesizedExpressionSyntax)
            node = (ExpressionSyntax)node.Parent;
        return node;
    }

    private static bool IsNullCheck(ExpressionSyntax node) =>
        node.Parent switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) =>
                (binary.Left == node ? binary.Right : binary.Left).IsKind(SyntaxKind.NullLiteralExpression),
            IsPatternExpressionSyntax pattern => pattern.Expression == node,
            _ => false,
        };

    private void Trace(ISymbol holder)
    {
        if (!visited.Add(holder))
            return;
        foreach (var read in ExpressionFlow.Reads(model, names, holder, cancellationToken))
            TraceRead(Unwrap(read));
    }

    private void TraceRead(ExpressionSyntax node)
    {
        if (IsNullCheck(node))
            return;
        if (FirstProperty(node) is IPropertySymbol property)
            chains.Add(Chain(node, property));
        else if (ExpressionFlow.AssignedTarget(model, node, cancellationToken) is ISymbol target)
            Trace(target);
        else
            escaped = true;
    }

    private IPropertySymbol? FirstProperty(ExpressionSyntax node) =>
        node.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Expression == node => model.GetSymbolInfo(access, cancellationToken).Symbol as IPropertySymbol,
            ConditionalAccessExpressionSyntax conditional when conditional.Expression == node =>
                conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault() is MemberBindingExpressionSyntax binding
                    ? model.GetSymbolInfo(binding, cancellationToken).Symbol as IPropertySymbol
                    : null,
            _ => null,
        };

    private List<IPropertySymbol> Chain(ExpressionSyntax node, IPropertySymbol first)
    {
        var chain = new List<IPropertySymbol> { first };
        if (node.Parent is not MemberAccessExpressionSyntax current)
            return chain;
        while (Unwrap(current) is var outer
            && outer.Parent is MemberAccessExpressionSyntax next
            && next.Expression == outer
            && model.GetSymbolInfo(next, cancellationToken).Symbol is IPropertySymbol property)
        {
            chain.Add(property);
            current = next;
        }
        return chain;
    }

    private List<IPropertySymbol> CommonChain()
    {
        if (chains.Count == 0)
            return new List<IPropertySymbol>();
        var shortest = chains.Min(chain => chain.Count);
        return Enumerable.Range(0, shortest)
            .TakeWhile(index => chains.All(chain => SymbolEqualityComparer.Default.Equals(chain[index], chains[0][index])))
            .Select(index => chains[0][index])
            .ToList();
    }
}
