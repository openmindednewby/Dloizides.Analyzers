using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

/// <summary>Diagnostic descriptors for the house comment standard.</summary>
public static class Descriptors
{
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
}
