using System.Net;
using System.Text;
using AwesomeAssertions;
using Corvus.Json;
using Example.Foo.Components.Schemas;
using OpenAPI.IntegrationTestHelpers.Auth;
using Put = Example.Foo.Foo.Foo1.Put;

namespace Example.OpenApi32.IntegrationTests;

public class UpdateFooTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task UpdatingFoo_ReturnsUpdatedFoo()
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
        var anyApplicationResponse = result.Response.Should().BeOfType<Put.Response.OK200.AnyApplication>()
            .Subject;
        anyApplicationResponse.Content.Name
            .Should().NotBeNull()
            .And.Be(new JsonString("test"));

        anyApplicationResponse.Headers.Status.Should().Be(new JsonInteger(2));
    }

    [Fact]
    public async Task UpdatingFoo_WithInvalidResponseObjects_ProducesValidationResult()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "Name": 1 }""", Encoding.UTF8, "application/json"),
            };
            response.Headers.TryAddWithoutValidation("Status", "not-an-integer&test");
            return response;
        }));
        httpClient.BaseAddress = new Uri("https://localhost");

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

        result.FailedRequestValidation.Should().BeFalse();
        result.IsSuccessful.Should().BeFalse();

        result.ValidationResults.Should().HaveCount(2);
        result.ValidationResults.Should().AllSatisfy(validationResult => validationResult.Valid.Should().BeFalse());
        var schemaLocations = result.ValidationResults.Select(validationResult =>
            validationResult.Location?.SchemaLocation).ToList();
        schemaLocations.Should().ContainEquivalentOf(new JsonReference("#/paths/~1foo~1{FooId}/put/responses/200/headers/Status/schema/type"));
        schemaLocations.Should().ContainEquivalentOf(new JsonReference("#/components/schemas/FooProperties/properties/Name/type"));

        result.Response.Should().BeOfType<Put.Response.OK200.AnyApplication>();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}