namespace OpenAPI.WebClientGenerator.CodeGeneration.Authentication;

internal static class SecurityScheme
{
    internal static class ApiKey
    {
        internal static Argument Key { get; } = new("apiKey", "T");
    }
    
    internal static class Http
    {
        internal static Argument Username { get; } = new("username", "string");
        internal static Argument Password { get; } = new("password", "string");
    }

    internal static class Bearer
    {
        internal static Argument Token { get; } = new("token", "string");
    }

    internal static class Custom
    {
        internal static Argument Apply { get; } = new("apply", "System.Action<RequestBuilder>");
    }

    internal sealed class Argument(string name, string type)
    {
        public string Type { get; } = type;
        public string Name { get; } = name;
    }    
}