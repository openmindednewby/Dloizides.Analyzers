using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

internal static class PackRuleFiles
{
    public const string RuleIdsFile = "Dloizides.Analyzers.PackRules.targets";
    public const string GlobalConfigFile = "Dloizides.Analyzers.packs.globalconfig";

    private static readonly string[] PackKeys = { "MeziantouPath", "ThreadingPath", "SonarPath" };
    private static readonly string VbFolder = $"{Path.DirectorySeparatorChar}vb{Path.DirectorySeparatorChar}";

    public static IReadOnlyList<DiagnosticDescriptor> Descriptors() =>
        PackKeys
            .SelectMany(key => AnalyzerDlls(Metadata(key)))
            .SelectMany(LoadDescriptors)
            .Where(descriptor => !descriptor.Id.Contains('-'))
            .GroupBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToList();

    public static string RuleIdsTargets(IReadOnlyList<DiagnosticDescriptor> descriptors) =>
        "<Project>\n  <PropertyGroup>\n    <DloizidesPackRuleIds>"
        + string.Join(";", descriptors.Select(descriptor => descriptor.Id))
        + "</DloizidesPackRuleIds>\n  </PropertyGroup>\n</Project>\n";

    public static string GlobalConfig(IReadOnlyList<DiagnosticDescriptor> descriptors)
    {
        var builder = new StringBuilder("is_global = true\nglobal_level = -100\n");
        foreach (var descriptor in descriptors.Where(IsRaisedToWarning))
            builder.Append("dotnet_diagnostic.").Append(descriptor.Id).Append(".severity = warning\n");
        return builder.ToString();
    }

    public static string Shipped(string fileName) =>
        File.ReadAllText(Path.Combine(Metadata("BuildTransitivePath"), fileName)).Replace("\r\n", "\n", StringComparison.Ordinal);

    public static string WriteRegenerated(string fileName, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "dlz-pack-rules", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static bool IsRaisedToWarning(DiagnosticDescriptor descriptor) =>
        descriptor.IsEnabledByDefault
        && descriptor.DefaultSeverity == DiagnosticSeverity.Info
        && !descriptor.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable);

    private static string Metadata(string key) =>
        typeof(PackRuleFiles).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!;

    private static IEnumerable<string> AnalyzerDlls(string packageRoot) =>
        Directory.EnumerateFiles(Path.Combine(packageRoot, "analyzers"), "*.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains(VbFolder, StringComparison.Ordinal) && !path.EndsWith(".resources.dll", StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Contains("CodeFix", StringComparison.Ordinal))
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(path => path.Length).ThenByDescending(path => path, StringComparer.Ordinal).First());

    private static IEnumerable<DiagnosticDescriptor> LoadDescriptors(string dll)
    {
        Type[] types;
        try
        {
            types = Assembly.LoadFrom(dll).GetTypes();
        }
        catch (ReflectionTypeLoadException partial)
        {
            types = partial.Types.OfType<Type>().ToArray();
        }
        return types
            .Where(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null)
            .SelectMany(type => ((DiagnosticAnalyzer)Activator.CreateInstance(type)!).SupportedDiagnostics);
    }
}
