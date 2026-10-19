using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0008 for a branch on role checks only that leads to Forbid.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeclaredAuthorizationAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ForbidMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Forbid", "ForbidAsync", "ForbiddenAsync", "SendForbiddenAsync");

    private static readonly ImmutableHashSet<string> CollectionChecks = ImmutableHashSet.Create(StringComparer.Ordinal, "Contains", "Any");

    private const string RoleCheckMethod = "IsInRole";
    private const string RolesSuffix = "Roles";
    private const string ForbidResultType = "ForbidResult";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.DeclaredAuthorization);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeConditional, OperationKind.Conditional);
    }

    private static void AnalyzeConditional(OperationAnalysisContext context)
    {
        var conditional = (IConditionalOperation)context.Operation;
        if (!IsOnlyRoleChecks(conditional.Condition))
            return;
        var branchForbids = Forbids(conditional.WhenTrue) || Forbids(conditional.WhenFalse);
        if (!branchForbids)
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.DeclaredAuthorization, conditional.Condition.Syntax.GetLocation()));
    }

    private static bool IsOnlyRoleChecks(IOperation operation) => operation switch
    {
        IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } negation => IsOnlyRoleChecks(negation.Operand),
        IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } logical =>
            IsOnlyRoleChecks(logical.LeftOperand) && IsOnlyRoleChecks(logical.RightOperand),
        IConversionOperation conversion => IsOnlyRoleChecks(conversion.Operand),
        IIsPatternOperation pattern => IsOnlyRoleChecks(pattern.Value),
        IInvocationOperation invocation => IsRoleCheck(invocation),
        _ => false,
    };

    private static bool IsRoleCheck(IInvocationOperation invocation)
    {
        var name = invocation.TargetMethod.Name;
        if (name == RoleCheckMethod)
            return true;
        if (!CollectionChecks.Contains(name))
            return false;
        var receiver = invocation.Instance ?? invocation.Arguments.FirstOrDefault()?.Value;
        var receiverName = NameOf(receiver);
        return receiverName is not null && receiverName.EndsWith(RolesSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NameOf(IOperation? operation) => operation switch
    {
        IConversionOperation conversion => NameOf(conversion.Operand),
        IMemberReferenceOperation member => member.Member.Name,
        ILocalReferenceOperation local => local.Local.Name,
        IParameterReferenceOperation parameter => parameter.Parameter.Name,
        _ => null,
    };

    private static bool Forbids(IOperation? operation)
    {
        if (operation is null or IConditionalOperation)
            return false;
        if (operation is IInvocationOperation invocation && ForbidMethods.Contains(invocation.TargetMethod.Name))
            return true;
        if (operation is IObjectCreationOperation { Type.Name: ForbidResultType })
            return true;
        return operation.ChildOperations.Any(Forbids);
    }
}
