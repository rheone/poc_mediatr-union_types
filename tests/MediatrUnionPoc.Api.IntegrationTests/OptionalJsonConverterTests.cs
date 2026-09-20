using System.Text.Json;
using MediatrUnionPoc.Api.Http;
using MediatrUnionPoc.Application.Common;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies <see cref="OptionalJsonConverterFactory"/> in isolation, without HTTP: it tells a missing member, an explicit null and a value apart for any <see cref="Optional{T}"/>.</summary>
public sealed class OptionalJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new OptionalJsonConverterFactory() },
    };

    /// <summary>Verifies a member with a value deserialises to a present optional holding it, and a missing member to an absent one.</summary>
    [Fact]
    public void Deserialize_ValueAndMissingMember_PresentAndAbsent_Test()
    {
        // Act
        var body = JsonSerializer.Deserialize<Body>("""{"name":"Widget"}""", Options);

        // Assert
        Assert.NotNull(body);
        Assert.Multiple(
            () => Assert.True(body.Name.IsPresent),
            () => Assert.Equal("Widget", body.Name.Value),
            () => Assert.False(body.Price.IsPresent)
        );
    }

    /// <summary>Verifies an explicit JSON null is a present optional holding null — distinct from a missing member — for both a reference and a value type.</summary>
    [Fact]
    public void Deserialize_ExplicitNull_PresentWithNull_Test()
    {
        // Act
        var body = JsonSerializer.Deserialize<Body>("""{"name":null,"price":null}""", Options);

        // Assert
        Assert.NotNull(body);
        Assert.Multiple(
            () => Assert.True(body.Name.IsPresent),
            () => Assert.Null(body.Name.Value),
            () => Assert.True(body.Price.IsPresent),
            () => Assert.Null(body.Price.Value)
        );
    }

    /// <summary>Verifies an empty object leaves every optional absent.</summary>
    [Fact]
    public void Deserialize_EmptyObject_AllAbsent_Test()
    {
        // Act
        var body = JsonSerializer.Deserialize<Body>("{}", Options);

        // Assert
        Assert.NotNull(body);
        Assert.Multiple(
            () => Assert.False(body.Name.IsPresent),
            () => Assert.False(body.Price.IsPresent)
        );
    }

    /// <summary>Verifies the converter works for any wrapped type, not just the two the patch contract uses.</summary>
    [Fact]
    public void Deserialize_OtherWrappedTypes_Bind_Test()
    {
        // Act
        var flags = JsonSerializer.Deserialize<Other>(
            """{"flag":true,"when":"2026-03-04T05:06:07+00:00"}""",
            Options
        );

        // Assert
        Assert.NotNull(flags);
        Assert.Multiple(
            () => Assert.True(flags.Flag.Value),
            () =>
                Assert.Equal(
                    new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
                    flags.When.Value
                )
        );
    }

    /// <summary>Verifies a present optional writes its value and an absent one writes null.</summary>
    [Fact]
    public void Serialize_PresentAndAbsent_WritesValueAndNull_Test()
    {
        // Arrange
        var body = new Body(Optional<string?>.Of("Widget"), default);

        // Act
        var json = JsonSerializer.Serialize(body, Options);

        // Assert
        Assert.Equal("""{"name":"Widget","price":null}""", json);
    }

    /// <summary>Verifies the factory claims only <see cref="Optional{T}"/> types.</summary>
    [Fact]
    public void CanConvert_OnlyOptionalTypes_Test()
    {
        // Arrange
        var factory = new OptionalJsonConverterFactory();

        // Act
        var results = (
            factory.CanConvert(typeof(Optional<int>)),
            factory.CanConvert(typeof(int?)),
            factory.CanConvert(typeof(string))
        );

        // Assert
        Assert.Equal((true, false, false), results);
    }

    private sealed record Other(Optional<bool> Flag, Optional<DateTimeOffset> When);

    private sealed record Body(Optional<string?> Name, Optional<decimal?> Price);
}
