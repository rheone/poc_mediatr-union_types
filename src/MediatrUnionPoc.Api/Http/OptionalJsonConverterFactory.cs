using System.Text.Json;
using System.Text.Json.Serialization;
using MediatrUnionPoc.Application.Common;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// Teaches <c>System.Text.Json</c> to bind any <see cref="Optional{T}"/>: a member missing from the
/// JSON leaves the property at <c>default</c> (absent), a member present with any value — an explicit
/// <c>null</c> included, when <c>T</c> admits it — becomes a present optional holding that value.
/// This is what lets a JSON Merge Patch body say "leave the name alone" (member omitted) apart from
/// "set the name to null" (member <c>null</c>). It lives in the Api project so the Application layer
/// stays serializer-free; register it once on the MVC JSON options and every <see cref="Optional{T}"/>
/// property in a request contract is covered.
/// </summary>
public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="typeToConvert"/> is <see langword="null"/>.</exception>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="typeToConvert"/> is <see langword="null"/>.</exception>
    public override JsonConverter? CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(
            typeToConvert.GetGenericArguments()[0]
        );

        return Activator.CreateInstance(converterType) as JsonConverter;
    }

    private sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        // A JSON null must reach Read (it is a supplied value, not a missing one).
        public override bool HandleNull => true;

        public override Optional<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        ) => Optional<T>.Of(JsonSerializer.Deserialize<T>(ref reader, options)!);

        public override void Write(
            Utf8JsonWriter writer,
            Optional<T> value,
            JsonSerializerOptions options
        )
        {
            if (value.IsPresent)
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
