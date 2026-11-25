using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Tests;

internal sealed class GlobalOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly DictionaryConfigOptions options;

    public GlobalOptionsProvider(ImmutableDictionary<string, string> values) =>
        options = new DictionaryConfigOptions(values.WithComparers(AnalyzerConfigOptions.KeyComparer));

    public override AnalyzerConfigOptions GlobalOptions => options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
}

internal sealed class DictionaryConfigOptions : AnalyzerConfigOptions
{
    private readonly ImmutableDictionary<string, string> values;

    public DictionaryConfigOptions(ImmutableDictionary<string, string> values) => this.values = values;

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
}
