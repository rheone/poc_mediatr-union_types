using MediatrUnionPoc.Application.Common.Auditing;
using MediatrUnionPoc.Application.Common.Results;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>The audit descriptions the product mutations share: a <c>Product</c> target and, for a refusal, the denial message.</summary>
internal static class ProductAudit
{
    /// <summary>The <see cref="AuditDescription.TargetType"/> of every product event.</summary>
    public const string TargetType = "Product";

    /// <summary>Describes an event aimed at the product <paramref name="id"/>.</summary>
    /// <param name="id">The product's id, or <see langword="null"/> when there is none (a create that produced no product).</param>
    /// <returns>The description.</returns>
    public static AuditDescription Target(string? id) => new(TargetType, id);

    /// <summary>Describes a refusal: the product plus why the caller was refused.</summary>
    /// <param name="id">The product's id.</param>
    /// <param name="denial">The refusal.</param>
    /// <returns>The description.</returns>
    public static AuditDescription Denied(string id, NotAuthorized denial) =>
        new(
            TargetType,
            id,
            Details: new Dictionary<string, string>
            {
                ["denial"] = string.Join(" ", denial.Reasons),
            }
        );
}
