using System.Net.Http;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class OperationGenerator(
    HttpMethod operation,
    ParameterGenerator[] parameterGenerators,
    RequestBodyGenerator requestBodyGenerator,
    ResponseGenerator responseGenerator,
    AuthenticationGenerator? authenticationGenerator)
{
    public HttpMethod Operation { get; } = operation;
    public string OperationClassName { get; } = operation.Method.ToLower().ToPascalCase();
    public RequestBodyGenerator RequestBodyGenerator { get; } = requestBodyGenerator;
    public QueryGenerator QueryGenerator { get; } = new(parameterGenerators);
    public HeadersGenerator HeadersGenerator { get; } = new(parameterGenerators);
    public ResponseGenerator ResponseGenerator { get; } = responseGenerator;
    public AuthenticationGenerator? AuthenticationGenerator { get; } = authenticationGenerator;
}