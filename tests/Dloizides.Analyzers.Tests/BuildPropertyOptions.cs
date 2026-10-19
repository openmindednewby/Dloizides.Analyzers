using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

internal sealed class BuildPropertyOptions : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty = new Options(ImmutableDictionary<string, string>.Empty);

    private BuildPropertyOptions(AnalyzerConfigOptions globalOptions) => GlobalOptions = globalOptions;

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public static AnalyzerOptions WithArchitectureStyle(string? style)
    {
        var values = style is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary<string, string>.Empty.Add("build_property.HouseArchitectureStyle", style);
        return new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new BuildPropertyOptions(new Options(values)));
    }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;

        public Options(ImmutableDictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }
}
