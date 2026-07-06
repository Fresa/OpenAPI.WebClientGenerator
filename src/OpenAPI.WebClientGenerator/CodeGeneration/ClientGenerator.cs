using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ClientGenerator(string clientName, string @namespace)
{
    public string ClassName { get; } = clientName.ToPascalCase();
    public string Namespace { get; } = @namespace;

    internal SourceCode Generate()
    {
        return new SourceCode($"{ClassName}.g.cs", $$"""
#nullable enable
using System.Net.Http;

namespace {{Namespace}};

/// <summary>
/// Create web client
/// </summary>
/// <param name="httpClient">The HTTP client used to send requests. Make sure to set base address to the root URL of the API server.</param>
/// <param name="configuration">The web client configuration.</param>
internal sealed partial class {{ClassName}}(
    HttpClient httpClient,
    WebClientConfiguration? configuration = null) : IDisposable
{
    private bool _disposeHttpClient;

    /// <summary>
    /// Create web client
    /// </summary>
    /// <param name="server">The server the client sends requests to.</param>
    /// <param name="configuration">The web client configuration.</param>
    internal {{ClassName}}(
        Servers.Server server,
        WebClientConfiguration? configuration = null) : this(new HttpClient
        {
            BaseAddress = server.BaseUrl
        }, configuration)
    {
        _disposeHttpClient = true;
    }

    private WebClientConfiguration _configuration = configuration ?? new();
    
    public void Dispose()
    {
        if (!_disposeHttpClient)
        {
            return;
        }
        httpClient.Dispose();
    }
}
#nullable restore
""");
    }

    public PathsGenerator GetPathsGenerator() => new(this);
}