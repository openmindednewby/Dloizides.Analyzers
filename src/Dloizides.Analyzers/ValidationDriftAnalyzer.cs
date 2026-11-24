using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidationDriftAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "validation-drift-baseline.txt";

    private const string RuleForName = "RuleFor";
    private const string MatchesName = "Matches";
    private const string MustName = "Must";
    private const string IsMatchName = "IsMatch";

    private static readonly ImmutableDictionary<string, string> SharedRuleByProperty = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Username"] = "ValidUsername",
        ["UserName"] = "ValidUsername",
        ["Password"] = "ValidPassword",
        ["NewPassword"] = "ValidPassword",
        ["Email"] = "ValidEmail",
        ["FirstName"] = "ValidPersonName",
        ["LastName"] = "ValidPersonName",
        ["Phone"] = "ValidPhone",
        ["PhoneNumber"] = "ValidPhone",
        ["Url"] = "ValidUrl",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> LengthRules = ImmutableHashSet.Create(StringComparer.Ordinal, "MinimumLength", "MaximumLength", "Length");

    private static readonly ImmutableHashSet<string> ValidatorBases = ImmutableHashSet.Create(StringComparer.Ordinal, "Validator", "AbstractValidator");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.ValidationDrift, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.ValidationDrift.Id, start.CancellationToken);
            start.RegisterSyntaxTreeAction(tree => Analyze(tree, baseline));
        });
    }

    private static void Analyze(SyntaxTreeAnalysisContext context, ViolationBaseline baseline)
    {
        if (GeneratedPath.IsMatch(context.Tree.FilePath))
            return;
        var violations = context.Tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(IsValidator)
            .SelectMany(Drifts)
            .ToList();
        baseline.Report(context, violations);
    }

    private static IEnumerable<Diagnostic> Drifts(ClassDeclarationSyntax validator)
    {
        var localConstants = new HashSet<string>(
            validator.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
                .SelectMany(field => field.Declaration.Variables.Select(variable => variable.Identifier.Text)),
            StringComparer.Ordinal);
        return validator.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => DriftOf(invocation, localConstants))
            .OfType<Diagnostic>();
    }

    private static Diagnostic? DriftOf(InvocationExpressionSyntax ruleFor, HashSet<string> localConstants)
    {
        if (!(ruleFor.Expression is IdentifierNameSyntax { Identifier.Text: RuleForName }))
            return null;
        var property = PropertyOf(ruleFor);
        if (property is null || !SharedRuleByProperty.TryGetValue(property, out var sharedRule))
            return null;
        var drift = Chain(ruleFor).FirstOrDefault(step => IsHandWritten(step, localConstants));
        return drift is null
            ? null
            : Diagnostic.Create(Descriptors.ValidationDrift, NameOf(drift).GetLocation(), property, NameOf(drift).Identifier.Text, sharedRule);
    }

    private static string? PropertyOf(InvocationExpressionSyntax ruleFor) =>
        ruleFor.ArgumentList.Arguments.FirstOrDefault()?.Expression is SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax member }
            ? member.Name.Identifier.Text
            : null;

    private static IEnumerable<InvocationExpressionSyntax> Chain(InvocationExpressionSyntax start)
    {
        SyntaxNode current = start;
        while (current.Parent is MemberAccessExpressionSyntax access && access.Expression == current
            && access.Parent is InvocationExpressionSyntax next)
        {
            yield return next;
            current = next;
        }
    }

    private static SimpleNameSyntax NameOf(InvocationExpressionSyntax step) => ((MemberAccessExpressionSyntax)step.Expression).Name;

    private static bool IsHandWritten(InvocationExpressionSyntax step, HashSet<string> localConstants)
    {
        var name = NameOf(step).Identifier.Text;
        if (name == MatchesName)
            return true;
        if (name == MustName)
            return step.ArgumentList.DescendantNodes().OfType<SimpleNameSyntax>().Any(called => called.Identifier.Text == IsMatchName);
        return LengthRules.Contains(name)
            && step.ArgumentList.Arguments.Any(argument => IsLocalNumber(argument.Expression, localConstants));
    }

    private static bool IsLocalNumber(ExpressionSyntax expression, HashSet<string> localConstants) =>
        expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression }
        || (expression is IdentifierNameSyntax identifier && localConstants.Contains(identifier.Identifier.Text));

    private static bool IsValidator(ClassDeclarationSyntax type) =>
        type.BaseList?.Types.Any(baseType => BaseName(baseType.Type) is string name && ValidatorBases.Contains(name)) == true;

    private static string? BaseName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.Text,
        QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic.Identifier.Text,
        _ => null,
    };
}
