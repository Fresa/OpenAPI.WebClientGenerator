using System;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.OpenApi;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ParameterGenerator(
    OpenApiSpecVersion openApiSpecVersion,
    TypeDeclaration typeDeclaration,
    IOpenApiParameter parameter)
{
    internal string FullyQualifiedTypeName =>
        $"{FullyQualifiedTypeDeclarationIdentifier}{(parameter.Required ? "" : "?")}";

    internal string FullyQualifiedTypeDeclarationIdentifier => typeDeclaration.FullyQualifiedDotnetTypeName();

    internal string ParameterName { get; } = parameter.GetName();
    internal bool IsParameterRequired { get; } = parameter.Required;
    internal ParameterLocation Location { get; } = parameter.In ?? throw new NullReferenceException("In is null");
    internal string SchemaLocation { get; } = typeDeclaration.RelativeSchemaLocation;

    internal string ParameterSpecificationAsJson { get; } = parameter.Serialize(openApiSpecVersion).ToString();
    
    internal bool IsSecuritySchemeParameter(IOpenApiSecurityScheme scheme) =>
        scheme.In == parameter.In &&
        scheme.Name == parameter.Name;
}