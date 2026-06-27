using AwesomeAssertions;
using Corvus.Json;
using Example.Foo.Components.Schemas;
using OpenAPI.IntegrationTestHelpers.Auth;
using Put = Example.Foo.Foo.Foo1.Put;

namespace Example.OpenApi30.IntegrationTests;

public class UpdateFooTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task When_Updating_Foo_It_Should_Return_Updated_Foo()
    {
        using var httpClient = app.CreateClient();

        var client = new Foo.Foo(httpClient);
        var result = await client.Foo_(1)
            .PutAsync(
                security: new Put.SecurityRequirement.PetstoreAuth(
                    OIDCAuthHttpHandler.GetJwt("update")), 
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
