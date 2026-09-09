using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.SourceGeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.OpenApi;
using JsonPointer = OpenAPI.WebClientGenerator.Json.JsonPointer;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SchemaGenerator(
    string rootNamespace,
    SourceProductionContext context,
    SourceGeneratorHelpers.GenerationContext generationContext)
{
    private static readonly IDocumentResolver MetaSchemaResolver = SourceGeneratorHelpers.CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = SourceGeneratorHelpers.CreateVocabularyRegistry(MetaSchemaResolver);
    private readonly Dictionary<string, TypeDeclaration> _typeCache = new();
    private readonly HashSet<string> _fileCache = [];

    internal static SchemaGenerator For(
        OpenApiSpecification openApiSpecification, 
        string rootNamespace,
        SourceProductionContext context)
    {
        var vocabulary = openApiSpecification.Version switch
        {
            OpenApiSpecVersion.OpenApi2_0 =>
                Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            OpenApiSpecVersion.OpenApi3_0 => 
                Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            OpenApiSpecVersion.OpenApi3_1 =>
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            OpenApiSpecVersion.OpenApi3_2 =>
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            _ => throw new InvalidOperationException($"OpenAPI specification {openApiSpecification.Version} is not supported")
        };
        var documentResolver = new PrepopulatedDocumentResolver();
        if (!documentResolver.AddDocument(openApiSpecification.Url, openApiSpecification.JsonDocument))
        {
            throw new InvalidOperationException("Could not add OpenApi document");
        }

        var globalOptions =
            new SourceGeneratorHelpers.GlobalOptions(
                fallbackVocabulary: vocabulary,
                optionalAsNullable: true,
                useOptionalNameHeuristics: true,
                alwaysAssertFormat: true,
                ImmutableArray<string>.Empty);
        var generationContext = new SourceGeneratorHelpers.GenerationContext(documentResolver, globalOptions);
        return new SchemaGenerator(rootNamespace, context, generationContext);
    }
    
    internal TypeDeclaration Generate(JsonReference reference)
    {
        if (_typeCache.TryGetValue(reference, out var typeDeclaration))
        {
            return typeDeclaration;
        }
        
        var pointer = JsonPointer.ParseFrom(reference);
        var metadata = TypeMetadata.From(pointer);
                
        var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
            ns: $"{rootNamespace}.{metadata.Namespace}",
            typeName: metadata.Path,
            location: reference,
            rebaseToRootPath: false);

        typeDeclaration = GenerateCode(generationSpecification);
        _typeCache.Add(reference, typeDeclaration);
        return typeDeclaration;
    }
    
    private TypeDeclaration GenerateCode(
        SourceGeneratorHelpers.GenerationSpecification specification)
    {
        var typeDeclarations = GenerateCode(new SourceGeneratorHelpers.TypesToGenerate(
            [specification], generationContext));
        return typeDeclarations.Single();
    }
    
    private List<TypeDeclaration> GenerateCode(SourceGeneratorHelpers.TypesToGenerate typesToGenerate)
    {
        if (typesToGenerate.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return [];
        }

        List<TypeDeclaration> typeDeclarationsToGenerate = [];
        Dictionary<string, string> namespaceToPathConversion = [];
        List<CSharpLanguageProvider.NamedType> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(typesToGenerate.DocumentResolver, VocabularyRegistry);

        string? defaultNamespace = null;

        foreach (var spec in typesToGenerate.GenerationSpecifications)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return [];
            }

            var schemaFile = spec.Location;
            JsonReference reference = new(schemaFile);
            var rootType = typeBuilder.AddTypeDeclarations(reference, typesToGenerate.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
            
            typeDeclarationsToGenerate.Add(rootType);

            defaultNamespace ??= spec.Namespace;

            if (string.IsNullOrEmpty(spec.TypeName))
            {
                throw new InvalidOperationException($"Missing type name for schema {spec.Location}");
            }

            // Corvus doesn't support defining paths for the source code file hint, so we piggyback such information on the type name property 
            var filePath = Path.GetDirectoryName(spec.TypeName!);
            if (filePath == string.Empty)
            {
                throw new InvalidOperationException($"Expected type {spec.TypeName} to contain a path");
            }
            
            var typeName = Path.GetFileName(spec.TypeName)!;
            if (typeName == string.Empty)
            {
                throw new InvalidOperationException($"Expected type {spec.TypeName} to contain a path + type name");
            }
            namedTypes.Add(
                new CSharpLanguageProvider.NamedType(
                    rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                    typeName,
                    spec.Namespace,
                    spec.Accessibility));
            
            namespaceToPathConversion[spec.Namespace] = filePath;
        }

        CSharpLanguageProvider.Options options = new(
            defaultNamespace ?? "GeneratedTypes",
            [.. namedTypes],
            useOptionalNameHeuristics: typesToGenerate.UseOptionalNameHeuristics,
            alwaysAssertFormat: typesToGenerate.AlwaysAssertFormat,
            optionalAsNullable: typesToGenerate.OptionalAsNullable,
            disabledNamingHeuristics: [.. typesToGenerate.DisabledNamingHeuristics],
            fileExtension: ".g.cs",
            defaultAccessibility: typesToGenerate.DefaultAccessibility);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        var generatedCode = typeBuilder.GenerateCodeUsing(
            languageProvider,
            typeDeclarationsToGenerate,
            context.CancellationToken);
        
        foreach (var codeFile in generatedCode)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            
            var filePath = namespaceToPathConversion[
                codeFile.TypeDeclaration?.DotnetNamespace() ?? throw new InvalidOperationException(
                    $"Code file {codeFile.FileName} does not have a namespace. This is an internal implementation error.")];
            var fileName = Path.Combine(filePath, codeFile.FileName);

            // Deduplicate nested schemas that might already have been generated
            if (!_fileCache.Add(fileName))
            {
                continue;
            }

            var sourceCode = new SourceCode(
                fileName,
                codeFile.FileContent
            );
            sourceCode.AddTo(context);
        }

        return typeDeclarationsToGenerate
            .Select(declaration => declaration.ReducedTypeDeclaration().ReducedType)
            .ToList();
    }
}