using System.Threading;
using AwesomeAssertions;
using OpenAPI.WebClientGenerator.Tests.Utils;
using Xunit;

namespace OpenAPI.WebClientGenerator.Tests;

public class EntityTests(ITestOutputHelper testOutputHelper)
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ClientNameThatOverlapsWithARootEntity_TheOverlappingEntityShouldBeRenamed()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "get": { "responses": { "200": { "description": "OK" } } }
            },
            "/pets/{petId}": {
              "parameters": [
                { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }
              ],
              "get": { "responses": { "200": { "description": "OK" } } }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "Pets",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        var source = compilation.GetSource("Pets.Pets.g.cs", Cancellation);
        source.Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class Pets
{
    internal Pets0 Pets_()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    internal partial class Pets0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/pets",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }

    internal Pets1 Pets_(
        Corvus.Json.JsonString petId)
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        requestBuilder.AddPathParameter("petId",
            petId,
            "#/paths/~1pets~1{petId}/parameters/0/schema",
            """
            {
              "name": "petId",
              "in": "path",
              "required": true,
              "schema": {
                "type": "string"
              }
            }
            """);
        return new(requestBuilder, _configuration);
    }

    internal partial class Pets1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/pets/{petId}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultipleRootPaths_EachPathShouldGetItsOwnRootEntity()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": { "get": { "responses": { "200": { "description": "OK" } } } },
            "/bar": { "get": { "responses": { "200": { "description": "OK" } } } },
            "/baz": { "get": { "responses": { "200": { "description": "OK" } } } }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        compilation.GetSource("TestClient.Foo.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Foo0 Foo()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    internal partial class Foo0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/foo",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Bar.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Bar0 Bar()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    internal partial class Bar0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/bar",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Baz.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Baz0 Baz()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    internal partial class Baz0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/baz",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultipleOperations_EntityShouldHaveOneMethodPerOperation()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items/{id}": {
              "parameters": [
                { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
              ],
              "get": { "responses": { "200": { "description": "OK" } } },
              "put": { "responses": { "200": { "description": "OK" } } },
              "delete": { "responses": { "200": { "description": "OK" } } }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        compilation.GetSource("TestClient.Items.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Items1 Items(
        Corvus.Json.JsonString id)
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        requestBuilder.AddPathParameter("id",
            id,
            "#/paths/~1items~1{id}/parameters/0/schema",
            """
            {
              "name": "id",
              "in": "path",
              "required": true,
              "schema": {
                "type": "string"
              }
            }
            """);
        return new(requestBuilder, _configuration);
    }

    internal partial class Items1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }

        internal async Task<Result<PutResponse>> PutAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<PutResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "PUT",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await PutResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<PutResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }

        internal async Task<Result<DeleteResponse>> DeleteAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<DeleteResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "DELETE",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await DeleteResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<DeleteResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void NestedPath_ChildEntityShouldBeContainedByParentEntity()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/parent/{id}/child": {
              "parameters": [
                { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
              ],
              "get": { "responses": { "200": { "description": "OK" } } }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        compilation.GetSource("TestClient.Parent.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Parent1 Parent(
        Corvus.Json.JsonString id)
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        requestBuilder.AddPathParameter("id",
            id,
            "#/paths/~1parent~1{id}~1child/parameters/0/schema",
            """
            {
              "name": "id",
              "in": "path",
              "required": true,
              "schema": {
                "type": "string"
              }
            }
            """);
        return new(requestBuilder, _configuration);
    }

    internal partial class Parent1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Parent.Child.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal partial class Parent1
    {
        internal Child0 Child()
        {
            return new(requestBuilder, configuration);
        }

        internal partial class Child0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            internal async Task<Result<GetResponse>> GetAsync(
                CancellationToken cancellation = default)
            {
                if (!requestBuilder.ValidationContext.IsValid)
                    return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                        .WithLocation(configuration.OpenApiSpecificationUri));
                var responseMessage = await requestBuilder
                    .SendAsync(
                        "/parent/{id}/child",
                        "GET",
                        null,
                        cancellation)
                    .ConfigureAwait(false);
                var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                    .ConfigureAwait(false);
                var responseValidationContext = configuration.ValidateResponses ?
                    response.Validate(configuration.ValidationLevel) :
                    ValidationContext.ValidContext;
                return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void PathWithParameter_MethodShouldIncludeTheParameter()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items/{id}": {
              "parameters": [
                { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
              ],
              "get": { "responses": { "200": { "description": "OK" } } }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        compilation.GetSource("TestClient.Items.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Items1 Items(
        Corvus.Json.JsonString id)
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        requestBuilder.AddPathParameter("id",
            id,
            "#/paths/~1items~1{id}/parameters/0/schema",
            """
            {
              "name": "id",
              "in": "path",
              "required": true,
              "schema": {
                "type": "string"
              }
            }
            """);
        return new(requestBuilder, _configuration);
    }

    internal partial class Items1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithRequestBody_GeneratesContentClassAndBodyParameter()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                    }
                  }
                },
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        var entitySource = compilation.GetSource("TestClient.Items.g.cs", Cancellation);
        entitySource.Should().Be("""
            #nullable enable
            using Corvus.Json;
            using System.Collections.Immutable;
            using System.IO.Pipelines;
            using System.Net.Http.Headers;
            using System.Text;
            
            namespace Example;
            internal partial class TestClient
            {
                internal Items0 Items()
                {
                    var requestBuilder = new RequestBuilder(httpClient, _configuration);
                    return new(requestBuilder, _configuration);
                }
            
                internal partial class Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
                {
                    internal async Task<Result<PostResponse>> PostAsync(Post.Content content,
                        CancellationToken cancellation = default)
                    {
                        if (!requestBuilder.ValidationContext.IsValid)
                            return Result<PostResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                                .WithLocation(configuration.OpenApiSpecificationUri));
                        var responseMessage = await requestBuilder
                            .SendAsync(
                                "/items",
                                "POST",
                                content.Get(),
                                cancellation)
                            .ConfigureAwait(false);
                        var response = await PostResponse.BindAsync(responseMessage, configuration, cancellation)
                            .ConfigureAwait(false);
                        var responseValidationContext = configuration.ValidateResponses ?
                            response.Validate(configuration.ValidationLevel) :
                            ValidationContext.ValidContext;
                        return Result<PostResponse>.WithResponse(response, responseValidationContext.Results
                            .WithLocation(configuration.OpenApiSpecificationUri));
                    }
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
        
        var contentSource = compilation.GetSource("TestClient.Items0.Post.Content.g.cs", Cancellation);
        contentSource.Should().Be("""
            #nullable enable
            using Corvus.Json;
            using System.Collections.Immutable;
            using System.IO.Pipelines;
            using System.Net.Http.Headers;
            using System.Text;
            
            namespace Example;
            internal partial class TestClient
            {
                internal partial class Items0
                {
                    internal partial class Post
                    {
                        internal abstract class Content
                        {
                            internal abstract string? MediaType { get; }
            
                            /// <summary>
                            /// Ensures that the specified content type matches the specification
                            /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified content type does not match the specification</exception>
                            /// </summary>
                            /// <param name="contentType">Content type</param>
                            /// <param name="expectedContentType">Expected content type</param>
                            protected void EnsureExpectedContentType(MediaTypeHeaderValue contentType, MediaTypeHeaderValue expectedContentType)
                            {
                                if (!contentType.IsSubsetOf(expectedContentType))
                                {
                                    throw new ArgumentOutOfRangeException($"Expected content type {contentType.MediaType} to be a subset of {expectedContentType.MediaType}");
                                }
                            }
            
                            internal abstract HttpContent Get();
            
                            internal abstract ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel);
            
                            /// <summary>
                            /// Request for content application/json
                            /// </summary>
                            internal sealed class ApplicationJson : Content
                            {
                                private Example.Paths.Items.Post.RequestBody.Content.ApplicationJson _content;
            
                                /// <summary>
                                /// Construct request for content application/json
                                /// </summary>
                                /// <param name="applicationJson">Content</param>
                                public ApplicationJson(Example.Paths.Items.Post.RequestBody.Content.ApplicationJson applicationJson)
                                {
                                    _content = applicationJson;
                                    MediaType = "application/json";
                                }
            
                                internal override string MediaType { get; }
            
                                internal override HttpContent Get() =>
                                   new StringContent(
                                       _content.Serialize(),
                                       encoding: Encoding.UTF8,
                                       mediaType: MediaType
                                   );
                                private const string ContentSchemaLocation = "#/paths/~1items/post/requestBody/content/application~1json/schema";
                                /// <inheritdoc/>
                                internal override ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel) =>
                                    _content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
                            }
                        }
                    }
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithOptionalRequestBody_GeneratesNullableContentParameter()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items": {
              "post": {
                "requestBody": {
                  "required": false,
                  "content": {
                    "application/json": {
                      "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                    }
                  }
                },
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        var source = compilation.GetSource("TestClient.Items.g.cs", Cancellation);

        source.Should().Be("""
            #nullable enable
            using Corvus.Json;
            using System.Collections.Immutable;
            using System.IO.Pipelines;
            using System.Net.Http.Headers;
            using System.Text;

            namespace Example;
            internal partial class TestClient
            {
                internal Items0 Items()
                {
                    var requestBuilder = new RequestBuilder(httpClient, _configuration);
                    return new(requestBuilder, _configuration);
                }

                internal partial class Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
                {
                    internal async Task<Result<PostResponse>> PostAsync(Post.Content? content = null,
                        CancellationToken cancellation = default)
                    {
                        if (!requestBuilder.ValidationContext.IsValid)
                            return Result<PostResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                                .WithLocation(configuration.OpenApiSpecificationUri));
                        var responseMessage = await requestBuilder
                            .SendAsync(
                                "/items",
                                "POST",
                                content?.Get(),
                                cancellation)
                            .ConfigureAwait(false);
                        var response = await PostResponse.BindAsync(responseMessage, configuration, cancellation)
                            .ConfigureAwait(false);
                        var responseValidationContext = configuration.ValidateResponses ?
                            response.Validate(configuration.ValidationLevel) :
                            ValidationContext.ValidContext;
                        return Result<PostResponse>.WithResponse(response, responseValidationContext.Results
                            .WithLocation(configuration.OpenApiSpecificationUri));
                    }
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithMixedRequiredAndOptionalParametersAndContent_OrdersSignatureRequiredFirst()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items": {
              "post": {
                "parameters": [
                  { "name": "trace", "in": "header", "required": false, "schema": { "type": "string"  } },
                  { "name": "limit", "in": "query",  "required": true,  "schema": { "type": "integer" } }
                ],
                "requestBody": {
                  "required": false,
                  "content": {
                    "application/json": {
                      "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                    }
                  }
                },
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        var source = compilation.GetSource("TestClient.Items.g.cs", Cancellation);

        source.Should().Contain(
            """
                    internal async Task<Result<PostResponse>> PostAsync(Query query,
                        Header? header = null,
                        Post.Content? content = null,
                        CancellationToken cancellation = default)
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithQueryParameters_GeneratesQueryClassWithInitProperties()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/items": {
              "get": {
                "parameters": [
                  { "name": "limit",  "in": "query", "required": true,  "schema": { "type": "integer" } },
                  { "name": "filter", "in": "query", "required": false, "schema": { "type": "string"  } }
                ],
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """;

        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "TestClient",
            @namespace: "Example",
            cancellationToken: Cancellation,
            diagnostics: out var diagnostics);

        diagnostics.Should().BeEmpty();

        var source = compilation.GetSource("TestClient.Items.g.cs", Cancellation);
        testOutputHelper.WriteLine(source);

        source.Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
internal partial class TestClient
{
    internal Items0 Items()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    internal partial class Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
    {
        internal async Task<Result<GetResponse>> GetAsync(Query query,
            CancellationToken cancellation = default)
        {
            query.AddTo(requestBuilder);
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<GetResponse>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await GetResponse.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<GetResponse>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }

        internal sealed class Query
        {
            internal required Corvus.Json.JsonInteger Limit { get; init; }
            internal Corvus.Json.JsonString? Filter { get; init; }

            internal RequestBuilder AddTo(RequestBuilder requestBuilder)
            {
                requestBuilder.AddQuery<Corvus.Json.JsonInteger>("limit",
                    Limit,
                    true,
                    "#/paths/~1items/get/parameters/0/schema",
                    """
                    {
                      "name": "limit",
                      "in": "query",
                      "required": true,
                      "schema": {
                        "type": "integer"
                      }
                    }
                    """);
                requestBuilder.AddQuery<Corvus.Json.JsonString>("filter",
                    Filter,
                    false,
                    "#/paths/~1items/get/parameters/1/schema",
                    """
                    {
                      "name": "filter",
                      "in": "query",
                      "schema": {
                        "type": "string"
                      }
                    }
                    """);
                return requestBuilder;
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }
}