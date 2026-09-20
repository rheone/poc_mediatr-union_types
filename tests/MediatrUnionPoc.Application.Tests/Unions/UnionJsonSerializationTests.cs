using System.Runtime.CompilerServices;
using System.Text.Json;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Documents an easy-to-get-wrong assumption about serializing a union directly: it is tempting
/// to assume <c>System.Text.Json</c> would serialize the generated struct's own shape — a single
/// <c>Value</c> property — the way it would for any other type with one public property. It
/// doesn't: .NET 11's <c>System.Text.Json</c> has built-in awareness of <c>IUnion</c> and
/// transparently flattens to whichever case type is boxed inside <c>Value</c>, with no wrapper.
/// That's genuinely convenient, but it does <em>not</em> remove the reason every controller action
/// still switches before returning anything — see the second test below.
/// </summary>
public class UnionJsonSerializationTests
{
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;
    private const string ErrorMessage = "boom";
    private const string ErrorCode = "BOOM";

    private static readonly ProductId SomeProductId = ProductId.From(
        Guid.Parse("88888888-8888-8888-8888-888888888888")
    );

    /// <summary>
    /// Verifies serializing the union directly produces byte-identical JSON to serializing the
    /// boxed case type on its own, with no <c>{"Value": {...}}</c> wrapper around it.
    /// </summary>
    [Fact]
    public void Serialize_RawUnion_FlattensToBoxedCaseShapeWithNoWrapper_Test()
    {
        // Arrange
        CreateProductResult result = new ProductDto(
            SomeProductId,
            ProductName,
            ProductPrice,
            ProductVersion.Initial,
            DateTimeOffset.UnixEpoch
        );

        // Act
        var unionJson = JsonSerializer.Serialize(result);
        var dtoJson = JsonSerializer.Serialize(((IUnion)result).Value);

        // Assert
        // Serializing the union directly produces byte-identical JSON to serializing the
        // unwrapped ProductDto — there is no {"Value": {...}} wrapper, unlike a plain struct
        // whose only public member happens to be named "Value".
        Assert.Equal(dtoJson, unionJson);
        using var document = JsonDocument.Parse(unionJson);
        Assert.Multiple(
            () => Assert.False(document.RootElement.TryGetProperty("Value", out _)),
            () => Assert.Equal(ProductName, document.RootElement.GetProperty("Name").GetString())
        );
    }

    /// <summary>
    /// Verifies no case-type discriminator (e.g. a <c>"case"</c>/<c>"$type"</c> tag) is present in
    /// the serialized JSON — the reason every controller action still switches on the union before
    /// returning anything, rather than letting callers infer the case from field shapes.
    /// </summary>
    [Fact]
    public void Serialize_DifferentCasesOfSameUnion_CarriesNoSharedCaseDiscriminator_Test()
    {
        // Arrange
        CreateProductResult productDto = new ProductDto(
            SomeProductId,
            ProductName,
            ProductPrice,
            ProductVersion.Initial,
            DateTimeOffset.UnixEpoch
        );
        CreateProductResult error = new Error(ErrorMessage, ErrorCode);
        DeleteProductResult success = new Success();

        // Act
        var productJson = JsonSerializer.Serialize(productDto);
        var errorJson = JsonSerializer.Serialize(error);
        var successJson = JsonSerializer.Serialize(success);

        // Assert
        // Two cases with genuinely no fields in common serialize to genuinely different shapes,
        // but neither carries a "case"/"$type" tag identifying which one it is, and an empty
        // case type serializes to a completely uninformative "{}" — a consumer who receives just
        // the bytes, out of the context of (say) an HTTP status code, has no formal way to tell
        // "the operation succeeded with nothing to report" apart from "an unrecognized case type
        // was added and I don't know what it means." This is *why* every controller action in
        // this repo still switches on the union before returning anything: to attach that missing
        // context (a status code, a route, a header) explicitly, per case, rather than relying on
        // the caller to infer it from field shapes.
        using var productDocument = JsonDocument.Parse(productJson);
        using var errorDocument = JsonDocument.Parse(errorJson);
        Assert.Multiple(
            () => Assert.NotEqual(productJson, errorJson),
            () => Assert.Equal("{}", successJson),
            () => Assert.False(productDocument.RootElement.TryGetProperty("case", out _)),
            () => Assert.False(errorDocument.RootElement.TryGetProperty("case", out _))
        );
    }
}
