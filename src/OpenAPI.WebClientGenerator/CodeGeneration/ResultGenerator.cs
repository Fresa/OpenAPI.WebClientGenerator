namespace OpenAPI.WebClientGenerator.CodeGeneration;

public class ResultGenerator(string @namespace)
{
    private const string ClassName = "Result";
    internal SourceCode GenerateClass() => new($"{ClassName}.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace {{@namespace}};

/// <summary>
/// The result of an operation
/// </summary>
/// <typeparam name="T">The response type</typeparam>
public sealed class {{ClassName}}<T>
{
    private {{ClassName}}(ImmutableList<ValidationResult> requestValidationResults)
    {
        ValidationResults = requestValidationResults;
        FailedRequestValidation = true;
        IsSuccessful = false;
        Response = default;
    }

    private {{ClassName}}(T response, ImmutableList<ValidationResult> responseValidationResults)
    {
        ValidationResults = responseValidationResults;
        Response = response;
        FailedRequestValidation = false;
        IsSuccessful = responseValidationResults.IsValid();
    }

    internal static {{ClassName}}<T> WithInvalidRequest(ImmutableList<ValidationResult> requestValidationResults) =>
        new(requestValidationResults);
    internal static {{ClassName}}<T> WithResponse(T response, ImmutableList<ValidationResult> responseValidationResults) =>
        new(response, responseValidationResults);

    /// <summary>
    /// The response or null if <see cref="FailedRequestValidation"/> is true
    /// </summary>
    public T? Response { get; }

    /// <summary>
    /// Validation results for the request if <see cref="FailedRequestValidation"/> is true, otherwise validation results for the response
    /// </summary>
    public ImmutableList<ValidationResult> ValidationResults { get; }
    
    /// <summary>
    /// True if request failed validation.
    /// Note: The request was never sent if it failed validation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Response))]
    public bool FailedRequestValidation { get; }
    
    /// <summary>
    /// True if the operation was executed successfully.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Response))]
    public bool IsSuccessful { get; }
}
#nullable restore
""");
}