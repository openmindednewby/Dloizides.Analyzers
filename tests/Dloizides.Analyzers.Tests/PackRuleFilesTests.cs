namespace Dloizides.Analyzers.Tests;

public class PackRuleFilesTests
{
    [Fact]
    public void RuleIdsTargets_WithPinnedPackVersions_MatchesShippedFile()
    {
        var expected = PackRuleFiles.RuleIdsTargets(PackRuleFiles.Descriptors());

        var shipped = PackRuleFiles.Shipped(PackRuleFiles.RuleIdsFile);

        Assert.True(expected == shipped, $"Out of date: copy {PackRuleFiles.WriteRegenerated(PackRuleFiles.RuleIdsFile, expected)} into buildTransitive.");
    }

    [Fact]
    public void GlobalConfig_WithPinnedPackVersions_MatchesShippedFile()
    {
        var expected = PackRuleFiles.GlobalConfig(PackRuleFiles.Descriptors());

        var shipped = PackRuleFiles.Shipped(PackRuleFiles.GlobalConfigFile);

        Assert.True(expected == shipped, $"Out of date: copy {PackRuleFiles.WriteRegenerated(PackRuleFiles.GlobalConfigFile, expected)} into buildTransitive.");
    }
}
