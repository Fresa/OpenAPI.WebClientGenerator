using System.IO;
using System.Linq;
using System.Threading;
using AwesomeAssertions;
using OpenAPI.WebClientGenerator.Tests.Utils;
using Xunit;

namespace OpenAPI.WebClientGenerator.Tests;

public class ResponseTests(ITestOutputHelper testOutputHelper)
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string ExpectedEmptyClass(string parentClassName) =>
$$""""
#nullable enable
using Corvus.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                public partial class {{parentClassName}}
                {
                    /// <summary>
                    /// Response with empty content
                    /// </summary>
                    public sealed class Empty : {{parentClassName}}
                    {
                        private Empty(HttpResponseMessage response) : base(response)
                        {
                        }

                        /// <summary>
                        /// Construct response for empty content
                        /// </summary>
                        /// <param name="response">Response message</param>
                        /// <param name="cancellationToken">Cancellation token</param>
                        internal static Task<Response> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default) =>
                            Task.FromResult<Response>(new Empty(response));

                        /// <inheritdoc/>
                        internal override ValidationContext Validate(ValidationLevel validationLevel) =>
                            base.Validate(validationLevel);
                    }
                }
            }
        }
    }
}
#nullable restore
"""";

    private const string ExpectedUnknownClass =
""""
#nullable enable
using Corvus.Json;
using System.Net;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                /// <summary>
                /// Unknown response
                /// </summary>
                public sealed class Unknown : Response
                {
                    public Stream Content { get; }

                    private Unknown(Stream content, HttpResponseMessage response)
                    {
                        Content = content;
                        StatusCode = response.StatusCode;
                    }

                    /// <summary>
                    /// Construct unknown response
                    /// </summary>
                    /// <param name="response">Response message</param>
                    /// <param name="cancellationToken">Cancellation token</param>
                    internal static async Task<Response> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
                    {
                        var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                            .ConfigureAwait(false);
                        return new Unknown(stream, response);
                    }

                    /// <summary>
                    /// Response status code
                    /// </summary>
                    public HttpStatusCode StatusCode { get; private set; }

                    /// <inheritdoc/>
                    internal override ValidationContext Validate(ValidationLevel validationLevel) =>
                        ValidationContext.ValidContext.UsingStack().UsingResults();
                }
            }
        }
    }
}
#nullable restore
"""";

    [Fact]
    public void SingleOkResponseWithoutContent_GeneratesEmptyResponseClass()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
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
        compilation.Output("TestClient.Foo0.Get.Response.g.cs", testOutputHelper, Cancellation);
        compilation.GetSource("TestClient.Foo0.Get.Response.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            /// <summary>
            /// Contains the operation's response objects
            /// </summary>
            public abstract partial class Response
            {
                /// <summary>
                /// Check if status code is 1xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches1xxStatusCode(int code) =>
                    code >= 100 && code <= 199;

                /// <summary>
                /// Check if status code is 2xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches2xxStatusCode(int code) =>
                    code >= 200 && code <= 299;

                /// <summary>
                /// Check if status code is 3xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches3xxStatusCode(int code) =>
                    code >= 300 && code <= 399;

                /// <summary>
                /// Check if status code is 4xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches4xxStatusCode(int code) =>
                    code >= 400 && code <= 499;

                /// <summary>
                /// Check if status code is 5xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches5xxStatusCode(int code) =>
                    code >= 500 && code <= 599;

                /// <summary>
                /// Validate the response
                /// </summary>
                /// <param name="validationLevel">Validation level</param>
                /// <returns>The validation result</returns>
                internal abstract ValidationContext Validate(ValidationLevel validationLevel);

                /// <summary>
                /// Read response content as json
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="cancellationToken">Cancellation token</param>
                protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
                {
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return document.RootElement.Clone();
                }

                /// <summary>
                /// Construct response
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="configuration">Web client configuration</param>
                /// <param name="cancellationToken">Cancellation token</param>
                internal static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default) =>
                    response.StatusCode switch
                    {
                        _ when OK200.MatchesStatusCode(response.StatusCode) => OK200.BindAsync(response, configuration, cancellationToken),
                        _ => Response.Unknown.BindAsync(response, cancellationToken)
                    };
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.Unknown.g.cs", Cancellation)
            .Should().Be(ExpectedUnknownClass.ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                /// <summary>
                /// <para>
                /// OK
                /// </para>
                /// </summary>
                public abstract partial class OK200 : Response
                {
                    protected OK200(HttpResponseMessage response)
                    {
                        StatusCode = response.StatusCode;
                    }

                    internal static bool MatchesStatusCode(HttpStatusCode statusCode) =>
                        ((int)statusCode) == 200;

                    /// <summary>
                    /// Response status code
                    /// </summary>
                    public HttpStatusCode StatusCode { get; private set; }

                    /// <summary>
                    /// Bind content from http response
                    /// </summary>
                    /// <param name="response">Http response message to bind from</param>
                    /// <param name="configuration">Web client configuration</param>
                    /// <param name="cancellationToken">Cancellation token</param>
                    /// <returns>An awaitable task for the response content</returns>
                    internal new static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
                    {
                        return Empty.BindAsync(response, cancellationToken);
                    }

                    /// <summary>
                    /// Create a validation context
                    /// </summary>
                    /// <returns>Validation context</returns>
                    protected ValidationContext CreateValidationContext() =>
                        ValidationContext.ValidContext.UsingStack().UsingResults();

                    /// <inheritdoc/>
                    internal override ValidationContext Validate(ValidationLevel validationLevel)
                    {
                        var validationContext = CreateValidationContext();
                        return validationContext;
                    }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.Empty.g.cs", Cancellation)
            .Should().Be(ExpectedEmptyClass("OK200").ReplaceLineEndings("\n"));
    }

    [Fact]
    public void DefaultStatusCode_DefaultClassMatchesAllStatusCodes()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
              "get": {
                "responses": {
                  "default": { "description": "Default response" }
                }
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

        compilation.Output("TestClient.Foo0.Get.Response.g.cs", testOutputHelper, Cancellation);
        compilation.GetSource("TestClient.Foo0.Get.Response.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            /// <summary>
            /// Contains the operation's response objects
            /// </summary>
            public abstract partial class Response
            {
                /// <summary>
                /// Check if status code is 1xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches1xxStatusCode(int code) =>
                    code >= 100 && code <= 199;

                /// <summary>
                /// Check if status code is 2xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches2xxStatusCode(int code) =>
                    code >= 200 && code <= 299;

                /// <summary>
                /// Check if status code is 3xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches3xxStatusCode(int code) =>
                    code >= 300 && code <= 399;

                /// <summary>
                /// Check if status code is 4xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches4xxStatusCode(int code) =>
                    code >= 400 && code <= 499;

                /// <summary>
                /// Check if status code is 5xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches5xxStatusCode(int code) =>
                    code >= 500 && code <= 599;

                /// <summary>
                /// Validate the response
                /// </summary>
                /// <param name="validationLevel">Validation level</param>
                /// <returns>The validation result</returns>
                internal abstract ValidationContext Validate(ValidationLevel validationLevel);

                /// <summary>
                /// Read response content as json
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="cancellationToken">Cancellation token</param>
                protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
                {
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return document.RootElement.Clone();
                }

                /// <summary>
                /// Construct response
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="configuration">Web client configuration</param>
                /// <param name="cancellationToken">Cancellation token</param>
                internal static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default) =>
                    response.StatusCode switch
                    {
                        _ when Default.MatchesStatusCode(response.StatusCode) => Default.BindAsync(response, configuration, cancellationToken),
                        _ => Response.Unknown.BindAsync(response, cancellationToken)
                    };
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.Unknown.g.cs", Cancellation)
            .Should().Be(ExpectedUnknownClass.ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.Default.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                /// <summary>
                /// <para>
                /// Default response
                /// </para>
                /// </summary>
                public abstract partial class Default : Response
                {
                    protected Default(HttpResponseMessage response)
                    {
                        StatusCode = response.StatusCode;
                    }

                    internal static bool MatchesStatusCode(HttpStatusCode statusCode) =>
                        true;

                    /// <summary>
                    /// Response status code
                    /// </summary>
                    public HttpStatusCode StatusCode { get; private set; }

                    /// <summary>
                    /// Bind content from http response
                    /// </summary>
                    /// <param name="response">Http response message to bind from</param>
                    /// <param name="configuration">Web client configuration</param>
                    /// <param name="cancellationToken">Cancellation token</param>
                    /// <returns>An awaitable task for the response content</returns>
                    internal new static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
                    {
                        return Empty.BindAsync(response, cancellationToken);
                    }

                    /// <summary>
                    /// Create a validation context
                    /// </summary>
                    /// <returns>Validation context</returns>
                    protected ValidationContext CreateValidationContext() =>
                        ValidationContext.ValidContext.UsingStack().UsingResults();

                    /// <inheritdoc/>
                    internal override ValidationContext Validate(ValidationLevel validationLevel)
                    {
                        var validationContext = CreateValidationContext();
                        return validationContext;
                    }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.Default.Empty.g.cs", Cancellation)
            .Should().Be(ExpectedEmptyClass("Default").ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ResponseWithJsonContent_GeneratesContentTypedResponseClass()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
              "get": {
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "name": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
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

        compilation.Output("TestClient.Foo0.Get.Response.g.cs", testOutputHelper, Cancellation);
        diagnostics.Should().BeEmpty();
        compilation.GetSource("TestClient.Foo0.Get.Response.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            /// <summary>
            /// Contains the operation's response objects
            /// </summary>
            public abstract partial class Response
            {
                /// <summary>
                /// Check if status code is 1xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches1xxStatusCode(int code) =>
                    code >= 100 && code <= 199;

                /// <summary>
                /// Check if status code is 2xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches2xxStatusCode(int code) =>
                    code >= 200 && code <= 299;

                /// <summary>
                /// Check if status code is 3xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches3xxStatusCode(int code) =>
                    code >= 300 && code <= 399;

                /// <summary>
                /// Check if status code is 4xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches4xxStatusCode(int code) =>
                    code >= 400 && code <= 499;

                /// <summary>
                /// Check if status code is 5xx
                /// </summary>
                /// <param name="code">Status code to match</param>
                /// <returns>true if code matches</returns>
                protected static bool Matches5xxStatusCode(int code) =>
                    code >= 500 && code <= 599;

                /// <summary>
                /// Validate the response
                /// </summary>
                /// <param name="validationLevel">Validation level</param>
                /// <returns>The validation result</returns>
                internal abstract ValidationContext Validate(ValidationLevel validationLevel);

                /// <summary>
                /// Read response content as json
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="cancellationToken">Cancellation token</param>
                protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
                {
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return document.RootElement.Clone();
                }

                /// <summary>
                /// Construct response
                /// </summary>
                /// <param name="response">Response message</param>
                /// <param name="configuration">Web client configuration</param>
                /// <param name="cancellationToken">Cancellation token</param>
                internal static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default) =>
                    response.StatusCode switch
                    {
                        _ when OK200.MatchesStatusCode(response.StatusCode) => OK200.BindAsync(response, configuration, cancellationToken),
                        _ => Response.Unknown.BindAsync(response, cancellationToken)
                    };

                public interface IAcceptContent
                {
                    public abstract static MediaTypeWithQualityHeaderValue MediaType { get; }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.Unknown.g.cs", Cancellation)
            .Should().Be(ExpectedUnknownClass.ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                /// <summary>
                /// <para>
                /// OK
                /// </para>
                /// </summary>
                public abstract partial class OK200 : Response
                {
                    protected OK200(HttpResponseMessage response)
                    {
                        StatusCode = response.StatusCode;
                    }

                    internal static bool MatchesStatusCode(HttpStatusCode statusCode) =>
                        ((int)statusCode) == 200;

                    /// <summary>
                    /// Response status code
                    /// </summary>
                    public HttpStatusCode StatusCode { get; private set; }

                    /// <summary>
                    /// Bind content from http response
                    /// </summary>
                    /// <param name="response">Http response message to bind from</param>
                    /// <param name="configuration">Web client configuration</param>
                    /// <param name="cancellationToken">Cancellation token</param>
                    /// <returns>An awaitable task for the response content</returns>
                    internal new static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
                    {
                        var contentType = response.Content.Headers.ContentType;
                        return contentType switch
                        {
                            null => Unknown.BindAsync(response, cancellationToken),
                            _ when contentType.IsSubsetOf(ApplicationJson.MediaType) => ApplicationJson.BindAsync(response, configuration, cancellationToken),
                            _ => Unknown.BindAsync(response, cancellationToken)
                        };
                    }

                    /// <summary>
                    /// Create a validation context
                    /// </summary>
                    /// <returns>Validation context</returns>
                    protected ValidationContext CreateValidationContext() =>
                        ValidationContext.ValidContext.UsingStack().UsingResults();

                    /// <inheritdoc/>
                    internal override ValidationContext Validate(ValidationLevel validationLevel)
                    {
                        var validationContext = CreateValidationContext();
                        return validationContext;
                    }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.Unknown.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                public partial class OK200
                {
                    /// <summary>
                    /// Response for unknown content
                    /// </summary>
                    public new sealed class Unknown : OK200
                    {
                        public Stream Content { get; }

                        private Unknown(Stream content, HttpResponseMessage response) : base(response)
                        {
                            Content = content;
                        }

                        /// <summary>
                        /// Construct response for unknown content
                        /// </summary>
                        /// <param name="response">Response message</param>
                        /// <param name="cancellationToken">Cancellation token</param>
                        internal static async Task<Response> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
                        {
                            var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                                .ConfigureAwait(false);
                            return new Unknown(stream, response);
                        }

                        /// <inheritdoc/>
                        internal override ValidationContext Validate(ValidationLevel validationLevel) =>
                            base.Validate(validationLevel);
                    }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));

        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.ApplicationJson.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using Corvus.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                public partial class OK200
                {
                    /// <summary>
                    /// Response for content application/json
                    /// </summary>
                    public sealed class ApplicationJson : OK200, IAcceptContent
                    {
                        public Example.Paths.Foo.Get.Responses._200.Content.ApplicationJson Content { get; }

                        private ApplicationJson(JsonElement content, HttpResponseMessage response) :
                            base(response)
                        {
                            Content = Example.Paths.Foo.Get.Responses._200.Content.ApplicationJson.FromJson(content);
                        }

                        /// <summary>
                        /// Construct response for content application/json
                        /// </summary>
                        /// <param name="response">Response message</param>
                        /// <param name="cancellationToken">Cancellation token</param>
                        internal new static async Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration _, CancellationToken cancellationToken = default)
                        {
                            var content = await OK200.ReadJsonAsync(response, cancellationToken)
                                .ConfigureAwait(false);
                            return new ApplicationJson(content, response);
                        }

                        public static MediaTypeWithQualityHeaderValue MediaType { get; } = MediaTypeWithQualityHeaderValue.Parse("application/json");

                        private const string ContentSchemaLocation = "#/paths/~1foo/get/responses/200/content/application~1json/schema";
                        /// <inheritdoc/>
                        internal override ValidationContext Validate(ValidationLevel validationLevel)
                        {
                            var validationContext = base.Validate(validationLevel);
                            return Content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
                        }
                    }
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ResponseWithHeader_GeneratesTypedResponseHeaders()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
              "get": {
                "responses": {
                  "200": {
                    "description": "OK",
                    "headers": {
                      "Location": {
                        "required": true,
                        "schema": { "type": "string" }
                      }
                    }
                  }
                }
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

        compilation.Output("TestClient.Foo0.Get.Response.OK200.g.cs", testOutputHelper, Cancellation);
        compilation.GetSource("TestClient.Foo0.Get.Response.OK200.g.cs", Cancellation).Should().Be(
            """"
            #nullable enable
            using Corvus.Json;
            using System.Net;
            using System.Net.Http.Headers;
            using System.Text.Json;
            
            namespace Example;
            public partial class TestClient
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public partial class Response
                        {
                            /// <summary>
                            /// <para>
                            /// OK
                            /// </para>
                            /// </summary>
                            public abstract partial class OK200 : Response
                            {
                                protected OK200(HttpResponseMessage response)
                                {
                                    StatusCode = response.StatusCode;
                                    Headers = ResponseHeaders.Bind(response);
                                }
            
                                internal static bool MatchesStatusCode(HttpStatusCode statusCode) =>
                                    ((int)statusCode) == 200;
            
                                /// <summary>
                                /// Response status code
                                /// </summary>
                                public HttpStatusCode StatusCode { get; private set; }
            
                                /// <summary>
                                /// Response Headers
                                /// </summary> 
                                public ResponseHeaders Headers { get; private set; }
            
                                /// <summary>
                                /// Response Headers
                                /// </summary> 
                                public sealed class ResponseHeaders 
                                {
                                    private readonly BindResult<Corvus.Json.JsonString> _location;

                                    private ResponseHeaders(HttpResponseMessage response)
                                    {
                                        _location = response.Bind<Corvus.Json.JsonString>(
                                            """
                                            {
                                              "name": "Location",
                                              "in": "header",
                                              "required": true,
                                              "schema": {
                                                "type": "string"
                                              }
                                            } 
                                            """);
                                    }

                                    internal static ResponseHeaders Bind(HttpResponseMessage response) =>
                                        new ResponseHeaders(response);

                                    public Corvus.Json.JsonString Location => _location.Value;

                                    internal ValidationContext Validate(ValidationContext validationContext,
                                        ValidationLevel validationLevel)
                                    {
                                        validationContext = _location.Validate("#/paths/~1foo/get/responses/200/headers/Location/schema", true, validationContext, validationLevel);
                                        return validationContext;
                                    }
                                }
            
                                /// <summary>
                                /// Bind content from http response
                                /// </summary>
                                /// <param name="response">Http response message to bind from</param>
                                /// <param name="configuration">Web client configuration</param>
                                /// <param name="cancellationToken">Cancellation token</param>
                                /// <returns>An awaitable task for the response content</returns>
                                internal new static Task<Response> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
                                {
                                    return Empty.BindAsync(response, cancellationToken);
                                }
            
                                /// <summary>
                                /// Create a validation context
                                /// </summary>
                                /// <returns>Validation context</returns>
                                protected ValidationContext CreateValidationContext() =>
                                    ValidationContext.ValidContext.UsingStack().UsingResults();
            
                                /// <inheritdoc/>
                                internal override ValidationContext Validate(ValidationLevel validationLevel)
                                {
                                    var validationContext = CreateValidationContext();
                                    validationContext = Headers.Validate(validationContext, validationLevel);
                                    return validationContext;
                                }
                            }
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ResponseWithContent_GeneratesAcceptClassInSeparateFile()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
              "get": {
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "name": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
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

        compilation.Output("TestClient.Foo0.Get.Response.Accept.g.cs", testOutputHelper, Cancellation);
        compilation.GetSource("TestClient.Foo0.Get.Response.Accept.g.cs", Cancellation).Should().Be(
""""
#nullable enable
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace Example;
public partial class TestClient
{
    public partial class Foo0
    {
        public partial class Get
        {
            public partial class Response
            {
                public sealed class Accept
                {
                    private Accept() {}
                    public static Accept Content<T>()
                        where T : Response.IAcceptContent =>
                        new Accept().And<T>();

                    public Accept And<T>()
                        where T : Response.IAcceptContent
                    {
                        _mediaTypes.Add(T.MediaType);
                        return this;
                    }

                    private readonly List<MediaTypeWithQualityHeaderValue> _mediaTypes = [];
                    internal MediaTypeWithQualityHeaderValue[] MediaTypes => _mediaTypes.ToArray();
                }
            }
        }
    }
}
#nullable restore
"""".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ResponseWithoutContent_DoesNotGenerateAcceptClass()
    {
        const string spec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
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

        compilation.SyntaxTrees
            .Select(tree => Path.GetFileName(tree.FilePath))
            .Should().NotContain("TestClient.Foo0.Get.Response.Accept.g.cs");
    }
}