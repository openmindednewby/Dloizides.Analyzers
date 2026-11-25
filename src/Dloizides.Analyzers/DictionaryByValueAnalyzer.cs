using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DictionaryByValueAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "dictionary-by-value-baseline.txt";

    private const string EnumerableType = "System.Linq.Enumerable";
    private const string CollectionsNamespace = "System.Collections.Generic";
    private const string ValueName = "Value";
    private const string ValuesName = "Values";
    private const string ContainsName = "Contains";
    private const string ContainsValueName = "ContainsValue";
    private static readonly string[] SearchNames = { "First", "FirstOrDefault", "Single", "SingleOrDefault", "Where", "Any", "Count" };
    private static readonly string[] DictionaryNames = { "IDictionary`2", "IReadOnlyDictionary`2" };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.DictionaryByValue, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.DictionaryByValue.Id, start.CancellationToken);
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
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => invocation.Expression as MemberAccessExpressionSyntax)
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => IsValueSearch(context.SemanticModel, access, context.CancellationToken))
            .Select(access => Diagnostic.Create(Descriptors.DictionaryByValue, access.Name.GetLocation(), access.Name.Identifier.ValueText))
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static bool IsValueSearch(SemanticModel model, MemberAccessExpressionSyntax access, CancellationToken cancellationToken)
    {
        if (access.Parent is not InvocationExpressionSyntax invocation)
            return false;
        var name = access.Name.Identifier.ValueText;
        if (SearchNames.Contains(name))
            return IsDictionary(model.GetTypeInfo(access.Expression, cancellationToken).Type)
                && IsLinqCall(model, invocation, cancellationToken)
                && invocation.ArgumentList.Arguments.Any(argument => ReadsValue(model, argument.Expression, cancellationToken));
        if (name == ContainsValueName)
            return IsDictionary(model.GetTypeInfo(access.Expression, cancellationToken).Type) && IsInLoop(invocation);
        return name == ContainsName
            && access.Expression is MemberAccessExpressionSyntax values
            && values.Name.Identifier.ValueText == ValuesName
            && IsDictionary(model.GetTypeInfo(values.Expression, cancellationToken).Type)
            && IsInLoop(invocation);
    }

    private static bool IsDictionary(ITypeSymbol? type) =>
        type is not null && (IsDictionaryContract(type) || type.AllInterfaces.Any(IsDictionaryContract));

    private static bool IsDictionaryContract(ITypeSymbol type) =>
        DictionaryNames.Contains(type.OriginalDefinition.MetadataName)
        && type.ContainingNamespace.ToDisplayString() == CollectionsNamespace;

    private static bool IsLinqCall(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
        && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() == EnumerableType;

    private static bool ReadsValue(SemanticModel model, ExpressionSyntax argument, CancellationToken cancellationToken)
    {
        var parameter = argument switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0],
            _ => null,
        };
        if (parameter is null || model.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol symbol)
            return false;
        return argument.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(access => access.Name.Identifier.ValueText == ValueName
                && access.Expression is IdentifierNameSyntax receiver
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(receiver, cancellationToken).Symbol, symbol));
    }

    private static bool IsInLoop(SyntaxNode node) =>
        node.Ancestors()
            .TakeWhile(ancestor => ancestor is not MemberDeclarationSyntax and not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .Any(ancestor => ancestor is ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax);
}
