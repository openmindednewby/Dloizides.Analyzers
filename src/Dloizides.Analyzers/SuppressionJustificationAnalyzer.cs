using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0010 for a [SuppressMessage] of DLZ0006-DLZ0009 with an empty Justification.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionJustificationAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> PatternIds = ImmutableHashSet.Create(
        StringComparer.Ordinal, "DLZ0006", "DLZ0007", "DLZ0008", "DLZ0009");

    private static readonly string[] SuppressAttributes =
    {
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
    };

    private const string JustificationName = "Justification";
    private const int CheckIdPosition = 1;
    private const char CheckIdSeparator = ':';

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.SuppressionJustification);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var attributeTypes = SuppressAttributes
                .Select(start.Compilation.GetTypeByMetadataName)
                .Where(type => type is not null)
                .Cast<INamedTypeSymbol>()
                .ToImmutableArray();
            if (attributeTypes.IsEmpty)
                return;
            start.RegisterSyntaxNodeAction(node => AnalyzeAttribute(node, attributeTypes), SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> attributeTypes)
    {
        var attribute = (AttributeSyntax)context.Node;
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null)
            return;
        var positional = arguments.Value.Where(a => a.NameEquals is null && a.NameColon is null).ToList();
        if (positional.Count <= CheckIdPosition)
            return;
        var checkId = ConstantText(context, positional[CheckIdPosition].Expression)?.Split(CheckIdSeparator)[0].Trim();
        if (checkId is null || !PatternIds.Contains(checkId) || !IsSuppressAttribute(context, attribute, attributeTypes))
            return;
        var justification = arguments.Value.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == JustificationName);
        if (justification is not null && !string.IsNullOrWhiteSpace(ConstantText(context, justification.Expression)))
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.SuppressionJustification, attribute.GetLocation(), checkId));
    }

    private static bool IsSuppressAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attribute, ImmutableArray<INamedTypeSymbol> attributeTypes)
    {
        var type = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol?.ContainingType;
        return attributeTypes.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, type));
    }

    private static string? ConstantText(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: string text } ? text : null;
}
