using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0013 for new of a service of this solution outside the composition root.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompositionRootAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] ServiceSuffixes = { "Service", "Repository", "Handler", "Provider", "Manager", "Client" };
    private static readonly string[] RootFiles = { "Program.cs", "Startup.cs" };
    private static readonly string[] RegistrationPrefixes = { "Add", "TryAdd", "Replace", "Configure", "Decorate" };
    private static readonly ImmutableHashSet<string> WiringTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal, "IServiceCollection", "IServiceProvider", "IHostApplicationBuilder", "WebApplicationBuilder", "IHostBuilder");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.CompositionRoot);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var solutionPrefix = FirstSegment(start.Compilation.AssemblyName ?? string.Empty);
            start.RegisterOperationAction(operation => AnalyzeCreation(operation, solutionPrefix), OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, string solutionPrefix)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol type || !IsSolutionService(type, context.Compilation, solutionPrefix))
            return;
        var isRoot = IsRootFile(creation.Syntax.SyntaxTree.FilePath)
            || IsOwnFactory(context.ContainingSymbol, type)
            || IsWiring(context.ContainingSymbol, creation);
        if (isRoot)
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.CompositionRoot, creation.Syntax.GetLocation(), type.Name));
    }

    private static bool IsSolutionService(INamedTypeSymbol type, Compilation compilation, string solutionPrefix)
    {
        var isConcreteClass = type.TypeKind == TypeKind.Class && !type.IsAbstract;
        var hasServiceName = ServiceSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal));
        var assembly = type.ContainingAssembly;
        var isSolutionType = assembly is not null
            && (SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly) || FirstSegment(assembly.Name) == solutionPrefix);
        return isConcreteClass && hasServiceName && isSolutionType;
    }

    private static bool IsRootFile(string? filePath)
    {
        if (filePath is null || filePath.Length == 0)
            return false;
        var fileName = filePath.Substring(filePath.LastIndexOfAny(new[] { '/', '\\' }) + 1);
        return RootFiles.Contains(fileName, StringComparer.Ordinal);
    }

    private static bool IsOwnFactory(ISymbol containingSymbol, INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(containingSymbol.ContainingType, type);

    private static bool IsWiring(ISymbol containingSymbol, IOperation creation)
    {
        if (containingSymbol is IMethodSymbol method && TakesWiringType(method))
            return true;
        var insideLambda = false;
        for (var current = creation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation lambda)
            {
                if (TakesWiringType(lambda.Symbol))
                    return true;
                insideLambda = true;
            }
            else if (insideLambda && current is IInvocationOperation invocation && IsRegistration(invocation.TargetMethod.Name))
                return true;
        }
        return false;
    }

    private static bool TakesWiringType(IMethodSymbol method) =>
        method.Parameters.Any(parameter => WiringTypes.Contains(parameter.Type.Name));

    private static bool IsRegistration(string methodName) =>
        RegistrationPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.Ordinal));

    private static string FirstSegment(string name)
    {
        var dot = name.IndexOf('.');
        return dot < 0 ? name : name.Substring(0, dot);
    }
}
