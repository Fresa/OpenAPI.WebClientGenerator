using AwesomeAssertions;
using Corvus.Json;
using Example.Foo.Definitions;
using OpenAPI.IntegrationTestHelpers.Auth;
using Put = Example.Foo.Foo.Foo1.Put;

namespace Example.OpenApi20.IntegrationTests;

public class UpdateFooTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task When_Updating_Foo_It_Should_Return_Updated_Foo()
    {
        using var httpClient = app.CreateClient();

        var client = new Foo.Foo(httpClient);
        var result = await client.Foo_(1)
            .PutAsync(
                security: new Put.SecurityRequirement.PetstoreAuth(OIDCAuthHttpHandler.GetJwt("update")),
                content: new Put.Content.ApplicationJson(
                    FooProperties.Create(name: "test")),
                header: new Put.Header
                {
                    Bar = new JsonString("foo")
                },
                cancellation: CancellationToken);
        result.IsSuccessful.Should().BeTrue();
        var anyApplicationResponse = result.Response.Should().BeOfType<Foo.Foo.Foo1.Put.Response.OK200.ApplicationJson>()
            .Subject;
        anyApplicationResponse.Content.Name
            .Should().NotBeNull()
            .And.Be(new JsonString("test"));
        
        // result.Headers.Should().HaveCount(1);
        // result.Headers.Should().ContainKey("Status")
        //     .WhoseValue.Should().HaveCount(1)
        //     .And.Contain("2");
        // result.Content.Headers.ContentType.Should().Be(MediaTypeHeaderValue.Parse("application/json"));
    }
}