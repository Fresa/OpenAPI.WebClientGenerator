namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class BindResultGenerator(
    string @namespace)
{
    private const string ClassName = "BindResult";

    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using Corvus.Json;
        
        namespace {{@namespace}};

        /// <summary>
        /// Extension methods for http response messages
        /// </summary>
        internal readonly struct {{ClassName}}<T>
            where T : struct, IJsonValue<T>
        {
            private readonly T _value;
            private readonly string? _error;
        
            internal {{ClassName}}(T value) 
            { 
                _value = value; 
                _error = null; 
            }
            internal {{ClassName}}(string error)
            {
                _value = T.Undefined;
                _error = error;
            }
        
            internal T Value => _value;
            
            internal ValidationContext Validate(
                string schemaLocation, 
                bool isRequired,
                ValidationContext validationContext, 
                ValidationLevel validationLevel)
                => _error is null
                    ? _value.Validate(schemaLocation, isRequired,
                        validationContext, validationLevel)
                    : validationContext
                        .PushSchemaLocation(schemaLocation)
                        .WithResult(false, _error)
                        .PopLocation();
        }
        #nullable restore
        """);
}