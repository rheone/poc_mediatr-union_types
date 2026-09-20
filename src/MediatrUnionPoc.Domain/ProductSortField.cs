using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Domain;

/// <summary>
/// The allowlist of <see cref="Product"/> properties a listing may be sorted by. An enum rather
/// than a property-name string, so a caller can never name anything outside this set. Serialized
/// as the camel-case name the HTTP <c>sort</c> parameter uses.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProductSortField>))]
public enum ProductSortField
{
    /// <summary>Order by <see cref="Product.Name"/>.</summary>
    [JsonStringEnumMemberName("name")]
    Name,

    /// <summary>Order by <see cref="Product.Price"/>.</summary>
    [JsonStringEnumMemberName("price")]
    Price,

    /// <summary>Order by <see cref="Product.CreatedAt"/>.</summary>
    [JsonStringEnumMemberName("createdAt")]
    CreatedAt,
}
