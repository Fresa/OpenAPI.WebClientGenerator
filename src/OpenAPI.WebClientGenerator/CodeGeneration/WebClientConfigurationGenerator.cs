namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class WebClientConfigurationGenerator(string @namespace, WebClientGeneratorConfig generatorConfig)
{
    private const string ClassName = "WebClientConfiguration";
    
    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
            $$"""
              #nullable enable
              using Corvus.Json;
              using Microsoft.AspNetCore.Authorization;
              using System;

              namespace {{@namespace}};

              public sealed class {{ClassName}}
              {
                  /// <summary>
                  /// The uri to the exposed OpenAPI specification used to generate the SDK.
                  /// This is used in the SchemaLocation of the ValidationResult.
                  /// <example>https://localhost/openapi.json</example>
                  /// </summary>
                  public Uri? OpenApiSpecificationUri { get; init; }

                  /// <summary>
                  /// Set validation level
                  /// </summary>
                  public ValidationLevel ValidationLevel { get; init; } = ValidationLevel.{{generatorConfig.ValidationLevel.ToString()}};

                  /// <summary>
                  /// Should responses be validated?
                  /// </summary>
                  public bool ValidateResponses { get; init; } = true;

                  /// <summary>
                  /// Should requests be validated?
                  /// </summary>
                  public bool ValidateRequests { get; init; } = true;
              }
              #nullable restore
              """);
}