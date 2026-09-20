using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.Cors;

/// <summary>Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for the DataAnnotations on <see cref="ApiCorsOptions"/>, including its <see cref="CorsOriginListAttribute"/> and <see cref="CorsTokenListAttribute"/> rules.</summary>
[OptionsValidator]
public sealed partial class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>;
