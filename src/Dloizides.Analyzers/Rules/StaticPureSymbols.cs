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
        var type = FullName(constructor.ContainingType);
        var opensPath = PathStreamTypes.Contains(type)
            && constructor.Parameters.FirstOrDefault()?.Type.SpecialType == SpecialType.System_String;
        return IoTypes.Contains(type) || opensPath ? DoesIo : null;
    }

    private static string? MemberEffect(ISymbol symbol)
    {
        var type = FullName(symbol.ContainingType);
        if (IoTypes.Contains(type))
            return DoesIo;
        if ((string.Equals(type, "System.DateTime", StringComparison.Ordinal) && DateTimeClock.Contains(symbol.Name)) || (string.Equals(type, "System.DateTimeOffset", StringComparison.Ordinal) && DateTimeOffsetClock.Contains(symbol.Name)))
            return ReadsClock;
        if (string.Equals(type, "System.Environment", StringComparison.Ordinal) && symbol.Name.StartsWith(EnvironmentVariablePrefix, StringComparison.Ordinal))
            return ReadsEnvironment;
        var receiver = Receiver(symbol);
        if (IsOrImplements(receiver, ConfigurationTypes))
            return ReadsConfiguration;
        return ServiceLookups.Contains(symbol.Name) && IsOrImplements(receiver, ServiceProviderTypes) ? ResolvesServices : null;
    }

    private static ITypeSymbol? Receiver(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { ReducedFrom: not null } reduced => reduced.ReceiverType,
        IMethodSymbol { IsExtensionMethod: true } extension => extension.Parameters[0].Type,
        _ => symbol.ContainingType,
    };

    private static bool IsOrImplements(ITypeSymbol? type, HashSet<string> names) =>
        type is not null
        && (SelfAndBases(type).Any(candidate => names.Contains(FullName(candidate)))
            || type.AllInterfaces.Any(candidate => names.Contains(FullName(candidate))));

    private static IEnumerable<ITypeSymbol> SelfAndBases(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }

    private static string FullName(ITypeSymbol? type) =>
        type is null || type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace
            ? type?.MetadataName ?? string.Empty
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
}
