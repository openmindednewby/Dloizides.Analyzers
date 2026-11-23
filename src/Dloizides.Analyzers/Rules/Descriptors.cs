using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

/// <summary>Diagnostic descriptors for the house comment standard.</summary>
public static class Descriptors
{
    public static readonly DiagnosticDescriptor StaleBaseline = new(
        "DLZ0019",
        "Baseline entry above the actual count",
        "'{0}' has {1} {2} violations but {3} allows {4}: lower the entry to {1}, or remove it at 0 (shrink-only baseline)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    private const string Category = "Maintainability";
    private const string HelpLink = "https://github.com/openmindednewby/Dloizides.Analyzers#readme";

    /// <summary>A line or block comment that is not a tool directive.</summary>
    public static readonly DiagnosticDescriptor NoComments = new(
        "DLZ0001",
        "Code must not contain comments",
        "Remove the comment: put the intent in a name and the business rule in a test name",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A doc comment on a member that is not visible outside the assembly.</summary>
    public static readonly DiagnosticDescriptor DocCommentPublicOnly = new(
        "DLZ0002",
        "Doc comments are allowed on public members only",
        "Remove the doc comment from '{0}': it is not visible outside the assembly",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A doc comment over the line caps, describing a call chain, or using a banned element.</summary>
    public static readonly DiagnosticDescriptor DocCommentShape = new(
        "DLZ0003",
        "Doc comments must be short",
        "{0}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A test method whose name is not Method_Scenario_Expected.</summary>
    public static readonly DiagnosticDescriptor TestNameShape = new(
        "DLZ0004",
        "Test names must be Method_Scenario_Expected",
        "Rename '{0}' to Method_Scenario_Expected: 3 PascalCase parts, the Scenario starting With, When, Without or Given",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A type in a namespace named after a technical type instead of a feature.</summary>
    public static readonly DiagnosticDescriptor FeatureFolders = new(
        "DLZ0005",
        "Organize code by feature, not by technical type",
        "'{0}' sits in the '{1}' namespace segment: organize by feature/use case (ARCH-VSA-1)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor LinqMethodSyntax = new(
        "DLZ0016",
        "Use LINQ method syntax",
        "Rewrite the query expression in method syntax: .Where(...), .Select(...) (LINQ-1)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor MemberOrder = new(
        "DLZ0017",
        "Members in access order",
        "{1} {2} '{0}' is above the more accessible {3} {2} '{4}': order each kind public, internal, protected internal, protected, private protected, private (MEMBER-ORDER-1)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor FileSize = new(
        "DLZ0018",
        "File stays under 300 lines",
        "'{0}' has {1} lines; keep it at or under {2} (FILE-SIZE-1: a file listed in file-size-baseline.txt may only shrink)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor ValidationDrift = new(
        "DLZ0024",
        "Shared field kinds use the shared rule",
        "'{0}' repeats a shared field rule with '{1}': use .{2}() from DLoizides.Validation (FORM-VALID-1, validation-drift-baseline.txt may only shrink)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);
}
