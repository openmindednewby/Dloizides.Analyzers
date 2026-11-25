namespace Dloizides.Analyzers.Tests;

public class Dlz0033NarrowOptionsTests
{
    private const string Dlz0033 = "DLZ0033";
    private const string Dlz0019 = "DLZ0019";
    private const string Options = "namespace Microsoft.Extensions.Options { public interface IOptions<out T> where T : class { T Value { get; } } public interface IOptionsMonitor<out T> { T CurrentValue { get; } } } ";
    private const string Settings = "namespace Sample { public sealed class AdmieSettings { public string Zone { get; set; } = \"\"; } public sealed class HenexSettings { public string Url { get; set; } = \"\"; } public sealed class AppSettings { public AdmieSettings Admie { get; set; } = new(); public HenexSettings Henex { get; set; } = new(); public string Name { get; set; } = \"\"; } } ";
    private const string ValueInField = Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AppSettings _settings; public Job(IOptions<AppSettings> settings) { _settings = settings.Value; } public string Zone() { AdmieSettings admie = _settings.Admie; return admie.Zone + this._settings.Admie.Zone; } } }";

    [Theory]
    [InlineData(ValueInField)]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly IOptions<AppSettings> _options; public Job(IOptions<AppSettings> options) { _options = options; } public string Zone() => _options.Value.Admie.Zone; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly IOptionsMonitor<AppSettings> _options; public Job(IOptionsMonitor<AppSettings> options) { _options = options; } public string Zone() => _options.CurrentValue.Admie.Zone; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AdmieSettings _admie; public Job(IOptions<AppSettings> options) { var value = options.Value; _admie = value.Admie; } public string Zone() => _admie.Zone; } }")]
    public async Task Analyze_WithOnlyOneSectionRead_ReportsDlz0033(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), source);

        Assert.Equal(new[] { Dlz0033 }, ids);
    }

    [Theory]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AppSettings _settings; public Job(IOptions<AppSettings> settings) { _settings = settings.Value; } public string Both() => _settings.Admie.Zone + _settings.Henex.Url; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AppSettings _settings; public Job(IOptions<AppSettings> settings) { _settings = settings.Value; } public string Zone() => _settings.Admie.Zone + Describe(_settings); private static string Describe(AppSettings all) => all.Name + all.Henex.Url; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AppSettings _settings; public Job(IOptions<AppSettings> settings) { _settings = settings.Value; } public string Name() => _settings.Name; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly IOptions<AppSettings> _options; public Job(IOptions<AppSettings> options) { _options = options; } public AppSettings All() => _options.Value; } }")]
    [InlineData(Options + Settings + "namespace Sample { using Microsoft.Extensions.Options; public sealed class Job { private readonly AdmieSettings _admie; public Job(IOptions<AdmieSettings> admie) { _admie = admie.Value; } public string Zone() => _admie.Zone; } }")]
    public async Task Analyze_WithWholeOrSeveralSectionsRead_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithOneSectionRead_ReportsAtTheParameter()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new NarrowOptionsAnalyzer(), ValueInField, HouseRuleRunner.SamplePath);

        var span = Assert.Single(diagnostics).Location.SourceSpan;
        Assert.Equal("IOptions<AppSettings> settings", ValueInField.Substring(span.Start, span.Length));
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/narrow-options-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), ValueInField, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/narrow-options-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), ValueInField, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
