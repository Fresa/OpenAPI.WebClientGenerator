using AwesomeAssertions;
using Corvus.Json;
using Example.Foo.Components.Schemas;
using OpenAPI.IntegrationTestHelpers.Auth;
using Put = Example.Foo.Foo.Foo1.Put;

namespace Example.OpenApi31.IntegrationTests;

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

        anyApplicationResponse.Headers.Status.Should().Be(new JsonInteger(2));
        anyApplicationResponse.Headers.Tag.Should().BeNull();
    }
}