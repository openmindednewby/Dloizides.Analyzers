namespace Dloizides.Analyzers.Tests;

public sealed class ServiceDefaultsGuardTargetsTests : IDisposable
{
    private const string Dlz0021 = "DLZ0021";

    private readonly string _directory = Directory.CreateTempSubdirectory("dlz-svcdef-").FullName;

    [Fact]
    public void ServiceDefaultsGuard_WithOptIn_RaisesDlz0021ToError()
    {
        var properties = TargetsProbe.Evaluate(_directory, OptIn("true"));

        Assert.Contains(Dlz0021, properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.DoesNotContain(Dlz0021, properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceDefaultsGuard_WithoutOptIn_KeepsDlz0021Warning()
    {
        var properties = TargetsProbe.Evaluate(_directory, OptIn("false"));

        Assert.DoesNotContain(Dlz0021, properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.Contains(Dlz0021, properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string OptIn(string value) =>
        $"<DloizidesEnableServiceDefaultsGuardErrors>{value}</DloizidesEnableServiceDefaultsGuardErrors>";
}
