namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class OperationGenerator(
    ParameterGenerator[] parameterGenerators,
    RequestBodyGenerator requestBodyGenerator,
    ResponseGenerator responseGenerator,
    AuthenticationGenerator? authenticationGenerator)
{
    public RequestBodyGenerator RequestBodyGenerator { get; } = requestBodyGenerator;
    public QueryGenerator QueryGenerator { get; } = new(parameterGenerators);
    public HeadersGenerator HeadersGenerator { get; } = new(parameterGenerators);
    public ResponseGenerator ResponseGenerator { get; } = responseGenerator;
    public AuthenticationGenerator? AuthenticationGenerator { get; } = authenticationGenerator;
}