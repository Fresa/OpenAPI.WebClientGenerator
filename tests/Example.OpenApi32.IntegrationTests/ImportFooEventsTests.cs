using System.Net;
using AwesomeAssertions;
using Corvus.Json;
using Example.Foo;
using Example.Foo.Components.Schemas;
using OpenAPI.IntegrationTestHelpers.Auth;

namespace Example.OpenApi32.IntegrationTests;

public class ImportFooEventsTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    internal async Task ImportingFooEventsAsJsonl_ShouldReturnAccepted()
    {
        var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationJsonl();
        var sendTask = SendAsync(content);
        (await content.WriteItemAsync(FooProperties.Create(name: "test"), CancellationToken)).IsValid().Should().BeTrue();
        (await content.WriteItemAsync(FooProperties.Create(name: "another test"), CancellationToken)).IsValid().Should().BeTrue();
        content.Dispose();
        await sendTask;
    }

    [Fact]
    internal async Task ImportingFooEventsAsXJsonlines_ShouldReturnAccepted()
    {
        var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationXJsonlines();
        var sendTask = SendAsync(content);
        (await content.WriteItemAsync(FooProperties.Create(name: "test"), CancellationToken)).IsValid().Should().BeTrue();
        (await content.WriteItemAsync(FooProperties.Create(name: "another test"), CancellationToken)).IsValid().Should().BeTrue();
        content.Dispose();
        await sendTask;
    }

    [Fact]
    internal async Task ImportingFooEventsAsXNdjson_ShouldReturnAccepted()
    {
        var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationXNdjson();
        var sendTask = SendAsync(content);
        (await content.WriteItemAsync(FooProperties.Create(name: "test"), CancellationToken)).IsValid().Should().BeTrue();
        (await content.WriteItemAsync(FooProperties.Create(name: "another test"), CancellationToken)).IsValid().Should().BeTrue();
        content.Dispose();
        await sendTask;
    }

    [Fact]
    internal async Task ImportingFooEventsAsJsonSeq_ShouldReturnAccepted()
    {
        var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationJsonSeq();
        var sendTask = SendAsync(content);
        (await content.WriteItemAsync(FooProperties.Create(name: "test"), CancellationToken)).IsValid().Should().BeTrue();
        (await content.WriteItemAsync(FooProperties.Create(name: "another test"), CancellationToken)).IsValid().Should().BeTrue();
        content.Dispose();
        await sendTask;
    }

    [Fact]
    internal async Task ImportingFooEventsAsGeoJsonSeq_ShouldReturnAccepted()
    {
        var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationGeoJsonSeq();
        var sendTask = SendAsync(content);
        (await content.WriteItemAsync(FooProperties.Create(name: "test"), CancellationToken)).IsValid().Should().BeTrue();
        (await content.WriteItemAsync(FooProperties.Create(name: "another test"), CancellationToken)).IsValid().Should().BeTrue();
        content.Dispose();
        await sendTask;
    }

    [Fact]
    internal async Task ImportingInvalidFooEvent_ShouldReturnInvalidValidationResults()
    {
        using var content = new Foo.Foo.Foo1.Events0.Post.Content.ApplicationJsonl();
        var notAFooEvent = FooProperties.Parse("\"not-a-foo-event\"");

        var validationResults = await content.WriteItemAsync(notAFooEvent, CancellationToken);

        validationResults.IsValid().Should().BeFalse();
    }

    private async Task SendAsync(Foo.Foo.Foo1.Events0.Post.Content content)
    {
        using var httpClient = app.CreateClient()
            .WithOAuth2ImplicitFlowAuthentication("update");
        var client = new Foo.Foo(httpClient);
        var result = await client.Foo_(1).Events().PostAsync(content, cancellation: CancellationToken);
        result.IsSuccessful.Should().BeTrue();
        var typedResponse = result.Response.Should().BeOfType<Foo.Foo.Foo1.Events0.PostResponse.Accepted202.Empty>()
            .Subject;
        typedResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        typedResponse.Headers.ImportedEvents.Should().Be(new JsonInteger(2));
    }
}