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
/// Web client
/// </summary>
internal sealed partial class {{ClassName}}(
    HttpClient httpClient, 
    WebClientConfiguration? configuration = null)
{
    private WebClientConfiguration _configuration = configuration ?? new();
}
#nullable restore
""");
    }

    public PathsGenerator GetPathsGenerator() => new(this);
}