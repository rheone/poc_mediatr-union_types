using MediatrUnionPoc.Api.Contracts;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>Object mother for the request bodies and fixed identities the Api tests send over HTTP.</summary>
public static class ProductRequestMother
{
    /// <summary>The caller id of the canonical product owner.</summary>
    public const string OwnerId = "owner-1";

    /// <summary>The caller id of a caller who owns nothing the tests create.</summary>
    public const string OtherCallerId = "owner-2";

    /// <summary>The name <see cref="Widget"/> carries.</summary>
    public const string WidgetName = "Widget";

    /// <summary>The price <see cref="Widget"/> carries.</summary>
    public const decimal WidgetPrice = 9.99m;

    /// <summary>The name <see cref="WidgetPro"/> carries.</summary>
    public const string WidgetProName = "Widget Pro";

    /// <summary>The price <see cref="WidgetPro"/> carries.</summary>
    public const decimal WidgetProPrice = 19.99m;

    /// <summary>The fixed identity of a product that is never created.</summary>
    public static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    /// <summary>Builds the canonical valid create request.</summary>
    /// <returns>A request for a product named "Widget" priced 9.99.</returns>
    public static CreateProductRequest Widget() => new(WidgetName, WidgetPrice);

    /// <summary>Builds a valid create request with the given name and a fixed price.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>A request for a product with that name priced 1.</returns>
    public static CreateProductRequest Named(string name) => new(name, 1m);

    /// <summary>Builds a create request that fails validation on both name and price.</summary>
    /// <returns>A request with an empty name and a negative price.</returns>
    public static CreateProductRequest InvalidCreate() => new(string.Empty, -5m);

    /// <summary>Builds the canonical valid update request.</summary>
    /// <returns>A request renaming a product to "Widget Pro" priced 19.99.</returns>
    public static UpdateProductRequest WidgetPro() => new(WidgetProName, WidgetProPrice);

    /// <summary>Builds an update request that fails validation on both name and price.</summary>
    /// <returns>A request with an empty name and a negative price.</returns>
    public static UpdateProductRequest InvalidUpdate() => new(string.Empty, -5m);
}
