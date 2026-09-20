using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RequestTimeouts;

/// <summary>Compile-time (source-generated, reflection-free) <see cref="IValidateOptions{TOptions}"/> for <see cref="RequestTimeoutOptions"/>.</summary>
[OptionsValidator]
public sealed partial class RequestTimeoutOptionsValidator
    : IValidateOptions<RequestTimeoutOptions>;
