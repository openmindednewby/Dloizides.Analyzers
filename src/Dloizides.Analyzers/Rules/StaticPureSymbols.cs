using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal static class StaticPureSymbols
{
    private const string DoesIo = "does I/O";
    private const string ReadsClock = "reads the clock";
    private const string ReadsEnvironment = "reads the environment";
    private const string ReadsConfiguration = "reads configuration";
    private const string ResolvesServices = "resolves services";
    private const string EnvironmentVariablePrefix = "GetEnvironmentVariable";
    private const string DateTimeType = "System.DateTime";
    private const string DateTimeOffsetType = "System.DateTimeOffset";
    private const string EnvironmentType = "System.Environment";

    private static readonly HashSet<string> IoTypes = new(StringComparer.Ordinal) { "System.IO.File", "System.IO.Directory", "System.Net.Http.HttpClient", "System.Diagnostics.Process" };
    private static readonly HashSet<string> PathStreamTypes = new(StringComparer.Ordinal) { "System.IO.FileStream", "System.IO.StreamReader", "System.IO.StreamWriter" };
    private static readonly HashSet<string> DateTimeClock = new(StringComparer.Ordinal) { "Now", "UtcNow", "Today" };
    private static readonly HashSet<string> DateTimeOffsetClock = new(StringComparer.Ordinal) { "Now", "UtcNow" };
    private static readonly HashSet<string> ServiceLookups = new(StringComparer.Ordinal)
        { "GetService", "GetRequiredService", "GetServices", "GetKeyedService", "GetRequiredKeyedService", "GetKeyedServices" };

    private static readonly HashSet<string> ConfigurationTypes = new(StringComparer.Ordinal) { "Microsoft.Extensions.Configuration.IConfiguration" };
    private static readonly HashSet<string> ServiceProviderTypes = new(StringComparer.Ordinal) { "System.IServiceProvider" };

    public static string? Effect(ISymbol? symbol) => symbol switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => ConstructorEffect(constructor),
        IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol => MemberEffect(symbol),
        _ => null,
    };

    private static string? ConstructorEffect(IMethodSymbol constructor)
    {
        var type = TypeNames.FullName(constructor.ContainingType);
        var opensPath = PathStreamTypes.Contains(type)
            && constructor.Parameters.FirstOrDefault()?.Type.SpecialType == SpecialType.System_String;
        return IoTypes.Contains(type) || opensPath ? DoesIo : null;
    }

    private static string? MemberEffect(ISymbol symbol)
    {
        var type = TypeNames.FullName(symbol.ContainingType);
        if (IoTypes.Contains(type))
            return DoesIo;
        if (IsClock(type, symbol.Name))
            return ReadsClock;
        if (string.Equals(type, EnvironmentType, StringComparison.Ordinal) && symbol.Name.StartsWith(EnvironmentVariablePrefix, StringComparison.Ordinal))
            return ReadsEnvironment;
        var receiver = Receiver(symbol);
        if (IsOrImplements(receiver, ConfigurationTypes))
            return ReadsConfiguration;
        return ServiceLookups.Contains(symbol.Name) && IsOrImplements(receiver, ServiceProviderTypes) ? ResolvesServices : null;
    }

    private static bool IsClock(string type, string name) =>
        (string.Equals(type, DateTimeType, StringComparison.Ordinal) && DateTimeClock.Contains(name))
        || (string.Equals(type, DateTimeOffsetType, StringComparison.Ordinal) && DateTimeOffsetClock.Contains(name));

    private static ITypeSymbol? Receiver(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { ReducedFrom: not null } reduced => reduced.ReceiverType,
        IMethodSymbol { IsExtensionMethod: true } extension => extension.Parameters[0].Type,
        _ => symbol.ContainingType,
    };

    private static bool IsOrImplements(ITypeSymbol? type, ICollection<string> names) =>
        type is not null
        && (SelfAndBases(type).Any(candidate => names.Contains(TypeNames.FullName(candidate)))
            || type.AllInterfaces.Any(candidate => names.Contains(TypeNames.FullName(candidate))));

    private static IEnumerable<ITypeSymbol> SelfAndBases(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }
}
