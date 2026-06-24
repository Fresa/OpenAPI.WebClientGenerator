using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenAPI.WebClientGenerator.Extensions;

internal static class EnumerableExtensions
{
    internal static string AggregateToString(this IEnumerable<string> items) =>
        items.AggregateToString(str => str);
    internal static string AggregateToString<T>(this IEnumerable<T> items, Func<T, string> convert, bool trimEnd = true)
    {
        var str = items.AggregateToString(new StringBuilder().AppendLine(), convert);
        return trimEnd ? str.TrimEnd() : str;
    }
    
    internal static string AggregateToStringAsIs<T>(this IEnumerable<T> items, Func<T, string> convert) => 
        items.AggregateToString(new StringBuilder(), convert);

    internal static string AggregateToString<T>(this IEnumerable<T> items, string firstLine, Func<T, string> convert) =>
        items
            .AggregateToString(new StringBuilder()
                .AppendLine(firstLine), convert)
            .TrimEnd();

    private static string AggregateToString<T>(this IEnumerable<T> items, StringBuilder stringBuilder, Func<T, string> convert) =>
        items
            .Aggregate(stringBuilder, (builder, item) =>
                builder.AppendLine(convert(item)))
            .ToString();
    
    internal static IEnumerable<(T Item, int I)> WithIndex<T>(this IEnumerable<T> items) =>
        items.Select((arg1, i) => (arg1, i));

    internal static IEnumerable<string> RemoveEmptyLines(this IEnumerable<string> list) =>
        list
            .Where(line => !string.IsNullOrWhiteSpace(line));
    
    internal static string AsParams(this IEnumerable<string> values)
    {
        var result = string.Join(", ", values.Select(scope => $"\"{scope}\""));
        return result == string.Empty ? "[]" : result;
    }
}