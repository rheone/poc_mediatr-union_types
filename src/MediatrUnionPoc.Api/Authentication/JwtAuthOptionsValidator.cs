using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for
/// <see cref="JwtAuthOptions"/>; the generator emits the implementation from the DataAnnotations on
/// that type, following the convention set by the health endpoints' options validator.
/// </summary>
[OptionsValidator]
public sealed partial class JwtAuthOptionsValidator : IValidateOptions<JwtAuthOptions>;
