using System.Diagnostics;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>
/// What loading a product for a change (update, patch) comes to before anything is mutated: the
/// product itself, cleared to be changed, or the reason it may not be — missing, not the caller's
/// to change, or changed by someone else since the caller last saw it. Handlers translate the
/// refusals into their own result union's cases.
/// </summary>
[DebuggerDisplay("{Value}")]
internal union LoadedForChangeResult(
    Product,
    NotFound<ProductId>,
    NotAuthorized,
    PreconditionFailed
);
