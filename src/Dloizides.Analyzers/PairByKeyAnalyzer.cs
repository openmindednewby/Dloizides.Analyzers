using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PairByKeyAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "pair-by-key-baseline.txt";

    private const string ZipName = "Zip";
    private const string EnumerableType = "System.Linq.Enumerable";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.PairByKey, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.PairByKey.Id, start.CancellationToken);
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
            .Select(invocation => ToDiagnostic(context.SemanticModel, invocation, context.CancellationToken))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static Diagnostic? ToDiagnostic(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };
        if (name is null || name.Identifier.ValueText != ZipName)
            return null;
        var method = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var definition = method?.ReducedFrom ?? method;
        return definition?.ContainingType.ToDisplayString() == EnumerableType
            ? Diagnostic.Create(Descriptors.PairByKey, name.GetLocation())
            : null;
    }
}
