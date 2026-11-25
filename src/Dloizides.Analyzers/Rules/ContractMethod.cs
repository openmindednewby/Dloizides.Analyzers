using System.Linq;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal static class ContractMethod
{
    public static bool Is(IMethodSymbol method) =>
        method.IsOverride
        || method.IsVirtual
        || method.IsAbstract
        || !method.ExplicitInterfaceImplementations.IsEmpty
        || method.ContainingType.AllInterfaces
            .SelectMany(contract => contract.GetMembers())
            .Any(member => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(member), method));
}
