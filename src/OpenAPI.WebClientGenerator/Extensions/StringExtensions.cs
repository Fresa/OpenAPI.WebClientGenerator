using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OpenAPI.WebClientGenerator.Extensions;

internal static class StringExtensions
{
    private static readonly char[] DefaultDelimiters = ['/', '?', '=', '&', '{', '}', '-', '_', '+', ':'];
    
    [return: NotNullIfNotNull(nameof(str))]
    public static string? ToPascalCase(this string? str, params char[] delimiters)
    {
        if (str is null or "")
        {
            return str;
        }

        if (delimiters.Length == 0)
        {
            delimiters = DefaultDelimiters;
        }
        
        var sections = str
            .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
            .Select(section => section.First().ToString().ToUpper() + string.Join(string.Empty, section.Skip(1)));

        return string.Concat(sections);
    }

    [return: NotNullIfNotNull(nameof(str))]
    public static string? ToCamelCase(this string? str, params char[] delimiters)
    {
        var strAsPascalCase = str.ToPascalCase();
        if (strAsPascalCase is null or "")
        {
            return strAsPascalCase;
        }

        var firstCharacter = strAsPascalCase[..1].ToLower();
        if (strAsPascalCase.Length == 1)
        {
            return firstCharacter;
        }

        return firstCharacter + strAsPascalCase[1..];
    }

    internal static string Indent(this string str, int spaces)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return string.Empty;
        }
        var indentation = new string(' ', spaces);
        return string.Join("\n",
            str
                .Split('\n')
                .Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : $"{indentation}{line}"));
    }

    internal static string AsComment(this string? str, params string[] commentTypes)
    {
        if (!commentTypes.Any())
        {
            throw new InvalidOperationException("Must specify at least one comment type");
        }
        
        if (str is null || string.IsNullOrWhiteSpace(str))
        {
            return string.Empty;
        }

        Array.Reverse(commentTypes);
        return string.Join("\n",
            commentTypes.Aggregate(str
                .Split('\n')
                .Select(line => $"/// {line}"), (current, commentType) =>
                current
                    .Prepend($"/// <{commentType}>")
                    .Append($"/// </{commentType}>")));
    }

    internal static string PrependNewline(this string str) => "\n" + str;
}