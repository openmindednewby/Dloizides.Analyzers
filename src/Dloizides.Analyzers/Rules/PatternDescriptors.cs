using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

/// <summary>Diagnostic descriptors for the house design-pattern catalogue (PATTERN-1).</summary>
public static class PatternDescriptors
{
    /// <summary>The category a [SuppressMessage] for a pattern rule names.</summary>
    public const string Category = "Dloizides.Patterns";

    private const string HelpLink = "https://github.com/openmindednewby/Dloizides.Analyzers#pattern-rules";

    /// <summary>An HttpClient constructed outside an AddHttpClient registration lambda.</summary>
    public static readonly DiagnosticDescriptor HttpClientFactory = new(
        "DLZ0006",
        "HTTP clients come from IHttpClientFactory",
        "Do not construct HttpClient here: register a typed or named client with AddHttpClient and inject it (PAT-HTTP-1)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>IConfiguration injected into a type instead of typed options.</summary>
    public static readonly DiagnosticDescriptor TypedOptions = new(
        "DLZ0007",
        "Settings are read through IOptions<T>",
        "'{0}' takes IConfiguration: bind a settings class with Configure<T> and inject IOptions<T> (PAT-CONFIG-1)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A role check in a method body that decides whether to forbid the call.</summary>
    public static readonly DiagnosticDescriptor DeclaredAuthorization = new(
        "DLZ0008",
        "Who may call an endpoint is declared, not checked in the body",
        "Declare the role on the endpoint (FastEndpoints Roles(...) or [Authorize(Policy = ...)]) instead of checking it before Forbid (PAT-AUTHZ-1)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A static ConcurrentDictionary written at runtime, used as a cache.</summary>
    public static readonly DiagnosticDescriptor SharedCache = new(
        "DLZ0009",
        "Runtime caches use IMemoryCache",
        "Static ConcurrentDictionary '{0}' is written at runtime: inject IMemoryCache or the shared cache (PAT-CACHE-1)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>A [SuppressMessage] for a pattern rule without a justification.</summary>
    public static readonly DiagnosticDescriptor SuppressionJustification = new(
        "DLZ0010",
        "A pattern-rule suppression needs a justification",
        "The [SuppressMessage] for {0} has no Justification: state why the house pattern does not apply",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);
    /// <summary>A compilation whose HouseArchitectureStyle is missing or not an approved style.</summary>
    public static readonly DiagnosticDescriptor ArchitectureStyle = new(
        "DLZ0011",
        "The project declares an approved architecture style",
        "HouseArchitectureStyle is '{0}': set it to Clean or CleanVerticalSlice in Directory.Build.props (PAT-STYLE-1)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
