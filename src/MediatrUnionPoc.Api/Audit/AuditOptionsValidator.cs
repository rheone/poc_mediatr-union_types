using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Audit;

/// <summary>Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for the DataAnnotations on <see cref="AuditOptions"/>.</summary>
[OptionsValidator]
public sealed partial class AuditOptionsValidator : IValidateOptions<AuditOptions>;
