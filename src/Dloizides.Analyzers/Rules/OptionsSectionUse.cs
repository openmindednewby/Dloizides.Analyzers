using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.Rules;

internal sealed class OptionsSectionUse
{
    private const string OptionsNamespace = "Microsoft.Extensions.Options";
    private static readonly string[] OptionsTypeNames = { "IOptions", "IOptionsMonitor", "IOptionsSnapshot" };
    private static readonly string[] ValueMemberNames = { "Value", "CurrentValue" };

    private readonly SemanticModel model;
    private readonly IReadOnlyList<IdentifierNameSyntax> names;
    private readonly CancellationToken cancellationToken;
    private readonly HashSet<IPropertySymbol> sections = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ISymbol> visited = new(SymbolEqualityComparer.Default);
    private bool escaped;

    private OptionsSectionUse(SemanticModel model, IReadOnlyList<IdentifierNameSyntax> names, CancellationToken cancellationToken)
    {
        this.model = model;
        this.names = names;
        this.cancellationToken = cancellationToken;
    }

    public static Diagnostic? ToDiagnostic(SemanticModel model, TypeDeclarationSyntax type, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol symbol || OptionsValueType(symbol.Type) is not ITypeSymbol settings)
            return null;
        var use = new OptionsSectionUse(model, type.DescendantNodes().OfType<IdentifierNameSyntax>().ToList(), cancellationToken);
        use.TraceOptions(symbol);
        if (use.escaped || use.sections.Count != 1)
            return null;
        var section = use.sections.Single();
        return IsSection(section.Type)
            ? Diagnostic.Create(Descriptors.NarrowOptions, parameter.GetLocation(), parameter.Identifier.ValueText, section.Name, settings.Name, section.Type.Name)
            : null;
    }

    private static ITypeSymbol? OptionsValueType(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named
        && OptionsTypeNames.Contains(named.Name)
        && named.ContainingNamespace.ToDisplayString() == OptionsNamespace
            ? named.TypeArguments[0]
            : null;

    private static bool IsSection(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Class
        && type.SpecialType != SpecialType.System_String
        && !type.AllInterfaces.Any(contract => contract.SpecialType == SpecialType.System_Collections_IEnumerable);

    private static ExpressionSyntax Outer(IdentifierNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access && access.Name == name && access.Expression is ThisExpressionSyntax
            ? access
            : name;

    private static bool IsWrite(ExpressionSyntax node) =>
        node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node;

    private static MemberAccessExpressionSyntax? MemberOf(ExpressionSyntax node) =>
        node.Parent is MemberAccessExpressionSyntax access && access.Expression == node ? access : null;

    private void TraceOptions(ISymbol holder)
    {
        foreach (var node in Uses(holder))
        {
            var access = MemberOf(node);
            if (access is not null && ValueMemberNames.Contains(access.Name.Identifier.ValueText))
                TraceValue(access);
            else
                Follow(node, TraceOptions);
        }
    }

    private void TraceValue(ExpressionSyntax value)
    {
        if (MemberOf(value) is MemberAccessExpressionSyntax access)
            RecordSection(access);
        else
            Follow(value, TraceSettings);
    }

    private void TraceSettings(ISymbol holder)
    {
        foreach (var node in Uses(holder))
            TraceValue(node);
    }

    private void Follow(ExpressionSyntax node, System.Action<ISymbol> trace)
    {
        if (AssignedTarget(node) is ISymbol target)
            trace(target);
        else
            escaped = true;
    }

    private IEnumerable<ExpressionSyntax> Uses(ISymbol holder)
    {
        if (!visited.Add(holder))
            return Enumerable.Empty<ExpressionSyntax>();
        return names
            .Where(name => name.Identifier.ValueText == holder.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, holder))
            .Select(Outer)
            .Where(node => !IsWrite(node))
            .ToList();
    }

    private void RecordSection(MemberAccessExpressionSyntax access)
    {
        if (model.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol property)
            sections.Add(property);
        else
            escaped = true;
    }

    private ISymbol? AssignedTarget(ExpressionSyntax node)
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
}
