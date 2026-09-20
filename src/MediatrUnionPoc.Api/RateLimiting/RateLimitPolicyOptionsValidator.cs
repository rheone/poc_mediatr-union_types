using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for the DataAnnotations on <see cref="RateLimitPolicyOptions"/>.</summary>
[OptionsValidator]
public sealed partial class RateLimitPolicyOptionsValidator
    : IValidateOptions<RateLimitPolicyOptions>;
