using System.Diagnostics;

namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>Shared case type: the command completed with no payload to return.</summary>
[DebuggerDisplay("Success")]
public sealed record Success;
