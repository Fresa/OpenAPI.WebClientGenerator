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
public partial class Pets
{
    public Pets0 Pets_()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    public partial class Pets0
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Pets0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/pets",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }

    public Pets1 Pets_(
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

    public partial class Pets1
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Pets1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/pets/{petId}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Foo0 Foo()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    public partial class Foo0
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Foo0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/foo",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Bar0 Bar()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    public partial class Bar0
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Bar0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/bar",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Baz0 Baz()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    public partial class Baz0
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Baz0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/baz",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Items1 Items(
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

    public partial class Items1
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Items1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }

        public async Task<Result<Put.Response>> PutAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Put.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "PUT",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Put.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Put.Response>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }

        public async Task<Result<Delete.Response>> DeleteAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Delete.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "DELETE",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Delete.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Delete.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Parent1 Parent(
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

    public partial class Parent1
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Parent1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

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
public partial class TestClient
{
    public partial class Parent1
    {
        public Child0 Child()
        {
            return new(requestBuilder, configuration);
        }

        public partial class Child0
        {
            private readonly RequestBuilder requestBuilder;
            private readonly WebClientConfiguration configuration;

            internal Child0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
            {
                this.requestBuilder = requestBuilder;
                this.configuration = configuration;
            }

            public async Task<Result<Get.Response>> GetAsync(
                CancellationToken cancellation = default)
            {
                if (!requestBuilder.ValidationContext.IsValid)
                    return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                        .WithLocation(configuration.OpenApiSpecificationUri));
                var responseMessage = await requestBuilder
                    .SendAsync(
                        "/parent/{id}/child",
                        "GET",
                        null,
                        cancellation)
                    .ConfigureAwait(false);
                var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                    .ConfigureAwait(false);
                var responseValidationContext = configuration.ValidateResponses ?
                    response.Validate(configuration.ValidationLevel) :
                    ValidationContext.ValidContext;
                return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
public partial class TestClient
{
    public Items1 Items(
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

    public partial class Items1
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Items1(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(
            CancellationToken cancellation = default)
        {
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items/{id}",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
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
            public partial class TestClient
            {
                public Items0 Items()
                {
                    var requestBuilder = new RequestBuilder(httpClient, _configuration);
                    return new(requestBuilder, _configuration);
                }
            
                public partial class Items0
                {
                    private readonly RequestBuilder requestBuilder;
                    private readonly WebClientConfiguration configuration;

                    internal Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
                    {
                        this.requestBuilder = requestBuilder;
                        this.configuration = configuration;
                    }

                    public async Task<Result<Post.Response>> PostAsync(Post.Content content,
                        CancellationToken cancellation = default)
                    {
                        if (!requestBuilder.ValidationContext.IsValid)
                            return Result<Post.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                                .WithLocation(configuration.OpenApiSpecificationUri));
                        var responseMessage = await requestBuilder
                            .SendAsync(
                                "/items",
                                "POST",
                                content.Get(),
                                cancellation)
                            .ConfigureAwait(false);
                        var response = await Post.Response.BindAsync(responseMessage, configuration, cancellation)
                            .ConfigureAwait(false);
                        var responseValidationContext = configuration.ValidateResponses ?
                            response.Validate(configuration.ValidationLevel) :
                            ValidationContext.ValidContext;
                        return Result<Post.Response>.WithResponse(response, responseValidationContext.Results
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
            public partial class TestClient
            {
                public partial class Items0
                {
                    public partial class Post
                    {
                        public abstract class Content
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
                            public sealed class ApplicationJson : Content
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
            public partial class TestClient
            {
                public Items0 Items()
                {
                    var requestBuilder = new RequestBuilder(httpClient, _configuration);
                    return new(requestBuilder, _configuration);
                }

                public partial class Items0
                {
                    private readonly RequestBuilder requestBuilder;
                    private readonly WebClientConfiguration configuration;

                    internal Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
                    {
                        this.requestBuilder = requestBuilder;
                        this.configuration = configuration;
                    }

                    public async Task<Result<Post.Response>> PostAsync(Post.Content? content = null,
                        CancellationToken cancellation = default)
                    {
                        if (!requestBuilder.ValidationContext.IsValid)
                            return Result<Post.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                                .WithLocation(configuration.OpenApiSpecificationUri));
                        var responseMessage = await requestBuilder
                            .SendAsync(
                                "/items",
                                "POST",
                                content?.Get(),
                                cancellation)
                            .ConfigureAwait(false);
                        var response = await Post.Response.BindAsync(responseMessage, configuration, cancellation)
                            .ConfigureAwait(false);
                        var responseValidationContext = configuration.ValidateResponses ?
                            response.Validate(configuration.ValidationLevel) :
                            ValidationContext.ValidContext;
                        return Result<Post.Response>.WithResponse(response, responseValidationContext.Results
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
                    public async Task<Result<Post.Response>> PostAsync(Post.Query query,
                        Post.Header? header = null,
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
public partial class TestClient
{
    public Items0 Items()
    {
        var requestBuilder = new RequestBuilder(httpClient, _configuration);
        return new(requestBuilder, _configuration);
    }

    public partial class Items0
    {
        private readonly RequestBuilder requestBuilder;
        private readonly WebClientConfiguration configuration;

        internal Items0(RequestBuilder requestBuilder, WebClientConfiguration configuration)
        {
            this.requestBuilder = requestBuilder;
            this.configuration = configuration;
        }

        public async Task<Result<Get.Response>> GetAsync(Get.Query query,
            CancellationToken cancellation = default)
        {
            query.AddTo(requestBuilder);
            if (!requestBuilder.ValidationContext.IsValid)
                return Result<Get.Response>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                    .WithLocation(configuration.OpenApiSpecificationUri));
            var responseMessage = await requestBuilder
                .SendAsync(
                    "/items",
                    "GET",
                    null,
                    cancellation)
                .ConfigureAwait(false);
            var response = await Get.Response.BindAsync(responseMessage, configuration, cancellation)
                .ConfigureAwait(false);
            var responseValidationContext = configuration.ValidateResponses ?
                response.Validate(configuration.ValidationLevel) :
                ValidationContext.ValidContext;
            return Result<Get.Response>.WithResponse(response, responseValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        var querySource = compilation.GetSource("TestClient.Items0.Get.Query.g.cs", Cancellation);
        testOutputHelper.WriteLine(querySource);

        querySource.Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace Example;
public partial class TestClient
{
    public partial class Items0
    {
        public partial class Get
        {
            public sealed class Query
            {
                public required Corvus.Json.JsonInteger Limit { get; init; }
                public Corvus.Json.JsonString? Filter { get; init; }

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
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }
}