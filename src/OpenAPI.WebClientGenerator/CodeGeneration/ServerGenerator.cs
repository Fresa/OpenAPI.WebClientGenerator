using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ServerGenerator(OpenApiDocument openApiDocument, string @namespace)
{
    private readonly IList<OpenApiServer> _servers = openApiDocument.Servers ?? [];

    internal SourceCode? GenerateClass()
    {
        if (!_servers.Any())
        {
            return null;
        }

        return new SourceCode("Server.g.cs",
$$"""
#nullable enable
using System;

namespace {{@namespace}};

internal static class Servers
{
    internal class Server(Uri baseUri)
    {
        /// <summary>
        /// The base uri of the server.
        /// </summary>
        internal Uri BaseUrl => baseUri;
    }{{_servers.Select(GenerateAccessor)
        .Concat(_servers
            .Select((server, index) => (server, index))
            .Where(item => item.server.Variables?.Any() == true)
            .Select(item => GenerateServerClass(item.server, item.index)))
        .InterleaveEmpty()
        .AggregateToString(member => member)
        .PrependNewline()
        .Indent(4)
    }}
}
#nullable restore
""");
    }

    private static string GenerateAccessor(OpenApiServer server, int index) =>
        server.Variables?.Any() == true
        ?
$$"""
internal static {{ServerName(server, index)}} Use{{ServerName(server, index)}}({{Parameters(ServerName(server, index), server.Variables)}}) =>
    new({{string.Join(", ", server.Variables.Keys.Select(key => key.ToCamelCase()))}});
"""
        : 
$$"""
internal static readonly Server {{ServerName(server, index)}} = new(new Uri("{{server.Url}}", UriKind.RelativeOrAbsolute));
""";

    private static string GenerateServerClass(OpenApiServer server, int index)
    {
        var url = server.Variables?.Aggregate(server.Url ?? string.Empty, (current, variable) =>
            current.Replace($"{{{variable.Key}}}", $"{{{variable.Key.ToCamelCase()}}}")) ?? string.Empty;
        return $$"""
internal sealed class {{ServerName(server, index)}}({{Parameters(ServerName(server, index), server.Variables)}}) :
    Server(new Uri($"{{url}}", UriKind.RelativeOrAbsolute))
{{{server.Variables?
    .Where(variable => 
        variable.Value.Enum?.Any() == true)
    .ToDictionary(variable => 
        variable.Key, variable => 
        variable.Value.Enum ?? [])
    .AggregateToString(GenerateEnum)
    .Indent(4)
}}
}
""";
    }

    private static string GenerateEnum(KeyValuePair<string, List<string>> @enum) =>
$$"""
internal enum {{@enum.Key.ToPascalCase()}}
{{{@enum.Value
    .AggregateToString(value => 
        $"{value.ToPascalCase()},")
    .TrimEnd(',')
    .Indent(4)
}}
}
""";

    private static string Parameters(string serverName, IDictionary<string, OpenApiServerVariable>? variables) =>
        string.Join(", ", variables?.Select(variable => variable.Value.Enum?.Any() == true
            ? $"{serverName}.{variable.Key.ToPascalCase()} {variable.Key.ToCamelCase()} = {serverName}.{variable.Key.ToPascalCase()}.{variable.Value.Default.ToPascalCase()}"
            : $"string {variable.Key.ToCamelCase()} = \"{variable.Value.Default}\"") ?? []);

    private static string ServerName(OpenApiServer server, int index) =>
        server.Name.ToPascalCase() is { Length: > 0 } name ? name : $"Server{index}";
}