# OpenApi.WebClientGenerator
Generates client SDKs from OpenAPI specifications. 

The generated functionality will route, serialize/deserialize and validate payloads according to the specification.

Supported OpenAPI version:
- [3.2.0](https://spec.openapis.org/oas/v3.2.0.html)
- [3.1.2](https://spec.openapis.org/oas/v3.1.2.html)
- [3.1.1](https://spec.openapis.org/oas/v3.1.1.html)
- [3.1.0](https://spec.openapis.org/oas/v3.1.0.html)
- [3.0.4](https://spec.openapis.org/oas/v3.0.4.html)
- [3.0.3](https://spec.openapis.org/oas/v3.0.3.html)
- [3.0.2](https://spec.openapis.org/oas/v3.0.2.html)
- [3.0.1](https://spec.openapis.org/oas/v3.0.1.html)
- [3.0.0](https://spec.openapis.org/oas/v3.0.0.html)
- [2.0](https://spec.openapis.org/oas/v2.0.html)

## Installation
```Shell
dotnet add package WebClientGenerator.OpenAPI
```

https://www.nuget.org/packages/WebClientGenerator.OpenAPI

## Getting Started
1. Add a reference to the generator in the project file where the API should exist:
```
<ItemGroup>
    <PackageReference Include="WebClientGenerator.OpenAPI" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```
2. Add a reference to your OpenAPI specification, and optionally specify other configuration parameters, see [Options](#options):
```
<ItemGroup>
    <WebClientGenerator Include="path/to/openapi.json" ClientName="Foo" Namespace="Example" />
</ItemGroup>
```
The first file containing the word "openapi" and have an ending of .json, .yaml or .yml will be read.

Supported data formats for the OpenAPI specification:
* JSON
* YAML

3. Add references to [Corvus.Json.ExtendedTypes](https://github.com/corvus-dotnet/Corvus.JsonSchema?tab=readme-ov-file#corvusjsonextendedtypes) and [ParameterStyleParsers.OpenAPI](https://github.com/Fresa/OpenAPI.ParameterStyleParsers).
```
<ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.4.2" />
    <PackageReference Include="ParameterStyleParsers.OpenAPI" Version="1.4.0" />
</ItemGroup>
```
* Corvus.Json.ExtendedTypes >= 4.4.2
* ParameterStyleParsers.OpenAPI >= 1.5.0

4. Compile the project.
5. Use the client:
```dotnet
using var client = new Example.Foo(Servers.Production);
```

## Examples
See the tests for each OpenAPI specification:
- [OpenAPI 2.0](tests/Example.OpenApi20.IntegrationTests)
- [OpenAPI 3.0](tests/Example.OpenApi30.IntegrationTests)
- [OpenAPI 3.1](tests/Example.OpenApi31.IntegrationTests)
- [OpenAPI 3.2](tests/Example.OpenApi32.IntegrationTests)

All specifications mostly generate similar abstractions. What might differ is the location of generated resources, which follows the respective structure of the OpenAPI specification, and the JSON types, which are based on the respective schema version.

**Note**: The examples reference the generator through a project reference. Use a package reference instead as described above.

## Content Negotiation
Content is negotiated for both request and responses.

See the [examples](#examples) for more details.
### Request Body Content
Request body content is selected programatically through the client which automatically set the [Content-Type](https://datatracker.ietf.org/doc/html/rfc9110#field.content-type) header. 

### Response Content
Response content can be negotiated using the `accept` argument for the operation.

This is only available and scoped to operations that define response with content.

Actual content in returned responses are mapped according to the OpenAPI specification. Type-test the response to figure out which response was returned. Unknown response objects are constructed for undefined responses and content respectively.

Example:
```dotnet
switch (result.Response)
{
    case Foo.Foo1.Events0.Get.Response.OK200.ApplicationGeoJsonSeq applicationGeoJsonSeq:
        ...
        break;
    case Foo.Foo1.Events0.Get.Response.OK200.ApplicationJsonl applicationJsonl:
        ...
        break;
    case Foo.Foo1.Events0.Get.Response.OK200.Unknown unknownOkContent:
        // Content needs to be parsed manually
        var unknownOkContent = Parse(unknownOkContent.Content);
        ...
        break;
    case Foo.Foo1.Events0.Get.Response.Unknown unknownResponse:
        // Response needs to be parsed manually
        var unknownStatusCode = unknownResponse.StatusCode;
        var unknownContent = Parse(unknownResponse.Content);
        ...
        break;
}
```

## Sequential Media Types
OpenAPI 3.2 added support for [sequential media types](https://spec.openapis.org/oas/v3.2.0.html#sequential-media-types). The following sequential media types are supported for both request and response media content:
- application/jsonl
- application/x-ndjson
- application/x-jsonlines
- application/json-seq
- application/geo+json-seq

Other sequential media types can be implemented by simply following the expected naming convention and placing the implementations in the expected namespace, see the compilation error of any missing media type class.

### Request Content
Inherit from `SequentialJsonWriter<T>` using the following naming convention:
- application/jsonl (lower case) -> `ApplicationJsonlWriter<T>`

### Response Content
Inherit from `SequentialJsonEnumerable<T>` using the following naming convention:
- application/jsonl (lower case) -> `ApplicationJsonlEnumerable<T>`

See the [OpenAPI 3.2 examples](#examples) for further details how to consume and produce sequential media types.

## Authentication and Authorization
OpenAPI defines [security scheme objects](https://spec.openapis.org/oas/latest#security-scheme-object) for authentication and authorization mechanisms. The generator implement typed security requirements per operation, and scheme configuration describing the authentication schemes.

The security schemes for the [security requirements](https://spec.openapis.org/oas/latest#security-requirement-object) declared by the operations must be implemented. Security scheme object configurations are generated to the `SecuritySchemes` class and can be used to configure the scheme implementations.

## Options
To configure the generator use the `WebClientGenerator` configuration directive:
```
<ItemGroup>
    <WebClientGenerator Include="path/to/openapi.json" ClientName="Foo" Namespace="Example", ValidationLevel="Detailed" />
</ItemGroup>
```
Supported configuration:
- **ClientName**

  Description: The name of the client, i.e. `new MyClientName(httpClient)`.  
  Values: Any valid class name string  
  Default: WebClient


- **Namespace**

  Description: The namespace where generated web client resources are generated into, i.e. `new MyNamespace.MyClientName(httpClient)`.  
  Values: Any valid namespace directive  
  Default: Current assembly name


- **ValidationLevel**

  Description: Sets global validation level  
  Values: Flag|Basic|Detailed|Verbose  
  Default: Detailed

See the client constructor for how to define instance specific configuration using `WebClientConfiguration`. 

# Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## Breaking Changes
The generated API surface is validated against a baseline using [ApiCompat](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/overview). Each `Example.OpenApi*` project has a `LastMajorVersionBinary/` directory containing the baseline reference assembly.

### Introduce Breaking Change
Build will fail when a breaking change is detected. Generate a suppression file for the breaking changes, and commit it to accept the breaking changes:
```Shell
dotnet build -p:ApiCompatGenerateSuppressionFile=true
```

### Update Baseline
The baseline doesn't need to be updated when using suppression files, but if it for some reason should, run:
```Shell
dotnet build -p:_ApiCompatGenerateContractAssembly=true -p:ApiCompatValidateAssemblies=false
```

This copies the current reference assemblies to `LastMajorVersionBinary/` for each example project. Commit the updated DLLs. The suppression files should also be purged at this point.

# License
[MIT](LICENSE)