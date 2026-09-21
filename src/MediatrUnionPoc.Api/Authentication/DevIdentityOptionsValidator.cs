using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for
/// <see cref="DevIdentityOptions"/>; the generator emits the implementation from the DataAnnotations
/// on that type.
/// </summary>
[OptionsValidator]
public sealed partial class DevIdentityOptionsValidator : IValidateOptions<DevIdentityOptions>;
