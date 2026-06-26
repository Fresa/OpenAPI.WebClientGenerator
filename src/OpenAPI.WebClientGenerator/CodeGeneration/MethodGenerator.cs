using System.Collections.Generic;
using System.Net.Http;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class MethodGenerator(string pathExpression, ParameterGenerator[] parameters)
{
    public string PathExpression { get; } = pathExpression;
    public ParameterGenerator[] Parameters { get; } = parameters;
    internal IReadOnlyCollection<OperationGenerator> Operations => _operations.Values;
    private readonly Dictionary<HttpMethod, OperationGenerator> _operations = new();
    internal Dictionary<string, EntityGenerator> Children { get; } = new();

    public void AddOperation(
        OperationGenerator operation)
    {
        _operations.Add(operation.Operation, operation);
    }

    internal EntityGenerator AddEntity(string name)
    {
        if (Children.TryGetValue(name, out var entity))
            return entity;
        entity = new EntityGenerator(name);
        Children.Add(name, entity);
        return entity;
    }
}