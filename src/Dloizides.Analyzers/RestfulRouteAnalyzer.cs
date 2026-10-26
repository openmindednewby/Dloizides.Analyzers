using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0014 for a route template with an action-word path segment.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RestfulRouteAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeSuffix = "Attribute";
    private const string ConfigureMethod = "Configure";

    private static readonly ImmutableHashSet<string> RouteAttributes = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Route", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch");

    private static readonly ImmutableHashSet<string> RouteMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Get", "Post", "Put", "Delete", "Patch", "Routes");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.RestfulRoute);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!RouteAttributes.Contains(AttributeName(attribute.Name)))
            return;
        var template = attribute.ArgumentList?.Arguments.FirstOrDefault(argument => argument.NameEquals is null && argument.NameColon is null);
        if (template is not null)
            Check(context, template.Expression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!RouteMethods.Contains(InvokedName(invocation.Expression)) || !IsInsideConfigure(invocation))
            return;
        foreach (var argument in invocation.ArgumentList.Arguments)
            Check(context, argument.Expression);
    }

    private static void Check(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return;
        var route = literal.Token.ValueText;
        var segment = ActionRouteSegment.Find(route);
        if (segment is not null)
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.RestfulRoute, literal.GetLocation(), route, segment));
    }

    private static string AttributeName(NameSyntax name)
    {
        var text = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => string.Empty,
        };
        return text.EndsWith(AttributeSuffix, StringComparison.Ordinal) ? text.Substring(0, text.Length - AttributeSuffix.Length) : text;
    }

    private static string InvokedName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool IsInsideConfigure(SyntaxNode node) =>
        string.Equals(node.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText, ConfigureMethod, StringComparison.Ordinal);
}
