using System.IO;
using System.Linq;
using System.Threading;
using AwesomeAssertions;
using OpenAPI.WebClientGenerator.Tests.Utils;
using Xunit;

namespace OpenAPI.WebClientGenerator.Tests;

public class SecurityRequirementGeneratorTests(ITestOutputHelper testOutputHelper)
{
    private const string SecurityRequirementFile = "Foo.Foo0.Get.SecurityRequirement.g.cs";

    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string Spec(string securitySchemes, string operationSecurity) =>
        $$"""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/foo": {
              "get": {
                {{operationSecurity}}
                "responses": { "200": { "description": "OK" } }
              }
            }
          },
          "components": {
            "securitySchemes": {
              {{securitySchemes}}
            }
          }
        }
        """;

    private string Generate(string securitySchemes, string operationSecurity, out string[] generatedFiles)
    {
        var compilation = WebClientGenerator.SetupFromContent(Spec(securitySchemes, operationSecurity),
            clientName: "Foo", @namespace: "Example",
            diagnostics: out var diagnostics, cancellationToken: Cancellation);
        diagnostics.Should().BeEmpty();
        generatedFiles = compilation.SyntaxTrees
            .Select(tree => Path.GetFileName(tree.FilePath))
            .ToArray();
        compilation.Output(SecurityRequirementFile, testOutputHelper, Cancellation);
        return generatedFiles.Contains(SecurityRequirementFile)
            ? compilation.GetSource(SecurityRequirementFile, Cancellation)
            : string.Empty;
    }

    [Fact]
    public void OperationWithOneScheme_GeneratesAuthentication()
    {
        var source = Generate(
            """
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            """
            "security": [ { "BearerAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class BearerAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.BearerAuth _scheme;

                                public BearerAuth(string token) =>
                                    _scheme = new SecuritySchemes.BearerAuth(token);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithTwoSchemes_GeneratesAuthenticationWithBothSchemes()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" },
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            """
            "security": [ { "ApiKeyAuth": [], "BearerAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuthAndBearerAuth : SecurityRequirement
                            {
                                public required SecuritySchemes.ApiKeyAuth ApiKeyAuth { init; get; }
                                public required SecuritySchemes.BearerAuth BearerAuth { init; get; }

                                internal override void AddTo(RequestBuilder requestBuilder)
                                {
                                    ApiKeyAuth.AddTo(requestBuilder);
                                    BearerAuth.AddTo(requestBuilder);
                                }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationAcceptingEitherScheme_GeneratesOneAuthenticationForEachScheme()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" },
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            """
            "security": [ { "ApiKeyAuth": [] }, { "BearerAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.ApiKeyAuth _scheme;

                                public ApiKeyAuth(Corvus.Json.JsonAny apiKey) =>
                                    _scheme = new SecuritySchemes.ApiKeyAuth(apiKey, false, string.Empty,
                                        """
                                        {
                                            "name": "X-API-Key",
                                            "in": "header"
                                        }
                                        """);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }

                            public sealed partial class BearerAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.BearerAuth _scheme;

                                public BearerAuth(string token) =>
                                    _scheme = new SecuritySchemes.BearerAuth(token);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithoutSecurity_DoesNotGenerateAnSecurityRequirementFile()
    {
        Generate(
            """
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            operationSecurity: "",
            out var generatedFiles);

        generatedFiles.Should().NotContain(SecurityRequirementFile);
    }

    [Fact]
    public void ApiKeySchemeReferencingParameter_DoesNotBindTheValue()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" }
            """,
            """
            "parameters": [
              { "name": "X-API-Key", "in": "header", "required": true, "schema": { "type": "string" } }
            ],
            "security": [ { "ApiKeyAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuth : SecurityRequirement
                            {
                                /// <summary>
                                /// The key is inferred from the "X-API-Key" request header parameter.
                                /// </summary>
                                public ApiKeyAuth()
                                {
                                }

                                internal override void AddTo(RequestBuilder requestBuilder) { }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ApiKeySchemeWithoutReferencedParameter_FallsBackToJsonAny()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" }
            """,
            """
            "security": [ { "ApiKeyAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.ApiKeyAuth _scheme;

                                public ApiKeyAuth(Corvus.Json.JsonAny apiKey) =>
                                    _scheme = new SecuritySchemes.ApiKeyAuth(apiKey, false, string.Empty,
                                        """
                                        {
                                            "name": "X-API-Key",
                                            "in": "header"
                                        }
                                        """);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultiSchemeWithApiKeyReferencingParameter_GeneratesTypedProperty()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" },
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            """
            "parameters": [
              { "name": "X-API-Key", "in": "header", "required": true, "schema": { "type": "string" } }
            ],
            "security": [ { "ApiKeyAuth": [], "BearerAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuthAndBearerAuth : SecurityRequirement
                            {
                                public required SecuritySchemes.ApiKeyAuth ApiKeyAuth { init; get; }
                                public required SecuritySchemes.BearerAuth BearerAuth { init; get; }

                                internal override void AddTo(RequestBuilder requestBuilder)
                                {
                                    ApiKeyAuth.AddTo(requestBuilder);
                                    BearerAuth.AddTo(requestBuilder);
                                }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MultiSchemeWithApiKeyWithoutParameter_GeneratesJsonAnyProperty()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" },
            "BearerAuth": { "type": "http", "scheme": "bearer" }
            """,
            """
            "security": [ { "ApiKeyAuth": [], "BearerAuth": [] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuthAndBearerAuth : SecurityRequirement
                            {
                                public required SecuritySchemes.ApiKeyAuth ApiKeyAuth { init; get; }
                                public required SecuritySchemes.BearerAuth BearerAuth { init; get; }

                                internal override void AddTo(RequestBuilder requestBuilder)
                                {
                                    ApiKeyAuth.AddTo(requestBuilder);
                                    BearerAuth.AddTo(requestBuilder);
                                }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithMultipleSchemes_GeneratesScopesPerScopedScheme()
    {
        var source = Generate(
            """
            "ApiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" },
            "OAuth2": {
              "type": "oauth2",
              "flows": {
                "authorizationCode": {
                  "authorizationUrl": "https://example.com/auth",
                  "tokenUrl": "https://example.com/token",
                  "scopes": {
                    "read:foo": "Read foo",
                    "write:foo": "Write foo"
                  }
                }
              }
            }
            """,
            """
            "security": [ { "ApiKeyAuth": [], "OAuth2": [ "read:foo", "write:foo" ] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class ApiKeyAuthAndOAuth2 : SecurityRequirement
                            {
                                public static class Scopes
                                {
                                    public static class OAuth2
                                    {
                                        public const string ReadFoo = "read:foo";
                                        public const string WriteFoo = "write:foo";
                                    }
                                }

                                public required SecuritySchemes.ApiKeyAuth ApiKeyAuth { init; get; }
                                public required SecuritySchemes.OAuth2 OAuth2 { init; get; }

                                internal override void AddTo(RequestBuilder requestBuilder)
                                {
                                    ApiKeyAuth.AddTo(requestBuilder);
                                    OAuth2.AddTo(requestBuilder);
                                }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithScopedScheme_GeneratesScopesForTheOperation()
    {
        var source = Generate(
            """
            "OAuth2": {
              "type": "oauth2",
              "flows": {
                "authorizationCode": {
                  "authorizationUrl": "https://example.com/auth",
                  "tokenUrl": "https://example.com/token",
                  "scopes": {
                    "read:foo": "Read foo",
                    "write:foo": "Write foo"
                  }
                }
              }
            }
            """,
            """
            "security": [ { "OAuth2": [ "read:foo", "write:foo" ] } ],
            """,
            out _);

        source.Should().Be(
            """"
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class OAuth2 : SecurityRequirement
                            {
                                public static class Scopes
                                {
                                    public const string ReadFoo = "read:foo";
                                    public const string WriteFoo = "write:foo";
                                }

                                private readonly SecuritySchemes.OAuth2 _scheme;

                                public OAuth2(string token) =>
                                    _scheme = new SecuritySchemes.OAuth2(token);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """".ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OperationWithEmptySecurityRequirement_GenerateAnonymousRequirement()
    {
        var source = Generate(
            "",
            operationSecurity: 
            """
            "security": [ {} ],
            """,
            out _);

        source.Should().Be(
            """
            #nullable enable
            namespace Example;
            public partial class Foo
            {
                public partial class Foo0
                {
                    public partial class Get
                    {
                        public abstract partial class SecurityRequirement
                        {
                            public sealed partial class Anonymous : SecurityRequirement
                            {
                                internal override void AddTo(RequestBuilder requestBuilder) { }
                            }

                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }
}