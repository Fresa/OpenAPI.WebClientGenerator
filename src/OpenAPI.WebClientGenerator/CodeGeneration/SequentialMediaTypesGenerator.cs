namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SequentialMediaTypesGenerator(string @namespace)
{
    internal SourceCode GenerateClasses() => new("SequentialMediaTypes.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Text.Json;

namespace {{@namespace}};

/// <summary>
/// Base class for sequential json enumerable
/// </summary>
public abstract class SequentialJsonEnumerable<T>(Stream stream, WebClientConfiguration configuration) : IAsyncEnumerable<(T, ImmutableList<ValidationResult>)>
    where T : struct, IJsonValue<T>
{
    private int _itemPosition = -1;
    private ValidationLevel _validationLevel = default;
    private string _schemaLocation = "#";
    private T? _current;

    /// <summary>
    /// Delimiter between each item
    /// </summary>
    protected abstract byte Delimiter { get; } 
    
    /// <summary>
    /// Does the sequence require ending with a delimiter? 
    /// </summary>
    protected abstract bool RequiresDelimiterAfterLastItem { get; }
    
    /// <inheritdoc/>
    public async IAsyncEnumerator<(T, ImmutableList<ValidationResult>)> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var pipeReader = PipeReader.Create(stream);
        try
        {
            do
            {
                var result = await pipeReader.ReadAsync(cancellationToken)
                    .ConfigureAwait(false);
                var buffer = result.Buffer;
                var position = buffer.PositionOf(Delimiter);

                switch (result.IsCompleted)
                {
                    // Found an item
                    case false or true when position is not null:
                        var data = buffer.Slice(0, position.Value);
                        _itemPosition++;
                        _current = ParseItem(data);
                        pipeReader.AdvanceTo(buffer.GetPosition(1, position.Value));
                        yield return (_current.Value, ValidateCurrentItem());
                        break;
                    // No more data
                    case true when buffer.IsEmpty:
                        yield break;
                    // No more data to read, data was found, but no delimiter.
                    // End delimiter is optional, so parse any found data.
                    case true when !RequiresDelimiterAfterLastItem:
                        _itemPosition++;
                        _current = ParseItem(buffer);
                        pipeReader.AdvanceTo(buffer.End);
                        yield return (_current.Value, ValidateCurrentItem());
                        yield break;
                    // No more data to read, data was found, but no delimiter.
                    // End delimiter is required, so discard any found data
                    case true:
                        pipeReader.AdvanceTo(buffer.End);
                        yield break;
                    // More data exist, and no item found yet
                    default:
                        pipeReader.AdvanceTo(buffer.Start, buffer.End);
                        break;
                }
            } while (true);
        }
        finally
        {
            await pipeReader.CompleteAsync()
                .ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Parse the read item
    /// </summary>
    /// <param name="data">Data read up until the Delimiter</param>
    /// <returns>The parsed item</returns>
    protected abstract T ParseItem(ReadOnlySequence<byte> data); 
    
    private ImmutableList<ValidationResult> ValidateCurrentItem()
    {
        if (!configuration.ValidateResponses || _current == null)
            return ValidationContext.ValidContext.Results;
        return _current.Validate($"{_schemaLocation}/{_itemPosition}", true, ValidationContext.ValidContext, _validationLevel).Results
            .WithLocation(configuration.OpenApiSpecificationUri);
    }
        
        
    /// <summary>
    /// Validates the sequence
    /// </summary>
    /// <param name="schemaLocation">The location of the schema describing the sequence</param>
    /// <param name="isRequired">Is the sequence required?</param>
    /// <param name="validationContext">Current validation context</param>
    /// <param name="validationLevel">The validation level</param>
    /// <returns>The validation result</returns>
    internal ValidationContext Validate(string schemaLocation, bool isRequired, ValidationContext validationContext, ValidationLevel validationLevel)
    {
        _schemaLocation = schemaLocation;
        _validationLevel = validationLevel;
        return validationContext;
    }
}

/// <summary>
/// Sequential json enumerable for jsonl
/// </summary>
public class ApplicationJsonlEnumerable<T>(Stream stream, WebClientConfiguration configuration) :
    SequentialJsonEnumerable<T>(stream, configuration)
    where T : struct, IJsonValue<T>
{
    protected override byte Delimiter => 0x0A;
    protected override bool RequiresDelimiterAfterLastItem => false;
    protected override T ParseItem(ReadOnlySequence<byte> data) => T.Parse(data);
}

/// <summary>
/// Sequential json enumerable for x-ndjson
/// </summary>
public class ApplicationXNdjsonEnumerable<T>(Stream stream, WebClientConfiguration configuration) : ApplicationJsonlEnumerable<T>(stream, configuration)
    where T : struct, IJsonValue<T>;

/// <summary>
/// Sequential json enumerable for x-jsonlines
/// </summary>
public class ApplicationXJsonlinesEnumerable<T>(Stream stream, WebClientConfiguration configuration) : ApplicationJsonlEnumerable<T>(stream, configuration)
    where T : struct, IJsonValue<T>;

/// <summary>
/// Sequential json enumerable for json-seq
/// </summary>
public class ApplicationJsonSeqEnumerable<T>(Stream stream, WebClientConfiguration configuration) :
    SequentialJsonEnumerable<T>(stream, configuration)
    where T : struct, IJsonValue<T>
{
    private const byte RecordSeparator = 0x1E;
    protected override byte Delimiter => 0x0A;
    protected override bool RequiresDelimiterAfterLastItem => true;

    protected override T ParseItem(ReadOnlySequence<byte> data)
    {
        // RS should be first.
        // If it is not, then the data is incomplete and invalid,
        // let JSON validation handle it
        if (!data.IsEmpty && data.FirstSpan[0] == RecordSeparator)
        {
            data = data.Slice(1);
        }

        return T.Parse(data);
    }
}

/// <summary>
/// Sequential json enumerable for geo+json-seq
/// </summary>
public class ApplicationGeoJsonSeqEnumerable<T>(Stream stream, WebClientConfiguration configuration) : ApplicationJsonSeqEnumerable<T>(stream, configuration)
    where T : struct, IJsonValue<T>;


/// <summary>
/// Writer for sequential media types
/// </summary>
/// <param name="writer"></param>
/// <typeparam name="T">Item type of the sequence</typeparam>
internal abstract class SequentialJsonWriter<T>(PipeWriter writer) : IDisposable
    where T : struct, IJsonValue<T>
{
    private readonly Utf8JsonWriter _jsonWriter = new(writer, new JsonWriterOptions
    {
        // Items should already have been validated so it's 
        // redundant to validate here again
        SkipValidation = true
    });
    private int _writtenItems;
    
    /// <summary>
    /// Delimiter between each item
    /// </summary>
    protected abstract byte Delimiter { get; }
    
    /// <summary>
    /// Optional prefix before each item
    /// </summary>
    protected virtual byte? Prefix { get; } = null;

    /// <summary>
    /// Validate the item
    /// </summary>
    /// <param name="item">The item to validate</param>
    /// <param name="schemaLocation">Schema location of this sequence</param>
    /// <param name="validationContext">Validation context</param>
    /// <param name="validationLevel">Validation level</param>
    /// <returns>The validation result</returns>
    internal ValidationContext Validate(T item, string schemaLocation, ValidationContext validationContext,
        ValidationLevel validationLevel) =>
        item.Validate($"{schemaLocation}/{_writtenItems}", true, validationContext, validationLevel);

    /// <summary>
    /// Write an item to the sequence
    /// </summary>
    /// <param name="item">Item to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An awaitable task for the write operation</returns>
    internal async ValueTask WriteItemAsync(T item, CancellationToken cancellationToken = default)
    {
        if (Prefix != null)
        {
            writer.GetSpan(1)[0] = Prefix.Value;
            writer.Advance(1);
        }
        item.WriteTo(_jsonWriter);
        _jsonWriter.Flush();
        _jsonWriter.Reset();
        writer.GetSpan(1)[0] = Delimiter;
        writer.Advance(1);
        _writtenItems++;
        var result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsCanceled)
        {
            throw new OperationCanceledException("The pending flush was canceled");
        }
        if (result.IsCompleted)
        {
            throw new OperationCanceledException("The sequence is no longer being read");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _jsonWriter.Dispose();
    }
}

/// <summary>
/// Sequential json writer for jsonl
/// </summary>
internal class ApplicationJsonlWriter<T>(PipeWriter writer) : SequentialJsonWriter<T>(writer)
    where T : struct, IJsonValue<T>
{
    protected override byte Delimiter => 0x0A;
}

/// <summary>
/// Sequential json writer for x-ndjson
/// </summary>
internal class ApplicationXNdjsonWriter<T>(PipeWriter writer) : ApplicationJsonlWriter<T>(writer)
    where T : struct, IJsonValue<T>;

/// <summary>
/// Sequential json writer for x-jsonlines
/// </summary>
internal class ApplicationXJsonlinesWriter<T>(PipeWriter writer) : ApplicationJsonlWriter<T>(writer)
    where T : struct, IJsonValue<T>;

/// <summary>
/// Sequential json writer for json-seq
/// </summary>
internal class ApplicationJsonSeqWriter<T>(PipeWriter writer) : SequentialJsonWriter<T>(writer) where T : struct, IJsonValue<T>
{
    protected override byte Delimiter => 0x0A;
    protected override byte? Prefix => 0x1E;
}

/// <summary>
/// Sequential json writer for geo+json-seq
/// </summary>
internal class ApplicationGeoJsonSeqWriter<T>(PipeWriter writer) : ApplicationJsonSeqWriter<T>(writer)
    where T : struct, IJsonValue<T>;

#nullable restore
""");
}