using AwesomeAssertions;
using Corvus.Json;
using Example.Foo.Components.Schemas;
using OpenAPI.IntegrationTestHelpers.Auth;

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
                authentication: new Foo.Foo.Foo1.Authentication.PetstoreAuth(OIDCAuthHttpHandler.GetJwt("update")), 
                content: new Foo.Foo.Foo1.Content.ApplicationJson(
                    FooProperties.Create(name: "test")),
                header: new Foo.Foo.Foo1.Header
                {
                    Bar = new JsonString("foo")
                },
                cancellation: CancellationToken);
        result.IsSuccessful.Should().BeTrue();
        var anyApplicationResponse = result.Response.Should().BeOfType<Foo.Foo.Foo1.PutResponse.OK200.ApplicationJson>()
            .Subject;
        anyApplicationResponse.Content.Name
            .Should().NotBeNull()
            .And.Be(new JsonString("test"));

        anyApplicationResponse.Headers.Status.Should().Be(new JsonInteger(2));
        anyApplicationResponse.Headers.Tag.Should().BeNull();
    }
    
    // [Fact]
    // public async Task Given_unauthenticated_request_When_Updating_Foo_It_Should_Return_401()
    // {
    //     using var client = app.CreateClient();
    //     var result = await client.SendAsync(new HttpRequestMessage()
    //     {
    //         RequestUri = new Uri(client.BaseAddress!, "/foo/1"),
    //         Method = new HttpMethod("PUT"),
    //         Content = CreateJsonContent(
    //             """
    //             {
    //                 "Name": "test"
    //             }
    //             """),
    //         Headers =
    //         {
    //             { "Bar", "test" }
    //         }
    //     }, CancellationToken);
    //     result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    // }
    //
    // [Fact]
    // public async Task Given_unauthorized_request_When_Updating_Foo_It_Should_Return_403()
    // {
    //     using var client = app.CreateClient().WithOAuth2ImplicitFlowAuthentication();
    //     var result = await client.SendAsync(new HttpRequestMessage()
    //     {
    //         RequestUri = new Uri(client.BaseAddress!, "/foo/1"),
    //         Method = new HttpMethod("PUT"),
    //         Content = CreateJsonContent(
    //             """
    //             {
    //                 "Name": "test"
    //             }
    //             """),
    //         Headers =
    //         {
    //             { "Bar", "test" }
    //         }
    //     }, CancellationToken);
    //     result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    // }
}