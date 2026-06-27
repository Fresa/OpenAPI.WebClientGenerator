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
            internal partial class Foo
            {
                internal partial class Foo0
                {
                    internal partial class Get
                    {
                        internal abstract partial class SecurityRequirement
                        {
                            internal sealed partial class BearerAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.BearerAuth _scheme;

                                internal BearerAuth(string token) =>
                                    _scheme = new SecuritySchemes.BearerAuth(token);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }
                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """");
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
            internal partial class Foo
            {
                internal partial class Foo0
                {
                    internal partial class Get
                    {
                        internal abstract partial class SecurityRequirement
                        {
                            internal sealed partial class ApiKeyAuthAndBearerAuth : SecurityRequirement
                            {
                                internal required SecuritySchemes.ApiKeyAuth ApiKeyAuth { init; get; }
                                internal required SecuritySchemes.BearerAuth BearerAuth { init; get; }

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
            """");
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
            internal partial class Foo
            {
                internal partial class Foo0
                {
                    internal partial class Get
                    {
                        internal abstract partial class SecurityRequirement
                        {
                            internal sealed partial class ApiKeyAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.ApiKeyAuth _scheme;

                                internal ApiKeyAuth(string apiKey) =>
                                    _scheme = new SecuritySchemes.ApiKeyAuth(apiKey);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }
                            internal sealed partial class BearerAuth : SecurityRequirement
                            {
                                private readonly SecuritySchemes.BearerAuth _scheme;

                                internal BearerAuth(string token) =>
                                    _scheme = new SecuritySchemes.BearerAuth(token);

                                internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
                            }
                            internal abstract void AddTo(RequestBuilder requestBuilder);
                        }
                    }
                }
            }
            #nullable restore
            """");
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
}