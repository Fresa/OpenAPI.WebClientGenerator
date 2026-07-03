namespace OpenAPI.WebClientGenerator.CodeGeneration.Authentication;

internal static class ArgumentExtensions
{
    internal static string GetMethodParameter(this SecurityScheme.Argument argument) =>
        $"{argument.Type} {argument.Name}";
}