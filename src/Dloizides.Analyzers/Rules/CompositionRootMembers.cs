using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.Rules;

internal static class CompositionRootMembers
{
    private const string ServiceProvider = "System.IServiceProvider";

    public static HashSet<MemberDeclarationSyntax> Of(SemanticModel model, TypeDeclarationSyntax type, CancellationToken cancellationToken)
    {
        var methods = type.Members.OfType<MethodDeclarationSyntax>()
            .Select(method => model.GetDeclaredSymbol(method, cancellationToken) is { } symbol ? new Candidate(method, symbol) : null)
            .OfType<Candidate>()
            .ToList();
        var exempt = new HashSet<MemberDeclarationSyntax>(methods.Where(candidate => TakesBuilder(candidate.Symbol)).Select(candidate => candidate.Syntax));
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword))
            return exempt;
        var references = References(model, type, cancellationToken);
        var helpers = methods.Where(candidate => candidate.Symbol.DeclaredAccessibility == Accessibility.Private && !exempt.Contains(candidate.Syntax)).ToList();
        int before;
        do
        {
            before = exempt.Count;
            helpers = helpers.Where(helper => !(IsCalledOnlyFrom(exempt, references, helper) && exempt.Add(helper.Syntax))).ToList();
        }
        while (exempt.Count > before);
        return exempt;
    }

    private static bool TakesBuilder(IMethodSymbol method) => method.Parameters.Any(parameter => StartupBuilder.IsMatch(parameter.Type));

    private static bool IsCalledOnlyFrom(ICollection<MemberDeclarationSyntax> exempt, ILookup<ISymbol, SimpleNameSyntax> references, Candidate helper)
    {
        var uses = references[helper.Symbol].ToList();
        var isFactory = IsServiceFactory(helper.Symbol);
        return uses.Count > 0 && uses.TrueForAll(use => (isFactory || IsInvoked(use)) && OwningMember(use) is { } owner && exempt.Contains(owner));
    }

    private static bool IsServiceFactory(IMethodSymbol method) =>
        method.Parameters.Length == 1 && TypeNames.Is(method.Parameters[0].Type, ServiceProvider);

    private static ILookup<ISymbol, SimpleNameSyntax> References(SemanticModel model, SyntaxNode type, CancellationToken cancellationToken) =>
        type.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(name => OwningMember(name) is not null)
            .Select(name => model.GetSymbolInfo(name, cancellationToken).Symbol is IMethodSymbol method ? new Reference(name, method) : null)
            .OfType<Reference>()
            .ToLookup(reference => (ISymbol)(reference.Method.ReducedFrom ?? reference.Method.OriginalDefinition), reference => reference.Name, SymbolEqualityComparer.Default);

    private static bool IsInvoked(SyntaxNode name)
    {
        var expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? (SyntaxNode)access : name;
        return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
    }

    private static MemberDeclarationSyntax? OwningMember(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => member.Parent is ClassDeclarationSyntax);

    private sealed record Candidate(MethodDeclarationSyntax Syntax, IMethodSymbol Symbol);

    private sealed record Reference(SimpleNameSyntax Name, IMethodSymbol Method);
}
