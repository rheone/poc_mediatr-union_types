using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Health;

/// <summary>
/// Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for
/// <see cref="HealthEndpointsOptions"/>; the generator emits the implementation from the
/// DataAnnotations on that type. This is the pattern for every options class.
/// </summary>
[OptionsValidator]
public sealed partial class HealthEndpointsOptionsValidator
    : IValidateOptions<HealthEndpointsOptions>;
