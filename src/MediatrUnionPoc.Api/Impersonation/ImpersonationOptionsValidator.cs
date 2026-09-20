using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Impersonation;

/// <summary>
/// Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for the
/// DataAnnotations on <see cref="ImpersonationOptions"/>; the rules that span several members (or another
/// options class) live in <see cref="ImpersonationOptionsRules"/>.
/// </summary>
[OptionsValidator]
public sealed partial class ImpersonationOptionsValidator : IValidateOptions<ImpersonationOptions>;
