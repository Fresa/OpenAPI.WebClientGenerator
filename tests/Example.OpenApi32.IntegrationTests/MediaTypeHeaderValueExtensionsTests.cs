using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Example.Foo;

namespace Example.OpenApi32.IntegrationTests;

[SuppressMessage("Usage", "AF0002:Access to internal generated member")]
public class MediaTypeHeaderValueExtensionsTests
{
    [Theory]
    [InlineData("application/json", "application/json")]
    [InlineData("application/geo+json", "application/json")]
    [InlineData("application/json", "*/*")]
    [InlineData("application/json", "application/*")]
    [InlineData("text/plain", "text/*")]
    [InlineData("application/vnd.api+json", "application/*+json")]
    [InlineData("application/vnd.api+json", "application/vnd.api+json")]
    [InlineData("application/vnd.api+json", "*/*")]
    [InlineData("application/json; charset=utf-8", "application/json")]
    [InlineData("application/json; charset=utf-8", "application/json; charset=utf-8")]
    [InlineData("application/json; charset=utf-8; q=0.8", "application/json; charset=utf-8")]
    public void IsSubsetOf_ReturnsTrue_WhenSelfMatchesSet(string self, string other)
    {
        MediaTypeHeaderValue.Parse(self).IsSubsetOf(MediaTypeHeaderValue.Parse(other))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("application/json", "text/plain")]
    [InlineData("application/json", "text/*")]
    [InlineData("application/json", "application/xml")]
    [InlineData("application/vnd.api+json", "application/*+xml")]
    [InlineData("application/json", "application/vnd.api+json")]
    [InlineData("application/json", "application/json; charset=utf-8")]
    [InlineData("application/json; charset=ascii", "application/json; charset=utf-8")]
    public void IsSubsetOf_ReturnsFalse_WhenSelfDoesNotMatchSet(string self, string other)
    {
        MediaTypeHeaderValue.Parse(self).IsSubsetOf(MediaTypeHeaderValue.Parse(other))
            .Should().BeFalse();
    }

    [Fact]
    public void IsSubsetOf_ReturnsFalse_WhenOtherIsNull()
    {
        MediaTypeHeaderValue.Parse("application/json").IsSubsetOf(null)
            .Should().BeFalse();
    }
}