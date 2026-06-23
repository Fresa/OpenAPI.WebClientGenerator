using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.OpenApi;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class RequestBuilderGenerator(
    OpenApiSpecVersion openApiSpecVersion)
{
    internal SourceCode Generate(string @namespace) =>
        new("RequestBuilder.g.cs", 
$$"""
#nullable enable
using Corvus.Json;
using OpenAPI.ParameterStyleParsers;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace {{@namespace}};

internal sealed partial class RequestBuilder(HttpClient httpClient, WebClientConfiguration configuration)
{
    private static readonly ConcurrentDictionary<string, IParameterValueParser> ParserCache = new();
    private const string ParameterValueParserVersion = "{{openApiSpecVersion.GetParameterVersion()}}";
    
    private readonly Dictionary<string, Func<string?>> _pathParameters = new();
    internal void AddPathParameter<T>(
        string name, 
        T value,
        string schemaLocation, 
        string parameterSpecificationAsJson)
        where T : struct, IJsonValue<T>
    {
        if (configuration.ValidateRequests)
            ValidationContext = value.Validate(schemaLocation, true, ValidationContext, configuration.ValidationLevel);
        _pathParameters[name] = () => Serialize(value, parameterSpecificationAsJson);
    }
    
    private readonly Dictionary<string, Func<string?>> _queryParameters = new();
    internal void AddQuery<T>(
        string name,
        T? value,
        bool isRequired,
        string schemaLocation, 
        string parameterSpecificationAsJson)
        where T : struct, IJsonValue<T>
    {
        var nonNullableValue = value ?? T.Undefined;
        if (configuration.ValidateRequests)
            ValidationContext = nonNullableValue.Validate(schemaLocation, isRequired, ValidationContext, configuration.ValidationLevel);
        if (value is null)
            return;
        _queryParameters[name] = () => Serialize(nonNullableValue, parameterSpecificationAsJson);
    }

    internal void AddQuery(string key, string value)
    {
        _queryParameters[key] = () => value;
    }

    private readonly Dictionary<string, Func<string?>> _headerParameters = new();
    internal void AddHeader<T>(
        string name,
        T? value,
        bool isRequired,
        string schemaLocation, 
        string parameterSpecificationAsJson)
        where T : struct, IJsonValue<T>
    {
        var nonNullableValue = value ?? T.Undefined;
        if (configuration.ValidateRequests)
            ValidationContext = nonNullableValue.Validate(schemaLocation, isRequired, ValidationContext, configuration.ValidationLevel);
        if (value is null)
            return;
        _headerParameters[name] = () => Serialize(nonNullableValue, parameterSpecificationAsJson);
    }

    internal void AddHeader(string key, string value)
    {
        _headerParameters[key] = () => value;
    }

    private MediaTypeWithQualityHeaderValue[] _acceptMediaTypes = [];
    internal void AcceptMediaTypes(MediaTypeWithQualityHeaderValue[] mediaTypes)
    {
        _acceptMediaTypes = mediaTypes;
    }

    internal Task<HttpResponseMessage> SendAsync(string pathTemplate, 
        string httpMethod,
        HttpContent? content,
        CancellationToken cancellation = default)
    {
        var path = _pathParameters.Aggregate(pathTemplate, (uri, parameter) => 
            uri.Replace("{" + parameter.Key + "}", parameter.Value()));
        var query = string.Join("&", _queryParameters.Values.Select(serializeValue => serializeValue()));
        if (query != string.Empty)
        {
            path += $"?{query}";
        }
        
        var message = new HttpRequestMessage
        {
            Method = new HttpMethod(httpMethod),
            RequestUri = new Uri(path, UriKind.Relative),
            Content = content
        };
        foreach (var header in _headerParameters)
        {
            message.Headers.Add(header.Key, header.Value());
        }
        foreach (var accept in _acceptMediaTypes)
        {
            message.Headers.Accept.Add(accept);
        }
        return httpClient.SendAsync(message, cancellation);
    }
    
    private string? Serialize<T>(T value, string parameterSpecificationAsJson)
        where T : struct, IJsonValue<T>
    {
        var parser = ParserCache.GetOrAdd(parameterSpecificationAsJson, 
            _ => ParameterValueParserFactory.OpenApi(ParameterValueParserVersion, parameterSpecificationAsJson));        
        var jsonValue = value.Serialize();
        return parser.Serialize(JsonNode.Parse(jsonValue));
    }
    
    internal ValidationContext ValidationContext { get; private set; } = ValidationContext.ValidContext.UsingStack().UsingResults();
}
#nullable restore
""");

}