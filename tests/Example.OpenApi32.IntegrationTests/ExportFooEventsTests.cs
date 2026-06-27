using System.Net;
using AwesomeAssertions;
using Corvus.Json;
using Example.Foo;

namespace Example.OpenApi32.IntegrationTests;

public class ExportFooEventsTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    internal async Task ExportingFooEventsAsJsonl_ShouldReturnOk()
    {
        var response = await SendAsync<Foo.Foo.Foo1.Events0.GetResponse.OK200.ApplicationJsonl>();
        await AssertContentAsync(response.Content);
    }

    [Fact]
    internal async Task ExportingFooEventsAsXJsonlines_ShouldReturnOk()
    {
        var response = await SendAsync<Foo.Foo.Foo1.Events0.GetResponse.OK200.ApplicationXJsonlines>();
        await AssertContentAsync(response.Content);
    }

    [Fact]
    internal async Task ExportingFooEventsAsXNdjson_ShouldReturnOk()
    {
        var response = await SendAsync<Foo.Foo.Foo1.Events0.GetResponse.OK200.ApplicationXNdjson>();
        await AssertContentAsync(response.Content);
    }

    [Fact]
    internal async Task ExportingFooEventsAsJsonSeq_ShouldReturnOk()
    {
        var response = await SendAsync<Foo.Foo.Foo1.Events0.GetResponse.OK200.ApplicationJsonSeq>();
        await AssertContentAsync(response.Content);
    }

    [Fact]
    internal async Task ExportingFooEventsAsGeoJsonSeq_ShouldReturnOk()
    {
        var response = await SendAsync<Foo.Foo.Foo1.Events0.GetResponse.OK200.ApplicationGeoJsonSeq>();
        await AssertContentAsync(response.Content);
    }
    
    private async Task<T> SendAsync<T>()
        where T : Foo.Foo.Foo1.Events0.GetResponse.OK200, Foo.Foo.Foo1.Events0.GetResponse.IAcceptContent
    {
        using var httpClient = app.CreateClient();
        var client = new Foo.Foo(httpClient);
        var result = await client.Foo_(1).Events().GetAsync(
            accepts: Foo.Foo.Foo1.Events0.GetResponse.Accept.Content<T>(), 
            cancellation: CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        var typedResponse = result.Response.Should().BeOfType<T>()
            .Subject;
        typedResponse.Validate(ValidationLevel.Detailed).IsValid.Should().BeTrue();
        typedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return typedResponse;
    }

    private static async Task AssertContentAsync(Foo.SequentialJsonEnumerable<Example.Foo.Components.Schemas.FooProperties> enumerable)
    {
        var expectedNames = new Queue<string>(["foo1", "foo2"]);
        await foreach (var (content, contentValidationContext) in enumerable.ConfigureAwait(false))
        {
            contentValidationContext.IsValid().Should().BeTrue();
            content.Name.Should().BeEquivalentTo(new JsonString(expectedNames.Dequeue()));
        }
    }
}