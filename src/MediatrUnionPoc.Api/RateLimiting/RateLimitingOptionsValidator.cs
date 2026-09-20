using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for <see cref="RateLimitingOptions"/>, descending into each policy through <see cref="RateLimitPolicyOptionsValidator"/>.</summary>
[OptionsValidator]
public sealed partial class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>;
