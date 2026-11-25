using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "readonly-param-baseline.txt";

    private const string ListType = "System.Collections.Generic.List<T>";
    private const string EnumerableType = "System.Linq.Enumerable";
    private const string CountName = "Count";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.ReadOnlyParameter, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.ReadOnlyParameter.Id, start.CancellationToken);
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
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Body is not null || method.ExpressionBody is not null)
            .Where(method => context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is IMethodSymbol symbol && !ContractMethod.Is(symbol))
            .SelectMany(method => method.ParameterList.Parameters.Select(parameter => ToDiagnostic(context.SemanticModel, method, parameter, context.CancellationToken)))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static Diagnostic? ToDiagnostic(SemanticModel model, MethodDeclarationSyntax method, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol symbol
            || symbol.RefKind != RefKind.None
            || symbol.Type is not INamedTypeSymbol { TypeArguments.Length: 1 } list
            || list.ConstructedFrom.ToDisplayString() != ListType)
            return null;
        var uses = method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == symbol.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, symbol))
            .ToList();
        return uses.Count > 0 && uses.All(use => IsRead(model, use, cancellationToken))
            ? Diagnostic.Create(Descriptors.ReadOnlyParameter, parameter.GetLocation(), symbol.Name, list.TypeArguments[0].ToDisplayString())
            : null;
    }

    private static bool IsRead(SemanticModel model, IdentifierNameSyntax use, CancellationToken cancellationToken) =>
        use.Parent switch
        {
            ForEachStatementSyntax loop => loop.Expression == use,
            ElementAccessExpressionSyntax element => element.Expression == use && !(element.Parent is AssignmentExpressionSyntax assignment && assignment.Left == element),
            MemberAccessExpressionSyntax access when access.Expression == use =>
                access.Name.Identifier.ValueText == CountName && access.Parent is not InvocationExpressionSyntax
                || IsLinqCall(model, access, cancellationToken),
            _ => false,
        };

    private static bool IsLinqCall(SemanticModel model, MemberAccessExpressionSyntax access, CancellationToken cancellationToken) =>
        access.Parent is InvocationExpressionSyntax invocation
        && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: IMethodSymbol definition }
        && definition.ContainingType.ToDisplayString() == EnumerableType;
}
