namespace Dloizides.Analyzers.Tests;

public class DeclaredAuthorizationAnalyzerTests
{
    private const string Dlz0008 = "DLZ0008";

    private const string Endpoint = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Security.Claims;
        using System.Threading;
        using System.Threading.Tasks;
        public sealed class Sender
        {
            public Task ForbiddenAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task OkAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
        public sealed class Order { public Guid OwnerId { get; set; } }
        public abstract class EndpointBase
        {
            public ClaimsPrincipal User { get; } = new();
            public Sender Send { get; } = new();
            public List<string> UserRoles { get; } = new();
            public object Forbid() => new();
            public object Ok() => new();
        }
        public sealed class UpdateTheme : EndpointBase
        {
            public async Task HandleAsync(Order order, Guid userId, CancellationToken ct)
            {

        """;

    private const string EndOfHandler = "\n    } }";

    [Theory]
    [InlineData("if (!User.IsInRole(\"Admin\")) { await Send.ForbiddenAsync(ct); return; }")]
    [InlineData("if (!User.IsInRole(\"Admin\") && !User.IsInRole(\"Owner\")) { await Send.ForbiddenAsync(ct); return; }")]
    [InlineData("if (UserRoles.Contains(\"Admin\")) { await Send.OkAsync(ct); } else { Forbid(); }")]
    [InlineData("var roles = UserRoles; if (!roles.Any(r => r == \"Admin\")) { Forbid(); return; }")]
    [InlineData("var result = User.IsInRole(\"Admin\") ? Ok() : Forbid();")]
    public async Task Analyze_WithRoleCheckBeforeForbid_ReportsDlz0008(string body)
    {
        var ids = await PatternRunner.IdsAsync(Endpoint + body + EndOfHandler);

        Assert.Equal(new[] { Dlz0008 }, ids);
    }

    [Theory]
    [InlineData("if (order.OwnerId != userId) { await Send.ForbiddenAsync(ct); return; }")]
    [InlineData("if (!User.IsInRole(\"Admin\") && order.OwnerId != userId) { Forbid(); return; }")]
    [InlineData("if (User.IsInRole(\"Admin\")) { await Send.OkAsync(ct); } else if (order.OwnerId != userId) { Forbid(); }")]
    [InlineData("var label = User.IsInRole(\"Admin\") ? \"admin\" : \"user\";")]
    public async Task Analyze_WithOwnershipOrNonForbiddingCheck_ReportsNothing(string body)
    {
        var ids = await PatternRunner.IdsAsync(Endpoint + body + EndOfHandler);

        Assert.Empty(ids);
    }
}
