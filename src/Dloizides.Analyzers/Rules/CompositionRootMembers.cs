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

    public static HashSet<MemberDeclarationSyntax> Of(SemanticModel model, ClassDeclarationSyntax type, CancellationToken cancellationToken)
    {
        var methods = type.Members.OfType<MethodDeclarationSyntax>()
            .Select(method => new Candidate(method, model.GetDeclaredSymbol(method, cancellationToken)))
            .Where(candidate => candidate.Symbol is not null)
            .ToList();
        var exempt = new HashSet<MemberDeclarationSyntax>(methods.Where(candidate => TakesBuilder(candidate.Symbol!)).Select(candidate => candidate.Syntax));
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword))
            return exempt;
        var references = References(model, type, cancellationToken);
        var helpers = methods.Where(candidate => candidate.Symbol!.DeclaredAccessibility == Accessibility.Private && !exempt.Contains(candidate.Syntax)).ToList();
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

    private static bool IsCalledOnlyFrom(HashSet<MemberDeclarationSyntax> exempt, ILookup<ISymbol, SimpleNameSyntax> references, Candidate helper)
    {
        var uses = references[helper.Symbol!].ToList();
        var isFactory = IsServiceFactory(helper.Symbol!);
        return uses.Count > 0 && uses.TrueForAll(use => (isFactory || IsInvoked(use)) && exempt.Contains(OwningMember(use)!));
    }

    private static bool IsServiceFactory(IMethodSymbol method) =>
        method.Parameters.Length == 1 && string.Equals(method.Parameters[0].Type.ToDisplayString(), ServiceProvider, System.StringComparison.Ordinal);

    private static ILookup<ISymbol, SimpleNameSyntax> References(SemanticModel model, ClassDeclarationSyntax type, CancellationToken cancellationToken) =>
        type.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(name => OwningMember(name) is not null)
            .Select(name => new { Name = name, Method = model.GetSymbolInfo(name, cancellationToken).Symbol as IMethodSymbol })
            .Where(reference => reference.Method is not null)
            .ToLookup(reference => (ISymbol)(reference.Method!.ReducedFrom ?? reference.Method.OriginalDefinition), reference => reference.Name, SymbolEqualityComparer.Default);

    private static bool IsInvoked(SimpleNameSyntax name)
    {
        var expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? (SyntaxNode)access : name;
        return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
    }

    private static MemberDeclarationSyntax? OwningMember(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => member.Parent is ClassDeclarationSyntax);

    private sealed class Candidate
    {
        public Candidate(MethodDeclarationSyntax syntax, IMethodSymbol? symbol)
        {
            Syntax = syntax;
            Symbol = symbol;
        }

        public MethodDeclarationSyntax Syntax { get; }

        public IMethodSymbol? Symbol { get; }
    }
}
