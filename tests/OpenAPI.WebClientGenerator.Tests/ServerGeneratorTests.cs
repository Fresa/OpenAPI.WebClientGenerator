using System.IO;
using System.Linq;
using System.Threading;
using AwesomeAssertions;
using OpenAPI.WebClientGenerator.Tests.Utils;
using Xunit;

namespace OpenAPI.WebClientGenerator.Tests;

public class ServerGeneratorTests(ITestOutputHelper testOutputHelper)
{
    private const string ServerFile = "Server.g.cs";

    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private string Generate(string spec, out string[] generatedFiles)
    {
        var compilation = WebClientGenerator.SetupFromContent(spec,
            clientName: "Foo", @namespace: "Example",
            diagnostics: out var diagnostics, cancellationToken: Cancellation);
        diagnostics.Should().BeEmpty();
        generatedFiles = compilation.SyntaxTrees
            .Select(tree => Path.GetFileName(tree.FilePath))
            .ToArray();
        compilation.Output(ServerFile, testOutputHelper, Cancellation);
        return Enumerable.Contains(generatedFiles, ServerFile)
            ? compilation.GetSource(ServerFile, Cancellation)
            : string.Empty;
    }

    public static TheoryData<string> NamedServerSpecs =>
    [
        """
        {
          "openapi": "3.2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "name": "Production", "description": "The production server.", "url": "https://api.example.com" } ]
        }
        """
    ];

    [Theory]
    [MemberData(nameof(NamedServerSpecs))]
    public void NamedServerWithoutVariables_GeneratesStaticInstance(string spec)
    {
        var source = Generate(spec, out _);

        source.Should().Be(
            """
            #nullable enable
            using System;

            namespace Example;

            internal static class Servers
            {
                internal class Server(Uri baseUri)
                {
                    /// <summary>
                    /// The base uri of the server.
                    /// </summary>
                    internal Uri BaseUrl => baseUri;
                }

                /// <summary>
                /// The production server.
                /// </summary>
                internal static readonly Server Production = new(new Uri("https://api.example.com", UriKind.RelativeOrAbsolute));
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    public static TheoryData<string> UnnamedServerSpecs =>
    [
        """
        {
          "swagger": "2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "host": "api.example.com",
          "basePath": "/v2",
          "schemes": [ "https" ]
        }
        """,
        """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.com/v2" } ]
        }
        """,
        """
        {
          "openapi": "3.1.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.com/v2" } ]
        }
        """,
        """
        {
          "openapi": "3.2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.com/v2" } ]
        }
        """
    ];

    [Theory]
    [MemberData(nameof(UnnamedServerSpecs))]
    public void ServerWithoutName_IsNamedByItsIndex(string spec)
    {
        var source = Generate(spec, out _);

        source.Should().Be(
            """
            #nullable enable
            using System;

            namespace Example;

            internal static class Servers
            {
                internal class Server(Uri baseUri)
                {
                    /// <summary>
                    /// The base uri of the server.
                    /// </summary>
                    internal Uri BaseUrl => baseUri;
                }

                /// <summary>
                /// The Server0 server.
                /// </summary>
                internal static readonly Server Server0 = new(new Uri("https://api.example.com/v2", UriKind.RelativeOrAbsolute));
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    public static TheoryData<string> ServerWithVariablesSpecs =>
    [
        """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "description": "The tenant server.", "url": "https://{tenant}.example.com/{basePath}", "variables": { "tenant": { "default": "demo" }, "basePath": { "default": "v2" } } } ]
        }
        """,
        """
        {
          "openapi": "3.1.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "description": "The tenant server.", "url": "https://{tenant}.example.com/{basePath}", "variables": { "tenant": { "default": "demo" }, "basePath": { "default": "v2" } } } ]
        }
        """,
        """
        {
          "openapi": "3.2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "description": "The tenant server.", "url": "https://{tenant}.example.com/{basePath}", "variables": { "tenant": { "default": "demo" }, "basePath": { "default": "v2" } } } ]
        }
        """
    ];

    [Theory]
    [MemberData(nameof(ServerWithVariablesSpecs))]
    public void ServerWithVariables_GeneratesStaticFactoryMethod(string spec)
    {
        var source = Generate(spec, out _);

        source.Should().Be(
            """
            #nullable enable
            using System;

            namespace Example;

            internal static class Servers
            {
                internal class Server(Uri baseUri)
                {
                    /// <summary>
                    /// The base uri of the server.
                    /// </summary>
                    internal Uri BaseUrl => baseUri;
                }

                /// <summary>
                /// The tenant server.
                /// </summary>
                internal static Server0 UseServer0(string tenant = "demo", string basePath = "v2") =>
                    new(tenant, basePath);

                /// <summary>
                /// The tenant server.
                /// </summary>
                internal sealed class Server0(string tenant = "demo", string basePath = "v2") :
                    Server(new Uri($"https://{tenant}.example.com/{basePath}", UriKind.RelativeOrAbsolute))
                {
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    public static TheoryData<string> ServerWithEnumVariableSpecs =>
    [
        """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://{region}.example.com", "variables": { "region": { "default": "us", "enum": [ "us", "eu" ] } } } ]
        }
        """,
        """
        {
          "openapi": "3.1.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://{region}.example.com", "variables": { "region": { "default": "us", "enum": [ "us", "eu" ] } } } ]
        }
        """,
        """
        {
          "openapi": "3.2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": [ { "url": "https://{region}.example.com", "variables": { "region": { "default": "us", "enum": [ "us", "eu" ] } } } ]
        }
        """
    ];

    [Theory]
    [MemberData(nameof(ServerWithEnumVariableSpecs))]
    public void ServerWithEnumVariable_GeneratesEnumTypedParameter(string spec)
    {
        var source = Generate(spec, out _);

        source.Should().Be(
            """
            #nullable enable
            using System;

            namespace Example;

            internal static class Servers
            {
                internal class Server(Uri baseUri)
                {
                    /// <summary>
                    /// The base uri of the server.
                    /// </summary>
                    internal Uri BaseUrl => baseUri;
                }

                /// <summary>
                /// The Server0 server.
                /// </summary>
                internal static Server0 UseServer0(Server0.Region region = Server0.Region.Us) =>
                    new(region);

                /// <summary>
                /// The Server0 server.
                /// </summary>
                internal sealed class Server0(Server0.Region region = Server0.Region.Us) :
                    Server(new Uri($"https://{RegionTranslation[region]}.example.com", UriKind.RelativeOrAbsolute))
                {
                    private static readonly Dictionary<Region, string> RegionTranslation = [
                        [Us] = "us",
                        [Eu] = "eu"
                    ];
                    internal enum Region
                    {
                        Us,
                        Eu
                    }
                }
            }
            #nullable restore
            """.ReplaceLineEndings("\n"));
    }

    public static TheoryData<string> WithoutServersSpecs =>
    [
        """
        {
          "swagger": "2.0",
          "info": { "title": "Test", "version": "1.0.0" }
        }
        """,
        """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": []
        }
        """,
        """
        {
          "openapi": "3.1.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": []
        }
        """,
        """
        {
          "openapi": "3.2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "servers": []
        }
        """
    ];

    [Theory]
    [MemberData(nameof(WithoutServersSpecs))]
    public void SpecificationWithoutServers_DoesNotGenerateServerFile(string spec)
    {
        Generate(spec, out var generatedFiles);

        generatedFiles.Should().NotContain(ServerFile);
    }
}