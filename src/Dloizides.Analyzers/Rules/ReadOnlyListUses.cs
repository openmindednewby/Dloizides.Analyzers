using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers.Rules;

internal sealed class ReadOnlyListUses
{
    private const string ListType = "System.Collections.Generic.List<T>";
    private const string EnumerableType = "System.Linq.Enumerable";
    private const string CountName = "Count";

    private static readonly ImmutableHashSet<string> ReadOnlyTypes = ImmutableHashSet.Create(
        "System.Collections.Generic.IReadOnlyList<T>",
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.IEnumerable<T>");

    private readonly SemanticModel _model;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<ISymbol, bool> _verdicts = new(SymbolEqualityComparer.Default);

    public ReadOnlyListUses(SemanticModel model, CancellationToken cancellationToken)
    {
        _model = model;
        _cancellationToken = cancellationToken;
    }

    public static bool IsList(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } list && list.ConstructedFrom.ToDisplayString() == ListType;

    public bool IsOnlyRead(ISymbol symbol)
    {
        if (_verdicts.TryGetValue(symbol, out var known))
            return known;
        _verdicts[symbol] = false;
        var scope = ScopeOf(symbol);
        var verdict = scope is not null && UsesIn(scope, symbol) is { Count: > 0 } uses && uses.All(IsRead);
        _verdicts[symbol] = verdict;
        return verdict;
    }

    private SyntaxNode? ScopeOf(ISymbol symbol) => symbol switch
    {
        IParameterSymbol { RefKind: RefKind.None, ContainingSymbol: IMethodSymbol method } parameter
            when IsList(parameter.Type) && method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor && !ContractMethod.Is(method)
            => DeclarationInTree(method) is BaseMethodDeclarationSyntax declaration && (declaration.Body is not null || declaration.ExpressionBody is not null) ? declaration : null,
        IFieldSymbol { IsReadOnly: true, DeclaredAccessibility: Accessibility.Private } field when IsList(field.Type)
            => DeclarationInTree(field.ContainingType),
        _ => null,
    };

    private SyntaxNode? DeclarationInTree(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences is { Length: 1 } references && references[0].SyntaxTree == _model.SyntaxTree
            ? references[0].GetSyntax(_cancellationToken)
            : null;

    private List<IdentifierNameSyntax> UsesIn(SyntaxNode scope, ISymbol symbol) =>
        scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == symbol.Name
                && SymbolEqualityComparer.Default.Equals(_model.GetSymbolInfo(name, _cancellationToken).Symbol, symbol))
            .ToList();

    private bool IsRead(IdentifierNameSyntax use)
    {
        ExpressionSyntax expression = use.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } self && self.Name == use ? self : use;
        return expression.Parent switch
        {
            ForEachStatementSyntax loop => loop.Expression == expression,
            ElementAccessExpressionSyntax element => element.Expression == expression && !IsWritten(element),
            MemberAccessExpressionSyntax access when access.Expression == expression =>
                access.Name.Identifier.ValueText == CountName && access.Parent is not InvocationExpressionSyntax
                || IsLinqCall(access),
            AssignmentExpressionSyntax assignment when assignment.Right == expression =>
                IsReadOnlyTarget(_model.GetSymbolInfo(assignment.Left, _cancellationToken).Symbol),
            AssignmentExpressionSyntax assignment when assignment.Left == expression =>
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not null
                && _model.GetSymbolInfo(expression, _cancellationToken).Symbol is IFieldSymbol,
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.None)
                && IsReadOnlyTarget((_model.GetOperation(argument, _cancellationToken) as IArgumentOperation)?.Parameter),
            _ => false,
        };
    }

    private bool IsReadOnlyTarget(ISymbol? target) => target switch
    {
        IParameterSymbol parameter => IsReadOnlyType(parameter.Type) || IsList(parameter.Type) && IsOnlyRead(parameter),
        IFieldSymbol field => IsReadOnlyType(field.Type) || IsList(field.Type) && IsOnlyRead(field),
        _ => false,
    };

    private static bool IsReadOnlyType(ITypeSymbol type) =>
        type is INamedTypeSymbol named && ReadOnlyTypes.Contains(named.ConstructedFrom.ToDisplayString());

    private static bool IsWritten(ElementAccessExpressionSyntax element) =>
        element.Parent is AssignmentExpressionSyntax assignment && assignment.Left == element
        || element.Parent is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
        || element.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None);

    private bool IsLinqCall(MemberAccessExpressionSyntax access) =>
        access.Parent is InvocationExpressionSyntax invocation
        && _model.GetSymbolInfo(invocation, _cancellationToken).Symbol is IMethodSymbol { ReducedFrom: IMethodSymbol definition }
        && definition.ContainingType.ToDisplayString() == EnumerableType;
}
