namespace Dloizides.Analyzers.Tests;

public class Dlz0024ValidationDriftTests
{
    private const string Dlz0024 = "DLZ0024";
    private const string Dlz0019 = "DLZ0019";
    private const string SourcePath = "/repo/src/Accounts/RegisterValidator.cs";
    private const string BaselinePath = "/repo/validation-drift-baseline.txt";
    private const string Stubs =
        "public sealed class Rule\n{\n    public Rule MinimumLength(int n) => this;\n    public Rule MaximumLength(int n) => this;\n    public Rule Length(int a, int b) => this;\n    public Rule Matches(string p) => this;\n    public Rule Must(System.Func<string, bool> f) => this;\n    public Rule NotEmpty() => this;\n    public Rule ValidUsername() => this;\n}\n"
        + "public abstract class AbstractValidator<T>\n{\n    protected Rule RuleFor(System.Func<T, string> f) => new Rule();\n}\n"
        + "public static class ValidationLimits\n{\n    public const int MaxEmailLength = 254;\n}\n"
        + "public sealed record Account(string Username, string Password, string Email, string Title);\n";
    private const string Open = "public sealed class RegisterValidator : AbstractValidator<Account>\n{\n    private const int MaxUsername = 50;\n    private static readonly System.Text.RegularExpressions.Regex Charset = new(\"^[a-z]+$\");\n    public RegisterValidator()\n    {\n        ";
    private const string Close = "\n    }\n}\n";
    private const string Drift = "RuleFor(x => x.Username).NotEmpty().MinimumLength(3);";

    [Theory]
    [InlineData(Drift)]
    [InlineData("RuleFor(x => x.Username).MaximumLength(MaxUsername);")]
    [InlineData("RuleFor(x => x.Username).Matches(\"^[a-z]+$\");")]
    [InlineData("RuleFor(x => x.Username).Must(value => Charset.IsMatch(value));")]
    [InlineData("RuleFor(x => x.Password).NotEmpty().Length(8, 128);")]
    [InlineData("RuleFor(x => x.Email).MaximumLength(254);")]
    public async Task Analyze_WithHandWrittenSharedKindRule_ReportsDlz0024(string rule)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), Stubs + Open + rule + Close, SourcePath);

        Assert.Equal(new[] { Dlz0024 }, ids);
    }

    [Theory]
    [InlineData("RuleFor(x => x.Username).NotEmpty().ValidUsername();")]
    [InlineData("RuleFor(x => x.Email).MaximumLength(ValidationLimits.MaxEmailLength);")]
    [InlineData("RuleFor(x => x.Title).MaximumLength(5).Matches(\"^[a-z]+$\");")]
    [InlineData("RuleFor(x => x.Username).NotEmpty();")]
    public async Task Analyze_WithSharedRuleOrOtherProperty_ReportsNothing(string rule)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), Stubs + Open + rule + Close, SourcePath);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithRuleOutsideAValidator_ReportsNothing()
    {
        var source = Stubs + "public sealed class Builder\n{\n    public Rule Build(Rule rule) => rule.MinimumLength(3);\n    private Rule RuleFor(System.Func<Account, string> f) => new Rule();\n    public Rule Username() => RuleFor(x => x.Username).MinimumLength(3);\n}\n";

        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), source, SourcePath);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithDriftAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText(BaselinePath, "src/Accounts/RegisterValidator.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), Stubs + Open + Drift + Close, SourcePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithNewDriftAboveBaselineCount_ReportsEveryDrift()
    {
        var baseline = new InMemoryAdditionalText(BaselinePath, "src/Accounts/RegisterValidator.cs 1");
        var rules = Drift + "\n        RuleFor(x => x.Password).MinimumLength(8);";

        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), Stubs + Open + rules + Close, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0024, Dlz0024 }, ids);
    }

    [Fact]
    public async Task Analyze_WithBaselineAboveActualCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText(BaselinePath, "src/Accounts/RegisterValidator.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new ValidationDriftAnalyzer(), Stubs + Open + Drift + Close, SourcePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
