using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Rules;

internal static class ArchitectureStyle
{
    public const string PropertyName = "HouseArchitectureStyle";
    public const string Clean = "Clean";
    public const string CleanVerticalSlice = "CleanVerticalSlice";

    private const string OptionKey = "build_property." + PropertyName;

    public static string Read(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(OptionKey, out var value) ? value.Trim() : string.Empty;

    public static bool IsKnown(string style) =>
        string.Equals(style, Clean, StringComparison.Ordinal) || string.Equals(style, CleanVerticalSlice, StringComparison.Ordinal);
}
