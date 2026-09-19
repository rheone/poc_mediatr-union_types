using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>
/// Case type: the requested entity could not be located.
/// </summary>
/// <typeparam name="TId">The identity type of the entity that was looked up.</typeparam>
/// <param name="Id">
/// The identity that was looked up and not found, or <see langword="null"/> when the failed
/// lookup has no single identity to report (e.g. a bulk operation).
/// </param>
[DebuggerDisplay("Id = {Id}")]
public sealed record NotFound<TId>(TId? Id)
    where TId : struct;
