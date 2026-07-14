using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Corvus.Json;
using Example.Foo;

namespace Example.OpenApi32.IntegrationTests;

[SuppressMessage("Usage", "AF0002:Access to internal generated member")]
public class RequestBuilderTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static (RequestBuilder Builder, CapturingHandler Handler) CreateBuilder(
        WebClientConfiguration? configuration = null)
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return (new RequestBuilder(httpClient, configuration ?? new WebClientConfiguration()), handler);
    }

    [Fact]
    public async Task SendAsync_SubstitutesPathParameter()
    {
        var (builder, handler) = CreateBuilder();
        builder.AddPathParameter("id", new JsonString("42"), "#",
            """{ "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items/{id}", "GET", null, Cancellation);

        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/items/42");
    }

    [Fact]
    public async Task SendAsync_AppendsQueryParameter()
    {
        var (builder, handler) = CreateBuilder();
        builder.AddQuery<JsonInteger>("limit", new JsonInteger(10), true, "#",
            """{ "name": "limit", "in": "query", "required": true, "schema": { "type": "integer" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "GET", null, Cancellation);

        handler.Request!.RequestUri!.Query.Should().Be("?limit=10");
    }

    [Fact]
    public async Task SendAsync_OmitsOptionalQueryParameter()
    {
        var (builder, handler) = CreateBuilder();
        builder.AddQuery<JsonString>("filter", null, false, "#",
            """{ "name": "filter", "in": "query", "schema": { "type": "string" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "GET", null, Cancellation);

        handler.Request!.RequestUri!.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_AddsHeaderParameter()
    {
        var (builder, handler) = CreateBuilder();
        builder.AddHeader<JsonString>("X-Trace", new JsonString("abc"), true, "#",
            """{ "name": "X-Trace", "in": "header", "required": true, "schema": { "type": "string" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "GET", null, Cancellation);

        handler.Request!.Headers.GetValues("X-Trace").Should().ContainSingle()
            .Which.Should().Be("abc");
    }

    [Fact]
    public async Task SendAsync_OmitsOptionalHeaderParameter()
    {
        var (builder, handler) = CreateBuilder();
        builder.AddHeader<JsonString>("X-Trace", null, false, "#",
            """{ "name": "X-Trace", "in": "header", "schema": { "type": "string" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "GET", null, Cancellation);

        handler.Request!.Headers.Contains("X-Trace").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_AddsAcceptMediaTypes()
    {
        var (builder, handler) = CreateBuilder();
        builder.AcceptMediaTypes([MediaTypeWithQualityHeaderValue.Parse("application/json")]);

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "GET", null, Cancellation);

        handler.Request!.Headers.Accept.Should().ContainSingle()
            .Which.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_UsesProvidedHttpMethod()
    {
        var (builder, handler) = CreateBuilder();

        builder.ValidationContext.IsValid.Should().BeTrue();

        await builder.SendAsync("/items", "DELETE", null, Cancellation);

        handler.Request!.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public void AddPathParameter_WithValidValue_Valid()
    {
        var (builder, _) = CreateBuilder(new WebClientConfiguration { ValidateRequests = true });
        builder.AddPathParameter("id", new JsonString("42"), "#",
            """{ "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddQuery_WithInvalidValue_Invalid()
    {
        var (builder, _) = CreateBuilder(new WebClientConfiguration { ValidateRequests = true });
        var notAnInteger = JsonInteger.Parse("\"not-an-integer\"");
        builder.AddQuery<JsonInteger>("limit", notAnInteger, true, "#",
            """{ "name": "limit", "in": "query", "required": true, "schema": { "type": "integer" } }""");

        builder.ValidationContext.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddPathParameter_WithInvalidValue_Invalid()
    {
        var (builder, _) = CreateBuilder(new WebClientConfiguration { ValidateRequests = true });
        var notAnInteger = JsonInteger.Parse("\"not-an-integer\"");
        builder.AddPathParameter("id", notAnInteger, "#",
            """{ "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } }""");

        builder.ValidationContext.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddHeader_WithInvalidValue_Invalid()
    {
        var (builder, _) = CreateBuilder(new WebClientConfiguration { ValidateRequests = true });
        var notAnInteger = JsonInteger.Parse("\"not-an-integer\"");
        builder.AddHeader<JsonInteger>("X-Limit", notAnInteger, true, "#",
            """{ "name": "X-Limit", "in": "header", "required": true, "schema": { "type": "integer" } }""");

        builder.ValidationContext.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddPathParameterWithValidationTurnedOff_WithInvalidValue_Valid()
    {
        var (builder, _) = CreateBuilder(new WebClientConfiguration { ValidateRequests = false });
        var notAnInteger = JsonInteger.Parse("\"not-an-integer\"");
        builder.AddPathParameter("id", notAnInteger, "#",
            """{ "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } }""");

        builder.ValidationContext.IsValid.Should().BeTrue();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        internal HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}