namespace Dloizides.Analyzers.Tests;

public sealed class BaselinedRulesTargetsTests : IDisposable
{
    private const string BaselineFile = "member-order-baseline.txt";

    private readonly string _directory = Directory.CreateTempSubdirectory("dlz-targets-").FullName;

    [Fact]
    public void BaselinedRulesAsErrors_WithOptInAndBaselineFile_EscalatesDlz0016To0019()
    {
        File.WriteAllText(Path.Combine(_directory, BaselineFile), string.Empty);

        var properties = Evaluate("true");

        Assert.Contains("DLZ0016;DLZ0017;DLZ0018;DLZ0019", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.DoesNotContain("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselinedRulesAsErrors_WithOptInAndMissingBaselineFile_KeepsWarnings()
    {
        var properties = Evaluate("true");

        Assert.DoesNotContain("DLZ0017", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.Contains("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselinedRulesAsErrors_WithoutOptIn_KeepsWarnings()
    {
        File.WriteAllText(Path.Combine(_directory, BaselineFile), string.Empty);

        var properties = Evaluate("false");

        Assert.DoesNotContain("DLZ0017", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.Contains("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselinedRulesAsErrors_WithOptInAndStaticPureBaseline_EscalatesDlz0026()
    {
        var staticPure = Path.Combine(_directory, "static-pure-baseline.txt");
        File.WriteAllText(staticPure, string.Empty);

        var properties = TargetsProbe.Evaluate(
            _directory,
            "<DloizidesBaselinedRulesAsErrors>true</DloizidesBaselinedRulesAsErrors>",
            $"<AdditionalFiles Include=\"{staticPure}\" />");

        Assert.Contains("DLZ0026;DLZ0019", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.DoesNotContain("DLZ0026", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private TargetsProperties Evaluate(string optIn) =>
        TargetsProbe.Evaluate(
            _directory,
            $"<DloizidesBaselinedRulesAsErrors>{optIn}</DloizidesBaselinedRulesAsErrors>",
            $"<AdditionalFiles Include=\"{Path.Combine(_directory, BaselineFile)}\" />");
}
