using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Application.Tests;
using MediatrUnionPoc.Application.Tests.Unions;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(
    typeof(UnionXunitSerializer),
    typeof(CreateProductResult),
    typeof(UpdateProductResult),
    typeof(DeleteProductResult),
    typeof(AdminActionResult)
)]

namespace MediatrUnionPoc.Application.Tests;

/// <summary>
/// Lets xUnit v3 serialize <c>union</c> instances used as <c>TheoryData</c> rows, so each row
/// shows up as its own test case at discovery time instead of the whole theory collapsing into one.
/// </summary>
/// <remarks>
/// A union value is stored as its active case type's assembly-qualified name plus that case
/// value's JSON, and rebuilt by invoking the union's constructor for that case type. Only the
/// case types this test project uses in theory rows need to round-trip through JSON.
/// </remarks>
internal sealed class UnionXunitSerializer : IXunitSerializer
{
    private const char Separator = '|';

    /// <inheritdoc />
    public object Deserialize(Type type, string serializedValue)
    {
        var separatorIndex = serializedValue.IndexOf(Separator, StringComparison.Ordinal);
        var caseType =
            Type.GetType(serializedValue[..separatorIndex], throwOnError: true)
            ?? throw new InvalidOperationException("Case type could not be resolved.");
        var json = Convert.FromBase64String(serializedValue[(separatorIndex + 1)..]);
        var caseValue = DeserializeCase(caseType, json);

        return Activator.CreateInstance(type, caseValue)
            ?? throw new InvalidOperationException($"Could not construct {type}.");
    }

    /// <inheritdoc />
    public bool IsSerializable(
        Type type,
        object? value,
        [NotNullWhen(false)] out string? failureReason
    )
    {
        if (value is IUnion { Value: not null })
        {
            failureReason = null;
            return true;
        }

        failureReason = $"{type.FullName} is not a union carrying a case value.";
        return false;
    }

    /// <inheritdoc />
    public string Serialize(object value)
    {
        var caseValue =
            ((IUnion)value).Value
            ?? throw new InvalidOperationException("Union carries no case value.");
        var caseType = caseValue.GetType();

        // Cases whose constructor parameter type differs from the exposed property type (the
        // collection-carrying ones) can't round-trip through System.Text.Json as-is, so they are
        // stored as their plain element arrays instead.
        object payload = caseValue switch
        {
            NotAuthorized notAuthorized => notAuthorized.Reasons.ToArray(),
            Failure failure => failure.Reasons.ToArray(),
            ValidationErrors validationErrors => validationErrors.Errors.ToArray(),
            _ => caseValue,
        };

        return caseType.AssemblyQualifiedName
            + Separator
            + Convert.ToBase64String(
                JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType())
            );
    }

    private static object DeserializeCase(Type caseType, byte[] json) =>
        caseType switch
        {
            _ when caseType == typeof(NotAuthorized) => new NotAuthorized(
                JsonSerializer.Deserialize<string[]>(json)
            ),
            _ when caseType == typeof(Failure) => new Failure(
                JsonSerializer.Deserialize<string[]>(json)
            ),
            _ when caseType == typeof(ValidationErrors) => new ValidationErrors(
                JsonSerializer.Deserialize<ValidationError[]>(json)
            ),
            _ => JsonSerializer.Deserialize(json, caseType)
                ?? throw new InvalidOperationException("Case value could not be deserialized."),
        };
}
