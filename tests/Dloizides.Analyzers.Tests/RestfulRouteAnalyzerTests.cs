namespace Dloizides.Analyzers.Tests;

public class RestfulRouteAnalyzerTests
{
    private const string Dlz0014 = "DLZ0014";

    private const string Prelude = """
        using System;
        public sealed class RouteAttribute : Attribute { public RouteAttribute(string template) { } }
        public sealed class HttpGetAttribute : Attribute { public HttpGetAttribute(string template) { } }
        public sealed class HttpPostAttribute : Attribute { public HttpPostAttribute(string template) { } }
        public sealed class HttpDeleteAttribute : Attribute { public HttpDeleteAttribute(string template) { } }
        public sealed class HttpPutAttribute : Attribute { public HttpPutAttribute(string template) { } }
        public sealed class HttpPatchAttribute : Attribute { public HttpPatchAttribute(string template) { } }
        public abstract class EndpointBase
        {
            protected void Get(params string[] routes) { }
            protected void Post(params string[] routes) { }
            protected void Routes(params string[] routes) { }
        }
        """;

    [Theory]
    [InlineData("[Route(\"api/orders/createwithclaims\")] public class OrdersController { }")]
    [InlineData("public class OrdersController { [HttpPost(\"orders/update-status\")] public void Patch() { } }")]
    [InlineData("public class ChatController { [HttpPost(\"chat/sendMessage\")] public void Send() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"api/orders/search\")] public void Find() { } }")]
    [InlineData("public class OrdersController { [HttpDelete(\"orders/{id}/remove\")] public void Drop() { } }")]
    [InlineData("public class CreateOrder : EndpointBase { public void Configure() { Post(\"/orders/create\"); } }")]
    [InlineData("public class GetOrder : EndpointBase { public void Configure() { Routes(\"/orders/{id}\", \"/orders/get/{id}\"); } }")]
    [InlineData("public class ListOrders : EndpointBase { public void Configure() { this.Get(\"/TenantOrders/list/all\"); } }")]
    [InlineData("[Route(\"[controller]/[action]\")] public class OrdersController { }")]
    [InlineData("[Route(\"api/[Action]\")] public class UsersController { }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/getall\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/getbyid/{id}\")] public void Read() { } }")]
    [InlineData("public class MailController { [HttpPost(\"mail/sendemail\")] public void Send() { } }")]
    [InlineData("public class UsersController { [HttpPut(\"users/{id}/addclaim\")] public void Add() { } }")]
    [InlineData("public class UsersController { [HttpGet(\"searchusers\")] public void Find() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/listall\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpPatch(\"orders/{id}/savechanges\")] public void Save() { } }")]
    [InlineData("public class JobsController { [HttpPost(\"jobs/runjob\")] public void Start() { } }")]
    public async Task Analyze_WithActionWordRouteSegment_ReportsDlz0014(string source)
    {
        var ids = await PatternRunner.IdsAsync(Prelude + source);

        Assert.Equal(new[] { Dlz0014 }, ids);
    }

    [Theory]
    [InlineData("[Route(\"api/settings\")] public class SettingsController { }")]
    [InlineData("public class SwitchController { [HttpGet(\"runtime-switch\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/{id}/updates\")] public void Read() { } }")]
    [InlineData("public class AddressesController { [HttpGet(\"addresses\")] public void Read() { } }")]
    [InlineData("public class ListingsController { [HttpGet(\"listings/{getId}\")] public void Read() { } }")]
    [InlineData("public class JobsController { [HttpPost(\"jobs/{id}/executions\")] public void Start() { } }")]
    [InlineData("public class SetupController { [HttpGet(\"setup/calculator\")] public void Read() { } }")]
    [InlineData("public class GetOrder : EndpointBase { public void Configure() { Get(\"/orders/{id}\"); } }")]
    [InlineData("public class OrdersController { [HttpDelete(\"orders/deletions\")] public void Read() { } }")]
    [InlineData("public class FetchersController { [HttpGet(\"fetcher/fetchers\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/createdby/{user}\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpGet(\"orders/createdon\")] public void Read() { } }")]
    [InlineData("public class AddressController { [HttpGet(\"address/settlements\")] public void Read() { } }")]
    [InlineData("public class OrdersController { [HttpPut(\"v1/orders/{id:guid}\")] public void Replace() { } }")]
    public async Task Analyze_WithNounRouteSegment_ReportsNothing(string source)
    {
        var ids = await PatternRunner.IdsAsync(Prelude + source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithRouteCallOutsideConfigure_ReportsNothing()
    {
        var source = Prelude + "public class Client : EndpointBase { public void Run() { Post(\"/orders/create\"); } }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsDlz0014()
    {
        var source = Prelude + "public class FakeController { [HttpPost(\"api/users/createwithclaims\")] public void Create() { } }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Equal(new[] { Dlz0014 }, ids);
    }
}
